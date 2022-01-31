using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace io.harness.cfsdk.client.connector
{
    interface IConnectionCallback
    {
        void OnReauthenticateRequested();
    }

    internal class HarnessConnector : IConnector
    {
        private String token;
        private String environment;
        private String cluster;

        public HttpClient apiHttpClient { get; set; }
        public HttpClient metricHttpClient { get; set; }
        public HttpClient sseHttpClient { get; set; }

        private string apiKey;
        private Config config;
        private IConnectionCallback callback;

        private IService currentStream;
        private CancellationTokenSource cancelToken = new CancellationTokenSource();
        public HarnessConnector(String apiKey, Config config, IConnectionCallback callback)
        {
            this.config = config;
            this.apiKey = apiKey;
            this.callback = callback;

            this.apiHttpClient = new HttpClient();
            this.apiHttpClient.BaseAddress = new Uri(config.ConfigUrl);
            this.apiHttpClient.Timeout = TimeSpan.FromSeconds(config.ConnectionTimeout);

            this.metricHttpClient = new HttpClient();
            this.metricHttpClient.BaseAddress = new Uri(config.eventUrl);
            this.metricHttpClient.Timeout = TimeSpan.FromSeconds(config.ConnectionTimeout);

            this.sseHttpClient = new HttpClient();
            this.sseHttpClient.BaseAddress = new Uri(this.config.ConfigUrl.EndsWith("/") ? this.config.ConfigUrl : this.config.ConfigUrl + "/" );
            this.sseHttpClient.DefaultRequestHeaders.Add("API-Key", this.apiKey);
            this.sseHttpClient.DefaultRequestHeaders.Add("Accept", "text /event-stream");
            this.sseHttpClient.Timeout = Timeout.InfiniteTimeSpan;
        }
        public IEnumerable<FeatureConfig> GetFlags()
        {
            try
            {
                Task<ICollection<FeatureConfig>> task = Task.Run(() =>
                {
                    Client client = new Client(this.apiHttpClient);
                    client.BaseUrl = this.config.ConfigUrl;
                    return client.ClientEnvFeatureConfigsGetAsync(this.environment, this.cluster, this.cancelToken.Token);
                });

                task.Wait();

                return task.Result;
            }
            catch (AggregateException ex)
            {
                ReauthenticateIfNeeded(ex);
                throw new CfClientException(ex.Message);
            }
        }
        public IEnumerable<Segment> GetSegments()
        {
            try
            {
                Task<ICollection<Segment>> task = Task.Run(() =>
                {
                    Client client = new Client(this.apiHttpClient);
                    client.BaseUrl = this.config.ConfigUrl;
                    return client.ClientEnvTargetSegmentsGetAsync(this.environment, this.cluster, this.cancelToken.Token);
                });

                task.Wait();

                return task.Result;
            }
            catch (AggregateException ex)
            {
                ReauthenticateIfNeeded(ex);
                throw new CfClientException(ex.Message);
            }
        }
        public FeatureConfig GetFlag(string identifier)
        {
            try
            {
                Task<FeatureConfig> task = Task.Run(() =>
                {

                    Client client = new Client(this.apiHttpClient);
                    client.BaseUrl = this.config.ConfigUrl;
                    return client.ClientEnvFeatureConfigsGetAsync(identifier, this.environment, this.cluster, this.cancelToken.Token);
                });

                task.Wait();

                return task.Result;
            }
            catch (AggregateException ex)
            {
                ReauthenticateIfNeeded(ex);
                throw new CfClientException(ex.Message);
            }
        }
        public Segment GetSegment(string identifer)
        {
            try
            {
                Task<Segment> task = Task.Run(() =>
                {
                    Client client = new Client(this.apiHttpClient);
                    client.BaseUrl = this.config.ConfigUrl;
                    return client.ClientEnvTargetSegmentsGetAsync(identifer, this.environment, this.cluster, this.cancelToken.Token);
                });

                task.Wait();

                return task.Result;
            }
            catch (AggregateException ex)
            {
                ReauthenticateIfNeeded(ex);
                throw new CfClientException(ex.Message);
            }
        }
        public IService Stream(IUpdateCallback updater)
        {
            if(currentStream != null)
            {
                currentStream.Close();
            }
            string url = $"stream?cluster={this.cluster}";
            this.currentStream =  new EventSource(this.sseHttpClient, url, this.config, updater);
            return currentStream;
        }
        public void PostMetrics(HarnessOpenMetricsAPIService.Metrics metrics)
        {
            try
            {
                DateTime startTime = DateTime.Now;
                Task task = Task.Run(() =>
                {
                    HarnessOpenMetricsAPIService.Client client = new HarnessOpenMetricsAPIService.Client(this.metricHttpClient);
                    return client.MetricsAsync(environment, cluster, metrics, this.cancelToken.Token);
                });
                task.Wait();

                DateTime endTime = DateTime.Now;
                if ((endTime - startTime).TotalMilliseconds > config.MetricsServiceAcceptableDuration)
                {
                    Log.Warning($"Metrics service API duration=[{endTime - startTime}]");
                }
            }
            catch (AggregateException ex)
            {
                ReauthenticateIfNeeded(ex);
                throw new CfClientException(ex.Message);
            }
        }
        public string Authenticate()
        {
            try
            {
                Task<AuthenticationResponse> task = Task.Run(() =>
                {
                    AuthenticationRequest authenticationRequest = new AuthenticationRequest();
                    authenticationRequest.ApiKey = apiKey;
                    authenticationRequest.Target = new Target2 { Identifier = "" };

                    Client client = new Client(this.apiHttpClient);
                    client.BaseUrl = this.config.ConfigUrl;
                    return client.ClientAuthAsync(authenticationRequest, cancelToken.Token);
                });
                // Wait for task to finish
                task.Wait();
                // Get the result
                AuthenticationResponse response = task.Result;
                this.token = response.AuthToken;

                var handler = new JwtSecurityTokenHandler();
                SecurityToken jsonToken = handler.ReadToken(this.token);
                JwtSecurityToken JWTToken = (JwtSecurityToken)jsonToken;

                this.environment = JWTToken.Payload["environment"].ToString();
                this.cluster = JWTToken.Payload["clusterIdentifier"].ToString();

                this.apiHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this.token);
                this.metricHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this.token);
                this.sseHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this.token);

                return this.token;

            }
            catch (AggregateException ex)
            {
                foreach (var e in ex.InnerExceptions)
                {
                    Log.Error($"Failed to get auth token {e.Message}");
                    ApiException apiEx = e as ApiException;
                    if (apiEx != null)
                    {

                        if (apiEx.StatusCode == (int)HttpStatusCode.Unauthorized || apiEx.StatusCode == (int)HttpStatusCode.Forbidden)
                        {
                            string errorMsg = $"Invalid apiKey {apiKey}. Serving default value.";
                            Log.Error(errorMsg);
                            throw new CfClientException(errorMsg);
                        }
                        throw new CfClientException(apiEx.Message);
                    }
                    throw e;
                }
                throw ex;
            }
        }
        private void ReauthenticateIfNeeded(AggregateException ex)
        {
            foreach (var e in ex.InnerExceptions)
            {
                ApiException apiEx = e as ApiException;
                if (apiEx != null && apiEx.StatusCode == (int)HttpStatusCode.Forbidden)
                {
                    Log.Error("Initiate reauthentication");
                    callback.OnReauthenticateRequested();
                    return;
                }
            }
        }

        public void Close()
        {
            this.cancelToken.Cancel();
            this.currentStream.Close();
        }
    }
}
