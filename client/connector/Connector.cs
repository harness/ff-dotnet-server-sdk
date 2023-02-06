using System.Collections.Generic;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.HarnessOpenAPIService;
using io.harness.cfsdk.HarnessOpenMetricsAPIService;

namespace io.harness.cfsdk.client.connector
{
    public interface IConnector
    {
        Task<string> Authenticate();
        Task<IEnumerable<FeatureConfig>> GetFlags();
        Task<FeatureConfig> GetFlag(string identifier);

        Task<IEnumerable<Segment>> GetSegments();
        Task<Segment> GetSegment(string identifier);

        IService Stream(IUpdateCallback updater);

        Task PostMetrics(Metrics metrics);

        void Close();
    }
}
