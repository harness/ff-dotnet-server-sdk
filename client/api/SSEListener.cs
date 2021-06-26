using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.HarnessOpenAPIService;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace io.harness.cfsdk.client.api
{
    public class SSEListener
    {
        private DefaultApi defaultApi;
        private FeatureConfigCache featureCache;
        private SegmentCache segmentCache;
        private string environmentID;
        private string clusterIdentifier;
        private CfClient cfClient;

        public SSEListener(DefaultApi defaultApi, FeatureConfigCache featureCache, SegmentCache segmentCache, string environmentID, string clusterIdentifier, CfClient cfClient)
        {
            this.defaultApi = defaultApi;
            this.featureCache = featureCache;
            this.segmentCache = segmentCache;
            this.environmentID = environmentID;
            this.clusterIdentifier = clusterIdentifier;
            this.cfClient = cfClient;
        }

        public async Task onMessage(string message)
        {
            if (!message.Contains("domain"))
            {
                return;
            }

            JObject jsommessage = JObject.Parse("{"+message+"}");


            string domain = (string)jsommessage["data"]["domain"];
            if (domain.Equals("flag"))
            {
               await processFeature(jsommessage);
            }
            else if (domain.Equals("target-segment"))
            {
                //processSegment(jsonObject);
            }
        }

        private async Task processFeature(JObject jsommessage)
        {
            Log.Information("Syncing the latest features..");
            string identifier = (string)jsommessage["data"]["identifier"];
            long version = long.Parse((string)jsommessage["data"]["version"]);  
            string eventcode = (string) jsommessage["data"]["event"];

            if (eventcode == "delete")
            {
                var f = featureCache.getIfPresent(identifier); // just to be loged
                featureCache.Delete(identifier);
                Log.Information("Feature: {@Key} - removed  ---> {@f}", identifier, f);
                return;
            }


            for (int i = 0; i < 3; i++)
            {
                try
                {
                    Client client = new Client(defaultApi.httpClient);
                    FeatureConfig featureConfig =
                          await client.ClientEnvFeatureConfigsGetAsync(identifier, environmentID, clusterIdentifier);
                    if (version.Equals(featureConfig.Version))
                    {
                        featureCache.Put(featureConfig.Feature, featureConfig);
                        Log.Information("New featue properties: {@Key} - {@f}", featureConfig.Feature, featureConfig);
                        break;
                    }
                }
                catch (ApiException e)
                {
                    Log.Error("Failed to sync the feature {f} due to {e}", identifier, e);
                }
            }
        }

        private async Task processSegment(JObject jsommessage)
        {
            Log.Information("Syncing the latest segments..");
            string identifier = (string)jsommessage["data"]["identifier"];
            try
            {
                Client client = new Client(defaultApi.httpClient);
                IEnumerable<Segment> segments = await client.ClientEnvTargetSegmentsGetAsync(environmentID, clusterIdentifier);
          
                if (segments != null)
                {
                    foreach (Segment item in segments)
                    {
                        Log.Information("{@Key} - {@s}", item.Identifier, item);
                        segmentCache.Put(item.Identifier, item);
                    }
                }
            }
            catch (ApiException e)
            {
                Log.Error("Failed to sync the segment {s} due to {@e}", identifier,e);
            }
        }

    }
}
