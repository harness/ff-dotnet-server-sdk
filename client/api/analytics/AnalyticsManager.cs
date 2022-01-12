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
        void PushToQueue(dto.Target target, FeatureConfig featureConfig, Variation variation);
    }
    internal class MetricsProcessor : IMetricsProcessor
    {
        private AnalyticsCache analyticsCache;
        private RingBuffer<Analytics> ringBuffer;
        private Timer timer;
        private AnalyticsPublisherService analyticsPublisherService;
        private IMetricCallback callback;
        private Config config;
        public MetricsProcessor(IConnector connector, Config config, IMetricCallback callback)
        {
            this.analyticsCache = new AnalyticsCache();
            this.callback = callback;
            this.config = config;
            this.analyticsPublisherService = new AnalyticsPublisherService(connector, analyticsCache);
            this.ringBuffer = createRingBuffer(config.getBufferSize(), analyticsPublisherService);
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
            if(config.analyticsEnabled && this.timer != null)
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
                Log.Warning("Insufficient capacity in the analytics ringBuffer");
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
        private RingBuffer<Analytics> createRingBuffer(int bufferSize, AnalyticsPublisherService analyticsPublisherService)
        {
            // The factory for the event
            //AnalyticsEventFactory factory = new AnalyticsEventFactory();

            // Construct the Disruptor
            Disruptor<Analytics> disruptor =
                new Disruptor<Analytics>(() => new Analytics(), bufferSize, TaskScheduler.Default);

            // Connect the handler
            disruptor.HandleEventsWith(
                new AnalyticsEventHandler(analyticsCache, analyticsPublisherService));

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
                Log.Warning("Insufficient capacity in the analytics ringBuffer");
            }
            else
            {
                Log.Information("Publishing timerInfo to ringBuffer");
                ringBuffer[sequence].EventType = EventType.TIMER; // Get the entry in the Disruptor for the sequence
            }

            if (sequence != -1)
            {
                ringBuffer.Publish(sequence);
            }
        }
    }
}
