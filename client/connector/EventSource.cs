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
        private readonly HttpClient httpClient;
        private readonly IUpdateCallback callback;
        private readonly CancellationTokenSource cancellationTokenSource;
        private static readonly Random Random = new();

        private const int InitialConnectionTimeoutMs = 10000;
        private const int ReadTimeoutMs = 35_000;

        public EventSource(HttpClient httpClient, string url, IUpdateCallback callback, ILoggerFactory loggerFactory)
        {
            this.httpClient = httpClient;
            this.url = url;
            this.callback = callback;
            this.logger = loggerFactory.CreateLogger<EventSource>();
            this.cancellationTokenSource = new CancellationTokenSource();
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
            cancellationTokenSource.Cancel();
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

        private async Task<HttpResponseMessage> GetStreamResponse()
        {
            var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationTokenSource.Token);
            response.EnsureSuccessStatusCode();
            return response;
        }

        private async Task StartStreaming()
        {
            var retryCount = 0;
            var token = cancellationTokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    Debug.Assert(httpClient != null);

                    logger.LogDebug("Starting EventSource service");
                    
                    var timeout = TimeSpan.FromMilliseconds(InitialConnectionTimeoutMs);
                    using (var response = await GetStreamResponse().RunWithTimeout(timeout, token))
                    using (var stream = await this.httpClient.GetStreamAsync(url))
                    {
                        retryCount = 0;
                        callback.OnStreamConnected();

                        string message;
                        while ((message = ReadLine(stream, ReadTimeoutMs)) != null && !token.IsCancellationRequested)
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
                                Domain = (string)data["domain"],
                                Event = (string)data["event"],
                                Identifier = (string)data["identifier"],
                                Version = long.Parse((string)data["version"])
                            };

                            callback.Update(msg, false);
                        }
                    }

                }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested)
                    {
                        logger.LogInformation("SDKCODE(stream:5004): SSE thread exited");
                        return;
                    }
                    retryCount++;

                    var delayTimeSpan = CalculateBackoffDelay(retryCount);

                    logger.LogError(e,
                        "EventSource service threw an error: {Reason}. Retrying in {Delay} seconds",
                        e.Message, delayTimeSpan.TotalSeconds);

                    await Task.Delay(delayTimeSpan);
                }
                finally
                {
                    if (!token.IsCancellationRequested)
                    {
                        callback.OnStreamDisconnected();
                    }
                }
            }
        }

        private TimeSpan CalculateBackoffDelay(int retryCount)
        {
            const int BaseDelayMs = 200; 
            const int MaxDelayMs = 5000;

            // Force MaxDelayMs at 12 as a higher retry count would result in a delay over 5 seconds.
            // Additionally, going over a retry count of 32 (with an int) will overflow the calculation of
            // Math.Pow(2, retryCount) and cause an exception due to a large negative value.
            var delay = retryCount > 12
                ? MaxDelayMs
                : Math.Min(BaseDelayMs * (int)Math.Pow(2, retryCount), MaxDelayMs);
            var jitter = Random.Next(0, BaseDelayMs);
            delay += jitter;

            var delayTimeSpan = TimeSpan.FromMilliseconds(delay);

            return delayTimeSpan;
        }
    }
}
