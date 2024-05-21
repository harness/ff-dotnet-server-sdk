using System;
using System.Collections.Generic;
using System.Threading;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.dto;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using NUnit.Framework;
using WireMock;
using WireMock.Logging;


namespace ff_server_sdk_test.api
{
    using static CannedResponses;

    [TestFixture]
    public class CfClientTest
    {
        private WireMockServer server;

        [SetUp]
        public void StartMockServer()
        {
            server = WireMockServer.Start(new WireMockServerSettings
            {
                Logger = new WireMockConsoleLogger()
            });
        }

        [TearDown]
        public void StopMockServer()
        {
            server.Stop();
        }

        [Test]
        public void IfFirstTargetSegmentsFailsAuthShouldNotFail() // FFM-11002
        {
            server
                .Given(Request.Create().WithPath("/api/1.0/client/auth").UsingPost())
                .RespondWith(MakeAuthResponse());

            server
                .Given(Request.Create()
                    .WithPath("/api/1.0/client/env/00000000-0000-0000-0000-000000000000/feature-configs").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody(MakeFeatureConfigBodyWithVariationToTargetMapSetToNull()));

            server
                .Given(Request.Create()
                    .WithPath("/api/1.0/client/env/00000000-0000-0000-0000-000000000000/target-segments").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithFault(FaultType.MALFORMED_RESPONSE_CHUNK)
                    .WithBody(MakeTargetSegmentsBody()));

            var target =
                Target.builder()
                    .Name("CfClientTest")
                    .Identifier("CfClientTest")
                    .build();

            Console.WriteLine("Running at " + server.Url);

            var client = new CfClient("dummy api key", Config.Builder()
                .debug(true)
                .SetStreamEnabled(false)
                .SetAnalyticsEnabled(false)
                .ConfigUrl(server.Url + "/api/1.0")
                .Build());

            CountdownEvent initLatch = new CountdownEvent(1);

            client.InitializationCompleted += (sender, e) =>
            {
                Console.WriteLine("Initialization Completed");
                initLatch.Signal();
            };

            var success = client.WaitForInitialization(10_000);
            Assert.IsTrue(success, "timeout while waiting for WaitForInitialization()");

            var ok = initLatch.Wait(TimeSpan.FromMinutes(2));
            Assert.That(ok, Is.True, "failed to init in time");

            var result = client.stringVariation("FeatureWithVariationToTargetMapSetAsNull", target, "failed");
            Assert.That(result, Is.EqualTo("on"), "did not get correct flag state");
        }


        [Test]
        public void PerformanceTestInClauseWithManyValues()
        {
            server
                .Given(Request.Create().WithPath("/api/1.0/client/auth").UsingPost())
                .RespondWith(MakeAuthResponse());

            server
                .Given(Request.Create()
                    .WithPath("/api/1.0/client/env/00000000-0000-0000-0000-000000000000/feature-configs").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody(MakeFeatureConfigBody(MakeVariationMap( "inRule", "on"))));

            server
                .Given(Request.Create()
                    .WithPath("/api/1.0/client/env/00000000-0000-0000-0000-000000000000/target-segments").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody(MakeTargetSegmentsBody("inRule", MakeRules(MakeInClause("hasValue9999", "find_this_attribute", 10_000)))));

            var target =
                Target.builder()
                    .Name("CfClientTest")
                    .Attributes(new Dictionary<string, string> { { "find_this_attribute", "value9999" } })
                    .Identifier("CfClientTest")
                    .build();

            Console.WriteLine("Running at " + server.Url);

            var client = new CfClient("dummy api key", Config.Builder()
                .debug(true)
                .SetStreamEnabled(false)
                .SetAnalyticsEnabled(false)
                .UseMapForInClause(true)
                .ConfigUrl(server.Url + "/api/1.0")
                .Build());

            CountdownEvent initLatch = new CountdownEvent(1);

            client.InitializationCompleted += (sender, e) =>
            {
                Console.WriteLine("Initialization Completed");
                initLatch.Signal();
            };

            var success = client.WaitForInitialization(10_000);
            Assert.IsTrue(success, "timeout while waiting for WaitForInitialization()");

            var ok = initLatch.Wait(TimeSpan.FromMinutes(2));
            Assert.That(ok, Is.True, "failed to init in time");

            var result = client.boolVariation("Feature", target, false);
            Assert.That(result, Is.EqualTo(true), "did not get correct flag state");
        }

        [Test]
        public void ShouldNotThrowErrorIfTargetToVariationMapNotPopulated()
        {
            server
                .Given(Request.Create().WithPath("/api/1.0/client/auth").UsingPost())
                .RespondWith(MakeAuthResponse());

            server
                .Given(Request.Create()
                    .WithPath("/api/1.0/client/env/00000000-0000-0000-0000-000000000000/feature-configs").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody(MakeFeatureConfigBodyWithVariationToTargetMapSetToNull()));

            server
                .Given(Request.Create()
                    .WithPath("/api/1.0/client/env/00000000-0000-0000-0000-000000000000/target-segments").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody(MakeTargetSegmentsBody()));

            var target =
                Target.builder()
                    .Name("CfClientTest")
                    .Attributes(new Dictionary<string, string> { { "attr", "val" } })
                    .Identifier("CfClientTest")
                    .build();

            Console.WriteLine("Running at " + server.Url);

            var client = new CfClient("dummy api key", Config.Builder()
                .debug(true)
                .SetStreamEnabled(false)
                .SetAnalyticsEnabled(false)
                .ConfigUrl(server.Url + "/api/1.0")
                .Build());

            CountdownEvent initLatch = new CountdownEvent(1);

            client.InitializationCompleted += (sender, e) =>
            {
                Console.WriteLine("Initialization Completed");
                initLatch.Signal();
            };

            var success = client.WaitForInitialization(10_000);
            Assert.IsTrue(success, "timeout while waiting for WaitForInitialization()");

            var ok = initLatch.Wait(TimeSpan.FromMinutes(2));
            Assert.That(ok, Is.True, "failed to init in time");

            var result = client.stringVariation("FeatureWithVariationToTargetMapSetAsNull", target, "failed");
            Assert.That(result, Is.EqualTo("on"), "did not get correct flag state");
        }

        [Ignore("Currently used for manual testing - might be extended into an actual test with assertions later")]
        [Test]
        public void ShouldTriggerPollingException()
        {
            server
                .Given(Request.Create().WithPath("/api/1.0/client/auth").UsingPost())
                .RespondWith(MakeAuthResponse());

            server
                .Given(Request.Create()
                    .WithPath("/api/1.0/client/env/00000000-0000-0000-0000-000000000000/feature-configs")
                    .UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(999) // Force exception handler in PollingProcessor -> OnTimedEventAsync to trigger
                    .WithBody(MakeEmptyBody())
                );

            server
                .Given(Request.Create()
                    .WithPath("/api/1.0/client/env/00000000-0000-0000-0000-000000000000/target-segments").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody(MakeEmptyBody()));

            var target =
                Target.builder()
                    .Name("CfClientTest")
                    .Attributes(new Dictionary<string, string> { { "attr", "val" } })
                    .Identifier("CfClientTest")
                    .build();

            Console.WriteLine("Running at " + server.Url);

            var client = new CfClient("dummy api key", Config.Builder()
                .debug(true)
                .SetStreamEnabled(false)
                .SetAnalyticsEnabled(false)
                .ConfigUrl(server.Url + "/api/1.0")
                .Build());

            CountdownEvent initLatch = new CountdownEvent(1);

            client.InitializationCompleted += (sender, e) =>
            {
                Console.WriteLine("Initialization Completed");
                initLatch.Signal();
            };

            var success = client.WaitForInitialization(10_000);
            Assert.IsTrue(success, "timeout while waiting for WaitForInitialization()");

            var ok = initLatch.Wait(TimeSpan.FromSeconds(30));
            Assert.That(ok, Is.True, "failed to init in time");

            // todo assert that the 2nd poll came through
        }
    }
}
