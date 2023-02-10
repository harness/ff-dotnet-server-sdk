using System;
using System.Linq;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api.analytics;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace io.harness.cfsdk.client.api
{
    internal sealed class InnerClient :
        IDisposable,
        IAuthCallback,
        IRepositoryCallback,
        IPollCallback,
        IUpdateCallback,
        IEvaluatorCallback,
        IMetricCallback,
        IConnectionCallback
    {
        // Services
        private IAuthService authService;
        private IRepository repository;
        private IPollingProcessor polling;
        private IUpdateProcessor update;
        private IEvaluator evaluator;
        private IMetricsProcessor metric;
        private IConnector connector;
        private bool _disposed;

        public event EventHandler InitializationCompleted;
        public event EventHandler<string> EvaluationChanged;

        private readonly CfClient parent;
        private ILogger logger;

        public InnerClient(CfClient parent)
        {
            this.parent = parent;
            this.logger = Config.DefaultLogger;
        }
        public InnerClient(string apiKey, Config config, CfClient parent)
            : this(parent)
        {
            Initialize(apiKey, config);
        }

        public InnerClient(IConnector connector, Config config, CfClient parent)
            : this(parent)
        {
            Initialize(connector, config);
        }

        public void Initialize(string apiKey, Config config)
        {
            Initialize(new HarnessConnector(apiKey, config, this), config);
        }

        public void Initialize(IConnector connector, Config config)
        {
            ThrowIfDisposed();

            if (this.connector != null) throw new CfClientException("Client already initialized.");

            this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
            this.logger = (config ?? throw new ArgumentNullException(nameof(config))).CreateLogger<InnerClient>();
            this.authService = new AuthService(connector, config, this);
            this.repository = new StorageRepository(config.Cache, config.Store, this, config.CreateLogger<StorageRepository>());
            this.polling = new PollingProcessor(connector, this.repository, config, this);
            this.update = new UpdateProcessor(connector, this.repository, config, this);
            this.evaluator = new Evaluator(this.repository, this, config.CreateLogger<Evaluator>());
            this.metric = new MetricsProcessor(connector, config, this);
        }
        public void Start()
        {
            ThrowIfDisposed();

            logger.LogInformation("Initialize authentication");
            // Start Authentication flow
            this.authService.Start();
        }
        public async Task WaitToInitialize()
        {
            ThrowIfDisposed();

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
            if (_disposed) return;
            this.polling.Stop();
        }
        public void OnStreamDisconnected()
        {
            if (_disposed) return;
            this.polling.Start();
        }
        #endregion

        #region Authentication callback
        public void OnAuthenticationSuccess()
        {
            if (_disposed) return;

            // after successfull authentication, start
            polling.Start();
            update.Start();
            metric.Start();
        }
        #endregion

        #region Reauthentication callback
        public void OnReauthenticateRequested()
        {
            if (_disposed) return;

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
            if (_disposed) return;
            repository.FindFlagsBySegment(identifier).ToList().ForEach(OnNotifyEvaluationChanged);
        }

        public void OnSegmentDeleted(string identifier)
        {
            if (_disposed) return;
            repository.FindFlagsBySegment(identifier).ToList().ForEach(OnNotifyEvaluationChanged);
        }
        #endregion

        private void OnNotifyInitializationCompleted()
        {
            InitializationCompleted?.Invoke(parent, EventArgs.Empty);
        }
        private void OnNotifyEvaluationChanged(string identifier)
        {
            EvaluationChanged?.Invoke(parent, identifier);
        }

        public bool BoolVariation(string key, dto.Target target, bool defaultValue)
        {
            ThrowIfDisposed();
            return evaluator.BoolVariation(key, target, defaultValue);
        }
        public string StringVariation(string key, dto.Target target, string defaultValue)
        {
            ThrowIfDisposed();
            return evaluator.StringVariation(key, target, defaultValue);
        }
        public double NumberVariation(string key, dto.Target target, double defaultValue)
        {
            ThrowIfDisposed();
            return evaluator.NumberVariation(key, target, defaultValue);
        }
        public JObject JsonVariation(string key, dto.Target target, JObject defaultValue)
        {
            ThrowIfDisposed();
            return evaluator.JsonVariation(key, target, defaultValue);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                this.connector.Dispose();
                this.authService.Stop();
                this.repository.Close();
                this.polling.Stop();
                this.update.Stop();
                this.metric.Stop();

                GC.SuppressFinalize(this);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CfClient));
        }

        public void Update(Message message, bool manual)
        {
            ThrowIfDisposed();
            this.update.Update(message, manual);
        }

        public void evaluationProcessed(FeatureConfig featureConfig, dto.Target target, Variation variation)
        {
            if (_disposed) return;
            this.metric.PushToQueue(target, featureConfig, variation);
        }
    }
}
