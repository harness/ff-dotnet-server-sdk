using io.harness.cfsdk.client.api.analytics;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.HarnessOpenAPIService;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Target = io.harness.cfsdk.client.dto.Target;

namespace io.harness.cfsdk.client.api
{
    internal class InnerClient :
        IAuthCallback,
        IRepositoryCallback,
        IPollCallback,
        IUpdateCallback,
        IEvaluatorCallback,
        IConnectionCallback,
        IDisposable
    {
        private ILoggerFactory loggerFactory;
        private ILogger logger;

        // Services
        private IAuthService authService;
        private IRepository repository;
        private IPollingProcessor polling;
        private IUpdateProcessor update;
        private IEvaluator evaluator;
        private MetricsProcessor metric;
        private IConnector connector;
        private Config config;
        private bool isDisposed = false;

        public event EventHandler InitializationCompleted;
        public event EventHandler<string> EvaluationChanged;
        public event EventHandler<IList<string>> FlagsLoaded;

        private readonly CfClient parent;
        private readonly CountdownEvent sdkReadyLatch = new(1);

        // Use property SdkInitialized for thread-safe access 
        private int sdkInitialized;

        private bool SdkInitialized
        {
            get => Interlocked.CompareExchange(ref sdkInitialized, 0, 0) == 1;
            set => Interlocked.Exchange(ref sdkInitialized, value ? 1 : 0);
        }

        public InnerClient(CfClient parent, ILoggerFactory loggerFactory) { this.parent = parent;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger<InnerClient>();
            this.sdkReadyLatch.Reset(1);
        }
        public InnerClient(string apiKey, Config config, CfClient parent, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.parent = parent;
            this.logger = loggerFactory.CreateLogger<InnerClient>();
            this.config = config;
            Initialize(apiKey, config);
        }

        public InnerClient(IConnector connector, Config config, CfClient parent, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.parent = parent;
            this.logger = loggerFactory.CreateLogger<InnerClient>();
            Initialize(connector, config);
        }

        public void Initialize(string apiKey, Config config)
        {
            if (config.LoggerFactory != null)
            {
                this.loggerFactory = config.LoggerFactory;
                this.logger = loggerFactory.CreateLogger<InnerClient>();
            }
            Initialize(new HarnessConnector(apiKey, config, this, loggerFactory), config);
        }

        public void Initialize(IConnector connector, Config config)
        {
            var evaluationAnalyticsCache = new EvaluationAnalyticsCache();
            var targetAnalyticsCache = new TargetAnalyticsCache();
            this.sdkReadyLatch.Reset(1);
            this.connector = connector;
            this.authService = new AuthService(connector, config, this, loggerFactory);
            this.repository = new StorageRepository(config.Cache, config.Store, this, loggerFactory, config);
            this.polling = new PollingProcessor(connector, this.repository, config, this, loggerFactory);
            this.update = new UpdateProcessor(connector, this.repository, config, this, loggerFactory);
            this.evaluator = new Evaluator(this.repository, this, loggerFactory, config.analyticsEnabled, polling, config);
            // Since 1.4.2, we enable the global target for evaluation metrics. 
            this.metric = new MetricsProcessor(config, evaluationAnalyticsCache, targetAnalyticsCache, new AnalyticsPublisherService(connector, evaluationAnalyticsCache, targetAnalyticsCache, loggerFactory, config), loggerFactory, true);
            Start();
        }
        internal void Start()
        {
            // Start Authentication flow
            Debug.Assert(authService != null, "CfClient has not been constructed properly - check you are using the right instance");
            this.authService.Start();
        }
        private void WaitToInitialize()
        {
            sdkReadyLatch.Wait();
        }
        public void StartAsync()
        {
            Start();
            WaitToInitialize();
        }
        #region Stream callback

        public void OnStreamConnected()
        {
            logger.LogInformation("SDKCODE(stream:5000): SSE stream connected ok");
            this.polling.Stop();
        }
        public void OnStreamDisconnected()
        {
            logger.LogInformation("SDKCODE(stream:5001): SSE stream disconnected");
            this.polling.Start();
        }
        #endregion



        #region Authentication callback
        public void OnAuthenticationSuccess()
        {
            logger.LogInformation("SDKCODE(auth:2000): Authenticated ok");

            polling.Start();
            update.Start();
            metric.Start();

            logger.LogTrace("Signal sdkReadyLatch to release");
            SdkInitialized = true;
            sdkReadyLatch.Signal();
            OnNotifyInitializationCompleted();
            // Check if there are any subscribers to the FlagsLoaded event before calling repository.GetFlags()
            if (FlagsLoaded != null)
            {
                var flagIDs = repository.GetFlags();
                OnNotifyFlagsLoaded(flagIDs);
            }
            logger.LogInformation("SDKCODE(init:1000): The SDK has successfully initialized");
            logger.LogInformation("SDK version: " + Assembly.GetExecutingAssembly().GetName().Version);
        }

        public void OnAuthenticationFailure()
        {
            SdkInitialized = false;
            // Auth has failed so we unblock the WaitForInitialization call 
            sdkReadyLatch.Signal();
        }

        /// <summary>
        /// SDK has authenticated and at least one poll of flags has happened
        /// </summary>
        /// <param name="timeoutMs"></param>
        /// <returns></returns>
        internal bool WaitForSdkToBeReady(int timeoutMs)
        {
            var success = sdkReadyLatch.Wait(timeoutMs);
            if (success)
            {
                logger.LogTrace("Got sdkReadyLatch signal, WaitForSdkToBeReady now released");
            }
            else
            {
                logger.LogWarning("Did not get a signal on sdkReadyLatch within given timeout");
            }

            return success;
        }

        #endregion

        #region Reauthentication callback
        public void OnReauthenticateRequested()
        {
            polling.Stop();
            update.Stop();
            metric.Stop();

            authService.Start();
        }
        #endregion

        #region Poller Callback
        public void OnPollerReady()
        {

        }
        public void OnPollError(string message)
        {

        }

        public void OnPollCompleted()
        {
            // Check if there are any subscribers to the FlagsLoaded event before calling repository.GetFlags()
            if (FlagsLoaded == null) return;
            var flagIDs = repository.GetFlags();
            OnNotifyFlagsLoaded(flagIDs);
        }

        #endregion

        #region Repository callback

        public void OnFlagStored(string identifier)
        {
            OnNotifyEvaluationChanged(identifier);
        }

        public void OnFlagsLoaded(IList<string> identifiers)
        {
            OnNotifyFlagsLoaded(identifiers);
        }

        public void OnFlagDeleted(string identifier)
        {
            OnNotifyEvaluationChanged(identifier);
        }

        public void OnSegmentStored(string identifier)
        {
            repository.FindFlagsBySegment(identifier).ToList().ForEach(i => {
                OnNotifyEvaluationChanged(i);
            });
        }

        public void OnSegmentDeleted(string identifier)
        {
            repository.FindFlagsBySegment(identifier).ToList().ForEach(i => {
                OnNotifyEvaluationChanged(i);
            });
        }
        #endregion

        private void OnNotifyInitializationCompleted()
        {
            InitializationCompleted?.Invoke(parent, EventArgs.Empty);
        }
        private void OnNotifyEvaluationChanged(string identifier)
        {
            EvaluationChanged?.Invoke(parent, identifier);
        }

        private void OnNotifyFlagsLoaded(IList<string> identifiers)
        {
            FlagsLoaded?.Invoke(parent, identifiers);
        }

        /// <summary>
        /// Centralized method to handle variation retrieval with cache state validation and recovery logic.
        /// </summary>
        /// <typeparam name="TValue">The type of the variation value.</typeparam>
        /// <param name="variationFunc">The function to retrieve the variation value.</param>
        /// <param name="kind">The kind of feature flag.</param>
        /// <param name="key">The key of the feature flag.</param>
        /// <param name="target">The target for which the variation is evaluated.</param>
        /// <param name="defaultValue">The default value to return if evaluation fails.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The evaluated variation value or the default value if evaluation fails.</returns>
        private async Task<TValue> RetrieveValue<TValue>(
            Func<Task<TValue>> variationFunc,
            FeatureConfigKind kind,
            string key,
            Target target,
            TValue defaultValue,
            CancellationToken cancellationToken)
        {
            if (!SdkInitialized) return LogAndReturnDefault(kind, key, target, defaultValue);

            try
            {
                return await variationFunc();
            }
            catch (InvalidCacheStateException ex)
            {
                logger.LogWarning(ex,
                    "Invalid cache state detected when evaluating {Kind} variation for flag {Key}, refreshing cache and retrying evaluation",
                    kind, key);

                var timeout = TimeSpan.FromMilliseconds(config.CacheRecoveryTimeoutInMs);
                var result = await polling.RefreshFlagsAndSegmentsAsync(timeout, cancellationToken);

                if (result != RefreshOutcome.Success)
                {
                    logger.LogError(ex,
                        "Refreshing cache for {Kind} variation for flag {Key} failed, returning default variation",
                        kind, key);
                    return LogAndReturnDefault(kind, key, target, defaultValue);
                }

                try
                {
                    return await variationFunc();
                }
                catch (InvalidCacheStateException cex)
                {
                    logger.LogWarning(cex,
                        "Attempted re-evaluation of {Kind} variation for flag {Key} after refreshing cache failed due to invalid cache state, returning default variation",
                        kind, key);
                    return LogAndReturnDefault(kind, key, target, defaultValue);
                }
            }
        }

        public bool BoolVariation(string key, Target target, bool defaultValue)
        {
            return BoolVariationAsync(key, target, defaultValue, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<bool> BoolVariationAsync(string key, Target target, bool defaultValue, CancellationToken cancellationToken)
        {
            return await RetrieveValue(
                async () => await evaluator.BoolVariationAsync(key, target, defaultValue, cancellationToken),
                FeatureConfigKind.Boolean,
                key,
                target,
                defaultValue,
                cancellationToken);
        }

        public string StringVariation(string key, Target target, string defaultValue)
        {
            return StringVariationAsync(key, target, defaultValue, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<string> StringVariationAsync(string key, Target target, string defaultValue, CancellationToken cancellationToken)
        {
            return await RetrieveValue(
                async () => await evaluator.StringVariationAsync(key, target, defaultValue, cancellationToken),
                FeatureConfigKind.String,
                key,
                target,
                defaultValue,
                cancellationToken);
        }

        public double NumberVariation(string key, Target target, double defaultValue)
        {
            return NumberVariationAsync(key, target, defaultValue, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<double> NumberVariationAsync(string key, Target target, double defaultValue, CancellationToken cancellationToken)
        {
            return await RetrieveValue(
                async () => await evaluator.NumberVariationAsync(key, target, defaultValue, cancellationToken),
                FeatureConfigKind.Int,
                key,
                target,
                defaultValue,
                cancellationToken);
        }

        public JToken JsonVariationToken(string key, Target target, JToken defaultValue)
        {
            return JsonVariationTokenAsync(key, target, defaultValue, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<JToken> JsonVariationTokenAsync(string key, Target target, JToken defaultValue, CancellationToken cancellationToken)
        {
            return await RetrieveValue(
                async () => await evaluator.JsonVariationTokenAsync(key, target, defaultValue, cancellationToken),
                FeatureConfigKind.Json,
                key,
                target,
                defaultValue,
                cancellationToken);
        }

        public JObject JsonVariation(string key, Target target, JObject defaultValue)
        {
            return JsonVariationAsync(key, target, defaultValue, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<JObject> JsonVariationAsync(string key, Target target, JObject defaultValue, CancellationToken cancellationToken)
        {
            return await RetrieveValue(
                async () => await evaluator.JsonVariationAsync(key, target, defaultValue, cancellationToken),
                FeatureConfigKind.Json,
                key,
                target,
                defaultValue,
                cancellationToken);
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
                Close();
            }

            isDisposed = true;
        }

        public void Close()
        {
            this.connector?.Close();
            this.authService?.Stop();
            this.repository?.Close();
            this.evaluator?.Dispose();
            this.polling?.Dispose();
            this.update?.Stop();
            this.metric?.Stop();
            this.SdkInitialized = false;
            logger.LogDebug("InnerClient was closed");
        }

        public void Update(Message message, bool manual)
        {
            this.update.Update(message, manual);
        }
        public void EvaluationProcessed(FeatureConfig featureConfig, dto.Target target, Variation variation)
        {
            this.metric.PushToCache(target, featureConfig, variation);
        }

        private T LogAndReturnDefault<T>(FeatureConfigKind kind, string key, Target target, T defaultValue)
        {
            LogEvaluationFailureError(kind, key, target, defaultValue?.ToString() ?? "null");
            return defaultValue;
        }


        private void LogEvaluationFailureError(FeatureConfigKind kind, string featureKey, Target target,
            string defaultValue)
        {
            if (!logger.IsEnabled(LogLevel.Warning)) return;

            // Avoid string concatenation in critical path.
            logger.LogWarning(
                SdkInitialized
                    ? "SDKCODE(eval:6001): Failed to evaluate {Kind} variation for {TargetId}, flag {FeatureId} and the default variation {DefaultValue} is being returned"
                    : "SDKCODE(eval:6001): SDK Not initialized - Failed to evaluate {Kind} variation for {TargetId}, flag {FeatureId} and the default variation {DefaultValue} is being returned",
                kind, target?.Identifier ?? "null target", featureKey, defaultValue);
        }
    }
}
