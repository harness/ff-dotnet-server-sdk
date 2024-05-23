using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using io.harness.cfsdk.client.cache;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api
{
    public class Config
    {
        public static int MIN_FREQUENCY = 60;
        internal bool analyticsEnabled = true;
        internal ICache cache = new FeatureSegmentCache();
        internal string configUrl = "https://config.ff.harness.io/api/1.0";
        internal int connectionTimeout = 10000;
        internal string eventUrl = "https://events.ff.harness.io/api/1.0";
        private readonly int frequency = 60;
        internal int maxAuthRetries = 10;
        internal long metricsServiceAcceptableDuration = 10000;
        internal int pollIntervalInSeconds = 60;
        internal IStore store;
        internal bool streamEnabled = true;
        internal int evaluationMetricsMaxSize = 10000;
        internal int targetMetricsMaxSize = 100000;
        internal int cacheRecoveryTimeoutInMs = 5000;
        internal bool useMapForInClause = false;
        internal int seenTargetsTtlInSeconds = 43200;

        public Config(string configUrl, string eventUrl, bool streamEnabled, int pollIntervalInSeconds,
            bool analyticsEnabled, int frequency, int targetMetricsMaxSize, int connectionTimeout, int readTimeout,
            int writeTimeout, bool debug, long metricsServiceAcceptableDuration, int cacheRecoveryTimeoutInMs, bool useMapForInClause, int seenTargetsTtlInSeconds)
        {
            this.configUrl = configUrl;
            this.eventUrl = eventUrl;
            this.streamEnabled = streamEnabled;
            this.pollIntervalInSeconds = pollIntervalInSeconds;
            this.analyticsEnabled = analyticsEnabled;
            this.frequency = frequency;
            this.targetMetricsMaxSize = targetMetricsMaxSize;
            this.connectionTimeout = connectionTimeout;
            this.readTimeout = readTimeout;
            this.writeTimeout = writeTimeout;
            this.debug = debug;
            this.metricsServiceAcceptableDuration = metricsServiceAcceptableDuration;
            this.cacheRecoveryTimeoutInMs = cacheRecoveryTimeoutInMs;
            this.useMapForInClause = useMapForInClause;
            this.seenTargetsTtlInSeconds = seenTargetsTtlInSeconds;
        }

        public Config()
        {
        }

        public string ConfigUrl => configUrl;
        public string EventUrl => eventUrl;
        public bool StreamEnabled => streamEnabled;

        public int PollIntervalInMiliSeconds => pollIntervalInSeconds * 1000;

        public int MaxAuthRetries => maxAuthRetries;

        // configurations for Analytics
        public bool AnalyticsEnabled => analyticsEnabled;

        public int Frequency => Math.Max(frequency, MIN_FREQUENCY);

        public ICache Cache => cache;

        public IStore Store => store;

        public int TargetMetricsMaxSize => targetMetricsMaxSize;
        public int EvaluationMetricsMaxSize => evaluationMetricsMaxSize;
        public int CacheRecoveryTimeoutInMs => cacheRecoveryTimeoutInMs;

        public bool UseMapForInClause => useMapForInClause;
        public int SeenTargetsTtlInSeconds => seenTargetsTtlInSeconds;

        /**
         * timeout in milliseconds to connect to CF Server
         */
        public int ConnectionTimeout => connectionTimeout;

        /**
         * timeout in milliseconds for reading data from CF Server
         */
        public int ReadTimeout => readTimeout;

        internal int readTimeout { get; set; } = 30000;

        /**
         * timeout in milliseconds for writing data to CF Server
         */
        public int WriteTimeout => writeTimeout;

        internal int writeTimeout { get; set; } = 10000;

        public bool Debug => debug;
        internal bool debug { get; set; }

        /**
         * If metrics service POST call is taking > this time, we need to know about it
         */

        public long MetricsServiceAcceptableDuration => metricsServiceAcceptableDuration;

        public ILoggerFactory LoggerFactory { get; set; }

        public List<X509Certificate2> TlsTrustedCAs { get; set; } = new();

        public static ConfigBuilder Builder()
        {
            return new ConfigBuilder();
        }
    }

    public class ConfigBuilder
    {
        private readonly Config configtobuild;

        public ConfigBuilder()
        {
            configtobuild = new Config();
        }

        public Config Build()
        {
            return configtobuild;
        }

        public ConfigBuilder SetPollingInterval(int pollIntervalInSeconds)
        {
            configtobuild.pollIntervalInSeconds = pollIntervalInSeconds;
            return this;
        }

        public ConfigBuilder SetCache(ICache cache)
        {
            configtobuild.cache = cache;
            return this;
        }

        public ConfigBuilder SetStore(IStore store)
        {
            configtobuild.store = store;
            return this;
        }

        public ConfigBuilder SetStreamEnabled(bool enabled = true)
        {
            configtobuild.streamEnabled = enabled;
            return this;
        }

        public ConfigBuilder MetricsServiceAcceptableDuration(long duration = 10000)
        {
            configtobuild.metricsServiceAcceptableDuration = duration;
            return this;
        }

        public ConfigBuilder SetAnalyticsEnabled(bool analyticsenabled = true)
        {
            configtobuild.analyticsEnabled = analyticsenabled;
            return this;
        }

        public ConfigBuilder ConfigUrl(string configUrl)
        {
            configtobuild.configUrl = configUrl;
            return this;
        }

        public ConfigBuilder EventUrl(string eventUrl)
        {
            configtobuild.eventUrl = eventUrl;
            return this;
        }

        public ConfigBuilder connectionTimeout(int connectionTimeout)
        {
            configtobuild.connectionTimeout = connectionTimeout;
            return this;
        }

        public ConfigBuilder readTimeout(int readTimeout)
        {
            configtobuild.readTimeout = readTimeout;
            return this;
        }

        public ConfigBuilder writeTimeout(int writeTimeout)
        {
            configtobuild.writeTimeout = writeTimeout;
            return this;
        }

        public ConfigBuilder debug(bool debug)
        {
            configtobuild.debug = debug;
            return this;
        }
        
        public ConfigBuilder SetCacheRecoveryTimeout(int timeoutMilliseconds)
        {
            configtobuild.cacheRecoveryTimeoutInMs = timeoutMilliseconds;
            return this;
        }

        /**
         * <summary>
         * Enable map for storing IN clause values for faster performance.
         * If you have a lot of values stored in any of your IN clauses it might be useful to enable this feature to
         * speed up the processing on those rules at the expense of using more memory.
         * </summary>
         */
        public ConfigBuilder UseMapForInClause(bool useMapForInClause)
        {
            configtobuild.useMapForInClause = useMapForInClause;
            return this;
        }

        /**
         * <summary>
         *     Set custom TTL for SeenTargets map keys. The default value is 43200 seconds, or 12 hours.  If you have
         *     a large number of targets and would prefer to clear this map sooner, decrease this TTL to free up memory.
         *     SeenTargets is used to reduce payload size to the Feature Flags Analytics service, so clearing it more frequently
         *     could result in larger payload sizes. 
         * </summary>
         */
        public ConfigBuilder SeenTargetsTtlInSeconds(int seenTargetsTtlInSeconds)
        {
            configtobuild.seenTargetsTtlInSeconds = seenTargetsTtlInSeconds;
            return this;
        }


        /// <summary>
        /// <para>
        /// Set the maximum number of unique targets (a target is considered unique based on its identifier and attributes) used in evaluations that the SDK will store in memory before sending on to the Feature
        /// Flags analytics service, at the end of an analytics interval. These targets will then be available to use within the
        /// Feature Flags UI. Defaults to 100,000. Does not affect flag evaluation metrics.
        /// Note: the maximum number is 100,000 unique targets. 
        /// If a number greater than this is set, it will default to 100,000.
        /// </para>
        /// </summary>
        /// <param name="bufferSize">The maximum number of targets that are stored and sent in a given interval. Default is 20,000.</param>
        public ConfigBuilder SetBufferSize(int bufferSize)
        {
            if (bufferSize > 100000)
            {
                configtobuild.targetMetricsMaxSize = 100000;
                return this;
            }
            configtobuild.targetMetricsMaxSize = bufferSize;
            return this;
        }

        /**
         * <summary>
         *     Set an ILoggerFactory for the SDK. note: cannot be used in conjunction with getInstance()
         *     </summary>
         */
        public ConfigBuilder LoggerFactory(ILoggerFactory loggerFactory)
        {
            configtobuild.LoggerFactory = loggerFactory;
            return this;
        }

        /**
         * <summary>
         *     List of trusted CAs - for when the given config/event URLs are signed with a private CA. You
         *     should include intermediate CAs too to allow the HTTP client to build a full trust chain.
         * </summary>
         */
        public ConfigBuilder TlsTrustedCAs(List<X509Certificate2> certs)
        {
            configtobuild.TlsTrustedCAs = certs;
            return this;
        }
    }
}