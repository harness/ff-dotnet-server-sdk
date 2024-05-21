using System.Collections.Generic;
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


            var config = Config.Builder().UseMapForInClause(false).Build();
            IRepository repo = new StorageRepository(cache, store, callback, factory, config);

            // Flags that don't exist

            var result = repo.GetFlag("i_do_not_exist");
            Assert.IsNull(result);
            var segmentResult = repo.GetSegment("i_do_not_exist2");
            Assert.IsNull(segmentResult);

            // Set/GetFlags

            var flag = new FeatureConfig()
            {
                Feature = "ident"
            };
            repo.SetFlag("flag1", flag);
            var getFlagResult = repo.GetFlag("flag1");
            Assert.IsNotNull(getFlagResult);

            var flag2 = new FeatureConfig()
            {
                Feature = "ident2"
            };
            repo.SetFlags(new List<FeatureConfig>() {flag2, flag2});

            // Set/GetSegment

            var segment = new Segment()
            {
                Identifier = "segmentIdent"
            };
            repo.SetSegment("segment1", segment);
            repo.SetSegment("segment1", segment);
            var getSegmentResult = repo.GetSegment("segment1");
            Assert.IsNotNull(getSegmentResult);

            var segment2 = new Segment()
            {
                Identifier = "ident2"
            };
            repo.SetSegments(new List<Segment>() {segment2, segment2});

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