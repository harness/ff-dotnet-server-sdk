using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using System.Timers;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api.analytics
{
    internal class MetricsProcessor
    {
        private readonly ILogger<MetricsProcessor> logger;
        private readonly AnalyticsCache analyticsCache;
        private Timer timer;
        private readonly AnalyticsPublisherService analyticsPublisherService;
        private readonly Config config;

        public MetricsProcessor(Config config, AnalyticsCache analyticsCache, AnalyticsPublisherService analyticsPublisherService, ILoggerFactory loggerFactory)
        {
            this.analyticsCache = analyticsCache;
            this.config = config;
            this.analyticsPublisherService = analyticsPublisherService;
            this.logger = loggerFactory.CreateLogger<MetricsProcessor>();
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
                logger.LogInformation("SDKCODE(metric:7000): Metrics thread started");
            }
        }


        public void Stop()
        {
            if (config.analyticsEnabled && this.timer != null)
            {
                this.timer.Stop();
                this.timer = null;
                logger.LogInformation("SDKCODE(metric:7001): Metrics thread exited");
            }
        }

        public void PushToCache(dto.Target target, FeatureConfig featureConfig, Variation variation)
        {
            var cacheSize = analyticsCache.GetAllElements().Count;
            var bufferSize = config.getBufferSize();

            if (cacheSize > bufferSize)
            {
                logger.LogWarning("Metric frequency map exceeded buffer size ({cacheSize} > {bufferSize}), force flushing", cacheSize, bufferSize);

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
            logger.LogDebug("Timer Elapsed - Processing/Sending analytics data");
            SendMetrics();
        }

        internal void SendMetrics()
        {
            try
            {
                analyticsPublisherService.SendDataAndResetCache();
            }
            catch (CfClientException ex)
            {
                logger.LogError(ex, "Failed to send analytics data to server");
            }
        }
    }
}
