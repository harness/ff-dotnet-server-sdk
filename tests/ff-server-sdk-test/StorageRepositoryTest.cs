using System.Linq;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace ff_server_sdk_test
{
    public class StorageRepositoryTest
    {
        [Test]
        public void TestRepository()
        {
            var cache = new FeatureSegmentCache();
            IStore store = null;
            IRepositoryCallback callback = null;

            var factory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddConsole();
            });

            IRepository repo = new StorageRepository(cache, store, callback, factory);

            // Flags that don't exist

            var result = repo.GetFlag("i_do_not_exist");
            Assert.IsNull(result);
            var segmentResult = repo.GetSegment("i_do_not_exist2");
            Assert.IsNull(segmentResult);

            // Get Flags

            var flag = new FeatureConfig()
            {
                Feature = "ident"
            };
            repo.SetFlag("flag1", flag);
            var getFlagResult = repo.GetFlag("flag1");
            Assert.IsNotNull(getFlagResult);

            // GetSegment

            var segment = new Segment()
            {
                Identifier = "segmentIdent"
            };
            repo.SetSegment("segment1", segment);
            repo.SetSegment("segment1", segment);
            var getSegmentResult = repo.GetSegment("segment1");
            Assert.IsNotNull(getSegmentResult);

            // iteration

            var foundSegments = repo.FindFlagsBySegment("segment1");
            Assert.IsNotNull(foundSegments);
            //Assert.IsTrue(foundSegments.Count() == 1);

            // Deletes

            repo.DeleteFlag("flag1");
            getFlagResult = repo.GetFlag("flag1");
            Assert.IsNull(getFlagResult);

            repo.DeleteSegment("segment1");
            getSegmentResult = repo.GetSegment("segment1");
            Assert.IsNull(getSegmentResult);



            repo.Close();
        }
    }
}