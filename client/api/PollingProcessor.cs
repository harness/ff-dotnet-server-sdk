using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api
{
    public enum RefreshOutcome
    {
        Success,
        Error,
        TooSoon,
    }
    
    internal interface IPollCallback
    {
        /// <summary>
        /// After initial data poll
        /// </summary>
        void OnPollerReady();

        void OnPollError(string message);

        void OnPollRan(List<string> identifiers);
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

        RefreshOutcome RefreshSegments(TimeSpan timeout);
        RefreshOutcome RefreshFlags(TimeSpan timeout);
    
        RefreshOutcome RefreshFlagsAndSegments(TimeSpan timeout);

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
        private Timer pollTimer;
        private bool isInitialized = false;
        private readonly object cacheRefreshLock = new object();
        private DateTime lastFlagsRefreshTime = DateTime.MinValue;
        private DateTime lastSegmentsRefreshTime = DateTime.MinValue;       
        private const int MaxCacheRefreshTime = 60;

        private readonly TimeSpan refreshCooldown = TimeSpan.FromSeconds(MaxCacheRefreshTime);

        public PollingProcessor(IConnector connector, IRepository repository, Config config, IPollCallback callback, ILoggerFactory loggerFactory)
        {
            this.callback = callback;
            this.repository = repository;
            this.connector = connector;
            this.config = config;
            this.logger = loggerFactory.CreateLogger<PollingProcessor>();
        }

        public void Start()
        {
            var intervalMs = config.PollIntervalInMiliSeconds;

            if (intervalMs < 60000)
            {
                logger.LogWarning("Poll interval cannot be less than 60 seconds");
                intervalMs = 60000;
            }

            logger.LogDebug("Populate cache for first time after authentication");

            try
            {
                Task.WhenAll(new List<Task> { ProcessFlags(), ProcessSegments() }).Wait();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "First poll failed: {Reason}", ex.Message);
            }

            logger.LogInformation("SDKCODE(poll:4000): Polling started, intervalMs: {intervalMs}", intervalMs);
            // start timer which will initiate periodic reading of flags and segments
            pollTimer = new Timer(OnTimedEventAsync, null, intervalMs, intervalMs);
        }
        public void Stop()
        {
            logger.LogInformation("SDKCODE(poll:4001): Polling stopped");
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
                repository.SetFlags(flags);
                logger.LogDebug("Loaded {SegmentRuleCount} flags", flags.Count());

            }
            catch (Exception ex)
            {
                logger.LogError(ex,"Exception was raised when fetching flags data with the message: {reason}", ex.Message);
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
                repository.SetSegments(segments);
                logger.LogDebug("Loaded {SegmentRuleCount} groups", segments.Count());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception was raised when fetching segments data with the message: {reason}", ex.Message);
                throw;
            }
        }

        public RefreshOutcome RefreshFlagsAndSegments(TimeSpan timeout)
        {
            lock (cacheRefreshLock)
            {
                if (!CanRefreshCache(ref lastSegmentsRefreshTime))
                {
                    logger.LogWarning("Attempt to refresh groups too soon after the last refresh");
                    return RefreshOutcome.TooSoon;
                }

                var processSegmentsTask = Task.Run(async () => await ProcessSegments());
                var processFlagsTask = Task.Run(async () => await ProcessFlags());

                try
                {
                    // Await both tasks to complete within the timeout
                    var refreshSuccessful = Task.WaitAll(new[] { processSegmentsTask, processFlagsTask }, timeout);
                    if (refreshSuccessful)
                    {
                        UpdateLastRefreshTime(ref lastSegmentsRefreshTime);
                        return RefreshOutcome.Success;
                    }

                    logger.LogWarning("Refreshing flags and groups did not complete within the specified timeout");
                    return RefreshOutcome.Error;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception occurred while refreshing flags and groups");
                    return RefreshOutcome.Error;
                }
            }
        }

        public RefreshOutcome RefreshSegments(TimeSpan timeout)
        {
            lock (cacheRefreshLock)
            {
                if (!CanRefreshCache(ref lastSegmentsRefreshTime))
                {
                    logger.LogWarning("Attempt to refresh groups too soon after the last refresh");
                    return RefreshOutcome.TooSoon;
                }

                try
                {
                    var task = Task.Run(async () => await ProcessSegments());
                    var refreshSuccessful = task.Wait(timeout);
                    if (refreshSuccessful)
                    {
                        UpdateLastRefreshTime(ref lastSegmentsRefreshTime);
                        return RefreshOutcome.Success;
                    }

                    logger.LogWarning("Refresh groups did not complete within the specified timeout");
                    return RefreshOutcome.Error;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception occurred while trying to refresh groups");
                    return RefreshOutcome.Error;
                }
            }
        }

        public RefreshOutcome RefreshFlags(TimeSpan timeout)
        {
            lock (cacheRefreshLock)
            {
                if (!CanRefreshCache(ref lastFlagsRefreshTime))
                {
                    logger.LogWarning("Attempt to refresh flags too soon after the last refresh");
                    return RefreshOutcome.TooSoon;
                }
                try
                {
                    var task = Task.Run(async () => await ProcessFlags());
                    var refreshSuccessful = task.Wait(timeout);
                    if (refreshSuccessful)
                    {
                        UpdateLastRefreshTime(ref lastFlagsRefreshTime);
                        return RefreshOutcome.Success;
                    }

                    logger.LogWarning("RefreshFlags did not complete within the specified timeout");
                    return RefreshOutcome.Error;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception occurred while trying to refresh flags");
                    return RefreshOutcome.Error;
                }
            }
        }

   private bool CanRefreshCache(ref DateTime lastRefreshTime)
    {
        return (DateTime.UtcNow - lastRefreshTime) >= refreshCooldown;
    }

    private void UpdateLastRefreshTime(ref DateTime lastRefreshTime)
    {
        lastRefreshTime = DateTime.UtcNow;
    }

        
        private async void OnTimedEventAsync(object source)
        {
            try
            {
                logger.LogDebug("Running polling iteration");
                await Task.WhenAll(new List<Task> { ProcessFlags(), ProcessSegments() });
                var flagIDs = repository.GetFlags();
                callback.OnPollRan(flagIDs);
                if (isInitialized) return;
                isInitialized = true;
                callback?.OnPollerReady();
            }
            catch(Exception ex)
            {
                logger.LogWarning(ex,"Polling failed with error: {reason}. Will retry in {pollIntervalInSeconds}", ex.Message, config.pollIntervalInSeconds);
                callback?.OnPollError(ex.Message);
            }
        }
    }
}
