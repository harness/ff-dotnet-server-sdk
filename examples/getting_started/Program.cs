using System;
using System.Collections.Generic;
using System.Threading;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.client.api;
using Microsoft.Extensions.Logging;
using Serilog;

namespace getting_started
{
    class Program
    {
        public static String apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
        public static String flagName = Environment.GetEnvironmentVariable("FF_FLAG_NAME") is string v && v.Length > 0 ? v : "harnessappdemodarkmode";

        static void Main(string[] args)
        {
            // Configure Serilog...
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            // Get the ILoggerFactory. Real world apps will typically get this from
            // their Service Container...
            using var loggerFactory = LoggerFactory.Create(b => b.AddSerilog(dispose: true));

            // Initialize the feature flag client
            var config = Config
                .Builder()
                .SetLoggerFactory(loggerFactory)
                .Build();

            CfClient.Instance.Initialize(apiKey, config);

            // Create a target (different targets can get different results based on rules)
            Target target = Target.builder()
                            .Name("Harness_Target_1")
                            .Identifier("HT_1")
                            .Attributes(new Dictionary<string, string>() { { "email", "demo@harness.io" } })
                            .build();

            // Loop forever reporting the state of the flag
            while (true)
            {
                bool resultBool = CfClient.Instance.boolVariation(flagName, target, false);
                Console.WriteLine("Flag variation " + resultBool);
                Thread.Sleep(10 * 1000);
            }
        }
    }
}
