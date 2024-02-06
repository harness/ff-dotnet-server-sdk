using System.Timers;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;
using Target = io.harness.cfsdk.client.dto.Target;

namespace io.harness.cfsdk.client.api.analytics
{
    internal class MetricsProcessor
    {
        private readonly AnalyticsCache analyticsCache;
        private readonly AnalyticsPublisherService analyticsPublisherService;
        private readonly Config config;
        private readonly ILogger<MetricsProcessor> logger;
        private Timer timer;

        public MetricsProcessor(Config config, AnalyticsCache analyticsCache,
            AnalyticsPublisherService analyticsPublisherService, ILoggerFactory loggerFactory)
        {
            this.analyticsCache = analyticsCache;
            this.config = config;
            this.analyticsPublisherService = analyticsPublisherService;
            logger = loggerFactory.CreateLogger<MetricsProcessor>();
        }

        public void Start()
        {
            if (config.analyticsEnabled)
            {
                timer = new Timer((long)config.Frequency * 1000);
                timer.Elapsed += Timer_Elapsed;
                timer.AutoReset = true;
                timer.Enabled = true;
                timer.Start();
                logger.LogInformation("SDKCODE(metric:7000): Metrics thread started");
            }
        }


        public void Stop()
        {
            if (config.analyticsEnabled && timer != null)
            {
                timer.Stop();
                timer = null;
                logger.LogInformation("SDKCODE(metric:7001): Metrics thread exited");
            }
        }

        public void PushToCache(Target target, FeatureConfig featureConfig, Variation variation)
        {
            var cacheSize = analyticsCache.GetAllElements().Count;
            var bufferSize = config.getBufferSize();

            if (cacheSize > bufferSize)
            {
                logger.LogWarning(
                    "Metric frequency map exceeded buffer size ({cacheSize} > {bufferSize}), force flushing", cacheSize,
                    bufferSize);

                // If the map is starting to grow too much then push the metrics now and reset the counters
                SendMetrics();
            }
            else
            {
                // Create evaluation metrics
                // Since 1.4.2, we use the global target identifier for evaluation metrics. 
                var evaluationAnalytics = createEvaluationAnalytics(featureConfig, variation);

                // Create target metrics 
                Analytics targetAnalytics = new TargetAnalytics(target);
                var count = analyticsCache.getIfPresent(evaluationAnalytics);

            }
        }

        private Analytics createEvaluationAnalytics(FeatureConfig featureConfig, Variation variation)
        {
            var globalTarget = new Target(EvaluationAnalytics.GlobalTargetIdentifier,
                EvaluationAnalytics.GlobalTargetName, null);
            Analytics evaluationAnalytics = new EvaluationAnalytics(featureConfig, variation, globalTarget);
            var evaluationCount = analyticsCache.getIfPresent(evaluationAnalytics);
            analyticsCache.Put(evaluationAnalytics, evaluationCount + 1);
            return evaluationAnalytics;
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