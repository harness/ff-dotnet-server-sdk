using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.HarnessOpenAPIService;
using io.harness.cfsdk.HarnessOpenMetricsAPIService;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace io.harness.cfsdk.client.connector
{
    public class LocalConnector : IConnector
    {
        private readonly string flagPath;
        private readonly string segmentPath;
        private readonly string metricPath;
        private readonly string source;
        private readonly ILogger logger;

        public LocalConnector(string source, ILogger<LocalConnector> logger = null)
        {
            this.source = source;
            this.logger = logger ?? Config.DefaultLogger;

            this.flagPath = Path.Combine(source, "flags");
            this.segmentPath = Path.Combine(source, "segments");
            this.metricPath = Path.Combine(source, "metrics");

            Directory.CreateDirectory(this.flagPath);
            Directory.CreateDirectory(this.segmentPath);
            Directory.CreateDirectory(this.metricPath);
        }

        public Task<string> Authenticate()
        {
            return Task.FromResult("success");
        }

        public void Close()
        {
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }

        public Task<FeatureConfig> GetFlag(string identifier)
        {
            string filePath = Path.Combine(flagPath, identifier + ".json");
            return Task.FromResult(JsonConvert.DeserializeObject<FeatureConfig>(File.ReadAllText(filePath)));
        }

        public Task<IEnumerable<FeatureConfig>> GetFlags()
        {
            var features = new List<FeatureConfig>();
            try
            {
                foreach (string fileName in Directory.GetFiles(flagPath, "*.json"))
                {
                    var feature = JsonConvert.DeserializeObject<FeatureConfig>(File.ReadAllText(fileName));
                    if (feature != null)
                    {
                        features.Add(feature);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error accessing feature files.");
            }
            return Task.FromResult((IEnumerable<FeatureConfig>)features);
        }

        public Task<Segment> GetSegment(string identifier)
        {
            string filePath = Path.Combine(segmentPath, identifier + ".json");
            return Task.FromResult(JsonConvert.DeserializeObject<Segment>(File.ReadAllText(filePath)));
        }

        public Task<IEnumerable<Segment>> GetSegments()
        {
            var segments = new List<Segment>();
            try
            {
                segments.AddRange(from string fileName in Directory.GetFiles(segmentPath, "*.json")
                                  let segment = JsonConvert.DeserializeObject<Segment>(File.ReadAllText(fileName))
                                  where segment != null
                                  select segment);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error accessing segment files.");
            }
            return Task.FromResult((IEnumerable<Segment>)segments);
        }

        public async Task PostMetrics(Metrics metrics)
        {
            var fileName = Path.Combine(metricPath, $"{DateTime.Now.ToLongDateString()}.jsonl");
            using (var w = File.AppendText(fileName))
            {
                var str = JsonConvert.SerializeObject(metrics);
                await w.WriteLineAsync(str);
            }
        }

        public IService Stream(IUpdateCallback updater)
        {
            return new FileWatcherService(this.flagPath, this.segmentPath, updater, logger);
        }


        private sealed class FileWatcherService : IService
        {
            private readonly FileWatcher flagWatcher;
            private readonly FileWatcher segmentWatcher;
            private readonly IUpdateCallback callback;

            public FileWatcherService(string flagPath, string segmentPath, IUpdateCallback callback, ILogger logger = null)
            {
                this.flagWatcher = new FileWatcher("flag", flagPath, callback, logger);
                this.segmentWatcher = new FileWatcher("target-segment", segmentPath, callback, logger);
                this.callback = callback;
            }
            public void Close()
            {
                Stop();
                this.flagWatcher.Stop();
                this.segmentWatcher.Stop();
            }

            public void Start()
            {
                this.flagWatcher.Start();
                this.segmentWatcher.Start();

                this.callback?.OnStreamConnected();
            }

            public void Stop()
            {
                this.flagWatcher.Stop();
                this.segmentWatcher.Stop();

                this.callback?.OnStreamDisconnected();
            }
        }
    }

}
