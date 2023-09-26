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
    internal class PollingProcessor : IPollingProcessor
    {
        private readonly ILogger<PollingProcessor> logger;
        private readonly IConnector connector;
        private readonly IRepository repository;
        private readonly IPollCallback callback;
        private readonly Config config;
        private readonly SemaphoreSlim readyEvent;
        private Timer pollTimer;
        private bool isInitialized = false;

        public PollingProcessor(IConnector connector, IRepository repository, Config config, IPollCallback callback, ILoggerFactory loggerFactory)
        {
            this.callback = callback;
            this.repository = repository;
            this.connector = connector;
            this.config = config;
            this.readyEvent = new SemaphoreSlim(0, 3);
            this.logger = loggerFactory.CreateLogger<PollingProcessor>();
        }
        public async Task<bool> ReadyAsync()
        {
            await readyEvent.WaitAsync();
            return true;
        }

        public void Start()
        {
            var intervalMs = config.PollIntervalInMiliSeconds;

            if (intervalMs < 60000)
            {
                logger.LogWarning("Poll interval cannot be less than 60 seconds");
                intervalMs = 60000;
            }

            logger.LogDebug("SDKCODE(poll:4000): Polling started, intervalMs: {intervalMs}", intervalMs);
            // start timer which will initiate periodic reading of flags and segments
            pollTimer = new Timer(OnTimedEventAsync, null, 0, intervalMs);
        }
        public void Stop()
        {
            logger.LogDebug("SDKCODE(poll:4001): Polling stopped");
            // stop timer
            if (pollTimer == null) return;
            pollTimer.Dispose();
            pollTimer = null;

        }
        private async Task ProcessFlags()
        {
            try
            {
                logger.LogDebug("Fetching flags started");
                var flags = await this.connector.GetFlags();
                logger.LogDebug("Fetching flags finished");
                foreach (var item in flags)
                {
                    repository.SetFlag(item.Feature, item);
                }

            }
            catch (CfClientException ex)
            {
                logger.LogError(ex,"Exception was raised when fetching flags data with the message {reason}", ex.Message);
                throw;
            }
        }
        private async Task ProcessSegments()
        {
            try
            {
                logger.LogDebug("Fetching segments started");
                IEnumerable<Segment> segments = await connector.GetSegments();
                logger.LogDebug("Fetching segments finished");
                foreach (Segment item in segments)
                {
                    repository.SetSegment(item.Identifier, item);
                }
            }
            catch (CfClientException ex)
            {
                logger.LogError(ex, "Exception was raised when fetching segments data with the message {reason}", ex.Message);
                throw;
            }
        }
        private async void OnTimedEventAsync(object source)
        {
            try
            {
                logger.LogDebug("Running polling iteration");
                await Task.WhenAll(new List<Task> { ProcessFlags(), ProcessSegments() });

                if (isInitialized) return;
                isInitialized = true;
                callback.OnPollerReady();
                readyEvent.Release();
            }
            catch(Exception ex)
            {
                logger.LogWarning(ex,"Polling failed with error: {reason}. Will retry in {pollIntervalInSeconds}", ex.Message, config.pollIntervalInSeconds);
                callback.OnPollError(ex.Message);
            }
        }
    }
}
