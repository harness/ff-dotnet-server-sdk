﻿using System.Timers;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;
using Target = io.harness.cfsdk.client.dto.Target;

namespace io.harness.cfsdk.client.api.analytics
{
    internal class MetricsProcessor
    {
        private readonly AnalyticsCache analyticsCache;
        private readonly AnalyticsPublisherService analyticsPublisherService;
        private readonly Config config;
        private readonly ILogger<MetricsProcessor> logger;
        private Timer timer;
        
        private bool isGlobalTargetEnabled;
        private bool warningLoggedForInterval = false;

        public MetricsProcessor(Config config, AnalyticsCache analyticsCache,
            AnalyticsPublisherService analyticsPublisherService, ILoggerFactory loggerFactory, bool globalTargetEnabled)
        {
            this.analyticsCache = analyticsCache;
            this.config = config;
            this.analyticsPublisherService = analyticsPublisherService;
            logger = loggerFactory.CreateLogger<MetricsProcessor>();
            isGlobalTargetEnabled = globalTargetEnabled;
        }

        public void Start()
        {
            if (config.analyticsEnabled)
            {
                timer = new Timer((long)config.Frequency * 1000);
                timer.Elapsed += Timer_Elapsed;
                timer.AutoReset = true;
                timer.Enabled = true;
                timer.Start();
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
            var cacheSize = analyticsCache.GetAllElements().Count;
            var bufferSize = config.bufferSize;

            if (isGlobalTargetEnabled)
            {
                var globalTarget = new Target(EvaluationAnalytics.GlobalTargetIdentifier,
                    EvaluationAnalytics.GlobalTargetName, null);
                PushToEvaluationAnalyticsCache(featureConfig, variation, globalTarget, cacheSize, bufferSize);
            }
            else
            {
                PushToEvaluationAnalyticsCache(featureConfig, variation, target, cacheSize, bufferSize);
            }

            // Create target metrics 
            PushToTargetAnalyticsCache(target, cacheSize, bufferSize);
        }

        private void PushToEvaluationAnalyticsCache(FeatureConfig featureConfig, Variation variation, Target target,
            int cacheSize, int bufferSize)
        {
            Analytics evaluationAnalytics = new EvaluationAnalytics(featureConfig, variation, target);

            var evaluationCount = analyticsCache.getIfPresent(evaluationAnalytics);

            // Cache is full and this is new entry, so we should discard this eval
            if (cacheSize > bufferSize && evaluationCount == 0)
            {
                LogMetricsIgnoredWarning(cacheSize, bufferSize);
                return;
            }
            
            // If cache isn't full, or if there is an existing matching key, add this one
            analyticsCache.Put(evaluationAnalytics, evaluationCount + 1);
        }


        private void PushToTargetAnalyticsCache(Target target, int cacheSize, int bufferSize)
        {
            //  Cache is full, discard target
            if (cacheSize > bufferSize)
            {
                LogMetricsIgnoredWarning(cacheSize, bufferSize);
                return;
            }

            Analytics targetAnalytics = new TargetAnalytics(target);

            // We don't need to keep count of targets, so use a constant value, 1, for the count. 
            // Since 1.4.2, the analytics cache was refactored to separate out Evaluation and Target metrics, but the 
            // change did not go as far as to maintain two caches (due to effort involved), but differentiate them based on subclassing, so 
            // the counter used for target metrics isn't needed, but causes no issue. 
            analyticsCache.Put(targetAnalytics, 1);
        }

        private void LogMetricsIgnoredWarning(int cacheSize, int bufferSize)
        {
            // Only log this once per interval
            if (warningLoggedForInterval)
            {
                return;
            }
            
            logger.LogWarning(
                "Metric frequency map exceeded buffer size ({cacheSize} > {bufferSize}), not sending any further" +
                "metrics for interval. Increase metrics buffer using client config option if required. ",
                cacheSize,
                bufferSize);
            
            warningLoggedForInterval = true;
        }

        internal void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            logger.LogDebug("Timer Elapsed - Processing/Sending analytics data");
            SendMetrics();
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
                logger.LogError(ex, "Failed to send analytics data to server");
            }
        }
    }
}