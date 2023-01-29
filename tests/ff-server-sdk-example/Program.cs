using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.connector;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace io.harness.example
{
    class Program
    {
        static readonly string API_KEY = "70d4d39e-e50f-4cee-99bf-6fd569138c74";
        static ICfClient client;

        static void Subscribe()
        {
            client.InitializationCompleted += (sender, e) =>
            {
                Console.WriteLine("Notification Initialization Completed");
            };
            client.EvaluationChanged += (sender, identifier) =>
            {
                Console.WriteLine($"Flag changed for {identifier}");
            };
        }
        static async Task LocalExample(ConfigBuilder configBuilder)
        {
            FileMapStore fileMapStore = new FileMapStore("Non-Freemium");
            var connector = new LocalConnector("local");
            client = new CfClient(connector, configBuilder.SetStore(fileMapStore).Build());
            Subscribe();
            await client.InitializeAndWait();

        }
        static async Task SingletonExample(ConfigBuilder configBuilder)
        {
            client = CfClient.Instance;
            FileMapStore fileMapStore = new FileMapStore("Non-Freemium");
            await client.Initialize(API_KEY, configBuilder.SetStore(fileMapStore).Build());
        }
        static async Task SimpleExample(ConfigBuilder configBuilder)
        {
            FileMapStore fileMapStore = new FileMapStore("Non-Freemium");
            client = new CfClient(API_KEY, configBuilder.SetStore(fileMapStore).Build());
            client.InitializationCompleted += (sender, e) =>
            {
                Console.WriteLine("Notification Initialization Completed");
            };

            await client.InitializeAndWait();
        }
        static async Task MultipleClientExample(ConfigBuilder configBuilder)
        {
            Random r = new Random();

            var dict = new Dictionary<string, string>
            {
                { "Freemium", "45d2a13a-c62f-4116-a1a7-86f25d715a2e"},
                { "Freemium-2" , "44255167-bc3e-4831-a79a-640234fdced8"},
                { "Non-Freemium" , "9ecc4ced-afc1-45af-9b54-c899cbff4b62" },
                { "Non-Freemium-2" , "32ba37eb-2c12-4143-9d05-fb4d6782b083" }
            };

            foreach (KeyValuePair<string, string> d in dict)
            {
                FileMapStore fileMapStore = new FileMapStore(d.Key);
                client = new CfClient(d.Value, configBuilder.SetStore(fileMapStore).Build());

                var rand = r.Next();

                io.harness.cfsdk.client.dto.Target target =
                    io.harness.cfsdk.client.dto.Target.builder()
                    .Name($"Target_{rand}")
                    .IsPrivate(false)
                    .Attributes(new Dictionary<string, string>
                    {
                        { $"Test_key_{r.Next()}", r.Next().ToString() },
                        { $"Test_key_{r.Next()}", r.Next().ToString() },
                        { $"Test_key_{r.Next()}", r.Next().ToString() }
                    })
                    .Identifier($"Target_{rand}")
                    .build();

                await client.InitializeAndWait();

                bool bResult = CfClient.Instance.boolVariation("flag1", target, false);
                Console.WriteLine($"Client: {d.Key} Bool Variation value ----> {bResult}");

                double nResult = CfClient.Instance.numberVariation("flag2", target, -1);
                Console.WriteLine($"Client: {d.Key} Number Variation value ----> {nResult}");

                string sResult = CfClient.Instance.stringVariation("flag3", target, "NO VALUE!!!!");
                Console.WriteLine($"Client: {d.Key} String Variation value ----> {sResult}");

                JObject jResult = CfClient.Instance.jsonVariation("flag4", target, new JObject());
                Console.WriteLine($"Client: {d.Key} Json Variation value ----> {jResult}");

            }
        }
        static async Task RemoteExample(ConfigBuilder configBuilder)
        {
            FileMapStore fileMapStore = new FileMapStore("file_cache");

            client = CfClient.Instance;
            Config config = configBuilder
                .SetAnalyticsEnabled()
                .SetStreamEnabled(false)
                .SetStore(fileMapStore)
                .ConfigUrl("https://config.feature-flags.uat.harness.io/api/1.0")
                .EventUrl("https://event.feature-flags.uat.harness.io/api/1.0")
                .Build();
            Subscribe();
            await client.Initialize(API_KEY, config);

        }
        static async Task Main(string[] args)
        {
            // dotnet add package Serilog.Sinks.Console
            // dotnet add package Serilog.Sinks.File
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .MinimumLevel.Debug()
                .WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // dotnet add package Serilog.Extensions.Logging
            using var lf = new Serilog.Extensions.Logging.SerilogLoggerFactory(dispose: true);
            ConfigBuilder newCfgBldr() => Config.Builder().SetLoggerFactory(lf);

            string line = Console.ReadLine();
            switch (line)
            {
                case "singleton": await SingletonExample(newCfgBldr()); break;
                case "remote": await RemoteExample(newCfgBldr()); break;
                case "simple": await SimpleExample(newCfgBldr()); break;
                case "local": await LocalExample(newCfgBldr()); break;
                case "multiple": await MultipleClientExample(newCfgBldr()); return;
            }
            if (client == null)
                return;

            io.harness.cfsdk.client.dto.Target target =
                io.harness.cfsdk.client.dto.Target.builder()
                .Name("target1")
                .IsPrivate(false)
                .Attributes(new Dictionary<string, string> { { "testKey", "TestValue" } })
                .Identifier("target1")
                .build();

            while (true)
            {
                bool bResult = client.boolVariation("flag1", target, false);
                Console.WriteLine($"Bool Variation value ----> {bResult}");

                JObject jResult = client.jsonVariation("flag4", target, new JObject());
                Console.WriteLine($"Bool Variation value ----> {jResult}");

                Thread.Sleep(20000);
            }
        }
    }
}
