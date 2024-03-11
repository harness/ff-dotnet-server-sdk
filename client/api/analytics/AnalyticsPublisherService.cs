using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenMetricsAPIService;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api.analytics
{
    internal class AnalyticsPublisherService
    {
        private static readonly string FeatureNameAttribute = "featureName";
        private static readonly string VariationValueAttribute = "featureValue";
        private static readonly string VariationIdentifierAttribute = "variationIdentifier";
        private static readonly string TargetAttribute = "target";
        internal static readonly ConcurrentDictionary<Target, byte> SeenTargets = new();
        private static readonly string SdkType = "SDK_TYPE";
        private static readonly string Server = "server";
        private static readonly string SdkLanguage = "SDK_LANGUAGE";
        private static readonly string SdkVersion = "SDK_VERSION";
        private readonly IConnector connector;
        private readonly EvaluationAnalyticsCache evaluationAnalyticsCache;
        private readonly ILogger<AnalyticsPublisherService> logger;

        private readonly string sdkVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
        private readonly TargetAnalyticsCache targetAnalyticsCache;

        public AnalyticsPublisherService(IConnector connector, EvaluationAnalyticsCache evaluationAnalyticsCache,
            TargetAnalyticsCache targetAnalyticsCache,
            ILoggerFactory loggerFactory)
        {
            this.evaluationAnalyticsCache = evaluationAnalyticsCache;
            this.targetAnalyticsCache = targetAnalyticsCache;
            this.connector = connector;
            logger = loggerFactory.CreateLogger<AnalyticsPublisherService>();
        }

        public void SendDataAndResetCache()
        {
            var evaluationAnalytics = evaluationAnalyticsCache.GetAllElements();
            var targetAnalytics = targetAnalyticsCache.GetAllElements();

            if (evaluationAnalytics.Count != 0)
                try
                {
                    var metrics = PrepareMessageBody(evaluationAnalytics, targetAnalytics);
                    if ((metrics.MetricsData != null && metrics.MetricsData.Count > 0)
                        || (metrics.TargetData != null && metrics.TargetData.Count > 0))
                    {
                        logger.LogDebug("Sending analytics data :{@a}", metrics);
                        connector.PostMetrics(metrics);
                    }
                    
                    logger.LogDebug("Successfully sent analytics data to the server");
                    evaluationAnalyticsCache.resetCache();
                    targetAnalyticsCache.resetCache();
                }
                catch (CfClientException ex)
                {
                    // Clear the set because the cache is only invalidated when there is no
                    // exception, so the targets will reappear in the next iteration
                    logger.LogError("SDKCODE(stream:7002): Posting metrics failed, reason: {reason}", ex.Message);
                }
        }

        private Metrics PrepareMessageBody(IDictionary<EvaluationAnalytics, int> evaluationsCache,
            IDictionary<TargetAnalytics, int> targetsCache)
        {
            var metrics = new Metrics();
            metrics.TargetData = new List<TargetData>();
            metrics.MetricsData = new List<MetricsData>();

            // Handle EvaluationAnalytics
            foreach (var evaluationAnalytic in evaluationsCache)
            {
                var evaluation = evaluationAnalytic.Key;
                var count = evaluationAnalytic.Value;

                var metricsData = new MetricsData();
                metricsData.Timestamp = GetCurrentUnixTimestampMillis();
                metricsData.Count = count;
                metricsData.MetricsType = MetricsDataMetricsType.FFMETRICS;

                SetMetricsAttributes(metricsData, FeatureNameAttribute, evaluation.FeatureConfig.Feature);
                SetMetricsAttributes(metricsData, VariationIdentifierAttribute,
                    evaluation.Variation.Identifier);
                SetMetricsAttributes(metricsData, VariationValueAttribute, evaluation.Variation.Value);
                SetMetricsAttributes(metricsData, TargetAttribute, evaluation.Target.Identifier);
                SetCommonSdkAttributes(metricsData);
                metrics.MetricsData.Add(metricsData);
            }

            // Handle TargetAnalytics
            foreach (var targetAnalytic in targetsCache)
            {
                var target = targetAnalytic.Key.Target;
                if (target != null && !target.IsPrivate)
                {
                    var targetData = new TargetData
                    {
                        Identifier = target.Identifier,
                        Name = target.Name,
                        Attributes = new List<KeyValue>()
                    };

                    metrics.TargetData.Add(targetData);
                }
            }


            return metrics;
        }
        
        public bool IsTargetSeen(Target target)
        {
            return SeenTargets.ContainsKey(target);
        }
        
        public bool MarkTargetAsSeen(Target target)
        {
            // Attempt to mark the target as seen by adding it to the SeenTargets dictionary.
            // The byte value associated with each target is not used, so it can be set to any arbitrary value.
            // We use 0 here for simplicity. The key operation is whether the target can be added (i.e., has not been seen before).
            return SeenTargets.TryAdd(target, 0);
        }


        private void SetCommonSdkAttributes(MetricsData metricsData)
        {
            SetMetricsAttributes(metricsData, SdkType, Server);
            SetMetricsAttributes(metricsData, SdkLanguage, ".NET");
            SetMetricsAttributes(metricsData, SdkVersion, sdkVersion);
        }

        private void SetMetricsAttributes(MetricsData metricsData, string key, string value)
        {
            var metricsAttributes = new KeyValue();
            metricsAttributes.Key = key;
            metricsAttributes.Value = value;
            metricsData.Attributes.Add(metricsAttributes);
        }

        private long GetCurrentUnixTimestampMillis()
        {
            return (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }
    }
}