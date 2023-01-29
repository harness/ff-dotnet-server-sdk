using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.HarnessOpenAPIService;
using Newtonsoft.Json;
using NUnit.Framework;

namespace ff_server_sdk_test
{
    public class VersionTest
    {
        static ICfClient client;
        static FileMapStore fileMapStore;
        static Segment remoteSegment;
        [SetUp]
        public async Task SetUp()
        {

            fileMapStore = new FileMapStore("local_storage");

            client = new CfClient(
                new LocalConnector("local_connector"),
                Config.Builder().SetStore(fileMapStore).Build());

            remoteSegment = new Segment
            {
                Identifier = "test_segment_identifier",
                Name = "test_segment_name",
                Rules = Array.Empty<Clause>()
            };

            SaveSegment(remoteSegment);

            await client.InitializeAndWait();
        }

        private void SaveSegment(Segment seg)
        {
            File.WriteAllText(Path.Combine("local_connector", "segments", "test_segment_identifier.json"), JsonConvert.SerializeObject(seg));
        }

        private Segment GetSegment()
        {
            return fileMapStore.Get("segments_test_segment_identifier", typeof(Segment)) as Segment;
        }

        [Test, Category("Version Testing")]
        public void TestVersionProperty()
        {

            // 1. Version from cache/storage should be the same as on "server"

            Thread.Sleep(500);

            Segment storedSegment = GetSegment();

            Assert.IsTrue(remoteSegment.Identifier == storedSegment.Identifier);
            Assert.IsTrue(remoteSegment.Name == storedSegment.Name);

            // 2. Storage and cache should be updated when serving content with higher Version

            remoteSegment.Version = 2;
            remoteSegment.Name = "updated_name";

            SaveSegment(remoteSegment);

            Thread.Sleep(500);

            storedSegment = GetSegment();

            Assert.IsTrue(remoteSegment.Identifier == storedSegment.Identifier);
            Assert.IsTrue(remoteSegment.Name == storedSegment.Name);
            Assert.IsTrue(remoteSegment.Version == storedSegment.Version);

            // 3. Changes are ignored when we have lower version returned from server

            remoteSegment.Version = 1;
            remoteSegment.Name = "ignore_lower_version";

            SaveSegment(remoteSegment);

            Thread.Sleep(500);

            storedSegment = GetSegment();

            Assert.IsTrue(remoteSegment.Identifier == storedSegment.Identifier);
            Assert.IsTrue(remoteSegment.Name != storedSegment.Name);
            Assert.IsTrue(remoteSegment.Version != storedSegment.Version);

            // 4. Changes are applied when server responce has 0 for Version or if Version doesn't exist

            remoteSegment.Version = 0;
            remoteSegment.Name = "accept_change_because_0_version";

            SaveSegment(remoteSegment);

            Thread.Sleep(500);

            storedSegment = GetSegment();

            Assert.IsTrue(remoteSegment.Identifier == storedSegment.Identifier);
            Assert.IsTrue(remoteSegment.Name == storedSegment.Name);
            Assert.IsTrue(remoteSegment.Version == storedSegment.Version);

        }
    }
}
