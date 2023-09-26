using System;
using System.Timers;

namespace io.harness.cfsdk.client.polling
{
    public interface IEvaluationPolling
    {
        void start(Action<object, ElapsedEventArgs> runnable);

        void stop();
    }
}
