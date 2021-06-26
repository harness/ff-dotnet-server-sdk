using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using io.harness.cfsdk.HarnessOpenMetricsAPIService;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace io.harness.cfsdk.client.api.analytics
{
    public class AnalyticsPublisherService
    {
        private static string FEATURE_NAME_ATTRIBUTE = "featureName";
        private static string FEATURE_VALUE_ATTRIBUTE = "featureValue";
        private static string VARIATION_VALUE_ATTRIBUTE = "featureValue";
        private static string VARIATION_IDENTIFIER_ATTRIBUTE = "variationIdentifier";
        private static string TARGET_ATTRIBUTE = "target";
        private static HashSet<dto.Target> globalTargetSet = new HashSet<dto.Target>();
        private static HashSet<dto.Target> stagingTargetSet = new HashSet<dto.Target>();
        private static string JAR_VERSION = "JAR_VERSION";
        private static string SDK_TYPE = "SDK_TYPE";
        private static string ANONYMOUS_TARGET = "anonymous";
        private static string SERVER = "server";
        private static string SDK_LANGUAGE = "SDK_LANGUAGE";
        private static string SDK_VERSION = "SDK_VERSION";


        private string sdkVerion = "";

        private DefaultApi metricsAPI;
        private AnalyticsCache analyticsCache;
        private string environmentID;
        private Config config;

        public AnalyticsPublisherService(string jwtToken, Config config, string environmentID, AnalyticsCache analyticsCache)
        {

            metricsAPI = MetricsApiFactory.create(jwtToken, config);
            this.analyticsCache = analyticsCache;
            this.environmentID = environmentID;
            this.config = config;
        }

        public async Task sendDataAndResetCache()
        {
            Log.Information("Reading from queue and building cache");

            Dictionary<Analytics, int> all = analyticsCache.GetAllElements();

            if (all.Count != 0)
            {
                try
                {
                    Metrics metrics = prepareMessageBody(all);
                    if ((metrics.MetricsData != null && metrics.MetricsData.Count >0)
                        || (metrics.TargetData != null && metrics.TargetData.Count > 0))
                    {
                        DateTime startTime = DateTime.Now;
                        HarnessOpenMetricsAPIService.Client client = new HarnessOpenMetricsAPIService.Client(metricsAPI.httpClient);
                        await client.MetricsAsync(environmentID, metrics);
                        Log.Information("Trying to send --->  {Eid} ----- {@mb}", environmentID, metrics);
                        DateTime endTime = DateTime.Now;
                        if ((endTime - startTime).TotalMilliseconds > config.MetricsServiceAcceptableDuration)
                        {
                            Log.Warning("Metrics service API duratopm=[{}]", (endTime - startTime));
                        }
                    }

                    stagingTargetSet.ToList().ForEach(element => globalTargetSet.Add(element));
                    stagingTargetSet.Clear();
                    Log.Information("Successfully sent analytics data to the server");
                    analyticsCache.resetCache();
                }
                catch (HarnessOpenMetricsAPIService.ApiException e)
                {
                    // Clear the set because the cache is only invalidated when there is no
                    // exception, so the targets will reappear in the next iteration
                    Log.Error("Failed to send metricsData {@e}", e);
                }
            }
        }

        private Metrics prepareMessageBody(Dictionary<Analytics, int> all)
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
                    Dictionary<String, string> attributes = target.Attributes;
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
                setMetricsAttriutes(metricsData, JAR_VERSION, "3.1");
                setMetricsAttriutes(metricsData, SDK_TYPE, SERVER);

                setMetricsAttriutes(metricsData, SDK_LANGUAGE, ".NET");
                setMetricsAttriutes(metricsData, SDK_VERSION, sdkVerion);
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
