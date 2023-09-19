using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using io.harness.cfsdk.HarnessOpenMetricsAPIService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api.analytics
{
    internal class AnalyticsPublisherService
    {
        private readonly ILogger<AnalyticsPublisherService> logger;

        private static string FEATURE_NAME_ATTRIBUTE = "featureName";
        private static string VARIATION_VALUE_ATTRIBUTE = "featureValue";
        private static string VARIATION_IDENTIFIER_ATTRIBUTE = "variationIdentifier";
        private static string TARGET_ATTRIBUTE = "target";
        private static HashSet<dto.Target> globalTargetSet = new HashSet<dto.Target>();
        private static HashSet<dto.Target> stagingTargetSet = new HashSet<dto.Target>();
        private static string SDK_TYPE = "SDK_TYPE";
        private static string ANONYMOUS_TARGET = "anonymous";
        private static string SERVER = "server";
        private static string SDK_LANGUAGE = "SDK_LANGUAGE";
        private static string SDK_VERSION = "SDK_VERSION";

        private readonly string sdkVersion = Assembly.GetExecutingAssembly().GetName().ToString();
        private AnalyticsCache analyticsCache;
        private IConnector connector;

        public AnalyticsPublisherService(IConnector connector, AnalyticsCache analyticsCache, ILoggerFactory loggerFactory)
        {
            this.analyticsCache = analyticsCache;
            this.connector = connector;
            this.logger = loggerFactory.CreateLogger<AnalyticsPublisherService>();
        }

        public void sendDataAndResetCache()
        {
            IDictionary<Analytics, int> all = analyticsCache.GetAllElements();

            if (all.Count != 0)
            {
                try
                {
                    Metrics metrics = prepareMessageBody(all);
                    if ((metrics.MetricsData != null && metrics.MetricsData.Count >0)
                        || (metrics.TargetData != null && metrics.TargetData.Count > 0))
                    {
                        logger.LogDebug("Sending analytics data :{@a}", metrics);
                        connector.PostMetrics(metrics);
                    }

                    stagingTargetSet.ToList().ForEach(element => globalTargetSet.Add(element));
                    stagingTargetSet.Clear();
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
                dto.Target target = analytics.Target;

                FeatureConfig featureConfig = analytics.FeatureConfig;
                Variation variation = analytics.Variation;
                if (target != null && !globalTargetSet.Contains(target) && !target.IsPrivate)
                {
                    HashSet<string> privateAttributes = analytics.Target.PrivateAttributes;
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

                    if (target.IsPrivate)
                    {
                        setMetricsAttributes(metricsData, TARGET_ATTRIBUTE, ANONYMOUS_TARGET);
                    }
                    else
                    {
                        setMetricsAttributes(metricsData, TARGET_ATTRIBUTE, target.Identifier);
                    }
                       
                    targetData.Identifier = target.Identifier;
                    targetData.Name = target.Name;
                    metrics.TargetData.Add(targetData);
                }

                metricsData.Timestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
                metricsData.Count = entry.Value;
                metricsData.MetricsType = MetricsDataMetricsType.FFMETRICS;
                setMetricsAttributes(metricsData, FEATURE_NAME_ATTRIBUTE, featureConfig.Feature);
                setMetricsAttributes(metricsData, VARIATION_IDENTIFIER_ATTRIBUTE, variation.Identifier);
                setMetricsAttributes(metricsData, VARIATION_VALUE_ATTRIBUTE, variation.Value);

                setMetricsAttributes(metricsData, SDK_TYPE, SERVER);

                setMetricsAttributes(metricsData, SDK_LANGUAGE, ".NET");
                setMetricsAttributes(metricsData, SDK_VERSION, sdkVersion);
                metrics.MetricsData.Add(metricsData);
            }

            return metrics;
        }

        private void setMetricsAttributes(MetricsData metricsData, String key, String value)
        {
            KeyValue metricsAttributes = new KeyValue();
            metricsAttributes.Key = key;
            metricsAttributes.Value = value;
            metricsData.Attributes.Add(metricsAttributes);
        }
    }
}
