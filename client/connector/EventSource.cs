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
        private string url;
        private Config config;
        private HttpClient httpClient;
        private StreamReader streamReader;
        private IUpdateCallback callback;
        private readonly ILogger logger;

        public EventSource(HttpClient httpClient, string url, Config config, IUpdateCallback callback, ILogger logger = null)
        {
            this.httpClient = httpClient;
            this.url = url;
            this.config = config;
            this.callback = callback;
            this.logger = logger ?? Log.Logger;
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

            logger.Information("Stopping EventSource service.");
        }

        private async Task StartStreaming()
        {
            try
            {
                logger.Information("Starting EventSource service.");
                using (this.streamReader = new StreamReader(await this.httpClient.GetStreamAsync(url)))
                {
                    this.callback.OnStreamConnected();

                    while (!streamReader.EndOfStream)
                    {
                        string message = await streamReader.ReadLineAsync();
                        if (!message.Contains("domain")) continue;

                        logger.Information("EventSource message received {Message}", message);

                        // parse message
                        JObject jsommessage = JObject.Parse("{" + message + "}");

                        Message msg = new Message();
                        msg.Domain = (string)jsommessage["data"]["domain"];
                        msg.Event = (string)jsommessage["data"]["event"];
                        msg.Identifier = (string)jsommessage["data"]["identifier"];
                        msg.Version = long.Parse((string)jsommessage["data"]["version"]);


                        this.callback.Update(msg, false);
                    }
                }
            }
            catch (Exception)
            {
                logger.Error("EventSource service throw error. Retrying in {PollIntervalInSeconds}", this.config.pollIntervalInSeconds);
            }
            finally
            {
                this.callback.OnStreamDisconnected();
            }

        }
    }
}
