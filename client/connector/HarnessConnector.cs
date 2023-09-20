using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Serialization;

namespace io.harness.cfsdk.client.connector
{
    interface IConnectionCallback
    {
        void OnReauthenticateRequested();
    }

    internal class HarnessConnector : IConnector
    {
        private readonly ILogger<HarnessConnector> logger;
        private readonly ILoggerFactory loggerFactory;
        private string token;
        private static string environment;
        private static string accountID;
        private string cluster;

        public HttpClient apiHttpClient { get; set; }
        public HttpClient metricHttpClient { get; set; }
        public HttpClient sseHttpClient { get; set; }

        private string apiKey;
        private Config config;
        private IConnectionCallback callback;
        private Client harnessClient;

        private IService currentStream;
        private CancellationTokenSource cancelToken = new CancellationTokenSource();

        private static readonly string sdkVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";

        private static HttpClient ApiHttpClient(Config config)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(config.ConfigUrl);
            client.Timeout = TimeSpan.FromSeconds(config.ConnectionTimeout);
            client.DefaultRequestHeaders.Add("Harness-SDK-Info", $".Net {sdkVersion} Server");
            return client;
        }
        private static HttpClient MetricHttpClient(Config config)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(config.EventUrl);
            client.Timeout = TimeSpan.FromSeconds(config.ConnectionTimeout);
            client.DefaultRequestHeaders.Add("Harness-SDK-Info", $".Net {sdkVersion} Client");
            if (accountID != null)
            {
                client.DefaultRequestHeaders.Add("Harness-AccountID", accountID);

            }
            client.DefaultRequestHeaders.Add("Harness-EnvironmentID", environment);
            return client;
        }
        private static HttpClient SseHttpClient(Config config, string apiKey)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(config.ConfigUrl.EndsWith("/") ? config.ConfigUrl : config.ConfigUrl + "/" );
            client.DefaultRequestHeaders.Add("API-Key", apiKey);
            client.DefaultRequestHeaders.Add("Accept", "text /event-stream");
            client.DefaultRequestHeaders.Add("Harness-SDK-Info", $".Net {sdkVersion} Server");
            client.DefaultRequestHeaders.Add("Harness-EnvironmentID", environment);
            if (accountID != null)
            {
                client.DefaultRequestHeaders.Add("Harness-AccountID", accountID);
            }
            client.Timeout = TimeSpan.FromMinutes(1);
            return client;
        }
        
        public HarnessConnector(string apiKey, Config config, IConnectionCallback callback, ILoggerFactory loggerFactory)
        : this(apiKey, config, callback, ApiHttpClient(config), MetricHttpClient(config), SseHttpClient(config, apiKey), loggerFactory)
        {
           
        }

        private static Client HarnessClient(Config config, HttpClient httpClient, ILoggerFactory loggerFactory)
        {
            Client client = new Client(httpClient);
            client.JsonSerializerSettings.ContractResolver = new JsonContractResolver(loggerFactory);
            client.BaseUrl = config.ConfigUrl;
            return client;
        }

        public HarnessConnector(
            string apiKey,
            Config config,
            IConnectionCallback callback,
            HttpClient apiHttpClient,
            HttpClient metricHttpClient,
            HttpClient sseHttpClient,
            ILoggerFactory loggerFactory
        ) : this(apiKey, config, callback, apiHttpClient, metricHttpClient, sseHttpClient, HarnessClient(config, apiHttpClient, loggerFactory), loggerFactory)
        {
        }

        public HarnessConnector(
            string apiKey,
            Config config,
            IConnectionCallback callback,
            HttpClient apiHttpClient,
            HttpClient metricHttpClient,
            HttpClient sseHttpClient,
            Client harnessClient,
            ILoggerFactory loggerFactory)
        {
            this.config = config;
            this.apiKey = apiKey;
            this.callback = callback;
            this.apiHttpClient = apiHttpClient;
            this.metricHttpClient = metricHttpClient;
            this.sseHttpClient = sseHttpClient;
            this.harnessClient = harnessClient;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger<HarnessConnector>();
        }

        private async Task<T> ReauthenticateIfNeeded<T>(Func<Task<T>> task)
        {
            try
            {
                return await task();
            }
            catch (ApiException ex)
            {
                if (ex.StatusCode == (int)HttpStatusCode.Forbidden)
                {
                    logger.LogError("Initiate reauthentication");
                    callback.OnReauthenticateRequested();
                }
                throw new CfClientException(ex.Message);
            }
            catch (Exception ex)
            {
                throw new CfClientException(ex.Message);
            }
        }
        public async Task<IEnumerable<FeatureConfig>> GetFlags()
        {
            return await ReauthenticateIfNeeded(() => harnessClient.ClientEnvFeatureConfigsGetAsync(environment, cluster, cancelToken.Token));
        }
        
        public async Task<IEnumerable<Segment>> GetSegments()
        {
            return await ReauthenticateIfNeeded(() => harnessClient.ClientEnvTargetSegmentsGetAsync(environment, cluster, cancelToken.Token));
        }
        public Task<FeatureConfig> GetFlag(string identifier)
        {
            return ReauthenticateIfNeeded(() => harnessClient.ClientEnvFeatureConfigsGetAsync(identifier, environment, cluster, cancelToken.Token));
        }
        public Task<Segment> GetSegment(string identifier)
        {
            return ReauthenticateIfNeeded(() => harnessClient.ClientEnvTargetSegmentsGetAsync(identifier, environment, cluster, cancelToken.Token));
        }
        public IService Stream(IUpdateCallback updater)
        {
            currentStream?.Close();
            var url = $"stream?cluster={cluster}";
            currentStream = new EventSource(sseHttpClient, url, config, updater, loggerFactory);
            return currentStream;
        }
        public async Task PostMetrics(HarnessOpenMetricsAPIService.Metrics metrics)
        {
            await ReauthenticateIfNeeded<Task>(async () =>
            {
                var startTime = DateTime.Now;
                var client = new HarnessOpenMetricsAPIService.Client(metricHttpClient)
                {
                    BaseUrl = config.EventUrl
                };
                try {
                    await client.MetricsAsync(environment, metrics, cancelToken.Token);
                }
                catch (ApiException ex) {
                    logger.LogWarning(ex, "SDKCODE(metric:7002): Posting metrics failed, reason: {reason}", ex.Message);
                }

                var endTime = DateTime.Now;
                if ((endTime - startTime).TotalMilliseconds > config.MetricsServiceAcceptableDuration)
                {
                    logger.LogWarning("Metrics post duration exceeded allowable=[{allowableTime}]", endTime - startTime);
                }

                return null;
            });
        }
        public async Task<string> Authenticate()
        {
            if (string.IsNullOrWhiteSpace(apiKey)) {
                var errorMsg = "SDKCODE(init:1002):The SDK has failed to initialize due to a missing or empty API key.";
                logger.LogError(errorMsg);
                throw new CfClientException(errorMsg);
            }

            try
            {
                var authenticationRequest = new AuthenticationRequest
                {
                    ApiKey = apiKey,
                    Target = new Target2 { Identifier = "" }
                };
                var response = await harnessClient.ClientAuthAsync(authenticationRequest, cancelToken.Token);
                token = response.AuthToken;

                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token);
                var jwtToken = (JwtSecurityToken)jsonToken;

                // accountID is not sent when using the relay proxy
                if (jwtToken.Payload.TryGetValue("accountID", out var accountIdValue) && accountIdValue != null)
                {
                    accountID = accountIdValue.ToString();
                }                
                environment = jwtToken.Payload["environment"].ToString();
                cluster = jwtToken.Payload["clusterIdentifier"].ToString();
                var environmentIdentifier = jwtToken.Payload.ContainsKey("environmentIdentifier") ? jwtToken.Payload["clusterIdentifier"].ToString() : environment;

                foreach (var httpClient in new[] { apiHttpClient, metricHttpClient, sseHttpClient })
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    if (!String.IsNullOrEmpty(environmentIdentifier))
                    {
                        httpClient.DefaultRequestHeaders.Add("Harness-EnvironmentID", environmentIdentifier);
                    }

                    if (!String.IsNullOrEmpty(accountID))
                    {
                        httpClient.DefaultRequestHeaders.Add("Harness-AccountID", accountID);
                    }
                }
                return token;

            }
            catch (ApiException ex)
            {
                logger.LogError(ex, "SDKCODE(init:1001):The SDK has failed to initialize due to the following authentication error: {reason}", ex.Message);

                if (ex.StatusCode == (int)HttpStatusCode.Unauthorized || ex.StatusCode == (int)HttpStatusCode.Forbidden)
                {
                    var errorMsg = "SDKCODE(init:1001):The SDK has failed to initialize due to the following authentication error: Invalid apiKey. Defaults will be served.";
                    logger.LogError(errorMsg);
                    throw new CfClientException(errorMsg);
                }
                throw new CfClientException(ex.Message);
            }
        }

        public void Close()
        {
            cancelToken.Cancel();
            currentStream.Close();
        }
    }
}
