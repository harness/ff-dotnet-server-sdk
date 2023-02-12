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
        private int retries = 0;

        public AuthService(IConnector connector, Config config, IAuthCallback callback = null)
        {
            this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.callback = callback;
            this.logger = config.CreateLogger<AuthService>();
        }
        public void Start()
        {
            this.retries = 0;
            // initiate authentication
            authTimer = new Timer(OnTimedEvent, null, 0, config.PollIntervalInMiliSeconds);
        }
        public void Stop()
        {
            authTimer?.Dispose();
            authTimer = null;
        }
        private async void OnTimedEvent(object source)
        {
            try
            {
                await connector.Authenticate();
                callback?.OnAuthenticationSuccess();
                Stop();
                logger.LogInformation("Stopping authentication service");
            }
            catch
            {
                // Exception thrown on Authentication. Timer will retry authentication.
                if (retries++ >= config.MaxAuthRetries)
                {
                    logger.LogError("Max authentication retries reached {Retries}", retries);
                    Stop();
                }
                else
                {
                    logger.LogError("Exception while authenticating, retry ({Retries}) in {PollInterval}msec", retries, config.PollIntervalInMiliSeconds);
                }
            }
        }
    }
}
