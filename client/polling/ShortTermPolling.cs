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

        public ShortTermPolling(int time)
        {
            pollingInterval = Math.Max(time, MINIMUM_POLLING_INTERVAL);

        }

        public void start(Action<object, ElapsedEventArgs> runnable)
        {
            if (timer != null)
            {
                Log.Information("POLLING timer - stopping before start");
                timer.Stop();
                timer.Dispose();
            }
            Log.Information("POLLING timer - scheduling new one");
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
                Log.Information("POLLING timer - stopping on exit");
                timer.Stop();
                timer.Dispose();
            }
            Log.Information("POLLING timer - stoped");
        }
    }
}
