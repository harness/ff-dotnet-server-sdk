using System;
using io.harness.cfsdk.client.connector;
using System.Threading;
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
    internal class AuthService : IAuthService
    {
        private readonly ILogger logger;
        private readonly IConnector connector;
        private readonly Config config;
        private readonly IAuthCallback callback;
        private Timer authTimer;
        private int retries = 0;

        public AuthService(IConnector connector, Config config, IAuthCallback callback, ILoggerFactory loggerFactory)
        {
            this.connector = connector;
            this.config = config;
            this.callback = callback;
            this.logger = loggerFactory.CreateLogger<AuthService>();
        }
        public void Start()
        {
            if (authTimer != null) return;

            this.retries = 0;
            logger.LogDebug("Initiate authentication");
            authTimer = new Timer(OnTimedEvent, null, 0, config.PollIntervalInMiliSeconds);
        }
        public void Stop()
        {
            if (authTimer == null) return;
            authTimer.Dispose();
            authTimer = null;
        }
        private async void OnTimedEvent(object source)
        {
            try
            {
                await connector.Authenticate();
                callback.OnAuthenticationSuccess();
                Stop();
                logger.LogDebug("Stopping authentication service");
            }
            catch (Exception ex)
            {
                // Exception thrown on Authentication. Timer will retry authentication.
                if (retries++ >= config.MaxAuthRetries)
                {
                    logger.LogError(ex, "SDKCODE(auth:2001): Authentication failed. Max authentication retries reached {retries} - defaults will be served", retries);
                    Stop();
                }
                else
                {
                    logger.LogWarning(ex, "SDKCODE(auth:2003): Retrying to authenticate. Retry {retries} in {pollIntervalInSeconds} seconds. Reason: {reason}", retries, config.pollIntervalInSeconds, ex.Message);
                }
            }
        }
    }
}
