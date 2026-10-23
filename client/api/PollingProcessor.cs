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

        void OnPollCompleted();
    }

    internal interface IPollingProcessor: IDisposable
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
        Task<RefreshOutcome> RefreshSegmentsAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
        RefreshOutcome RefreshFlags(TimeSpan timeout);
        Task<RefreshOutcome> RefreshFlagsAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
        RefreshOutcome RefreshFlagsAndSegments(TimeSpan timeout);
        Task<RefreshOutcome> RefreshFlagsAndSegmentsAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

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
        private readonly SemaphoreSlim cacheRefreshLock = new SemaphoreSlim(1, 1);
        private DateTime lastFlagsRefreshTime = DateTime.MinValue;
        private DateTime lastSegmentsRefreshTime = DateTime.MinValue;
        private const int MaxCacheRefreshTime = 60;
        private bool isDisposed = false;

        private readonly TimeSpan refreshCooldown = TimeSpan.FromSeconds(MaxCacheRefreshTime);

        public PollingProcessor(IConnector connector, IRepository repository, Config config, IPollCallback callback, ILoggerFactory loggerFactory)
        {
            this.callback = callback;
            this.repository = repository;
            this.connector = connector;
            this.config = config;
            this.logger = loggerFactory.CreateLogger<PollingProcessor>();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }

            if (disposing)
            {
                Stop();
                cacheRefreshLock.Dispose();
            }
                
            isDisposed = true;
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
                ProcessFlagsAndSegments(TimeSpan.FromSeconds(config.CacheRecoveryTimeoutInMs))
                    .ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "First poll did not complete within the specified timeout");
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
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Loaded {SegmentRuleCount} flags", flags.Count());
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,"Exception was raised when fetching flags data with the message: {reason}", ex.Message);
                throw;
            }
        }

        private async Task ProcessFlags(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await ProcessFlags()
                .RunWithTimeout(timeout, cancellationToken);
        }

        private async Task ProcessSegments()
        {
            try
            {
                logger.LogDebug("Fetching segments started");
                IEnumerable<Segment> segments = await connector.GetSegments();
                logger.LogDebug("Fetching segments finished");
                repository.SetSegments(segments);
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Loaded {SegmentRuleCount} groups", segments.Count());
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception was raised when fetching segments data with the message: {reason}", ex.Message);
                throw;
            }
        }

        private async Task ProcessSegments(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await ProcessSegments()
                .RunWithTimeout(timeout, cancellationToken);
        }

        private async Task ProcessFlagsAndSegments(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            // Await both tasks to complete within the timeout
            var tasks = new List<Task>
            {
                ProcessFlags(),
                ProcessSegments()
            };
            await tasks
                .RunWithTimeout(timeout, cancellationToken);
        }

        public RefreshOutcome RefreshFlagsAndSegments(TimeSpan timeout)
        {
            return RefreshFlagsAndSegmentsAsync(timeout, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<RefreshOutcome> RefreshFlagsAndSegmentsAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await cacheRefreshLock.WaitAsync(cancellationToken);

            try
            {
                if (!CanRefreshCache(ref lastSegmentsRefreshTime))
                {
                    logger.LogWarning("Attempt to refresh groups too soon after the last refresh");
                    return RefreshOutcome.TooSoon;
                }

                await ProcessFlagsAndSegments(timeout, cancellationToken);

                UpdateLastRefreshTime(ref lastSegmentsRefreshTime);
                return RefreshOutcome.Success;
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "Refreshing flags and groups did not complete within the specified timeout");
                return RefreshOutcome.Error;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception occurred while refreshing flags and groups");
                return RefreshOutcome.Error;
            }
            finally
            {
                cacheRefreshLock.Release();
            }
        }

        public RefreshOutcome RefreshSegments(TimeSpan timeout)
        {
            return RefreshSegmentsAsync(timeout, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<RefreshOutcome> RefreshSegmentsAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await cacheRefreshLock.WaitAsync(cancellationToken);

            try
            {
                if (!CanRefreshCache(ref lastSegmentsRefreshTime))
                {
                    logger.LogWarning("Attempt to refresh groups too soon after the last refresh");
                    return RefreshOutcome.TooSoon;
                }

                await ProcessSegments(timeout, cancellationToken);
                UpdateLastRefreshTime(ref lastSegmentsRefreshTime);
                return RefreshOutcome.Success;
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "Refresh groups did not complete within the specified timeout");
                return RefreshOutcome.Error;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception occurred while trying to refresh groups");
                return RefreshOutcome.Error;
            }
            finally
            {
                cacheRefreshLock.Release();
            }
        }

        public RefreshOutcome RefreshFlags(TimeSpan timeout)
        {
            return RefreshFlagsAsync(timeout, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<RefreshOutcome> RefreshFlagsAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await cacheRefreshLock.WaitAsync(cancellationToken);

            try
            {
                if (!CanRefreshCache(ref lastFlagsRefreshTime))
                {
                    logger.LogWarning("Attempt to refresh flags too soon after the last refresh");
                    return RefreshOutcome.TooSoon;
                }

                await ProcessFlags(timeout, cancellationToken);
                UpdateLastRefreshTime(ref lastFlagsRefreshTime);
                return RefreshOutcome.Success;
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "RefreshFlags did not complete within the specified timeout");
                return RefreshOutcome.Error;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception occurred while trying to refresh flags");
                return RefreshOutcome.Error;
            }
            finally
            {
                cacheRefreshLock.Release();
            }
        }

        private bool CanRefreshCache(ref DateTime lastRefreshTime)
        {
            return (DateTime.UtcNow - lastRefreshTime) >= refreshCooldown;
        }

        private static void UpdateLastRefreshTime(ref DateTime lastRefreshTime)
        {
            lastRefreshTime = DateTime.UtcNow;
        }


        private async void OnTimedEventAsync(object source)
        {
            try
            {
                logger.LogDebug("Running polling iteration");
                await ProcessFlagsAndSegments(TimeSpan.FromSeconds(config.CacheRecoveryTimeoutInMs));
                callback.OnPollCompleted();
                if (isInitialized) return;
                isInitialized = true;
                callback?.OnPollerReady();
            }
            catch(Exception ex)
            {
                if (ex is TaskCanceledException)
                {
                    logger.LogDebug("Polling thread cancelled");
                    return;
                }
                logger.LogWarning(ex,"Polling failed with error: {reason}. Will retry in {pollIntervalInSeconds}", ex.Message, config.pollIntervalInSeconds);
                callback?.OnPollError(ex.Message);
            }
        }
    }
}
