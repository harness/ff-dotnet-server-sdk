using System;
using System.Timers;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.polling
{
    public class ShortTermPolling : IEvaluationPolling
    {
        private readonly ILogger<ShortTermPolling> logger;
        private const int MinimumPollingInterval = 10000;
        private readonly long pollingInterval;
        private Timer timer;

        public ShortTermPolling(int time) : this(time, LoggerFactory.Create(builder => { builder.AddConsole(); }))
        {
        }

        public ShortTermPolling(int time, ILoggerFactory loggerFactory)
        {
            pollingInterval = Math.Max(time, MinimumPollingInterval);
            logger = loggerFactory.CreateLogger<ShortTermPolling>();
        }

        public void start(Action<object, ElapsedEventArgs> runnable)
        {
            if (timer != null)
            {
                logger.LogDebug("POLLING timer - stopping before start");
                timer.Stop();
                timer.Dispose();
            }
            logger.LogDebug("POLLING timer - scheduling new one");
            timer = new Timer(pollingInterval);
            timer.Elapsed += new ElapsedEventHandler(runnable);
            timer.AutoReset = true;
            timer.Enabled = true;
            timer.Start();
        }

        public void stop()
        {
            if (timer != null)
            {
                logger.LogDebug("POLLING timer - stopping on exit");
                timer.Stop();
                timer.Dispose();
            }
            logger.LogDebug("POLLING timer - stoped");
        }
    }
}
