using io.harness.cfsdk.client.connector;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api
{
    public interface ICfClient
    {
        Task Initialize(string apiKey);
        Task Initialize(string apiKey, Config config);
        Task Initialize(IConnector connector);
        Task Initialize(IConnector connector, Config config);

        Task InitializeAndWait();

        bool boolVariation(string key, dto.Target target, bool defaultValue);
        string stringVariation(string key, dto.Target target, string defaultValue);
        double numberVariation(string key, dto.Target target, double defaultValue);
        JObject jsonVariation(string key, dto.Target target, JObject defaultValue);


        event EventHandler InitializationCompleted;
        event EventHandler<string> EvaluationChanged;

        void Update(Message msg);
        void Close();
    }

    public class CfClient : ICfClient
    {
        // Singleton implementation
        private static readonly Lazy<CfClient> lazy = new Lazy<CfClient>(() => new CfClient());
        public static ICfClient Instance { get { return lazy.Value; } }

        private readonly InnerClient client;

        public event EventHandler InitializationCompleted
        {
            add { client.InitializationCompleted += value; }
            remove { client.InitializationCompleted -= value;    }
        }
        public event EventHandler<string> EvaluationChanged
        {
            add { client.EvaluationChanged += value; }
            remove { client.EvaluationChanged -= value; }
        }

        // alternative client creation
        public CfClient(string apiKey) : this(apiKey, Config.Builder().Build()) {}
        public CfClient(IConnector connector) : this(connector, Config.Builder().Build()) { }
        public CfClient()
        {
            client = new InnerClient(this, SetUpDefaultLogging(null));
        }
        public CfClient(string apiKey, Config config)
        {
            client = new InnerClient(apiKey, config, this,  SetUpDefaultLogging(config));
        }
        public CfClient(IConnector connector, Config config)
        {
            client = new InnerClient(connector, config, this, SetUpDefaultLogging(config));
        }

        private ILoggerFactory SetUpDefaultLogging(Config config)
        {
            if (config != null && config.LoggerFactory != null)
            {
                return config.LoggerFactory;
            }

            // Default logging is to console
            return LoggerFactory.Create(builder =>
            {
                 builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddConsole();
            });
        }

        // start authentication with server
        public async Task InitializeAndWait()
        {
            await client.StartAsync();
        }
        // initialize singletone instance
        public async Task Initialize(string apiKey)
        {
            await Initialize(apiKey, Config.Builder().Build());
        }
        public async Task Initialize(IConnector connector)
        {
           await Initialize(connector, Config.Builder().Build());
        }
        public async Task Initialize(string apiKey, Config config)
        {
            client.Initialize(apiKey, config);
            await client.StartAsync();
        }
        public async Task Initialize(IConnector connector, Config config)
        {
            client.Initialize(connector, config);
            await client.StartAsync();
        }

        // read values
        public bool boolVariation(string key, dto.Target target, bool defaultValue) { return client.BoolVariation(key, target, defaultValue);  }
        public string stringVariation(string key, dto.Target target, string defaultValue) { return client.StringVariation(key, target, defaultValue); }
        public double numberVariation(string key, dto.Target target, double defaultValue) { return client.NumberVariation(key, target, defaultValue); }
        public JObject jsonVariation(string key, dto.Target target, JObject defaultValue) {  return client.JsonVariation(key, target, defaultValue); }

        // force message
        public void Update(Message msg) { client.Update(msg, true);  }

        public void Close() { client.Close();  }

    }
}
