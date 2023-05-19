using Disruptor;
using Disruptor.Dsl;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using Serilog;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace io.harness.cfsdk.client.api.analytics
{
    interface IMetricCallback
    {

    }
    interface IMetricsProcessor
    {
        void Start();
        void Stop();
        void PushToCache(dto.Target target, FeatureConfig featureConfig, Variation variation);
    }
    internal class MetricsProcessor : IMetricsProcessor
    {
        private AnalyticsCache analyticsCache;
        private Timer timer;
        private AnalyticsPublisherService analyticsPublisherService;
        private IMetricCallback callback;
        private Config config;
        public MetricsProcessor(IConnector connector, Config config, IMetricCallback callback, AnalyticsCache analyticsCache, AnalyticsPublisherService analyticsPublisherService)
        {
            this.analyticsCache = analyticsCache;
            this.callback = callback;
            this.config = config;
            this.analyticsPublisherService = analyticsPublisherService;
        }

        public void Start()
        {
            if (config.analyticsEnabled)
            {
                this.timer = new Timer((long)config.Frequency * 1000);
                this.timer.Elapsed += Timer_Elapsed;
                this.timer.AutoReset = true;
                this.timer.Enabled = true;
                this.timer.Start();
            }
        }


        public void Stop()
        {
            if (config.analyticsEnabled && this.timer != null)
            {
                this.timer.Stop();
                this.timer = null;
            }
        }

        public void PushToCache(dto.Target target, FeatureConfig featureConfig, Variation variation)
        {
            var cacheSize = analyticsCache.GetAllElements().Count;
            var bufferSize = config.getBufferSize();

            if (cacheSize > bufferSize)
            {
                Log.Warning("Metric frequency map exceeded buffer size ({0} > {1}), force flushing", cacheSize, bufferSize);

                // If the map is starting to grow too much then push the metrics now and reset the counters
                SendMetrics();

            }
            else
            {
                Analytics analytics = new Analytics(featureConfig, target, variation, EventType.METRICS);
                int count = analyticsCache.getIfPresent(analytics);
                analyticsCache.Put(analytics, count + 1);
            }
        }

        internal void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Log.Debug("Timer Elapsed - Processing/Sending analytics data");
            SendMetrics();
        }

        internal void SendMetrics()
        {
            try
            {
                analyticsPublisherService.sendDataAndResetCache();
            }
            catch (CfClientException ex)
            {
                Log.Warning("Failed to send analytics data to server", ex);
            }
        }
    }
}
