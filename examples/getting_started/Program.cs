using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.client.api;
using Serilog;
using Serilog.Extensions.Logging;

var apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
var flagName = Environment.GetEnvironmentVariable("FF_FLAG_NAME") ?? "harnessappdemodarkmode";

// Configure Serilog...
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

// Initialize the feature flag client
var config = Config.Builder()
    .SetLoggerFactory(new SerilogLoggerFactory())
    .Build();

await CfClient.Instance.Initialize(apiKey, config);

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
    Console.WriteLine($"Flag variation {resultBool}");
    await Task.Delay(TimeSpan.FromSeconds(3));
}
