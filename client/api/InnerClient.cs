using io.harness.cfsdk.client.api.analytics;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.HarnessOpenAPIService;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api
{
    internal class InnerClient :
        IAuthCallback,
        IRepositoryCallback,
        IPollCallback,
        IUpdateCallback,
        IEvaluatorCallback,
        IMetricCallback,
        IConnectionCallback
    {
        private ILoggerFactory loggerFactory;
        private ILogger logger;

        // Services
        private IAuthService authService;
        private IRepository repository;
        private IPollingProcessor polling;
        private IUpdateProcessor update;
        private IEvaluator evaluator;
        private IMetricsProcessor metric;
        private IConnector connector;

        public event EventHandler InitializationCompleted;
        public event EventHandler<string> EvaluationChanged;

        private readonly CfClient parent;
        public InnerClient(CfClient parent, ILoggerFactory loggerFactory) { this.parent = parent;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger<InnerClient>();
        }
        public InnerClient(string apiKey, Config config, CfClient parent, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.parent = parent;
            this.logger = loggerFactory.CreateLogger<InnerClient>();
            Initialize(apiKey, config);
        }

        public InnerClient(IConnector connector, Config config, CfClient parent, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.parent = parent;
            this.logger = loggerFactory.CreateLogger<InnerClient>();
            Initialize(connector, config);
        }

        public void Initialize(string apiKey, Config config)
        {
            Initialize(new HarnessConnector(apiKey, config, this, loggerFactory), config);
        }

        public void Initialize(IConnector connector, Config config)
        {
            var analyticsCache = new AnalyticsCache();
            this.connector = connector;
            this.authService = new AuthService(connector, config, this, loggerFactory);
            this.repository = new StorageRepository(config.Cache, config.Store, this, loggerFactory);
            this.polling = new PollingProcessor(connector, this.repository, config, this, loggerFactory);
            this.update = new UpdateProcessor(connector, this.repository, config, this, loggerFactory);
            this.evaluator = new Evaluator(this.repository, this, loggerFactory);
            this.metric = new MetricsProcessor(connector, config, this, analyticsCache, new AnalyticsPublisherService(connector, analyticsCache, loggerFactory), loggerFactory);
        }
        public void Start()
        {
            logger.LogDebug("Authenticating");
            // Start Authentication flow
            this.authService.Start();
        }
        public async Task WaitToInitialize()
        {
            var initWork = new[] {
                this.polling.ReadyAsync()
            };

            // We finished with initialization when Polling processor returns.
            await Task.WhenAll(initWork);

            OnNotifyInitializationCompleted();
        }
        public async Task StartAsync()
        {
            Start();
            await WaitToInitialize();
        }
        #region Stream callback

        public void OnStreamConnected()
        {
            logger.LogInformation("SDKCODE(stream:5000): SSE stream connected ok");
            this.polling.Stop();
        }
        public void OnStreamDisconnected()
        {
            logger.LogInformation("SDKCODE(stream:5001): SSE stream disconnected");
            this.polling.Start();
        }
        #endregion



        #region Authentication callback
        public void OnAuthenticationSuccess()
        {
            // after successfull authentication, start
            logger.LogInformation("SDKCODE(auth:2000): Authenticated ok");

            polling.Start();
            update.Start();
            metric.Start();
        }

        #endregion

        #region Reauthentication callback
        public void OnReauthenticateRequested()
        {
            polling.Stop();
            update.Stop();
            metric.Stop();

            authService.Start();
        }
        #endregion

        #region Poller Callback
        public void OnPollerReady()
        {

        }
        public void OnPollError(string message)
        {

        }
        #endregion

        #region Repository callback

        public void OnFlagStored(string identifier)
        {
            OnNotifyEvaluationChanged(identifier);
        }

        public void OnFlagDeleted(string identifier)
        {
            OnNotifyEvaluationChanged(identifier);
        }

        public void OnSegmentStored(string identifier)
        {
            repository.FindFlagsBySegment(identifier).ToList().ForEach(i => {
                OnNotifyEvaluationChanged(i);
            });
        }

        public void OnSegmentDeleted(string identifier)
        {
            repository.FindFlagsBySegment(identifier).ToList().ForEach(i => {
                OnNotifyEvaluationChanged(i);
            });
        }
        #endregion

        private void OnNotifyInitializationCompleted()
        {
            logger.LogInformation("SDKCODE(init:1000): The SDK has successfully initialized");
            logger.LogInformation("SDK version: " + Assembly.GetExecutingAssembly().GetName().Version);
            InitializationCompleted?.Invoke(parent, EventArgs.Empty);
        }
        private void OnNotifyEvaluationChanged(string identifier)
        {
            EvaluationChanged?.Invoke(parent, identifier);
        }

        public bool BoolVariation(string key, dto.Target target, bool defaultValue)
        {
            return evaluator.BoolVariation(key, target, defaultValue);
        }
        public string StringVariation(string key, dto.Target target, string defaultValue)
        {
            return evaluator.StringVariation(key, target, defaultValue);
        }
        public double NumberVariation(string key, dto.Target target, double defaultValue)
        {
            return evaluator.NumberVariation(key, target, defaultValue);
        }
        public JObject JsonVariation(string key, dto.Target target, JObject defaultValue)
        {
            return evaluator.JsonVariation(key, target, defaultValue);
        }

        public void Close()
        {
            this.connector.Close();
            this.authService.Stop();
            this.repository.Close();
            this.polling.Stop();
            this.update.Stop();
            this.metric.Stop();
        }

        public void Update(Message message, bool manual)
        {
            this.update.Update(message, manual);
        }
        public void evaluationProcessed(FeatureConfig featureConfig, dto.Target target, Variation variation)
        {
            this.metric.PushToCache(target, featureConfig, variation);
        }
    }
}
