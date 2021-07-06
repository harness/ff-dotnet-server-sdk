using System;

namespace io.harness.cfsdk.client.api
{
    public class Config
    {
        public static int MIN_FREQUENCY = 60;

        public string ConfigUrl { get => configUrl;  } 
        internal string configUrl  = "https://config.feature-flags.uat.harness.io/api/1.0";
        public string EventUrl { get => eventUrl; } 
        internal string eventUrl = "https://event.feature-flags.uat.harness.io/api/1.0";
        public bool StreamEnabled { get => streamEnabled; }
        internal bool streamEnabled = true;

        public int PollIntervalInSeconds { get => pollIntervalInSeconds*1000;  }
        internal int pollIntervalInSeconds = 60;

        // configurations for Analytics
        public bool AnalyticsEnabled { get => analyticsEnabled; }
        internal bool analyticsEnabled;

        public int Frequency { get => Math.Max(frequency, Config.MIN_FREQUENCY); }
        private int frequency = 60;


        //BufferSize must be a power of 2 for LMAX to work. This function vaidates
        //that. Source: https://stackoverflow.com/a/600306/1493480
        public int BufferSize
        {
            get
            {
                if (!(bufferSize != 0 && ((bufferSize & (bufferSize - 1)) == 0)))
                {
                    throw new CfClientException("BufferSize must be a power of 2");
                }
                return bufferSize;
            }
        }
        private int bufferSize = 1024;


        /** timeout in milliseconds to connect to CF Server */
        public int ConnectionTimeout { get =>connectionTimeout;} 
        internal int connectionTimeout = 10000;

        /** timeout in milliseconds for reading data from CF Server */
        public int ReadTimeout { get => readTimeout;  } 
        internal int readTimeout { get; set; } = 30000;

        /** timeout in milliseconds for writing data to CF Server */
        public int WriteTimeout { get => WriteTimeout;  } 
        internal int writeTimeout { get; set; } = 10000;

        public bool Debug { get => debug;  }
        internal bool debug { get; set; } = false;

        /** If metrics service POST call is taking > this time, we need to know about it */

        public long MetricsServiceAcceptableDuration { get => metricsServiceAcceptableDuration;  }
        internal long metricsServiceAcceptableDuration = 10000;

        public Config(string configUrl, string eventUrl, bool streamEnabled, int pollIntervalInSeconds, bool analyticsEnabled, int frequency, int bufferSize,  int connectionTimeout, int readTimeout, int writeTimeout, bool debug, long metricsServiceAcceptableDuration)
        {
            this.configUrl = configUrl;
            this.eventUrl = eventUrl;
            this.streamEnabled = streamEnabled;
            this.pollIntervalInSeconds = pollIntervalInSeconds;
            this.analyticsEnabled = analyticsEnabled;
            this.frequency = frequency;
            this.bufferSize = bufferSize;
            this.connectionTimeout = connectionTimeout;
            this.readTimeout = readTimeout;
            this.writeTimeout = writeTimeout;
            this.debug = debug;
            this.metricsServiceAcceptableDuration = metricsServiceAcceptableDuration;
        }

        public Config()
        {
        }

        public static ConfigBuilder Builder()
        {
            return new ConfigBuilder();
        }

        /*
  BufferSize must be a power of 2 for LMAX to work. This function vaidates
  that. Source: https://stackoverflow.com/a/600306/1493480
 */
        public int getBufferSize()
        {
            if (!(bufferSize != 0 && ((bufferSize & (bufferSize - 1)) == 0)))
            {
                throw new CfClientException("BufferSize must be a power of 2");
            }
            return bufferSize;
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
            this.configtobuild.pollIntervalInSeconds = pollIntervalInSeconds;
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
            this.configtobuild.analyticsEnabled = analyticsenabled;
            return this;
        }
        public ConfigBuilder ConfigUrl(string configUrl)
        {
            this.configtobuild.configUrl = configUrl;
            return this;
        }
        public ConfigBuilder EventUrl(string eventUrl)
        {
            this.configtobuild.eventUrl = eventUrl;
            return this;
        }
        public ConfigBuilder connectionTimeout(int connectionTimeout)
        {
            this.configtobuild.connectionTimeout = connectionTimeout;
            return this;
        }
        public ConfigBuilder readTimeout(int readTimeout)
        {
            this.configtobuild.readTimeout = readTimeout;
            return this;
        }

        public ConfigBuilder writeTimeout(int writeTimeout)
        {
            this.configtobuild.writeTimeout = writeTimeout;
            return this;
        }

        public ConfigBuilder debug(bool debug)
        {
            this.configtobuild.debug = debug;
            return this;
        }
    }
}
