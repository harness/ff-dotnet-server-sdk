using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.HarnessOpenAPIService;
using Serilog;

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
        private IConnector connector;
        private IRepository repository;
        private IPollCallback callback;
        private Timer pollTimer;
        private Config config;
        private ILogger loggerWithContext;
        private bool isInitialized = false;
        private SemaphoreSlim readyEvent;

        public PollingProcessor(IConnector connector, IRepository repository, Config config, IPollCallback callback)
        {
            this.callback = callback;
            this.repository = repository;
            this.connector = connector;
            this.config = config;
            loggerWithContext = Log.ForContext<PollingProcessor>();
            this.readyEvent = new SemaphoreSlim(0, 3);
        }
        public async Task<bool> ReadyAsync()
        {
            await readyEvent.WaitAsync();
            return true;
        }

        public void Start()
        {
            loggerWithContext.Information($"Starting PollingProcessor with request interval: {config.pollIntervalInSeconds}");
            // start timer which will initiate periodic reading of flags and segments
            pollTimer = new Timer(OnTimedEventAsync, null, 0, config.PollIntervalInMiliSeconds);
        }
        public void Stop()
        {
            loggerWithContext.Information("Stopping PollingProcessor");
            // stop timer
            if (pollTimer == null) return;
            pollTimer.Dispose();
            pollTimer = null;

        }
        private async Task ProcessFlags()
        {
            try
            {
                loggerWithContext.Debug("Fetching flags started");
                var flags = await this.connector.GetFlags();
                loggerWithContext.Debug("Fetching flags finished");
                foreach (var item in flags)
                {
                    repository.SetFlag(item.Feature, item);
                }

            }
            catch (CfClientException ex)
            {
                loggerWithContext.Error($"Exception was raised when fetching flags data with the message {ex.Message}");
                throw;
            }
        }
        private async Task ProcessSegments()
        {
            try
            {
                loggerWithContext.Debug("Fetching segments started");
                IEnumerable<Segment> segments = await connector.GetSegments();
                loggerWithContext.Debug("Fetching segments finished");
                foreach (Segment item in segments)
                {
                    repository.SetSegment(item.Identifier, item);
                }
            }
            catch (CfClientException ex)
            {
                loggerWithContext.Error($"Exception was raised when fetching segments data with the message {ex.Message}");
                throw;
            }
        }
        private async void OnTimedEventAsync(object source)
        {
            try
            {
                loggerWithContext.Debug("Running polling iteration");
                await Task.WhenAll(new List<Task> { ProcessFlags(), ProcessSegments() });

                if (isInitialized) return;
                isInitialized = true;
                callback.OnPollerReady();
                readyEvent.Release();
            }
            catch (Exception ex)
            {
                loggerWithContext.Information($"Polling will retry in {config.pollIntervalInSeconds}");
                callback.OnPollError(ex.Message);
            }
        }
    }
}
