using Disruptor;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.dto;
using Serilog;

namespace io.harness.cfsdk.client.api.analytics
{
    //Consumer class for consuming the incoming objects in the LMAX ring buffer.It has the following
    //functionalities 1) Listens to the queue and take out the incoming object 2) Place them
    //appropriately in the cache for further processing

    internal class AnalyticsEventHandler : IEventHandler<Analytics>
    {
        private AnalyticsCache analyticsCache;
        private AnalyticsPublisherService analyticsPublisherService;
        private readonly ILogger logger;

        public AnalyticsEventHandler(AnalyticsCache analyticsCache, AnalyticsPublisherService analyticsPublisherService, ILogger logger = null)
        {
            this.analyticsCache = analyticsCache;
            this.analyticsPublisherService = analyticsPublisherService;
            this.logger = logger ?? Log.Logger;
        }
        public void OnEvent(Analytics analytics, long sequence, bool endOfBatch)
        {
            switch (analytics.EventType)
            {
                case EventType.TIMER:
                    try
                    {
                        analyticsPublisherService.sendDataAndResetCache();
                    }
                    catch (CfClientException e)
                    {
                        logger.Warning(e, "Failed to send analytics data to server");
                    }
                    break;
                case EventType.METRICS:
                    int count = analyticsCache.getIfPresent(analytics);
                    analyticsCache.Put(analytics, count + 1);
                    break;
                default:
                    break;
            }
        }
    }
}
