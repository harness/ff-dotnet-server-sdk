using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace io.harness.cfsdk.client.connector
{
    internal sealed class EventSource : IService
    {
        private readonly string url;
        private readonly Config config;
        private readonly HttpClient httpClient;
        private readonly IUpdateCallback callback;
        private readonly ILogger logger;
        private StreamReader streamReader;

        public EventSource(HttpClient httpClient, string url, Config config, IUpdateCallback callback = null)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.url = url ?? throw new ArgumentNullException(nameof(url));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.callback = callback;
            this.logger = config.CreateLogger<EventSource>();
        }

        public void Close()
        {
            Stop();
        }

        public void Start()
        {
            Task.Run(() => StartStreaming());
        }

        public void Stop()
        {
            if (this.streamReader != null)
            {
                this.streamReader.Close();
                this.streamReader.Dispose();
                this.streamReader = null;
            }

            logger.LogInformation("Stopping EventSource service.");
        }

        private async Task StartStreaming()
        {
            try
            {
                logger.LogInformation("Starting EventSource service.");
                using (this.streamReader = new StreamReader(await this.httpClient.GetStreamAsync(url)))
                {
                    this.callback?.OnStreamConnected();

                    while (!streamReader.EndOfStream)
                    {
                        string message = await streamReader.ReadLineAsync();
                        if (!message.Contains("domain")) continue;

                        logger.LogInformation("EventSource message received {Message}", message);

                        // parse message
                        JObject jsommessage = JObject.Parse("{" + message + "}");

                        Message msg = new Message();
                        msg.Domain = (string)jsommessage["data"]["domain"];
                        msg.Event = (string)jsommessage["data"]["event"];
                        msg.Identifier = (string)jsommessage["data"]["identifier"];
                        msg.Version = long.Parse((string)jsommessage["data"]["version"]);


                        this.callback?.Update(msg, false);
                    }
                }
            }
            catch (Exception)
            {
                logger.LogError("EventSource service throw error. Retrying in {PollIntervalInSeconds}", this.config.PollIntervalInSeconds);
            }
            finally
            {
                this.callback?.OnStreamDisconnected();
            }

        }
    }
}
