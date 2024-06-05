using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Newtonsoft.Json;

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
        private static string _environment;
        private static string _accountId;
        private string cluster;
        private const string TargetSegmentRulesQueryParameter = "v2";

        public HttpClient apiHttpClient { get; set; }
        public HttpClient metricHttpClient { get; set; }
        public HttpClient sseHttpClient { get; set; }

        private readonly string apiKey;
        private readonly Config config;
        private readonly IConnectionCallback callback;
        private readonly Client harnessClient;

        private IService currentStream;
        private readonly CancellationTokenSource cancelToken = new CancellationTokenSource();

        private static readonly string SdkVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";

        private static HttpClient CreateHttpClientWithTls(Config config, ILoggerFactory loggerFactory)
        {
            if (config.TlsTrustedCAs.IsNullOrEmpty())
            {
                return new HttpClient();
            }

#if (NETSTANDARD || NET461)
            throw new NotSupportedException("Custom TLS certificates require .net5.0 target or greater");
#else
            var logger = loggerFactory.CreateLogger<HarnessConnector>();
            var handler = new HttpClientHandler();

            handler.ServerCertificateCustomValidationCallback = delegate (HttpRequestMessage request, X509Certificate2 serverCertificate, X509Chain serverChain, SslPolicyErrors sslPolicyErrors)
            {
                logger.LogDebug("TLS: Validating server certificate {subject} for {url}, policyErrors={sslPolicyErrors}", serverCertificate.Subject, request.RequestUri, sslPolicyErrors);
                PrintCert(logger, serverCertificate);

                var requestHost = request.RequestUri?.Host;
                var certHost = serverCertificate.GetNameInfo(X509NameType.DnsFromAlternativeName, false);

                if (requestHost == null || certHost == null)
                {
                    logger.LogError("Missing hostname/certhost");
                    return false;
                }

                var match = IPAddress.TryParse(certHost, out _) ? requestHost.Equals(certHost) : requestHost.EndsWith(certHost);

                if (!match)
                {
                    logger.LogError("SDKCODE(init:1005): TLS Hostname validation failed (sdk requested={reqhost} server cert wants={svrhost}) for {url}",
                        requestHost,
                        certHost,
                        request.RequestUri);
                    return false;
                }

                using var chain = new X509Chain(false);
                chain.ChainPolicy.DisableCertificateDownloads = true;
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Clear();

                foreach (var nextCa in config.TlsTrustedCAs)
                {
                    logger.LogDebug("TLS truststore: Adding cert: {subject}", nextCa.Subject);
                    chain.ChainPolicy.CustomTrustStore.Add(nextCa);
                    PrintCert(logger, nextCa);
                }

                foreach (var nextCa in serverChain.ChainElements)
                {
                    var builder = new StringBuilder();
                    foreach (var status in nextCa.ChainElementStatus)
                    {
                        builder.Append(status.Status).Append(' ').Append(status.StatusInformation).Append(' ');
                        PrintCert(logger, nextCa.Certificate);
                    }

                    logger.LogDebug("TLS truststore: Adding server cert: {subject} chainStatus=[{status}]", nextCa.Certificate.Subject, builder.ToString());
                    chain.ChainPolicy.CustomTrustStore.Add(nextCa.Certificate);
                }

                if (!chain.Build(serverCertificate))
                {
                  if (chain.ChainStatus.Any(s => s.Status != X509ChainStatusFlags.NoError))
                  {
                      logger.LogError("SDKCODE(init:1004): TLS Certificate did not validate against trust store (reason={reason}) for {url}",
                          chain.ChainStatus.First(c => c.Status != X509ChainStatusFlags.NoError).Status,
                          request.RequestUri);
                  }

                  return false;
                }

                logger.LogDebug("TLS: Endpoint {hostname}:{port} is trusted", request.RequestUri?.Host ?? "", request.RequestUri?.Port ?? -1);

                return true;
            };

            return new HttpClient(handler, true);

#endif // NETSTANDARD
        }

        private static HttpClient ApiHttpClient(Config config, ILoggerFactory loggerFactory)
        {
            HttpClient client = CreateHttpClientWithTls(config, loggerFactory);
            client.BaseAddress = new Uri(config.ConfigUrl);
            client.Timeout = TimeSpan.FromMilliseconds(config.ConnectionTimeout);
            client.DefaultRequestHeaders.Add("Harness-SDK-Info", $".Net {SdkVersion} Server");
            return client;
        }
        private static HttpClient MetricHttpClient(Config config, ILoggerFactory loggerFactory)
        {
            HttpClient client = CreateHttpClientWithTls(config, loggerFactory);
            client.BaseAddress = new Uri(config.EventUrl);
            client.Timeout = TimeSpan.FromMilliseconds(config.ConnectionTimeout);
            client.DefaultRequestHeaders.Add("Harness-SDK-Info", $".Net {SdkVersion} Client");
            if (_accountId != null)
            {
                client.DefaultRequestHeaders.Add("Harness-AccountID", _accountId);

            }
            client.DefaultRequestHeaders.Add("Harness-EnvironmentID", _environment);
            return client;
        }
        private static HttpClient SseHttpClient(Config config, string apiKey, ILoggerFactory loggerFactory)
        {
            HttpClient client = CreateHttpClientWithTls(config, loggerFactory);
            // Don't rely on the http client timeout to kill dead streams, as behaviour can be different
            // depending on the .NET version.  Instead, we use custom logic to kill a stream if a heartbeat hasn't
            // been received in a given period. 
            client.Timeout = Timeout.InfiniteTimeSpan;;
            client.BaseAddress = new Uri(config.ConfigUrl.EndsWith("/") ? config.ConfigUrl : config.ConfigUrl + "/" );
            client.DefaultRequestHeaders.Add("API-Key", apiKey);
            client.DefaultRequestHeaders.Add("Accept", "text /event-stream");
            client.DefaultRequestHeaders.Add("Harness-SDK-Info", $".Net {SdkVersion} Server");
            client.DefaultRequestHeaders.Add("Harness-EnvironmentID", _environment);
            if (_accountId != null)
            {
                client.DefaultRequestHeaders.Add("Harness-AccountID", _accountId);
            }
            return client;
        }
        
        public HarnessConnector(string apiKey, Config config, IConnectionCallback callback, ILoggerFactory loggerFactory)
        : this(apiKey, config, callback, ApiHttpClient(config, loggerFactory), MetricHttpClient(config, loggerFactory), SseHttpClient(config, apiKey, loggerFactory), loggerFactory)
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
                throw new CfClientException(ex.Message, ex);
            }
            catch (Exception ex)
            {
                throw new CfClientException(ex.Message, ex);
            }
        }

        public async Task<IEnumerable<FeatureConfig>> GetFlags()
        {
            var flags = await ReauthenticateIfNeeded(() =>
                harnessClient.ClientEnvFeatureConfigsGetAsync(_environment, cluster, cancelToken.Token));
            if (flags != null)
                logger.LogDebug("Fetched {Count} flags from the server", flags.Count);
            else
                logger.LogDebug("No feature flags were fetched from the server");

            return flags;
        }

        public async Task<IEnumerable<Segment>> GetSegments()
        {
            var segments = await ReauthenticateIfNeeded(() =>
                harnessClient.ClientEnvTargetSegmentsGetAsync(_environment, cluster, TargetSegmentRulesQueryParameter,
                    cancelToken.Token));
            if (segments != null)
                logger.LogDebug("Fetched {Count} groups from the server", segments.Count);
            else
                logger.LogDebug("No segments were fetched from the server");

            return segments;
        }
        public Task<FeatureConfig> GetFlag(string identifier)
        {
            return ReauthenticateIfNeeded(() => harnessClient.ClientEnvFeatureConfigsGetAsync(identifier, _environment, cluster, cancelToken.Token));
        }
        public Task<Segment> GetSegment(string identifier)
        {
            return ReauthenticateIfNeeded(() => harnessClient.ClientEnvTargetSegmentsGetAsync(identifier, _environment,
                cluster, TargetSegmentRulesQueryParameter, cancelToken.Token));
        }
        public IService Stream(IUpdateCallback updater)
        {
            currentStream?.Close();
            var url = $"stream?cluster={cluster}";
            currentStream = new EventSource(sseHttpClient, url, updater, loggerFactory);
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
                    await client.MetricsAsync(_environment, cluster, metrics, cancelToken.Token);
                }
                catch (Exception ex) {
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
                    _accountId = accountIdValue.ToString();
                }                
                _environment = jwtToken.Payload["environment"].ToString();
                cluster = jwtToken.Payload["clusterIdentifier"].ToString();
                var environmentIdentifier = jwtToken.Payload.ContainsKey("environmentIdentifier") ? jwtToken.Payload["clusterIdentifier"].ToString() : _environment;

                foreach (var httpClient in new[] { apiHttpClient, metricHttpClient, sseHttpClient })
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    if (!String.IsNullOrEmpty(environmentIdentifier))
                    {
                        httpClient.DefaultRequestHeaders.Add("Harness-EnvironmentID", environmentIdentifier);
                    }

                    if (!String.IsNullOrEmpty(_accountId))
                    {
                        httpClient.DefaultRequestHeaders.Add("Harness-AccountID", _accountId);
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
            cancelToken?.Cancel();
            currentStream?.Close();
        }

        private static void PrintCert(ILogger logger, X509Certificate2 cert)
        {
            if (!logger.IsEnabled(LogLevel.Trace)) return;
            logger.LogTrace(cert.ToString());
            foreach (X509Extension ext in cert.Extensions)
            {
                AsnEncodedData asn = new AsnEncodedData(ext.Oid, ext.RawData);
                logger.LogTrace("Oid Name: {name} Oid: {value} Data: {data}", ext.Oid?.FriendlyName, asn.Oid?.Value, asn.Format(true));
            }
        }
    }
}
