using System;
using System.Threading.Tasks;
using System.Timers;
using Disruptor;
using Disruptor.Dsl;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api.analytics
{
    interface IMetricCallback
    {

    }
    interface IMetricsProcessor
    {
        void Start();
        void Stop();
        void PushToQueue(dto.Target target, FeatureConfig featureConfig, Variation variation);
    }
    internal sealed class MetricsProcessor : IMetricsProcessor
    {
        private readonly AnalyticsCache analyticsCache;
        private readonly RingBuffer<Analytics> ringBuffer;
        private readonly AnalyticsPublisherService analyticsPublisherService;
        private readonly IMetricCallback callback;
        private readonly Config config;
        private readonly ILogger logger;
        private Timer timer;

        public MetricsProcessor(IConnector connector, Config config, IMetricCallback callback = null)
        {
            this.analyticsCache = new AnalyticsCache();
            this.callback = callback;
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.logger = config.CreateLogger<MetricsProcessor>();

            this.analyticsPublisherService = new AnalyticsPublisherService(connector, analyticsCache, config);
            this.ringBuffer = createRingBuffer(config, analyticsPublisherService);
        }

        public void Start()
        {
            if (config.AnalyticsEnabled)
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
            if (config.AnalyticsEnabled && this.timer != null)
            {
                this.timer.Stop();
                this.timer = null;
            }
        }

        public void PushToQueue(dto.Target target, FeatureConfig featureConfig, Variation variation)
        {
            Analytics analytics = new Analytics(featureConfig, target, variation, EventType.METRICS);
            long sequence = -1;
            if (!ringBuffer.TryNext(out sequence)) // Grab the next sequence if we can
            {
                logger.LogWarning("Insufficient capacity in the analytics ringBuffer");
            }
            else
            {
                ringBuffer[sequence].FeatureConfig = analytics.FeatureConfig;
                ringBuffer[sequence].Target = analytics.Target;
                ringBuffer[sequence].Variation = analytics.Variation;
            }

            if (sequence != -1)
            {
                ringBuffer.Publish(sequence);
            }
        }
        private RingBuffer<Analytics> createRingBuffer(Config config, AnalyticsPublisherService analyticsPublisherService)
        {
            // The factory for the event
            //AnalyticsEventFactory factory = new AnalyticsEventFactory();

            // Construct the Disruptor
            Disruptor<Analytics> disruptor =
                new Disruptor<Analytics>(() => new Analytics(), config.BufferSize, TaskScheduler.Default);

            // Connect the handler
            disruptor.HandleEventsWith(
                new AnalyticsEventHandler(analyticsCache, analyticsPublisherService, config.CreateLogger<AnalyticsEventHandler>()));

            // Start the Disruptor, starts all threads running
            disruptor.Start();

            // Get the ring buffer from the Disruptor to be used for publishing.
            return disruptor.RingBuffer;
        }
        internal void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            long sequence = -1;
            if (!ringBuffer.TryNext(out sequence)) // Grab the next sequence if we can
            {
                logger.LogWarning("Insufficient capacity in the analytics ringBuffer");
            }
            else
            {
                logger.LogInformation("Publishing timerInfo to ringBuffer");
                ringBuffer[sequence].EventType = EventType.TIMER; // Get the entry in the Disruptor for the sequence
            }

            if (sequence != -1)
            {
                ringBuffer.Publish(sequence);
            }
        }
    }
}
