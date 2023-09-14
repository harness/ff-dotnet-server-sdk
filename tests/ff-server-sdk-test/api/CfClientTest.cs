using System;
using System.Collections.Generic;
using System.Threading;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.dto;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Serilog;
using WireMock.Logging;
using WireMock.Settings;
using NUnit.Framework;
namespace ff_server_sdk_test.api;
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
            Logger = new WireMockConsoleLogger(),
            ThrowExceptionWhenMatcherFails = true
        });

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .CreateLogger();
    }
    
    [TearDown]
    public void StopMockServer()
    {
        server.Stop();
    }

    [Test]
    public void ShouldNotThrowErrorIfTargetToVariationMapNotPopulated()
    {
        server
            .Given(Request.Create().WithPath("/api/1.0/client/auth").UsingPost())
            .RespondWith(MakeAuthResponse());
 
        server
            .Given(Request.Create().WithPath("/api/1.0/client/env/00000000-0000-0000-0000-000000000000/feature-configs").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(MakeFeatureConfigBodyWithVariationToTargetMapSetToNull()));

        server
            .Given(Request.Create().WithPath("/api/1.0/client/env/00000000-0000-0000-0000-000000000000/target-segments").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(MakeTargetSegmentsBody()));
        
        var target =
            Target.builder()
                .Name("CfClientTest")
                .Attributes(new Dictionary<string, string> { { "attr", "val" } })
                .Identifier("CfClientTest")
                .build();
        
        Log.Information("Running at " + server.Url);
        
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
        
        client.InitializeAndWait();
        
        var ok = initLatch.Wait(TimeSpan.FromMinutes(2));
        Assert.That(ok, Is.True, "failed to init in time");

        var result = client.stringVariation("FeatureWithVariationToTargetMapSetAsNull", target, "failed");
        Assert.That(result, Is.EqualTo("on"), "did not get correct flag state");
    }
    
    
}
