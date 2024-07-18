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
        IConnectionCallback
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

        public event EventHandler InitializationCompleted;
        public event EventHandler<string> EvaluationChanged;
        public event EventHandler<IList<string>> FlagsLoaded;

        private readonly CfClient parent;
        private readonly CountdownEvent sdkReadyLatch = new(1);
        
        // Use property SdkInitialized for thread-safe access 
        private int sdkInitialized; 
    
        public bool SdkInitialized 
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

        public void OnPollCompleted(IList<string> identifiers)
        {
            OnNotifyFlagsLoaded(identifiers);
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

        public bool BoolVariation(string key, Target target, bool defaultValue)
        {
            try
            {
                if (SdkInitialized) return evaluator.BoolVariation(key, target, defaultValue);

                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning(
                        "SDK not initialized, returning default variation for {Flag}", key);
                LogEvaluationFailureError(FeatureConfigKind.Boolean, key, target, defaultValue.ToString());
                return defaultValue;
            }

            catch (InvalidCacheStateException ex)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning(ex,
                    "Invalid cache state detected when evaluating boolean variation for flag {Key}, refreshing cache and retrying evaluation ", key);

                // Attempt to refresh cache
                var result = polling.RefreshFlagsAndSegments(TimeSpan.FromMilliseconds(2000));

                // If the refresh has failed or exceeded the timout, return default variation
                if (result != RefreshOutcome.Success)
                {
                    if (logger.IsEnabled(LogLevel.Error))
                        logger.LogError(ex, "Refreshing cache for boolean variation for flag {Key} failed, returning default variation ", key);

                    LogEvaluationFailureError(FeatureConfigKind.Boolean, key, target, defaultValue.ToString());
                    return defaultValue;
                }

                try
                {
                    return evaluator.BoolVariation(key, target, defaultValue);
                }
                
                catch (InvalidCacheStateException)
                {
                    if (logger.IsEnabled(LogLevel.Error))
                        logger.LogWarning(
                            "SDK not initialized, returning default variation for {Flag}", key);

                    LogEvaluationFailureError(FeatureConfigKind.Boolean, key, target, defaultValue.ToString());
                    return defaultValue;
                }
            }
        }

        public string StringVariation(string key, Target target, string defaultValue)
        {
            try
            {
                if (SdkInitialized) return evaluator.StringVariation(key, target, defaultValue);

                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning(
                        "SDK not initialized, returning default variation for {Flag}", key);
                
                LogEvaluationFailureError(FeatureConfigKind.String, key, target, defaultValue);
                return defaultValue;
            }
            
            catch (InvalidCacheStateException ex)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning(ex,
                        "Invalid cache state detected when evaluating string variation for flag {Key}, refreshing cache and retrying evaluation",
                        key);

                var result = polling.RefreshFlagsAndSegments(TimeSpan.FromSeconds(config.CacheRecoveryTimeoutInMs));
                if (result != RefreshOutcome.Success)
                {
                    if (logger.IsEnabled(LogLevel.Error))
                        logger.LogError(
                            "Refreshing cache for string variation for flag {Key} failed, returning default variation",
                            key);
                    LogEvaluationFailureError(FeatureConfigKind.String, key, target, defaultValue);
                    return defaultValue;
                }

                try
                {
                    return evaluator.StringVariation(key, target, defaultValue);
                }
                
                catch (InvalidCacheStateException)
                {
                    if (logger.IsEnabled(LogLevel.Warning))
                        logger.LogWarning(
                            "Attempted re-evaluation of string variation for flag {Key} after refreshing cache failed due to invalid cache state, returning default variation",
                            key);
                    LogEvaluationFailureError(FeatureConfigKind.String, key, target, defaultValue);
                    return defaultValue;
                }
            }
        }


        public double NumberVariation(string key, Target target, double defaultValue)
        {
            try
            {
                if (SdkInitialized) return evaluator.NumberVariation(key, target, defaultValue);

                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning(
                        "SDK not initialized, returning default variation for {Flag}", key);
                
                LogEvaluationFailureError(FeatureConfigKind.Int, key, target, defaultValue.ToString());
                return defaultValue;
            }
            
            catch (InvalidCacheStateException ex)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning(ex,
                        "Invalid cache state detected when evaluating number variation for flag {Key}, refreshing cache and retrying evaluation",
                        key);
                var result = polling.RefreshFlagsAndSegments(TimeSpan.FromSeconds(config.CacheRecoveryTimeoutInMs));
                if (result != RefreshOutcome.Success)
                {
                    if (logger.IsEnabled(LogLevel.Error))
                        logger.LogError(
                            "Refreshing cache for number variation for flag {Key} failed, returning default variation",
                            key);

                    LogEvaluationFailureError(FeatureConfigKind.Int, key, target, defaultValue.ToString());
                    return defaultValue;
                }

                try
                {
                    return evaluator.NumberVariation(key, target, defaultValue);
                }
                
                catch (InvalidCacheStateException)
                {
                    if (logger.IsEnabled(LogLevel.Warning))
                        logger.LogWarning(
                            "Attempted re-evaluation of number variation for flag {Key} after refreshing cache failed due to invalid cache state, returning default variation",
                            key);
                    LogEvaluationFailureError(FeatureConfigKind.Int, key, target, defaultValue.ToString());
                    return defaultValue;
                }
            }
        }

        public JToken JsonVariationToken(string key, Target target, JToken defaultValue)
        {
            try
            {
                if (SdkInitialized) return evaluator.JsonVariationToken(key, target, defaultValue);

                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning(
                        "SDK not initialized, returning default variation for {Flag}", key);
                
                LogEvaluationFailureError(FeatureConfigKind.Json, key, target, defaultValue.ToString());
                return defaultValue;
            }
            
            catch (InvalidCacheStateException ex)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning(ex,
                        "Invalid cache state detected when evaluating json variation for flag {Key}, refreshing cache and retrying evaluation",
                        key);
                var result = polling.RefreshFlagsAndSegments(TimeSpan.FromSeconds(config.CacheRecoveryTimeoutInMs));
                if (result != RefreshOutcome.Success)
                {
                    if (logger.IsEnabled(LogLevel.Error))
                        logger.LogError(
                            "Refreshing cache for json variation for flag {Key} failed, returning default variation",
                            key);
                    LogEvaluationFailureError(FeatureConfigKind.Json, key, target, defaultValue.ToString());
                    return defaultValue;
                }

                try
                {
                    return evaluator.JsonVariationToken(key, target, defaultValue);
                }
                
                catch (InvalidCacheStateException)
                {
                    if (logger.IsEnabled(LogLevel.Warning))
                        logger.LogWarning(
                            "Attempted re-evaluation of json variation for flag {Key} after refreshing cache failed due to invalid cache state, returning default variation",
                            key);
                    LogEvaluationFailureError(FeatureConfigKind.Json, key, target, defaultValue.ToString());
                    return defaultValue;
                }
            }
        }
        

        public JObject JsonVariation(string key, Target target, JObject defaultValue)
        {
            try
            {
                if (SdkInitialized) return evaluator.JsonVariation(key, target, defaultValue);

                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning(
                        "SDK not initialized, returning default variation for {Flag}", key);
                
                LogEvaluationFailureError(FeatureConfigKind.Json, key, target, defaultValue.ToString());
                return defaultValue;
            }
            catch (InvalidCacheStateException ex)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning(ex,
                        "Invalid cache state detected when evaluating json variation for flag {Key}, refreshing cache and retrying evaluation",
                        key);
                var result = polling.RefreshFlagsAndSegments(TimeSpan.FromSeconds(config.CacheRecoveryTimeoutInMs));
                if (result != RefreshOutcome.Success)
                {
                    if (logger.IsEnabled(LogLevel.Error))
                        logger.LogError(
                            "Refreshing cache for json variation for flag {Key} failed, returning default variation",
                            key);
                    LogEvaluationFailureError(FeatureConfigKind.Json, key, target, defaultValue.ToString());
                    return defaultValue;
                }

                try
                {
                    return evaluator.JsonVariation(key, target, defaultValue);
                }
                catch (InvalidCacheStateException)
                {
                    if (logger.IsEnabled(LogLevel.Warning))
                        logger.LogWarning(
                            "Attempted re-evaluation of json variation for flag {Key} after refreshing cache failed due to invalid cache state, returning default variation",
                            key);
                    LogEvaluationFailureError(FeatureConfigKind.Json, key, target, defaultValue.ToString());
                    return defaultValue;
                }
            }
        }

        public void Close()
        {
            this.connector?.Close();
            this.authService?.Stop();
            this.repository?.Close();
            this.polling?.Stop();
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
        
        public void LogEvaluationFailureError(FeatureConfigKind kind, string featureKey, dto.Target target,
            string defaultValue)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning(
                    "SDKCODE(eval:6001): Failed to evaluate {Kind} variation for {TargetId}, flag {FeatureId} and the default variation {DefaultValue} is being returned",
                    kind, target?.Identifier ?? "null target", featureKey, defaultValue);
        }
    }
}
