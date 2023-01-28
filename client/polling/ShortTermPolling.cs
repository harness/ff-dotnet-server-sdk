using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;

namespace io.harness.cfsdk.client.polling
{
    public class ShortTermPolling : IEvaluationPolling
    {
        private static int MINIMUM_POLLING_INTERVAL = 10000;
        private long pollingInterval;
        private Timer timer;
        private readonly ILogger logger;

        public ShortTermPolling(int time, ILogger logger = null)
        {
            pollingInterval = Math.Max(time, MINIMUM_POLLING_INTERVAL);
            this.logger = logger ?? Log.Logger;
        }

        public void start(Action<object, ElapsedEventArgs> runnable)
        {
            if (timer != null)
            {
                logger.Information("POLLING timer - stopping before start");
                timer.Stop();
                timer.Dispose();
            }
            logger.Information("POLLING timer - scheduling new one");
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
                logger.Information("POLLING timer - stopping on exit");
                timer.Stop();
                timer.Dispose();
            }
            logger.Information("POLLING timer - stoped");
        }
    }
}
