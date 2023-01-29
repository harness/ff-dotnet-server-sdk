using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api
{
    internal interface IPollCallback
    {
        /// <summary>
        /// After initial data poll
        /// </summary>
        void OnPollerReady();

        void OnPollError(string message);
    }

    internal interface IPollingProcessor
    {
        /// <summary>
        /// Stop pooling
        /// </summary>
        void Stop();
        /// <summary>
        /// Start periodic pooling
        /// </summary>
        void Start();
        /// <summary>
        /// async function, returns after initial set of flags and segments are returned 
        /// </summary>
        /// <returns>true</returns>
        Task<bool> ReadyAsync();
    }

    /// <summary>
    /// This class is responsible to periodically read from server and persist all flags and
    /// segments.
    /// PollingProcessor will be always started after library is initialized, and continue to
    /// read periodically date in case if SSE is turned off, or unavailable.  
    /// </summary>
    internal sealed class PollingProcessor : IPollingProcessor
    {
        private readonly IConnector connector;
        private readonly IRepository repository;
        private readonly IPollCallback callback;
        private readonly Config config;
        private readonly System.Threading.AutoResetEvent readyEvent;
        private readonly ILogger logger;
        private Timer pollTimer;
        private bool isInitialized = false;

        public PollingProcessor(IConnector connector, IRepository repository, Config config, IPollCallback callback = null)
        {
            this.callback = callback;
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.readyEvent = new System.Threading.AutoResetEvent(false);
            this.logger = config.CreateLogger<PollingProcessor>();
        }
        public async Task<bool> ReadyAsync()
        {
            return await Task.Run(() =>
            {
                this.readyEvent.WaitOne();
                return true;
            });
        }

        public void Start()
        {
            logger.LogInformation("Starting PollingProcessor with request interval: {PollIntervalInSeconds}", this.config.PollIntervalInSeconds);
            // start timer which will initiate periodic reading of flags and segments
            pollTimer = new Timer(new TimerCallback(OnTimedEventAsync), null, 0, this.config.PollIntervalInMiliSeconds);
        }
        public void Stop()
        {
            logger.LogInformation("Stopping PollingProcessor");
            // stop timer
            if (pollTimer != null)
            {
                pollTimer.Dispose();
                pollTimer = null;
            }

        }
        private void ProcessFlags()
        {
            try
            {
                logger.LogDebug("Fetching flags started");
                IEnumerable<FeatureConfig> flags = this.connector.GetFlags();
                logger.LogDebug("Fetching flags finished");
                foreach (FeatureConfig item in flags)
                {
                    repository.SetFlag(item.Feature, item);
                }

            }
            catch (CfClientException ex)
            {
                logger.LogError(ex, "Exception was raised when fetching flags data with the message {Error}", ex.Message);
                throw ex;
            }
        }
        private void ProcessSegments()
        {
            try
            {
                logger.LogDebug("Fetching segments started");
                IEnumerable<Segment> segments = this.connector.GetSegments();
                logger.LogDebug("Fetching segments finished");
                foreach (Segment item in segments)
                {
                    repository.SetSegment(item.Identifier, item);
                }
            }
            catch (CfClientException ex)
            {
                logger.LogError(ex, "Exception was raised when fetching segments data with the message {Message}", ex.Message);
                throw ex;
            }
        }
        private void OnTimedEventAsync(object source)
        {
            try
            {
                logger.LogDebug("Running polling iteration");
                var tasks = new List<Task>();
                tasks.Add(Task.Run(() => ProcessFlags()));
                tasks.Add(Task.Run(() => ProcessSegments()));

                Task.WaitAll(tasks.ToArray());

                if (!isInitialized)
                {
                    this.isInitialized = true;
                    this.callback?.OnPollerReady();
                    this.readyEvent.Set();
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation("Polling will retry in {PollIntervalInSeconds}", this.config.PollIntervalInSeconds);
                this.callback?.OnPollError(ex.Message);
            }
        }
    }
}
