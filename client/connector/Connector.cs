using System;
using System.Collections.Generic;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.HarnessOpenAPIService;
using io.harness.cfsdk.HarnessOpenMetricsAPIService;

namespace io.harness.cfsdk.client.connector
{
    public interface IConnector : IDisposable
    {
        string Authenticate();
        IEnumerable<FeatureConfig> GetFlags();
        FeatureConfig GetFlag(string identifier);

        IEnumerable<Segment> GetSegments();
        Segment GetSegment(string identifier);

        IService Stream(IUpdateCallback updater);

        void PostMetrics(Metrics metrics);

        void Close();
    }
}
