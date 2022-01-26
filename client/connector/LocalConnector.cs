using System;
using System.Collections.Generic;
using System.IO;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.HarnessOpenAPIService;
using io.harness.cfsdk.HarnessOpenMetricsAPIService;
using Newtonsoft.Json;
using Serilog;
using System.Linq;

namespace io.harness.cfsdk.client.connector
{
    public class LocalConnector : IConnector
    {
        private string flagPath;
        private string segmentPath;
        private string metricPath;
        private string source;
        public LocalConnector(string source)
        {
            this.source = source;

            this.flagPath = Path.Combine(source, "flags");
            this.segmentPath = Path.Combine(source, "segments");
            this.metricPath = Path.Combine(source, "metrics");

            Directory.CreateDirectory(this.flagPath);
            Directory.CreateDirectory(this.segmentPath);
            Directory.CreateDirectory(this.metricPath);
        }

        public string Authenticate()
        {
            // there is no authentication so just return any string
            return "success";
        }
        public void Close()
        {

        }

        public FeatureConfig GetFlag(string identifier)
        {
            string filePath = Path.Combine(flagPath, identifier + ".json");
            return JsonConvert.DeserializeObject<FeatureConfig>(File.ReadAllText(filePath));
        }

        public IEnumerable<FeatureConfig> GetFlags()
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
                Log.Error("Error accessing feature files.", ex);
            }
            return features;
        }

        public Segment GetSegment(string identifier)
        {
            string filePath =Path.Combine(segmentPath, identifier + ".json");
            return JsonConvert.DeserializeObject<Segment>(File.ReadAllText(filePath));
        }

        public IEnumerable<Segment> GetSegments()
        {
            var segments = new List<Segment>();
            try
            {
                segments.AddRange(from string fileName in Directory.GetFiles(segmentPath, "*.json")
                                  let segment = JsonConvert.DeserializeObject<Segment>(File.ReadAllText(fileName))
                                  where segment != null
                                  select segment);
            }
            catch(Exception ex)
            {
                Log.Error("Error accessing segment files.", ex);
            }
            return segments;
        }

        public void PostMetrics(Metrics metrics)
        {
            string fileName = Path.Combine(metricPath, $"{DateTime.Now.ToLongDateString()}.jsonl" );
            using (StreamWriter w = File.AppendText(fileName))
            {
                var str = JsonConvert.SerializeObject(metrics);
                w.WriteLine(str);
            }
        }

        public IService Stream(IUpdateCallback updater)
        {
            return new FileWatcherService(this.flagPath, this.segmentPath, updater);
        }


        private class FileWatcherService : IService, IDisposable
        {
            private FileWatcher flagWatcher;
            private FileWatcher segmentWatcher;
            private IUpdateCallback callback;
            public FileWatcherService(string flagPath, string segmentPath, IUpdateCallback callback)
            {
                this.flagWatcher = new FileWatcher("flag", flagPath, callback);
                this.segmentWatcher = new FileWatcher("target-segment", segmentPath, callback);
                this.callback = callback;
            }
            public void Close()
            {
                Stop();
            }

            public void Dispose()
            {
                this.flagWatcher.Dispose();
                this.segmentWatcher.Dispose();
            }

            public void Start()
            {
                this.flagWatcher.Start();
                this.segmentWatcher.Start();

                this.callback.OnStreamConnected();
            }

            public void Stop()
            {
                this.flagWatcher.Stop();
                this.segmentWatcher.Stop();

                this.callback.OnStreamDisconnected();
            }
        }
    }

}
