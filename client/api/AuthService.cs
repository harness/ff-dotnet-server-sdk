using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.HarnessOpenAPIService;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace io.harness.cfsdk.client.api
{
    interface IAuthCallback
    {
        void OnAuthenticationSuccess();
    }
    interface IAuthService
    {
        void Start();
        void Stop();
    }

    /// <summary>
    /// The class is in charge of initiating authentication requests and retry until successful authentication.
    /// </summary>
    internal class AuthService : IAuthService
    {
        private IConnector connector;
        private Config config;
        private Timer authTimer;
        private IAuthCallback callback;
        private readonly ILogger logger;

        public AuthService(IConnector connector, Config config, IAuthCallback callback, ILogger logger = null)
        {
            this.connector = connector;
            this.config = config;
            this.callback = callback;
            this.logger = logger ?? Log.Logger;
        }
        public void Start()
        {
            // initiate authentication
            authTimer = new Timer(new TimerCallback(OnTimedEvent), null, 0, this.config.PollIntervalInMiliSeconds);
        }
        public void Stop()
        {
            if (authTimer != null)
            {
                authTimer.Dispose();
                authTimer = null;
            }
        }
        private void OnTimedEvent(object source)
        {
            try
            {
                connector.Authenticate();
                callback.OnAuthenticationSuccess();
                Stop();
                logger.Information("Stopping authentication service");
            }
            catch
            {
                // Exception thrown on Authentication. Timer will retry authentication.
                logger.Error("Exception while authenticating, retry in {PollIntervalInSeconds}", this.config.pollIntervalInSeconds);
            }
        }
    }
}
