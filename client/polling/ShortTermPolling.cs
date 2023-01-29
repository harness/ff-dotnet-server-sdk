using System;
using System.Timers;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.polling
{
    public class ShortTermPolling : IEvaluationPolling
    {
        private static readonly int MINIMUM_POLLING_INTERVAL = 10000;
        private readonly long pollingInterval;
        private readonly ILogger logger;
        private Timer timer;

        public ShortTermPolling(int time, ILogger<ShortTermPolling> logger = null)
        {
            pollingInterval = Math.Max(time, MINIMUM_POLLING_INTERVAL);
            this.logger = logger ?? api.Config.DefaultLogger;
        }

        public void start(Action<object, ElapsedEventArgs> runnable)
        {
            if (timer != null)
            {
                logger.LogInformation("POLLING timer - stopping before start");
                timer.Stop();
                timer.Dispose();
            }
            logger.LogInformation("POLLING timer - scheduling new one");
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
                logger.LogInformation("POLLING timer - stopping on exit");
                timer.Stop();
                timer.Dispose();
            }
            logger.LogInformation("POLLING timer - stoped");
        }
    }
}
