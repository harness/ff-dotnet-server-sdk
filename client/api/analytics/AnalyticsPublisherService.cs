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
        private static readonly ConcurrentDictionary<Target, byte> StagingSeenTargets = new();
        private static readonly string SdkType = "SDK_TYPE";
        private static readonly string AnonymousTarget = "anonymous";
        private static readonly string Server = "server";
        private static readonly string SdkLanguage = "SDK_LANGUAGE";
        private static readonly string SdkVersion = "SDK_VERSION";
        private readonly AnalyticsCache analyticsCache;
        private readonly IConnector connector;
        private readonly ILogger<AnalyticsPublisherService> logger;

        private readonly string sdkVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";

        public AnalyticsPublisherService(IConnector connector, AnalyticsCache analyticsCache,
            ILoggerFactory loggerFactory)
        {
            this.analyticsCache = analyticsCache;
            this.connector = connector;
            logger = loggerFactory.CreateLogger<AnalyticsPublisherService>();
        }

        public void SendDataAndResetCache()
        {
            var all = analyticsCache.GetAllElements();

            if (all.Count != 0)
                try
                {
                    var metrics = PrepareMessageBody(all);
                    if ((metrics.MetricsData != null && metrics.MetricsData.Count > 0)
                        || (metrics.TargetData != null && metrics.TargetData.Count > 0))
                    {
                        logger.LogDebug("Sending analytics data :{@a}", metrics);
                        connector.PostMetrics(metrics);
                    }

                    foreach (var uniqueTarget in StagingSeenTargets.Keys) SeenTargets.TryAdd(uniqueTarget, 0);
                    StagingSeenTargets.Clear();
                    logger.LogDebug("Successfully sent analytics data to the server");
                    analyticsCache.resetCache();
                }
                catch (CfClientException ex)
                {
                    // Clear the set because the cache is only invalidated when there is no
                    // exception, so the targets will reappear in the next iteration
                    logger.LogError("SDKCODE(stream:7002): Posting metrics failed, reason: {reason}", ex.Message);
                }
        }

        private Metrics PrepareMessageBody(IDictionary<Analytics, int> all)
        {
            var metrics = new Metrics();
            metrics.TargetData = new List<TargetData>();
            metrics.MetricsData = new List<MetricsData>();

            foreach (var entry in all)
            {
                var analytics = entry.Key;
                var count = entry.Value;

                if (analytics is EvaluationAnalytics evaluationAnalytics)
                {
                    // Handle Evaluation Analytics
                    var metricsData = new MetricsData();
                    metricsData.Timestamp = GetCurrentUnixTimestampMillis();
                    metricsData.Count = count;
                    metricsData.MetricsType = MetricsDataMetricsType.FFMETRICS;

                    SetMetricsAttributes(metricsData, FeatureNameAttribute, evaluationAnalytics.FeatureConfig.Feature);
                    SetMetricsAttributes(metricsData, VariationIdentifierAttribute,
                        evaluationAnalytics.Variation.Identifier);
                    SetMetricsAttributes(metricsData, VariationValueAttribute, evaluationAnalytics.Variation.Value);
                    SetMetricsAttributes(metricsData, TargetAttribute, evaluationAnalytics.Target.Identifier);
                    SetCommonSdkAttributes(metricsData);

                    metrics.MetricsData.Add(metricsData);
                }
                else if (analytics is TargetAnalytics targetAnalytics)
                {
                    var target = targetAnalytics.Target;
                    if (target != null && !SeenTargets.ContainsKey(target) && !target.IsPrivate)
                    {
                        var targetData = new TargetData
                        {
                            Identifier = target.Identifier,
                            Name = target.Name,
                            Attributes = new List<KeyValue>(),
                        };

                        // Add target attributes, respecting private attributes
                        foreach (var attribute in target.Attributes)
                        {
                            if (target.PrivateAttributes == null || !target.PrivateAttributes.Contains(attribute.Key))
                            {
                                targetData.Attributes.Add(new KeyValue
                                    { Key = attribute.Key, Value = attribute.Value });
                            }
                        }

                        // Add to StagingSeenTargets for future reference
                        StagingSeenTargets.TryAdd(target, 0);

                        metrics.TargetData.Add(targetData);
                    }
                }
            }

            return metrics;
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