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
        private string source;
        public LocalConnector(string source)
        {
            this.source = source;
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
            string filePath = Path.Combine(Path.Combine(this.source, "flags"), identifier + ".json");
            return JsonConvert.DeserializeObject<FeatureConfig>(File.ReadAllText(filePath));
        }

        public IEnumerable<FeatureConfig> GetFlags()
        {
            var features = new List<FeatureConfig>();
            try
            {
                foreach (string fileName in Directory.GetFiles(Path.Combine(this.source, "flags"), "*.json"))
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
            string filePath =Path.Combine(this.source, "flags", identifier + ".json");
            return JsonConvert.DeserializeObject<Segment>(File.ReadAllText(filePath));
        }

        public IEnumerable<Segment> GetSegments()
        {
            var segments = new List<Segment>();
            try
            {
                segments.AddRange(from string fileName in Directory.GetFiles(Path.Combine(this.source, "segments"), "*.json")
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
            string fileName = Path.Combine(this.source, "metrics", $"{DateTime.Now.ToLongDateString()}.jsonl" );
            using (StreamWriter w = File.AppendText(fileName))
            {
                var str = JsonConvert.SerializeObject(metrics);
                w.WriteLine(str);
            }
        }

        public IService Stream(IUpdateCallback updater)
        {
            return new FileWatcherService(this.source, updater);
        }


        private class FileWatcherService : IService, IDisposable
        {
            private FileWatcher flagWatcher;
            private FileWatcher segmentWatcher;
            private IUpdateCallback callback;
            public FileWatcherService(string source, IUpdateCallback callback)
            {
                this.flagWatcher = new FileWatcher("flag", Path.Combine(source, "flags"), callback);
                this.segmentWatcher = new FileWatcher("target-segment", Path.Combine(source, "segments"), callback);
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
