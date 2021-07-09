using io.harness.cfsdk.client.api.analytics;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.polling;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace io.harness.cfsdk.client.api
{
    public class CfClient
    {
        private  string apiKey;
        private  Config config;
        private  bool isAnalyticsEnabled;
        private string jwtToken; 
        private string environmentID;
        private string cluster;

        private FeatureConfigCache featureCache;
        private SegmentCache segmentCache;
        private Evaluator evaluator;

        private DefaultApi defaultApi;
        private HttpClient SSEHttpclient;
        private ShortTermPolling poller;
        //private Request sseRequest;
        private SSEListener listener;
        public StreamReader streamReader { get; set; }
        //private ServerSentEvent sse;
        private AnalyticsManager analyticsManager;
        
        public bool isInitialized { get; set; }
        internal static CfClient instance;

        public static async  Task<CfClient> getInstance(string apiKey, Config config)
        {
            if (instance == null)
            {
                Log.Information("\n\nSTARTING NEW INSTANCE");
                instance = new CfClient(apiKey,config);
                await instance.Authenticate();
                await instance.init();
            }

            if (instance.apiKey != apiKey)
            {
                Log.Error("Client with different ApiKey exist");
                throw new ApiException("Client with different ApiKey exist",0,null,null,null);
            };

            return instance;
        }

       

        public static async Task<CfClient> getInstance()
        {
           
            if (instance == null)
            {
                Log.Error("Client not created yet");
                throw new ApiException("Client not created yet", 0, null, null, null);

            };

            return instance;
        }
        /// <summary>
        /// setter for jwtToken
        /// </summary>
        /// <param name="jwt"></param>
        public void setjwtToken(string jwt)
        {
            jwtToken = jwt;
        }

        public CfClient(string apiKey): this(apiKey, Config.Builder().Build()) { }

        public CfClient(string apiKey, Config config)
        {
            this.apiKey = apiKey;
            this.config = config;

            isAnalyticsEnabled = config.analyticsEnabled;
            //cache init
            featureCache = new FeatureConfigCache();
            segmentCache = new SegmentCache();

            defaultApi =
                DefaultApiFactory.create(
                    config.configUrl,
                    config.connectionTimeout,
                    config.readTimeout,
                    config.writeTimeout,
                    config.debug);

            isInitialized = false;
        }

        private async Task Authenticate()
        {
            if(instance == null)
            {
                throw new Exception("Client not created");
            }

            // try to authenticate
            AuthService authService =
                    new AuthService(defaultApi, apiKey, this, config.PollIntervalInSeconds);
            await authService.Authenticate();
           
        }

        private async Task init()
        {
            jwtToken = defaultApi.jwttoken;

            var handler = new JwtSecurityTokenHandler();
            SecurityToken jsonToken = handler.ReadToken(jwtToken);
            JwtSecurityToken JWTToken = (JwtSecurityToken)jsonToken;
            Log.Information("JWT Payload is --> {j}\n\n", JWTToken.Payload);

            environmentID = JWTToken.Payload["environment"].ToString();
            cluster = JWTToken.Payload["clusterIdentifier"].ToString();

            evaluator = new Evaluator(segmentCache);

            await initCache(environmentID);


            if (!config.StreamEnabled)
            {
                startPollingMode(config.pollIntervalInSeconds);
                
                Log.Information("Start Running in POLLING mode on {p} sec - SSE disabled.\n\n", config.pollIntervalInSeconds);
            }
            else
            {
                StartSSE();
            }

            analyticsManager =
                config.AnalyticsEnabled ? new AnalyticsManager(environmentID, jwtToken, config) : null;
            isInitialized = true;
        }

        public void StartSSE()
        {
            if (streamReader != null) return;

            config.streamEnabled = true;
            if (listener == null)
            {
                listener = new SSEListener(defaultApi, featureCache, segmentCache, environmentID, cluster, this);
            }
            Task.Run(() => initStreamingMode(jwtToken, cluster));

            // startSSE();
            Log.Information("Start Running in SSE mode.\n\n");
        }

        private async Task   initStreamingMode(string jwttoken, string cluster)
        {

            try 
            {
                SSEHttpclient = new HttpClient();
                SSEHttpclient.DefaultRequestHeaders.Authorization
                                             = new AuthenticationHeaderValue("Bearer", jwttoken);
                SSEHttpclient.DefaultRequestHeaders.Add("API-Key", this.apiKey);
                SSEHttpclient.DefaultRequestHeaders.Add("Accept", "text /event-stream");

                SSEHttpclient.Timeout = Timeout.InfiniteTimeSpan;


                while (this.config.streamEnabled)
                {
                    try
                    {
                        Log.Information("SSE --> Establishing connection");
                        using (streamReader = new StreamReader(await SSEHttpclient.GetStreamAsync(this.defaultApi.getBasePath()+"/stream?cluster=" + cluster)))
                        {
                            while (!streamReader.EndOfStream)
                            {
                                string message = await streamReader.ReadLineAsync();

                                if (!string.IsNullOrEmpty(message)) Log.Information("SSE Received update  ---> {@m} ", message);
                                await listener.onMessage(message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //Here you can check for 
                        //specific types of errors before continuing
                        //Since this is a simple example, i'm always going to retry
                        Log.Error("Error: {@e}", ex);
                        Log.Information("SSE - interupted");
                        Log.Information("POLLING - one iteration from sse");
                        if (config.streamEnabled)
                        {
                            await ReschedulePooling();
                            await Task.Delay(TimeSpan.FromSeconds(10000));
                        }
                        else
                        {
                            startPollingMode(10000);
                            Log.Information("Start Running in POLLING mode - SSE disabled.\n\n");
                        }

                    }
                    Log.Information("SSE --> STOP-ed");
                }
            }
            catch (Exception e )
            {
                Log.Error("SSE --> Failed to establish connection {@e}", e);
                await ReschedulePooling();
                await Task.Delay(TimeSpan.FromSeconds(10000));
            }
        }

        private async Task initCache(string environmentID)
        {
            if (!string.IsNullOrEmpty(environmentID))
            {
                Client client = new Client(defaultApi.httpClient);

                IEnumerable<FeatureConfig> respF = await client.ClientEnvFeatureConfigsGetAsync(environmentID, cluster);
                Log.Information("Cache INIT with FeatureConfig's");
                foreach (FeatureConfig item in respF)
                {
                    Log.Information("{@Key} - {@f}", item.Feature, item);
                    featureCache.Put(item.Feature, item);
                }

                IEnumerable<Segment> respS = await client.ClientEnvTargetSegmentsGetAsync(environmentID, cluster);
                Log.Information("Cache INIT with Segments's\n\n");
                foreach (Segment item in respS)
                {
                    Log.Information("{@Key} - {@s}", item.Identifier, item);
                    segmentCache.Put(item.Identifier, item);
                }
            }
        }
        private void startPollingMode(int interval)
        {
            poller = new ShortTermPolling(interval);
            poller.start(ReschedulePooling_timerOP);

        }

        public void StopPollingMode()
        {
            poller.stop();
        }

        /// <summary>
        /// method retrives by polling all FeatureConfig's & Segment's
        /// used to be trigered by polling timer
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        internal async void ReschedulePooling_timerOP(object source, System.Timers.ElapsedEventArgs e)
        {
            await ReschedulePooling();

        }

        private async Task ReschedulePooling()
        {
            Client client = new Client(defaultApi.httpClient);
            Log.Information("POLLING Started - one iteration");
            IEnumerable<FeatureConfig> respF = await client.ClientEnvFeatureConfigsGetAsync(environmentID, cluster);
            foreach (FeatureConfig item in respF)
            {
                featureCache.Put(item.Feature, item);
            }
            Log.Information("Cache Updated with FeatureConfig's");

            IEnumerable<Segment> respS = await client.ClientEnvTargetSegmentsGetAsync(environmentID, cluster);
            foreach (Segment item in respS)
            {
                segmentCache.Put(item.Identifier, item);
            }
            Log.Information("Cache Updated with Segment's");
            Log.Information("POLLING Stoped");


            if (config.StreamEnabled)
            {
                if(poller !=null) poller.stop();

                StartSSE();
            }
        }

        public async Task<bool> boolVariation(string key, dto.Target target, bool defaultValue)
        {
            bool servedVariation = defaultValue;
            Variation variation = null;
            FeatureConfig featureConfig = featureCache.getIfPresent(key);
            try
            {
                if (featureConfig == null || featureConfig.Kind != FeatureConfigKind.Boolean)
                {
                    return defaultValue;
                }

                // If pre requisite exists, go ahead till the last dependency else return
                if (!(featureConfig.Prerequisites == null || featureConfig.Prerequisites.Count ==0))
                {
                    bool result = checkPreRequisite(featureConfig, target);
                    if (!result)
                    {
                        servedVariation = bool.Parse(featureConfig.OffVariation);
                        return servedVariation;
                    }
                }
                variation = evaluator.evaluate(featureConfig, target);
                servedVariation = bool.Parse(variation.Value);
                return servedVariation;
            }
            catch (Exception e)
            {
                Log.Error("err {e}", e);
                return defaultValue;
            }
            finally
            {
                if (!target.IsPrivate
                    && target.isValid()
                    && isAnalyticsEnabled
                    && analyticsManager != null
                    && featureConfig != null
                    && variation != null)
                {
                    analyticsManager.pushToQueue(target, featureConfig, variation);
                }
            }
        }

        public async Task<string> stringVariation(string key, dto.Target target, string defaultValue)
        {
            string stringVariation = defaultValue;
            Variation variation = null;
            FeatureConfig featureConfig = featureCache.getIfPresent(key);
            try
            {
                if (featureConfig == null || featureConfig.Kind != FeatureConfigKind.String)
                {
                    return defaultValue;
                }

                // If pre requisite exists, go ahead till the last dependency else return
                if (!(featureConfig.Prerequisites == null || featureConfig.Prerequisites.Count == 0))
                {
                    bool result = checkPreRequisite(featureConfig, target);
                    if (!result)
                    {
                        stringVariation = featureConfig.Variations.FirstOrDefault(f=>f.Identifier==  featureConfig.OffVariation).Value;
                        return stringVariation;
                    }
                }
                variation = evaluator.evaluate(featureConfig, target);
                stringVariation = (string)variation.Value;
                return stringVariation;
            }
            catch (Exception e)
            {
                Log.Error("err {e}", e);
                return defaultValue;
            }
            finally
            {
                if (!target.IsPrivate
                    && target.isValid()
                    && isAnalyticsEnabled
                    && analyticsManager != null
                    && featureConfig != null
                    && variation != null)
                {
                    analyticsManager.pushToQueue(target, featureConfig, variation);
                }
            }
        }

        public async Task<double> numberVariation(string key, dto.Target target, int defaultValue)
        {
            double numberVariation = defaultValue;
            Variation variation = null;
            FeatureConfig featureConfig = featureCache.getIfPresent(key);
            if (featureConfig == null || featureConfig.Kind != FeatureConfigKind.Int)
            {
                return defaultValue;
            }

            try
            {
                // If pre requisite exists, go ahead till the last dependency else return
                if (!(featureConfig.Prerequisites == null || featureConfig.Prerequisites.Count == 0))
                {
                    bool result = checkPreRequisite(featureConfig, target);
                    if (!result)
                    {
                        numberVariation = int.Parse(featureConfig.Variations.FirstOrDefault(f => f.Identifier == featureConfig.OffVariation).Value);

                        //numberVariation = int.Parse(featureConfig.OffVariation);
                        return numberVariation;
                    }
                }
                variation = evaluator.evaluate(featureConfig, target);
                numberVariation = int.Parse(variation.Value);
                return numberVariation;
            }
            catch (Exception e)
            {
                Log.Error("err {e}", e);
                return defaultValue;
            }
            finally
            {
                if (!target.IsPrivate
                    && target.isValid()
                    && isAnalyticsEnabled
                    && analyticsManager != null
                    && featureConfig != null
                    && variation != null)
                {
                    analyticsManager.pushToQueue(target, featureConfig, variation);
                }
            }
        }


        public async Task<JObject> jsonVariation(string key, dto.Target target, JObject defaultValue)
        {
            JObject jsonObject = defaultValue;
            Variation variation = null;
            FeatureConfig featureConfig = featureCache.getIfPresent(key);
            try
            {
                if (featureConfig == null || featureConfig.Kind != FeatureConfigKind.Json)
                {
                    return defaultValue;
                }

                // If pre requisite exists, go ahead till the last dependency else return
                if (!(featureConfig.Prerequisites == null || featureConfig.Prerequisites.Count == 0))
                {
                    bool result = checkPreRequisite(featureConfig, target);
                    if (!result)
                    {
                        jsonObject = JObject.Parse(featureConfig.Variations.FirstOrDefault(f => f.Identifier == featureConfig.OffVariation).Value);

                       // jsonObject = JObject.Parse(featureConfig.OffVariation);
                        return jsonObject;
                    }
                }
                variation = evaluator.evaluate(featureConfig, target);
                jsonObject = JObject.Parse(variation.Value);
                //jsonObject = new Gson().fromJson((string)variation.Value, JObject.class);
                return jsonObject;
            }
            catch (Exception e)
            {
                Log.Error("err {e}", e);
                return defaultValue;
            }
            finally
            {
                if (!target.IsPrivate
                    && target.isValid()
                    && isAnalyticsEnabled
                    && analyticsManager != null
                    && featureConfig != null
                    && variation != null)
                {
                    analyticsManager.pushToQueue(target, featureConfig, variation);
                }
            }
        }

        private bool checkPreRequisite(FeatureConfig parentFeatureConfig, dto.Target target)
        {
            bool result = true;
            List<Prerequisite> prerequisites = parentFeatureConfig.Prerequisites.ToList();
            if (!(prerequisites == null || prerequisites.Count==0)) 
            {
                Log.Information(
                    "Checking pre requisites {@p} of parent feature {@f}",
                    prerequisites,
                    parentFeatureConfig);
                foreach (Prerequisite pqs in prerequisites) {
                    string preReqFeature = pqs.Feature;
                    FeatureConfig preReqFeatureConfig = featureCache.getIfPresent(preReqFeature);
                    if (preReqFeatureConfig == null) {
                        Log.Error(
                            "Could not retrieve the pre requisite details of feature flag :{f}",
                            preReqFeatureConfig.Feature);
                    }

                    // Pre requisite variation value evaluated below
                    object preReqEvaluatedVariation =
                        evaluator.evaluate(preReqFeatureConfig, target).Value;
                    Log.Information(
                            "Pre requisite flag {f} has variation {@v} for target {@t}",
                            preReqFeatureConfig.Feature,
                            preReqEvaluatedVariation,
                            target);

                    // Compare if the pre requisite variation is a possible valid value of
                    // the pre requisite FF
                    List<string> validPreReqVariations = pqs.Variations.ToList();
                    Log.Information(
                            "Pre requisite flag {f} should have the variations {@v}",
                            preReqFeatureConfig.Feature,
                            validPreReqVariations);
                    if (!validPreReqVariations.Contains(preReqEvaluatedVariation.ToString())) {
                        return false;
                    } else
                    {
                        result = checkPreRequisite(preReqFeatureConfig, target);
                    }
                }
            }
            return result;
        }


        public async Task  StopSSE(bool streamenabled = false)
        {
            this.config.streamEnabled = streamenabled;
            streamReader.Close();
            streamReader.Dispose();
            streamReader = null;
        }

        public FeatureConfigCache GetFCache()
        {
            return featureCache;
        }
        public SegmentCache GetSCache()
        {
            return segmentCache;
        }
    }

}
