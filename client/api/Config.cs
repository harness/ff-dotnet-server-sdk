using System;
using io.harness.cfsdk.client.cache;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api
{
    public class Config
    {
        public static readonly int MIN_FREQUENCY = 60;

        public string ConfigUrl { get; internal set; } = "https://config.ff.harness.io/api/1.0";
        public string EventUrl { get; internal set; } = "https://events.ff.harness.io/api/1.0";
        public bool StreamEnabled { get; internal set; } = true;

        public int PollIntervalInMiliSeconds { get; internal set; } = 60000;
        public int PollIntervalInSeconds => PollIntervalInMiliSeconds / 1000;

        public int MaxAuthRetries { get; internal set; } = 10;

        // configurations for Analytics
        public bool AnalyticsEnabled { get; internal set; } = true;

        public int Frequency { get => Math.Max(frequency, Config.MIN_FREQUENCY); }
        private int frequency = 60;

        public ICache Cache { get; internal set; } = new FeatureSegmentCache();

        public IStore Store { get; internal set; } = null;

        public int BufferSize
        {
            get { return bufferSize; }
            internal set
            {
                // BufferSize must be a positive power of 2 for LMAX to work.
                // Source: https://stackoverflow.com/a/600306/1493480
                if (value <= 0 || (value & (value - 1)) != 0)
                    throw new CfClientException("BufferSize must be a power of 2");
                bufferSize = value;
            }
        }
        private int bufferSize = 1024; // Must be power of 2


        /** timeout in milliseconds to connect to CF Server */
        public int ConnectionTimeout { get; internal set; } = 10000;

        /** timeout in milliseconds for reading data from CF Server */
        public int ReadTimeout { get; internal set; } = 30000;

        /** timeout in milliseconds for writing data to CF Server */
        public int WriteTimeout { get; internal set; } = 10000;

        public bool Debug { get; internal set; } = false;

        /** If metrics service POST call is taking > this time, we need to know about it */

        public long MetricsServiceAcceptableDuration { get; internal set; } = 10000;

        public ILoggerFactory LoggerFactory { get; internal set; }
        internal static ILogger DefaultLogger => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        internal ILogger CreateLogger<T>() => LoggerFactory?.CreateLogger<T>() ?? DefaultLogger;

        public Config(string configUrl, string eventUrl, bool streamEnabled, int pollIntervalInSeconds, bool analyticsEnabled, int frequency, int bufferSize, int connectionTimeout, int readTimeout, int writeTimeout, bool debug, long metricsServiceAcceptableDuration)
        {
            this.ConfigUrl = configUrl;
            this.EventUrl = eventUrl;
            this.StreamEnabled = streamEnabled;
            this.PollIntervalInMiliSeconds = pollIntervalInSeconds * 1000;
            this.AnalyticsEnabled = analyticsEnabled;
            this.frequency = frequency;
            this.BufferSize = bufferSize;
            this.ConnectionTimeout = connectionTimeout;
            this.ReadTimeout = readTimeout;
            this.WriteTimeout = writeTimeout;
            this.Debug = debug;
            this.MetricsServiceAcceptableDuration = metricsServiceAcceptableDuration;
        }

        public Config()
        {
        }

        public static ConfigBuilder Builder()
        {
            return new ConfigBuilder();
        }
    }

    public class ConfigBuilder
    {
        Config configtobuild;

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
            this.configtobuild.PollIntervalInMiliSeconds = pollIntervalInSeconds * 1000;
            return this;
        }
        public ConfigBuilder SetCache(ICache cache)
        {
            this.configtobuild.Cache = cache;
            return this;
        }
        public ConfigBuilder SetStore(IStore store)
        {
            this.configtobuild.Store = store;
            return this;
        }
        public ConfigBuilder SetStreamEnabled(bool enabled = true)
        {
            configtobuild.StreamEnabled = enabled;
            return this;
        }
        public ConfigBuilder MetricsServiceAcceptableDuration(long duration = 10000)
        {
            configtobuild.MetricsServiceAcceptableDuration = duration;
            return this;
        }
        public ConfigBuilder SetAnalyticsEnabled(bool analyticsenabled = true)
        {
            this.configtobuild.AnalyticsEnabled = analyticsenabled;
            return this;
        }
        public ConfigBuilder ConfigUrl(string configUrl)
        {
            this.configtobuild.ConfigUrl = configUrl;
            return this;
        }
        public ConfigBuilder EventUrl(string eventUrl)
        {
            this.configtobuild.EventUrl = eventUrl;
            return this;
        }
        public ConfigBuilder connectionTimeout(int connectionTimeout)
        {
            this.configtobuild.ConnectionTimeout = connectionTimeout;
            return this;
        }
        public ConfigBuilder readTimeout(int readTimeout)
        {
            this.configtobuild.ReadTimeout = readTimeout;
            return this;
        }

        public ConfigBuilder writeTimeout(int writeTimeout)
        {
            this.configtobuild.WriteTimeout = writeTimeout;
            return this;
        }

        public ConfigBuilder debug(bool debug)
        {
            this.configtobuild.Debug = debug;
            return this;
        }

        public ConfigBuilder SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            this.configtobuild.LoggerFactory = loggerFactory;
            return this;
        }
    }
}
