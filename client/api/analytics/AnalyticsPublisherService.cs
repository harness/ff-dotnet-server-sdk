using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using io.harness.cfsdk.HarnessOpenMetricsAPIService;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api.analytics
{
    internal class AnalyticsPublisherService
    {
        private readonly ILogger<AnalyticsPublisherService> logger;

        private static readonly string FeatureNameAttribute = "featureName";
        private static readonly string VariationValueAttribute = "featureValue";
        private static readonly string VariationIdentifierAttribute = "variationIdentifier";
        private static readonly string TargetAttribute = "target";
        private static readonly ConcurrentDictionary<dto.Target, byte> GlobalTargetSet = new ConcurrentDictionary<dto.Target, byte>();
        private static readonly ConcurrentDictionary<dto.Target, byte> StagingTargetSet = new ConcurrentDictionary<dto.Target, byte>();
        private static readonly string SdkType = "SDK_TYPE";
        private static readonly string AnonymousTarget = "anonymous";
        private static readonly string Server = "server";
        private static readonly string SdkLanguage = "SDK_LANGUAGE";
        private static readonly string SdkVersion = "SDK_VERSION";

        private readonly string sdkVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
        private readonly AnalyticsCache analyticsCache;
        private readonly IConnector connector;

        public AnalyticsPublisherService(IConnector connector, AnalyticsCache analyticsCache, ILoggerFactory loggerFactory)
        {
            this.analyticsCache = analyticsCache;
            this.connector = connector;
            this.logger = loggerFactory.CreateLogger<AnalyticsPublisherService>();
        }

        public void SendDataAndResetCache()
        {
            IDictionary<Analytics, int> all = analyticsCache.GetAllElements();

            if (all.Count != 0)
            {
                try
                {
                    Metrics metrics = PrepareMessageBody(all);
                    if ((metrics.MetricsData != null && metrics.MetricsData.Count >0)
                        || (metrics.TargetData != null && metrics.TargetData.Count > 0))
                    {
                        logger.LogDebug("Sending analytics data :{@a}", metrics);
                        connector.PostMetrics(metrics);
                    }

                    foreach (var uniqueTarget in StagingTargetSet.Keys)
                    {
                        GlobalTargetSet.TryAdd(uniqueTarget, 0);
                    }                    StagingTargetSet.Clear();
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

        private Metrics PrepareMessageBody(IDictionary<Analytics, int> all)
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
                if (target != null && !GlobalTargetSet.ContainsKey(target) && !target.IsPrivate)
                {
                    HashSet<string> privateAttributes = analytics.Target.PrivateAttributes;
                    StagingTargetSet.TryAdd(target, 0);
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
                        SetMetricsAttributes(metricsData, TargetAttribute, AnonymousTarget);
                    }
                    else
                    {
                        SetMetricsAttributes(metricsData, TargetAttribute, target.Identifier);
                    }
                       
                    targetData.Identifier = target.Identifier;
                    targetData.Name = target.Name;
                    metrics.TargetData.Add(targetData);
                }

                metricsData.Timestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
                metricsData.Count = entry.Value;
                metricsData.MetricsType = MetricsDataMetricsType.FFMETRICS;
                SetMetricsAttributes(metricsData, FeatureNameAttribute, featureConfig.Feature);
                SetMetricsAttributes(metricsData, VariationIdentifierAttribute, variation.Identifier);
                SetMetricsAttributes(metricsData, VariationValueAttribute, variation.Value);

                SetMetricsAttributes(metricsData, SdkType, Server);

                SetMetricsAttributes(metricsData, SdkLanguage, ".NET");
                SetMetricsAttributes(metricsData, SdkVersion, sdkVersion);
                metrics.MetricsData.Add(metricsData);
            }

            return metrics;
        }

        private void SetMetricsAttributes(MetricsData metricsData, String key, String value)
        {
            KeyValue metricsAttributes = new KeyValue();
            metricsAttributes.Key = key;
            metricsAttributes.Value = value;
            metricsData.Attributes.Add(metricsAttributes);
        }
    }
}
