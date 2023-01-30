using System;
using System.IO;
using System.Net.Http;
using System.Threading;
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
            Stop();
            Task.Run(() => StartStreaming());
        }

        public void Stop()
        {
            var streamReader = Interlocked.Exchange(ref this.streamReader, null);
            if (streamReader != null)
            {
                logger.LogInformation("Stopping EventSource service.");

                streamReader.Dispose();
            }
        }

        private async Task StartStreaming()
        {
            try
            {
                logger.LogInformation("Starting EventSource service.");
                using (var streamReader = new StreamReader(await this.httpClient.GetStreamAsync(url)))
                {
                    if (Interlocked.CompareExchange(ref this.streamReader, streamReader, null) != null)
                        return;

                    this.callback?.OnStreamConnected();

                    try
                    {
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
                    finally
                    {
                        Interlocked.CompareExchange(ref this.streamReader, null, streamReader);
                        this.callback?.OnStreamDisconnected();
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception e)
            {
                logger.LogError("EventSource service throw error: {Error}. Retrying in {PollIntervalInSeconds}", e.Message, this.config.PollIntervalInSeconds);
            }

        }
    }
}
