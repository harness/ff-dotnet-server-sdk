using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace io.harness.cfsdk.client.connector
{
    public class EventSource : IService
    {
        private readonly ILogger<EventSource> logger;
        private readonly string url;
        private readonly Config config;
        private readonly HttpClient httpClient;
        private readonly IUpdateCallback callback;
        private const int ReadTimeoutMs = 60_000;

        public EventSource(HttpClient httpClient, string url, Config config, IUpdateCallback callback, ILoggerFactory loggerFactory)
        {
            this.httpClient = httpClient;
            this.url = url;
            this.config = config;
            this.callback = callback;
            this.logger = loggerFactory.CreateLogger<EventSource>();
        }

        public void Close()
        {
            Stop();
        }

        public void Start()
        {
            _ = StartStreaming();
        }

        public void Stop()
        {
            logger.LogDebug("Stopping EventSource service.");
        }

        private string ReadLine(Stream stream, int timeoutMs)
        {
            using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs)))
            using (cancellationTokenSource.Token.Register(stream.Dispose))
            {
                StringBuilder builder = new StringBuilder();
                int next;
                do
                {
                    next = stream.ReadByte();
                    if (next == -1)
                    {
                        return null;
                    }

                    builder.Append((char)next);
                } while (next != 10);

                return builder.ToString();
            }
        }

        private async Task StartStreaming()
        {
            try
            {
                Debug.Assert(httpClient != null);

                logger.LogDebug("Starting EventSource service.");
                using (Stream stream = await this.httpClient.GetStreamAsync(url))
                {
                    callback.OnStreamConnected();

                    string message;
                    while ((message = ReadLine(stream, ReadTimeoutMs)) != null)
                    {
                        if (!message.Contains("domain"))
                        {
                            logger.LogTrace("Received event source heartbeat");
                            continue;
                        }

                        logger.LogInformation("SDKCODE(stream:5002): SSE event received {message}", message);

                        // parse message
                        var jsonMessage = JObject.Parse("{" + message + "}");
                        var data = jsonMessage["data"];
                        var msg = new Message
                        {
                            Domain = (string) data["domain"],
                            Event = (string) data["event"],
                            Identifier = (string) data["identifier"],
                            Version = long.Parse((string) data["version"])
                        };
                        
                        callback.Update(msg, false);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "EventSource service threw an error: {reason} Retrying in {pollIntervalInSeconds}", e.Message, config.pollIntervalInSeconds);
                Debug.WriteLine(e.ToString());
            }
            finally
            {
                Thread.Sleep(TimeSpan.FromSeconds(10));
                callback.OnStreamDisconnected();
            }

        }
    }
}
