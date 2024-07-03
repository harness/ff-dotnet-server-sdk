using System;
using System.Collections.Generic;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.client.api;
using System.Threading;
using Serilog;
using Serilog.Extensions.Logging;

namespace getting_started
{
    class Program
    {
        public static String apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
        public static String flagName = Environment.GetEnvironmentVariable("FF_FLAG_NAME") is string v && v.Length > 0 ? v : "harnessappdemodarkmode";

        static void Main(string[] args)
        {
            var loggerFactory = new SerilogLoggerFactory(
                new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.Console()
                    .CreateLogger());

            // Create a feature flag client
            var client = new CfClient(apiKey, Config.Builder().LoggerFactory(loggerFactory).Build());
            var isInit = client.WaitForInitialization(30000);
            if (!isInit)
            {
                Console.WriteLine("Failed to init the SDK within 30seconds");
            }

            // Create a target (different targets can get different results based on rules)
            Target target = Target.builder()
                            .Name("DotNET SDK")
                            .Identifier("dotnetsdk")
                            .Attributes(new Dictionary<string, string>(){{"email", "demo@harness.io"}})
                            .build();

            // Add some sample events
            client.InitializationCompleted += (sender, e) => { Console.WriteLine("NOTIFICATION: Initialization Completed"); };
            client.EvaluationChanged += (sender, identifier) => { Console.WriteLine($"NOTIFICATION: Flag changed for '{identifier}'"); };

           // Loop forever reporting the state of the flag
            while (true)
            {
                bool resultBool = client.boolVariation(flagName, target, false);
                Console.WriteLine($"POLL: Flag '{flagName}' = " + resultBool);
                Thread.Sleep(10 * 1000);
            }
        }
    }
}
