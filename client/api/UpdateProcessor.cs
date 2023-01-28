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
        private readonly ILogger logger;

        public UpdateProcessor(IConnector connector, IRepository repository, Config config, IUpdateCallback callback, ILogger logger = null)
        {
            this.callback = callback;
            this.repository = repository;
            this.connector = connector;
            this.config = config;
            this.logger = logger ?? Log.Logger;
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
            if (manual && this.config.StreamEnabled)
            {
                logger.Information("You run the update method manually with the stream enabled. Please turn off the stream in this case.");
            }
            //we got a message from server. Dispatch in separate thread.
            Task.Run(() => ProcessMessage(message));
        }
        public void OnStreamConnected()
        {
            this.callback.OnStreamConnected();
        }
        public void OnStreamDisconnected()
        {
            this.callback.OnStreamDisconnected();
            Stop();
            Task.Run(() =>
            {
                Task.Delay(TimeSpan.FromSeconds(this.config.pollIntervalInSeconds)).Wait();
                Start();
            });
        }
        private void ProcessMessage(Message message)
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
                        FeatureConfig feature = this.connector.GetFlag(message.Identifier);
                        this.repository.SetFlag(message.Identifier, feature);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error processing flag: {Identifier} event: {Event}.", message.Identifier, message.Event);
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
                        Segment segment = this.connector.GetSegment(message.Identifier);
                        this.repository.SetSegment(message.Identifier, segment);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error processing segment: {Identifier} event: {Event}.", message.Identifier, message.Event);
                }
            }
        }
    }
}
