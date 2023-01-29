using System;
using System.Collections.Generic;
using System.Linq;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using io.harness.cfsdk.HarnessOpenMetricsAPIService;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api.analytics
{
    internal sealed class AnalyticsPublisherService
    {
        private static readonly string FEATURE_NAME_ATTRIBUTE = "featureName";
        private static readonly string VARIATION_VALUE_ATTRIBUTE = "featureValue";
        private static readonly string VARIATION_IDENTIFIER_ATTRIBUTE = "variationIdentifier";
        private static readonly string TARGET_ATTRIBUTE = "target";
        private static readonly HashSet<dto.Target> globalTargetSet = new HashSet<dto.Target>();
        private static readonly HashSet<dto.Target> stagingTargetSet = new HashSet<dto.Target>();
        private static readonly string SDK_TYPE = "SDK_TYPE";
        private static readonly string ANONYMOUS_TARGET = "anonymous";
        private static readonly string SERVER = "server";
        private static readonly string SDK_LANGUAGE = "SDK_LANGUAGE";
        private static readonly string SDK_VERSION = "SDK_VERSION";
        private static readonly Version sdkVersion = typeof(AnalyticsPublisherService).Assembly.GetName().Version;

        private readonly AnalyticsCache analyticsCache;
        private readonly IConnector connector;
        private readonly ILogger logger;

        public AnalyticsPublisherService(IConnector connector, AnalyticsCache analyticsCache, Config config)
        {
            _ = config ?? throw new ArgumentNullException(nameof(config));
            this.analyticsCache = analyticsCache ?? throw new ArgumentNullException(nameof(analyticsCache));
            this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
            this.logger = config.CreateLogger<AnalyticsPublisherService>();
        }

        public void sendDataAndResetCache()
        {
            logger.LogInformation("Reading from queue and building cache, SDL version: {SdkVersion}", sdkVersion);

            IDictionary<Analytics, int> all = analyticsCache.GetAllElements();

            if (all.Count != 0)
            {
                try
                {
                    Metrics metrics = prepareMessageBody(all);
                    if ((metrics.MetricsData != null && metrics.MetricsData.Count > 0)
                        || (metrics.TargetData != null && metrics.TargetData.Count > 0))
                    {
                        logger.LogDebug("Sending analytics data: {@Metrics}", metrics);
                        connector.PostMetrics(metrics);
                    }

                    stagingTargetSet.ToList().ForEach(element => globalTargetSet.Add(element));
                    stagingTargetSet.Clear();
                    logger.LogInformation("Successfully sent analytics data to the server");
                    analyticsCache.resetCache();
                }
                catch (CfClientException ex)
                {
                    // Clear the set because the cache is only invalidated when there is no
                    // exception, so the targets will reappear in the next iteration
                    logger.LogError(ex, "Failed to send metricsData: {Error}", ex.Message);
                }
            }
        }

        private Metrics prepareMessageBody(IDictionary<Analytics, int> all)
        {
            Metrics metrics = new Metrics();
            metrics.TargetData = new List<TargetData>();
            metrics.MetricsData = new List<MetricsData>();

            // using for-each loop for iteration over Map.entrySet()
            foreach (KeyValuePair<Analytics, int> entry in all)
            {
                // Set target data
                TargetData targetData = new TargetData();
                // Set Metrics data
                MetricsData metricsData = new MetricsData();

                Analytics analytics = entry.Key;
                HashSet<string> privateAttributes = analytics.Target.PrivateAttributes;
                dto.Target target = analytics.Target;
                FeatureConfig featureConfig = analytics.FeatureConfig;
                Variation variation = analytics.Variation;
                if (!globalTargetSet.Contains(target) && !target.IsPrivate)
                {
                    stagingTargetSet.Add(target);
                    Dictionary<string, string> attributes = target.Attributes;
                    attributes.ToList().ForEach(el =>
                       {
                           KeyValue keyValue = new KeyValue();
                           if ((privateAttributes.Count != 0))
                           {
                               if (!privateAttributes.Contains(el.Key))
                               {
                                   keyValue.Key = el.Key;
                                   keyValue.Value = el.Value;
                               }
                           }
                           else
                           {
                               keyValue.Key = el.Key;
                               keyValue.Value = el.Value;
                           }
                           targetData.Attributes.Add(keyValue);
                       });
                    targetData.Identifier = target.Identifier;
                    targetData.Name = target.Name;
                    metrics.TargetData.Add(targetData);
                }

                metricsData.Timestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
                metricsData.Count = entry.Value;
                metricsData.MetricsType = MetricsDataMetricsType.FFMETRICS;
                setMetricsAttriutes(metricsData, FEATURE_NAME_ATTRIBUTE, featureConfig.Feature);
                setMetricsAttriutes(metricsData, VARIATION_IDENTIFIER_ATTRIBUTE, variation.Identifier);
                setMetricsAttriutes(metricsData, VARIATION_VALUE_ATTRIBUTE, variation.Value);
                if (target.IsPrivate)
                {
                    setMetricsAttriutes(metricsData, TARGET_ATTRIBUTE, ANONYMOUS_TARGET);
                }
                else
                {
                    setMetricsAttriutes(metricsData, TARGET_ATTRIBUTE, target.Identifier);
                }
                setMetricsAttriutes(metricsData, SDK_TYPE, SERVER);

                setMetricsAttriutes(metricsData, SDK_LANGUAGE, ".NET");
                setMetricsAttriutes(metricsData, SDK_VERSION, sdkVersion.ToString());
                metrics.MetricsData.Add(metricsData);
            }

            return metrics;
        }

        private void setMetricsAttriutes(MetricsData metricsData, String key, String value)
        {
            KeyValue metricsAttributes = new KeyValue();
            metricsAttributes.Key = key;
            metricsAttributes.Value = value;
            metricsData.Attributes.Add(metricsAttributes);
        }
    }
}
