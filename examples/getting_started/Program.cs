using System;
using System.Collections.Generic;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.client.api;
using System.Threading;
using Serilog;

namespace getting_started
{
    class Program
    {
        public static String apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
        public static String flagName = Environment.GetEnvironmentVariable("FF_FLAG_NAME") is string v && v.Length > 0 ? v : "harnessappdemodarkmode";

        static void Main(string[] args)
        {
            // Create a feature flag client
            CfClient.Instance.Initialize(apiKey, Config.Builder().Build());

            // Create a target (different targets can get different results based on rules)
            Target target = Target.builder()
                            .Name("DotNetSDK")
                            .Identifier("dotnetsdk")
                            .Attributes(new Dictionary<string, string>(){{"location", "emea"}})
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
