using io.harness.cfsdk.client.connector;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace io.harness.cfsdk.client.api
{
    public interface ICfClient
    {
        Task Initialize(string apiKey);
        Task Initialize(string apiKey, Config config);
        Task Initialize(IConnector connector);
        Task Initialize(IConnector connector, Config config);

        Task InitializeAndWait();

        bool BoolVariation(string key, dto.Target target, bool defaultValue);
        string StringVariation(string key, dto.Target target, string defaultValue);
        double NumberVariation(string key, dto.Target target, double defaultValue);
        JObject JsonVariation(string key, dto.Target target, JObject defaultValue);

        IDisposable Subscribe(IObserver<Event> observer);
        IDisposable Subscribe(NotificationType evn, IObserver<Event> observer);
        void Update(Message msg);

        void Close();
    }

    public class CfClient : ICfClient, IObservable<Event>
    {
        // Singleton implementation
        private static readonly Lazy<CfClient> lazy = new Lazy<CfClient>(() => new CfClient());
        public static ICfClient Instance { get { return lazy.Value; } }

        private readonly InnerClient client = null;

        // alternative client creation
        public CfClient(string apiKey) : this(apiKey, Config.Builder().Build()) {}
        public CfClient(IConnector connector) : this(connector, Config.Builder().Build()) { }
        public CfClient()
        {
            client = new InnerClient();
        }
        public CfClient(string apiKey, Config config)
        {
            client = new InnerClient(apiKey, config);
        }
        public CfClient(IConnector connector, Config config)
        {
            client = new InnerClient(connector, config);
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
        public bool BoolVariation(string key, dto.Target target, bool defaultValue) { return client.BoolVariation(key, target, defaultValue);  }
        public string StringVariation(string key, dto.Target target, string defaultValue) { return client.StringVariation(key, target, defaultValue); }
        public double NumberVariation(string key, dto.Target target, double defaultValue) { return client.NumberVariation(key, target, defaultValue); }
        public JObject JsonVariation(string key, dto.Target target, JObject defaultValue) {  return client.JsonVariation(key, target, defaultValue); }

        // subscribe to receive notificatins
        public IDisposable Subscribe(IObserver<Event> observer) { return client.Subscribe(observer); }
        public IDisposable Subscribe(NotificationType evn, IObserver<Event> observer) { return client.Subscribe(evn, observer); }

        // force message
        public void Update(Message msg) { client.Update(msg, true);  }

        public void Close() { client.Close();  }

    }
}
