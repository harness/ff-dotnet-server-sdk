using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private StreamReader streamReader;

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
            if (streamReader != null)
            { 
                streamReader.Close();
                streamReader.Dispose();
                streamReader = null;
            }

            Log.Information("Stopping EventSource service.");
        }

        private async Task StartStreaming()
        {
            try
            {
                Log.Information("Starting EventSource service.");
                using (streamReader = new StreamReader(await this.httpClient.GetStreamAsync(url)))
                {
                    callback.OnStreamConnected();

                    while (!streamReader.EndOfStream)
                    {
                        var message = await streamReader.ReadLineAsync();
                        if (!message.Contains("domain")) continue;

                        Log.Information($"EventSource message received {message}");

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
            catch (Exception)
            {
                Log.Error($"EventSource service throw error. Retrying in {config.pollIntervalInSeconds}");
            }
            finally
            {
                callback.OnStreamDisconnected();
            }

        }
    }
}
