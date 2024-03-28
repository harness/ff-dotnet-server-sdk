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

        [Obsolete("This has been deprecated since its name is confusing implies it calls initialize when it does not, use WaitForInitialization() instead")]
        Task InitializeAndWait();
        void WaitForInitialization();
        bool WaitForInitialization(int timeoutMs);

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

        /// <summary>
        /// Returns a <c>CfClient()</c> instance. Subsequent calls to <c>Instance</c> will return the same instance.
        /// This is a convenience method for scenarios where only a single FF client and api key is required.
        /// The instance returned is constructed only. You need to call <c>Initialize()</c> once to start the SDK. e.g.
        /// <code>
        /// await CfClient.Instance.Initialize(API_KEY, config);
        /// </code>
        /// For scenarios where multiple SDK instances are needed (e.g. different server api keys) you will need
        /// construct <c>CfClient()</c> directly and call <c>Initialize()</c> for each instance.
        /// <para>
        /// This instance will be shared by all code that uses <c>CfClient.Instance</c> so any state set by
        /// <c>Initialize()</c>, such as logging factories will be seen by all other users of <c>CfClient.Instance</c>.
        /// </para>
        /// </summary>
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

        /// <summary>
        /// Creates a client instance. This is used by <c>CfClient.Instance</c>
        /// <para>
        /// WARNING unlike the other CfClient() constructors this will not initialize the SDK and you must
        /// call <c>Initialize()</c> manually.
        /// </para>
        /// </summary>
        [Obsolete("this method will be made internal in a future release of the SDK. Use one of the other constructors")]
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

        /// <summary>
        /// This will be removed. Use <see cref="WaitForInitialization()"/> instead
        /// </summary>
        [Obsolete("This has been deprecated since its name is confusing implies it calls initialize when it does not, use WaitForInitialization() instead")]
        public async Task InitializeAndWait()
        {
            client.Start();
            client.WaitForSdkToBeReady(-1);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Wait for the SDK to initialize, which includes authenticating against the FF server and loading the local
        /// cache. This version will wait indefinitely. If you require a hard timeout use <see cref="WaitForInitialization(int)"/>
        /// </summary>
        public void WaitForInitialization()
        {
            client.WaitForSdkToBeReady(-1);
        }

        /// <summary>
        /// See <see cref="WaitForInitialization()"/>
        /// </summary>
        /// <param name="timeoutMs">Time in milliseconds to wait</param>
        /// <returns>true if SDK authenticated ok and populated it cache within the given timeout.
        /// false means the cache may not have gotten populated and default variations may be served.
        /// Consider increasing the timeout in this case.</returns>
        public bool WaitForInitialization(int timeoutMs)
        {
            return client.WaitForSdkToBeReady(timeoutMs);
        }

        /// <summary>
        /// <para>
        /// Initialize the SDK. The SDK will attempt to authenticate against the FF server then retrieve and populate
        /// its local flag cache. You must call this on <c>CfClient.Instance</c> or <c>new CfClient()</c> and you
        /// should use <c>await</c> to allow the operation to complete and the cache to populate. If you don't
        /// call 'await' you may get defaults returned (SDKCODE 6001). Example:
        /// <code>
        /// CfClient.Instance.Initialize(apiKey, config);
        /// CfClient.Instance.WaitForInitialization();
        /// </code>
        /// or
        /// <code>
        /// var client = new CfClient();
        /// await client.Initialize(ApiKey, config);
        /// </code>
        /// </para>
        /// <para>
        /// You can also use Wait() directly to set a timeout, depending on your use-case you may throw an exception
        /// or log a warning. However be aware that continuing to use the instance will result in default variations
        /// being served.
        /// </para>
        /// <code>
        /// var success = CfClient.Instance.Initialize(ApiKey, config).Wait(10000);
        /// if (!success)
        /// {
        ///     Console.WriteLine("WARNING: SDK did not init correctly - default values may be served");
        /// }
        /// </code>
        /// </summary>
        /// <param name="apiKey">.NET server API key configured via Harness UI</param>
        public async Task Initialize(string apiKey)
        {
            await Initialize(apiKey, Config.Builder().Build());
        }

        /// <summary>
        /// See <see cref="Initialize(string)"/> for details
        /// </summary>
        /// <param name="connector">A concrete class implementing IConnector for custom connections</param>
        public async Task Initialize(IConnector connector)
        {
           await Initialize(connector, Config.Builder().Build());
        }

        /// <summary>
        /// See <see cref="Initialize(string)"/> for details
        /// </summary>
        /// <param name="apiKey">.NET server API key configured via Harness UI</param>
        /// <param name="config">Configuration for client</param>
        public async Task Initialize(string apiKey, Config config)
        {
            client.Initialize(apiKey, config);
            client.Start();
            await Task.CompletedTask;
        }

        /// <summary>
        /// See <see cref="Initialize(string)"/> for details
        /// </summary>
        /// <param name="connector">A concrete class implementing IConnector for custom connections</param>
        /// <param name="config">Configuration for client</param>
        public async Task Initialize(IConnector connector, Config config)
        {
            client.Initialize(connector, config);
            client.Start();
            await Task.CompletedTask;
        }

        // read values
        public bool boolVariation(string key, dto.Target target, bool defaultValue) { return client.BoolVariation(key, target, defaultValue);  }
        public string stringVariation(string key, dto.Target target, string defaultValue) { return client.StringVariation(key, target, defaultValue); }
        public double numberVariation(string key, dto.Target target, double defaultValue) { return client.NumberVariation(key, target, defaultValue); }
        public JObject jsonVariation(string key, dto.Target target, JObject defaultValue) {  return client.JsonVariation(key, target, defaultValue); }
        
        public int GetFlagsCacheSize()
        {
            return client.GetFlagsCacheSize();
        }

        public int GetSegmentsCacheSize()
        {
            return client.GetSegmentsCacheSize();
        }

        // force message
        public void Update(Message msg) { client.Update(msg, true);  }

        public void Close()
        {
            if (this == Instance)
            {
                return;
            }

            client?.Close();
        }

    }
}
