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
    class Observer : IObserver<Event>
    {
        public void OnCompleted(){}

        public void OnError(Exception error){}

        public void OnNext(Event value)
        {
            switch (value.type)
            {
                case NotificationType.READY:
                    Console.WriteLine("Notification Ready");
                    break;
                case NotificationType.CHANGED:
                    Console.WriteLine($"Flag changed for {value.identifier}");
                    break;
                case NotificationType.FAILED:
                    Console.WriteLine("Notification Error");
                    break;
            }
           
        }
    }
    class Program 
    {
        static readonly string API_KEY = "70d4d39e-e50f-4cee-99bf-6fd569138c74";
        static readonly Observer observer = new();
        static ICfClient client;
        static void LocalExample()
        {
            FileMapStore fileMapStore = new FileMapStore("Non-Freemium");
            var connector = new LocalConnector("local");
            client = new CfClient(connector, Config.Builder().SetStore(fileMapStore).Build());
            client.Subscribe(NotificationType.READY, observer);
            client.Subscribe(NotificationType.CHANGED, observer);

        }
        static void SingletonExample()
        {
            client = CfClient.Instance;
            FileMapStore fileMapStore = new FileMapStore("Non-Freemium");
            client.Initialize(API_KEY, Config.Builder().SetStore(fileMapStore).Build());
        }
        static void SimpleExample()
        {
            FileMapStore fileMapStore = new FileMapStore("Non-Freemium");
            client = new CfClient(API_KEY, Config.Builder().SetStore(fileMapStore).Build());
            client.Subscribe(NotificationType.READY, observer);
        }
        static async void MultipleClientExample()
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
                client = new CfClient(d.Value, Config.Builder().SetStore(fileMapStore).Build());

                var rand =  r.Next();

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


                await client.StartAsync();

                bool bResult = CfClient.Instance.BoolVariation("flag1", target, false);
                Console.WriteLine($"Client: {d.Key} Bool Variation value ----> {bResult}");

                double nResult = CfClient.Instance.NumberVariation("flag2", target, -1);
                Console.WriteLine($"Client: {d.Key} Number Variation value ----> {nResult}");

                string sResult = CfClient.Instance.StringVariation("flag3", target, "NO VALUE!!!!");
                Console.WriteLine($"Client: {d.Key} String Variation value ----> {sResult}");

                JObject jResult = CfClient.Instance.JsonVariation("flag4", target, new JObject());
                Console.WriteLine($"Client: {d.Key} Json Variation value ----> {jResult}");

            }
        }
        static void RemoteExample()
        {
            FileMapStore fileMapStore = new FileMapStore("file_cache");

            client = CfClient.Instance;
            Config config = Config.Builder()
                .SetAnalyticsEnabled()
                .SetStreamEnabled(true)
                .SetStore(fileMapStore)
                .ConfigUrl("https://config.feature-flags.uat.harness.io/api/1.0")
                .EventUrl("https://event.feature-flags.uat.harness.io/api/1.0")
                .Build();
            client.Initialize(API_KEY, config);
            client.Subscribe(observer);

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

            string line = Console.ReadLine();
            switch (line)
            {
                case "singleton":   SingletonExample();break;
                case "remote":      RemoteExample();break;
                case "simple":      SimpleExample();break;
                case "local":       LocalExample(); break;
                case "multiple":    MultipleClientExample(); return;
            }
            if (client == null)
                return;
            

            await client.StartAsync();

            io.harness.cfsdk.client.dto.Target target =
                io.harness.cfsdk.client.dto.Target.builder()
                .Name("target1")
                .IsPrivate(false)
                .Attributes(new Dictionary<string, string> { { "testKey", "TestValue" } })
                .Identifier("target1") 
                .build();

            while (true)
            {
                bool bResult = client.BoolVariation("flag1", target, false);
                Console.WriteLine($"Bool Variation value ----> {bResult}");

                JObject jResult = client.JsonVariation("flag4", target, new JObject());
                Console.WriteLine($"Bool Variation value ----> {jResult}");

                Thread.Sleep(20000);
            }
        }
    }
}
