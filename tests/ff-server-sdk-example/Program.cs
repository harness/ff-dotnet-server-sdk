using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.connector;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace io.harness.example
{
    class Observer : IObserver<Event>
    {
        public void OnCompleted()
        {
     
        }

        public void OnError(Exception error)
        {
           
        }

        public void OnNext(Event value)
        {
            Console.WriteLine("Value Observed: " + value.identifier);
        }
    }
    class Program 
    {
        static readonly string boolflagname = "andrija_test";
        static readonly string API_KEY = "70d4d39e-e50f-4cee-99bf-6fd569138c74";
        static readonly Observer observe = new Observer();
        static void MainLocal()
        {
            var local = new LocalConnector("./local");
            CfClient.Instance.Initialize(local);

            io.harness.cfsdk.client.dto.Target target =
                io.harness.cfsdk.client.dto.Target.builder()
                .Name("Andrija Test Target") //can change with your target name
                .Identifier("andrija_test_target") //can change with your target identifier
                .build();

        }
        static void MainRemote()
        {
            
            Config config = Config.Builder()
                .SetAnalyticsEnabled()
                .SetStreamEnabled(false)
                .ConfigUrl("https://config.feature-flags.uat.harness.io/api/1.0")
                .EventUrl("https://event.feature-flags.uat.harness.io/api/1.0")
                .Build();
            CfClient.Instance.Initialize(API_KEY, config);
            CfClient.Instance.Subscribe(observe);

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

            MainRemote();

            io.harness.cfsdk.client.dto.Target target =
                io.harness.cfsdk.client.dto.Target.builder()
                .Name("Andrija Test Target") //can change with your target name
                .Identifier("andrija_test_target") //can change with your target identifier
                .build();

            await CfClient.Instance.StartAsync();

            while (true)
            {
                /*
                Console.WriteLine("Bool Variation Calculation Command Start ============== " + boolflagname);
                bool result = CfClient.Instance.BoolVariation(boolflagname, target, false);
                Console.WriteLine("Bool Variation value ---->" + result);
                Console.WriteLine("Bool Variation Calculation Command Stop ---------------\n\n\n");
                */
                Thread.Sleep(2000);
            }
        }
    }
}
