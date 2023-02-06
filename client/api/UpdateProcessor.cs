using System;
using System.Threading;
using System.Threading.Tasks;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.HarnessOpenAPIService;
using Newtonsoft.Json;
using Serilog;

namespace io.harness.cfsdk.client.api
{
    public interface IUpdateCallback
    {
        void Update(Message message, bool manual);
        void OnStreamConnected();
        void OnStreamDisconnected();
    }
    internal interface IUpdateProcessor
    {
        void Start();
        void Stop();
        void Update(Message message, bool manual);
    }
    /// <summary>
    /// Class responsible to initiate permanent connection with server
    /// and update state of 
    /// </summary>
    internal class UpdateProcessor : IUpdateCallback, IUpdateProcessor
    {
        private IConnector connector;
        private IRepository repository;
        private IUpdateCallback callback;

        private IService service;
        private Config config;

        public UpdateProcessor(IConnector connector, IRepository repository, Config config, IUpdateCallback callback)
        {
            this.callback = callback;
            this.repository = repository;
            this.connector = connector;
            this.config = config;
        }

        public void Start()
        {
            if (config.streamEnabled)
            {
                this.service = connector.Stream(this);
                this.service.Start();
            }
        }
        public void Stop()
        {
            if (this.service != null)
            {
                this.service.Stop();
                this.service = null;
            }
        }

        public void Update(Message message, bool manual)
        {
            if( manual && this.config.StreamEnabled)
            {
                Log.Information("You run the update method manually with the stream enabled. Please turn off the stream in this case.");
            }
            //we got a message from server. Dispatch in separate thread.
            _ = ProcessMessage(message);
        }
        public void OnStreamConnected()
        {
            this.callback.OnStreamConnected();
        }

        private async Task StartAfterInterval()
        {
            await Task.Delay(TimeSpan.FromSeconds(this.config.pollIntervalInSeconds));
            Start();
        }
        public void OnStreamDisconnected()
        {
            this.callback.OnStreamDisconnected();
            Stop();
            _ = StartAfterInterval();
        }
        private async Task ProcessMessage(Message message)
        {
            if (message.Domain.Equals("flag"))
            {
                try
                {
                    if (message.Event.Equals("delete"))
                    {
                        this.repository.DeleteFlag(message.Identifier);
                    }
                    else if (message.Event.Equals("create") || message.Event.Equals("patch"))
                    {
                        FeatureConfig feature = await this.connector.GetFlag(message.Identifier);
                        this.repository.SetFlag(message.Identifier, feature);
                    }
                }
                catch(Exception ex)
                {
                    Log.Error($"Error processing flag: {message.Identifier} event: {message.Event}.", ex);
                }
            }
            else if (message.Domain.Equals("target-segment"))
            {
                try
                {
                    if (message.Event.Equals("delete"))
                    {
                        this.repository.DeleteSegment(message.Identifier);
                    }
                    else if (message.Event.Equals("create") || message.Event.Equals("patch"))
                    {
                        Segment segment = await this.connector.GetSegment(message.Identifier);
                        this.repository.SetSegment(message.Identifier, segment);
                    }
                }
                catch(Exception ex)
                {
                    Log.Error($"Error processing segment: {message.Identifier} event: {message.Event}.", ex);
                }
            }
        }
    }
}
