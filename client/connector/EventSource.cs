using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using Newtonsoft.Json.Linq;
using Serilog;

namespace io.harness.cfsdk.client.connector
{
    public class EventSource : IService
    {
        private readonly string url;
        private readonly Config config;
        private readonly HttpClient httpClient;
        private readonly IUpdateCallback callback;
        private const int ReadTimeoutMs = 60_000;

        public EventSource(HttpClient httpClient, string url, Config config, IUpdateCallback callback)
        {
            this.httpClient = httpClient;
            this.url = url;
            this.config = config;
            this.callback = callback;
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
            Log.Debug("Stopping EventSource service.");
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

                Log.Debug("Starting EventSource service.");
                using (Stream stream = await this.httpClient.GetStreamAsync(url))
                {
                    callback.OnStreamConnected();

                    string message;
                    while ((message = ReadLine(stream, ReadTimeoutMs)) != null)
                    {
                        if (!message.Contains("domain"))
                        {
                            Log.Verbose("Received event source heartbeat");
                            continue;
                        }

                        Log.Information($"SDKCODE(stream:5002): SSE event received {message}");

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
                Log.Error($"EventSource service threw an error: {e.Message} Retrying in {config.pollIntervalInSeconds}", e);
                Debug.WriteLine(e.ToString());
            }
            finally
            {
                callback.OnStreamDisconnected();
            }

        }
    }
}
