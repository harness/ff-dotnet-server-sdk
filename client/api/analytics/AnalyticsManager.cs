using Disruptor;
using Disruptor.Dsl;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using Serilog;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace io.harness.cfsdk.client.api.analytics
{

    //This class handles various analytics service related components and prepares them 1) It creates
    //the LMAX ring buffer 2) It pushes data to the buffer and publishes it for consumption 3)
    //Initilazes the cache for analytics
    public class AnalyticsManager
    {
        private AnalyticsCache analyticsCache;
        private RingBuffer<Analytics> ringBuffer;
        private Timer timer;

        public AnalyticsManager(String environmentID, String jwtToken, Config config)
        {
            this.analyticsCache = new AnalyticsCache();

            AnalyticsPublisherService analyticsPublisherService =
                 new AnalyticsPublisherService(jwtToken, config, environmentID, analyticsCache);
            ringBuffer = createRingBuffer(config.getBufferSize(), analyticsPublisherService);

            timer = new Timer((long)config.Frequency * 1000);
            timer.Elapsed += new ElapsedEventHandler(run_onTimer);
            timer.AutoReset = true;
            timer.Enabled = true;
            timer.Start();
        }
        public void pushToQueue(dto.Target target, FeatureConfig featureConfig, Variation variation)
        {
            Analytics analytics =
                Analytics.Builder()
                    .featureConfig(featureConfig)
                    .target(target)
                    .variation(variation)
                    .eventType(EventType.METRICS)
                    .Build();
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
        internal async void run_onTimer(object source, System.Timers.ElapsedEventArgs e)
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
