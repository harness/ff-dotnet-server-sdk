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
        private const int InitialConnectionTimeoutMs = 10000;
        private const int ReadTimeoutMs = 35_000;
        private const int BaseDelayMs = 200; 
        private const int MaxDelayMs = 5000; 
        private static readonly Random Random = new();
        private CancellationTokenSource cancellationTokenSource;

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

        private async Task StartStreaming()
        {
            var retryCount = 0;
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    Debug.Assert(httpClient != null);

                    logger.LogDebug("Starting EventSource service");
                    
                    // In .NET 4.8, we can't use a traditional HTTP Client timeout, or cacncellation token, 
                    // as the HTTP client interprets reading from the stream as the connection is still open. 
                    // We use this workaround instead to simulate a timeout for the initial request.
                    var initialTask = Task.Run(async () =>
                    {
                        await Task.Delay(InitialConnectionTimeoutMs);
                        throw new TimeoutException("Initial connection timeout");
                    });

                    var requestTask = Task.Run(async () =>
                    {
                        var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();
                        return response;
                    });

                    var completedTask = await Task.WhenAny(initialTask, requestTask);

                    if (completedTask == initialTask)
                    {
                        await initialTask; 
                    }
                    
                    using (Stream stream = await this.httpClient.GetStreamAsync(url))
                    {
                        retryCount = 0;
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
                    retryCount++;

                    int delay = Math.Min(BaseDelayMs * (int)Math.Pow(2, retryCount), MaxDelayMs);
                    var jitter = Random.Next(0, BaseDelayMs);
                    delay += jitter;


                    logger.LogError(e,
                        "EventSource service threw an error: {Reason}. Retrying in {Delay} seconds",
                        e.Message, delay / 1000.0);
                    await Task.Delay(delay);
                }
                finally
                {
                    callback.OnStreamDisconnected();
                }
            }
        }
    }
}
