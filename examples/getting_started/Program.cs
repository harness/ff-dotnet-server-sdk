using System;
using System.Collections.Generic;
using System.Threading;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.dto;
using Serilog;
using Serilog.Extensions.Logging;

namespace getting_started
{
    internal class Program
    {
        public static string apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");

        public static string flagName = Environment.GetEnvironmentVariable("FF_FLAG_NAME") is string v && v.Length > 0
            ? v
            : "harnessappdemodarkmode";

        private static void Main(string[] args)
        {
            var loggerFactory = new SerilogLoggerFactory(
                new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.Console()
                    .CreateLogger());

            // Create a feature flag client
            var client = new CfClient(apiKey, Config.Builder().LoggerFactory(loggerFactory).Build());

            // Create a target (different targets can get different results based on rules)
            var target = Target.builder()
                .Name("DotNET SDK")
                .Identifier("dotnetsdk")
                .Attributes(new Dictionary<string, string> { { "email", "demo@harness.io" } })
                .build();

            // Events need to be created **before** calling WaitForInitialization
            client.InitializationCompleted += (sender, e) =>
            {
                Console.WriteLine("NOTIFICATION: Initialization Completed");
            };
            client.EvaluationChanged += (sender, identifier) =>
            {
                var resultBool = client.boolVariation(flagName, target, false);
                Console.WriteLine($"stream: Flag '{flagName}' = " + resultBool);
            };
            client.FlagsLoaded += (sender, identifiers) =>
            {
                foreach (var identifier in identifiers) Console.WriteLine($"Flag loaded: Flag '{identifier}");
            };

            var isInit = client.WaitForInitialization(30000);
            if (!isInit) Console.WriteLine("Failed to init the SDK within 30seconds");

            // Loop forever reporting the state of the flag
            while (true)
            {
                var resultBool = client.boolVariation(flagName, target, false);
                Console.WriteLine($"POLL: Flag '{flagName}' = " + resultBool);
                Thread.Sleep(10 * 1000);
            }
        }
    }
}