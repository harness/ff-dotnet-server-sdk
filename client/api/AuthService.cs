using System;
using System.Threading;
using io.harness.cfsdk.client.connector;
using Microsoft.Extensions.Logging;

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
    internal sealed class AuthService : IAuthService
    {
        private readonly IConnector connector;
        private readonly Config config;
        private readonly IAuthCallback callback;
        private readonly ILogger logger;
        private Timer authTimer;

        public AuthService(IConnector connector, Config config, IAuthCallback callback = null)
        {
            this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.callback = callback;
            this.logger = config.CreateLogger<AuthService>();
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
                callback?.OnAuthenticationSuccess();
                Stop();
                logger.LogInformation("Stopping authentication service");
            }
            catch
            {
                // Exception thrown on Authentication. Timer will retry authentication.
                logger.LogError("Exception while authenticating, retry in {PollIntervalInSeconds}", this.config.PollIntervalInSeconds);
            }
        }
    }
}
