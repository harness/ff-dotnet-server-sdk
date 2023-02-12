using System;
using System.Threading.Tasks;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;

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
    internal sealed class UpdateProcessor : IUpdateCallback, IUpdateProcessor
    {
        private readonly IConnector connector;
        private readonly IRepository repository;
        private readonly IUpdateCallback callback;
        private readonly Config config;
        private readonly ILogger logger;

        private IService service;
        private bool stopping;

        public UpdateProcessor(IConnector connector, IRepository repository, Config config, IUpdateCallback callback = null)
        {
            this.callback = callback;
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.logger = config.CreateLogger<UpdateProcessor>();
        }

        public void Start()
        {
            if (config.StreamEnabled)
            {
                this.service = connector.Stream(this);
                this.service.Start();
            }
        }
        public void Stop()
        {
            if (this.service != null)
            {
                stopping = true;
                this.service.Stop();
                this.service = null;
                stopping = false;
            }
        }

        public void Update(Message message, bool manual)
        {
            if (manual && this.config.StreamEnabled)
            {
                logger.LogInformation("You run the update method manually with the stream enabled. Please turn off the stream in this case.");
            }
            //we got a message from server. Dispatch in separate thread.
            _ = ProcessMessage(message);
        }
        public void OnStreamConnected()
        {
            this.callback?.OnStreamConnected();
        }

        private async Task StartAfterInterval()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(this.config.PollIntervalInMiliSeconds));
            Start();
        }
        public void OnStreamDisconnected()
        {
            this.callback?.OnStreamDisconnected();

            if (stopping) return;

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
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing flag: {Identifier} event: {Event}.", message.Identifier, message.Event);
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
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing segment: {Identifier} event: {Event}.", message.Identifier, message.Event);
                }
            }
        }
    }
}
