using System;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.dto;
using NUnit.Framework;
using Serilog;
using Serilog.Extensions.Logging;

namespace ff_server_sdk_test
{
    [Ignore("This test is designed for running manually with an FF_API_KEY env variable")]
    public class InitTest
    {
        private string apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
        private Config config;
        private Target target;

        [OneTimeSetUp]
        public void Init()
        {
            apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
            if (apiKey == null) throw new ArgumentNullException("FF_API_KEY","FF_API_KEY env variable is not set");

            var loggerFactory = new SerilogLoggerFactory(
                new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.Console()
                    .CreateLogger());

            config = Config.Builder()
                .LoggerFactory(loggerFactory).Build();

            target = Target.builder()
                .Name("DotNET SDK init test")
                .Identifier("dotnetsdkinittest")
                .build();
        }

        private void Do10FlagChecks(ICfClient client, string testName)
        {
            for (int i = 0; i < 10; i++)
            {
                var result = client.boolVariation("test", target, false);
                Console.WriteLine(testName + " " + i + " got " + result);
            }
        }

        [Test]
        public void CfClientInstance_With_WaitForInitialization()
        {
            CfClient.Instance.Initialize(apiKey, config);
            CfClient.Instance.WaitForInitialization();
            Do10FlagChecks(CfClient.Instance, System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "unknown");
            CfClient.Instance.Close();                 // trying to close the singleton is a non-op, versions prior to 1.4.0 would throw exceptions
        }

        [Test]
        public void CfClientCtor_With_WaitForInitialization()
        {
            var client = new CfClient(apiKey, config);
            client.WaitForInitialization();
            Do10FlagChecks(client, System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "unknown");
            client.Close();
            Console.WriteLine("initWith_ConstructorWaitForInit done");
        }

        [Test]
        public void CfClientDefaultCtor_With_WaitForInitialization()
        {
            // NOTE this constructor pattern will fail since no api key was or can be given, the constructor will be deprecated
            var client = new CfClient();
            var result = client.WaitForInitialization(1000);
            client.Close();
            Assert.IsFalse(result);
        }

        [Test]
        public void CfClientCtor_With_InitAndWait_HappyPath_LegacyWillBeRemoved()
        {
            var client = new CfClient(apiKey, config);
            var result = client.InitializeAndWait().Wait(-1);
            Assert.IsTrue(result);
            Do10FlagChecks(client, System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "unknown");
            client.Close();
        }

        [Test]
        public void CfClientCtor_With_InitAndWait_ShouldThrowIfWrongInstanceUsed_LegacyWillBeRemoved()
        {
            var client = new CfClient(apiKey, config);

            // wrong instance is used so SDK will throw assertion error
            Assert.Throws(Is.TypeOf<AggregateException>(), 
                () => CfClient.Instance.InitializeAndWait().Wait(5000));

            client.Close();
        }

        [Test]
        public void CfClientInstance_With_InitAndWait_LegacyUsage2_WillBeRemoved()
        {
            // Check case where CfClient.Instance.Initialize + CfClient.Instance.InitializeAndWait().Wait are used together
            CfClient.Instance.Initialize(apiKey, config);
            var result = CfClient.Instance.InitializeAndWait().Wait(5000);
            Do10FlagChecks(CfClient.Instance, System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "unknown");
            Assert.IsTrue(result);
        }
    }
}