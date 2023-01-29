using System;
using Disruptor;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.dto;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api.analytics
{
    //Consumer class for consuming the incoming objects in the LMAX ring buffer.It has the following
    //functionalities 1) Listens to the queue and take out the incoming object 2) Place them
    //appropriately in the cache for further processing

    internal sealed class AnalyticsEventHandler : IEventHandler<Analytics>
    {
        private readonly AnalyticsCache analyticsCache;
        private readonly AnalyticsPublisherService analyticsPublisherService;
        private readonly ILogger logger;

        public AnalyticsEventHandler(AnalyticsCache analyticsCache, AnalyticsPublisherService analyticsPublisherService, ILogger logger = null)
        {
            this.analyticsCache = analyticsCache ?? throw new ArgumentNullException(nameof(analyticsCache));
            this.analyticsPublisherService = analyticsPublisherService ?? throw new ArgumentNullException(nameof(analyticsPublisherService));
            this.logger = logger ?? Config.DefaultLogger;
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
                        logger.LogWarning(e, "Failed to send analytics data to server");
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
