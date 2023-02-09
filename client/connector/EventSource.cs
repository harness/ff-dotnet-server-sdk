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
            _ = StartStreaming();
        }

        public void Stop()
        {
            var streamReader = Interlocked.Exchange(ref this.streamReader, null);
            if (streamReader != null)
            {
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

                    callback?.OnStreamConnected();

                    try
                    {
                        while (!streamReader.EndOfStream)
                        {
                            var message = await streamReader.ReadLineAsync();
                            if (!message.Contains("domain")) continue;

                            logger.LogInformation("EventSource message received {Message}", message);

                            // parse message
                            var jsonMessage = JObject.Parse("{" + message + "}");
                            var data = jsonMessage["data"];
                            var msg = new Message
                            {
                                Domain = (string)data["domain"],
                                Event = (string)data["event"],
                                Identifier = (string)data["identifier"],
                                Version = long.Parse((string)data["version"])
                            };

                            callback?.Update(msg, false);
                        }
                    }
                    finally
                    {
                        Interlocked.CompareExchange(ref this.streamReader, null, streamReader);
                        callback?.OnStreamDisconnected();
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception)
            {
                logger.LogError("EventSource service throw error. Retrying in {PollIntervalInSeconds}", config.PollIntervalInSeconds);
            }

        }
    }
}
