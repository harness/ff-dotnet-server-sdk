﻿using System.Timers;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;
using Target = io.harness.cfsdk.client.dto.Target;
using Timer = System.Timers.Timer;

namespace io.harness.cfsdk.client.api.analytics
{
    internal class MetricsProcessor
    {
        private readonly EvaluationAnalyticsCache evaluationAnalyticsCache;
        private readonly TargetAnalyticsCache targetAnalyticsCache;
        private readonly AnalyticsPublisherService analyticsPublisherService;
        private readonly Config config;
        private readonly ILogger<MetricsProcessor> logger;
        private readonly int evaluationMetricsMaxSize;
        private readonly int targetMetricsMaxSize;
        private Timer timer;
        private Timer seenTargetsCacheResetTimer;

        private readonly bool isGlobalTargetEnabled;
        private bool warningLoggedForInterval = false;
        
        private readonly Target globalTarget = new (EvaluationAnalytics.GlobalTargetIdentifier,
            EvaluationAnalytics.GlobalTargetName, null);

        public MetricsProcessor(Config config, EvaluationAnalyticsCache evaluationAnalyticsCache, TargetAnalyticsCache targetAnalyticsCache,
            AnalyticsPublisherService analyticsPublisherService, ILoggerFactory loggerFactory, bool globalTargetEnabled)
        {
            this.evaluationAnalyticsCache = evaluationAnalyticsCache;
            this.targetAnalyticsCache = targetAnalyticsCache;
            this.config = config;
            this.analyticsPublisherService = analyticsPublisherService;
            evaluationMetricsMaxSize = config.evaluationMetricsMaxSize;
            targetMetricsMaxSize = config.targetMetricsMaxSize;
            logger = loggerFactory.CreateLogger<MetricsProcessor>();
            isGlobalTargetEnabled = globalTargetEnabled;
        }

        public void Start()
        {
            if (config.analyticsEnabled)
            {
                // Metrics thread timer
                timer = new Timer((long)config.Frequency * 1000);
                timer.Elapsed += Timer_Elapsed;
                timer.AutoReset = true;
                timer.Enabled = true;
                timer.Start();
                
                // SeenTargetsCache timer
                seenTargetsCacheResetTimer = new Timer((long)config.seenTargetsCacheTtlInSeconds * 1000);
                seenTargetsCacheResetTimer.Elapsed += SeenTargetsCacheResetTimer_Elapsed;
                seenTargetsCacheResetTimer.AutoReset = true;
                seenTargetsCacheResetTimer.Enabled = true;
                seenTargetsCacheResetTimer.Start();
                
                logger.LogInformation("SDKCODE(metric:7000): Metrics thread started");

            }
        }


        public void Stop()
        {
            if (config.analyticsEnabled && timer != null)
            {
                timer.Stop();
                timer = null;
                logger.LogInformation("SDKCODE(metric:7001): Metrics thread exited");
            }
        }

        public void PushToCache(Target target, FeatureConfig featureConfig, Variation variation)
        {
            if (isGlobalTargetEnabled)
            {
                PushToEvaluationAnalyticsCache(featureConfig, variation, globalTarget);
            }
            else
            {
                PushToEvaluationAnalyticsCache(featureConfig, variation, target);
            }

            // Create target metrics 
            PushToTargetAnalyticsCache(target);
        }

        private void PushToEvaluationAnalyticsCache(FeatureConfig featureConfig, Variation variation, Target target)
        {
            var cacheSize = evaluationAnalyticsCache.Count();
            
            EvaluationAnalytics evaluationAnalytics = new EvaluationAnalytics(featureConfig, variation, target);

            var evaluationCount = evaluationAnalyticsCache.getIfPresent(evaluationAnalytics);

            // Cache is full and this is new entry, so we should discard this eval
            if (cacheSize > evaluationMetricsMaxSize && evaluationCount == 0)
            {
                LogMetricsIgnoredWarning("Evaluation metrics", cacheSize, evaluationMetricsMaxSize);
                return;
            }
            
            // If cache isn't full, or if there is an existing matching key, add this one
            evaluationAnalyticsCache.Put(evaluationAnalytics, evaluationCount + 1);
        }


        private void PushToTargetAnalyticsCache(Target target)
        {

            if (target?.Identifier == null || target.IsPrivate)
            {
                return;
            }
            
            if (analyticsPublisherService.IsTargetSeen(target.Identifier))
            {
                // Target has already been processed in a previous interval, so ignore it.
                return;
            }
            
            var cacheSize = targetAnalyticsCache.Count();
            
            //  Cache is full, discard target
            if (cacheSize > targetMetricsMaxSize)
            {
                LogMetricsIgnoredWarning("Target metrics", cacheSize, targetMetricsMaxSize);
                return;
            }

            TargetAnalytics targetAnalytics = new TargetAnalytics(target);
            
            targetAnalyticsCache.Put(target.Identifier, targetAnalytics);

            analyticsPublisherService.MarkTargetAsSeen(target.Identifier);
        }

        private void LogMetricsIgnoredWarning(string cacheType, int cacheSize, int bufferSize)
        {
            // Only log this once per interval
            if (warningLoggedForInterval || !logger.IsEnabled(LogLevel.Warning))
            {
                return;
            }

            {
                logger.LogWarning(
                    "{cacheType} frequency map exceeded buffer size ({cacheSize} > {bufferSize}), not sending any further" +
                    " {cacheType} metrics for interval. Increase buffer size using client config option if required.",
                    cacheType,
                    cacheSize,
                    bufferSize,
                    cacheType);

                warningLoggedForInterval = true;
            }
        }

        internal void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Timer Elapsed - Processing/Sending analytics data");
            SendMetrics();
        }
        
        private void SeenTargetsCacheResetTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            analyticsPublisherService.ResetSeenTargetsCache();
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("SeenTargetsCache reset");
        }

        internal void SendMetrics()
        {
            try
            {
                analyticsPublisherService.SendDataAndResetCache();
                warningLoggedForInterval = false;
            }
            catch (CfClientException ex)
            {
                if (logger.IsEnabled(LogLevel.Error))
                    logger.LogError(ex, "Failed to send analytics data to server");
            }
        }
    }
}