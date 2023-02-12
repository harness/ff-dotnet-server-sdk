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
            // `this.streamReader` is owned by the `StartStreaming` function
            // which is running in a separate thread. Beware race conditions!
            var strmReader = Interlocked.Exchange(ref this.streamReader, null);
            if (strmReader != null)
            {
                // Just close the reader. This will cause `StartStreaming`
                // to terminate and dispose the stream.
                try
                {
                    strmReader.Close();
                }
                catch (ObjectDisposedException)
                {
                    // Fine
                }
            }
        }

        private async Task StartStreaming()
        {
            try
            {
                logger.LogInformation("Starting EventSource service.");
                using (var strmReader = new StreamReader(await this.httpClient.GetStreamAsync(url)))
                {
                    // Set `this.streamReader` IIF it is currently null...
                    if (Interlocked.CompareExchange(ref this.streamReader, strmReader, null) != null)
                    {
                        // `this.streamReader` is not null, meaning there is another stream reader.
                        // This can happen if Start() is called twice very quickly.
                        return;
                    }

                    try
                    {
                        callback?.OnStreamConnected();

                        while (!strmReader.EndOfStream)
                        {
                            var message = await strmReader.ReadLineAsync();
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
                    catch (ObjectDisposedException)
                    {
                        // Stream was closed/disposed, probably via `Stop`.
                    }
                    finally
                    {
                        // Clear `this.streamReader` IIF it contains _our_ `strmReader`.
                        Interlocked.CompareExchange(ref this.streamReader, null, strmReader);

                        callback?.OnStreamDisconnected();
                    }
                }
            }
            catch (Exception)
            {
                logger.LogError("EventSource service throw error. Retrying in {PollInterval}msec", config.PollIntervalInMiliSeconds);
            }
        }
    }
}
