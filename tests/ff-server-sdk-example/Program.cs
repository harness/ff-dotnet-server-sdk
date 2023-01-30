using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Serilog;

// Execute via commandline using:
//   `dotnet run --project .\tests\ff-server-sdk-example\ -- remote`
const string syntax =
    "Syntax: ff-server-sdk-example <singleton|remote|simple|local|multiple>";

// Configure Serilog...
// `dotnet add package Serilog`
// `dotnet add package Serilog.Sinks.Console`
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

// Create a DI ServiceProvider with an ILoggerFactory...
// `dotnet add package Microsoft.Extensions.DependencyInjection`
// `dotnet add package Serilog.Extensions.Logging`
using var serviceProvider = new ServiceCollection()
    .AddLogging(bldr => bldr.AddSerilog(dispose: true))
    .BuildServiceProvider();

try
{
    // Set the executable's path as the working folder...
    Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!);

    // Create our feature flag client...
    using var client = await CreateFFClient(args, serviceProvider);

    // Monitor some feature flags for "target1"...
    var target = Target.builder()
        .Name("target1")
        .IsPrivate(false)
        .Attributes(new Dictionary<string, string> { { "testKey", "TestValue" } })
        .Identifier("target1")
        .build();

    while (true)
    {
        bool bResult = client.boolVariation("flag1", target, false);
        Log.Information("Bool Variation value ----> {Result}", bResult);

        JObject jResult = client.jsonVariation("flag4", target, new JObject());
        Log.Information("JSON Variation value ----> {Result}", jResult);

        await Task.Delay(TimeSpan.FromSeconds(3));
    }
}
catch (TaskCanceledException) { }
catch (Exception e) { Log.Fatal(e, "Unhandled exception:"); }
finally { await Log.CloseAndFlushAsync(); }

async Task<ICfClient> CreateFFClient(string[] args, IServiceProvider serviceProvider)
{
    var configBuilder = Config.Builder()
        .SetLoggerFactory(serviceProvider.GetService<ILoggerFactory>());

    var arg = args.SingleOrDefault();
    return arg?.ToLowerInvariant() switch
    {
        "singleton" => await SingletonExample(configBuilder),
        "remote" => await RemoteExample(configBuilder),
        "simple" => await SimpleExample(configBuilder),
        "local" => await LocalExample(configBuilder),
        "multiple" => await MultipleClientExample(configBuilder),
        _ => printSyntax()
    };

    ICfClient printSyntax()
    {
        Console.WriteLine($"Missing or unknown argument: {arg}");
        Console.WriteLine(syntax);
        throw new TaskCanceledException();
    }
}

const string API_KEY = "70d4d39e-e50f-4cee-99bf-6fd569138c74";

void Subscribe(ICfClient client)
{
    client.InitializationCompleted += (_, e) => Log.Information("Notification Initialization Completed");
    client.EvaluationChanged += (_, identifier) => Log.Information("Flag changed for {Identifier}", identifier);
}

async Task<ICfClient> LocalExample(ConfigBuilder configBuilder)
{
    var fileMapStore = new FileMapStore("Non-Freemium");
    var connector = new LocalConnector("local");
    var client = new CfClient(connector, configBuilder.SetStore(fileMapStore).Build());
    Subscribe(client);
    await client.InitializeAndWait();
    return client;
}

async Task<ICfClient> SingletonExample(ConfigBuilder configBuilder)
{
    var fileMapStore = new FileMapStore("Non-Freemium");
    await CfClient.Instance.Initialize(API_KEY, configBuilder.SetStore(fileMapStore).Build());
    return CfClient.Instance;
}

async Task<ICfClient> SimpleExample(ConfigBuilder configBuilder)
{
    var fileMapStore = new FileMapStore("Non-Freemium");
    var client = new CfClient(API_KEY, configBuilder.SetStore(fileMapStore).Build());
    client.InitializationCompleted += (_, e) => Log.Information("Notification Initialization Completed");
    await client.InitializeAndWait();
    return client;
}

async Task<ICfClient> RemoteExample(ConfigBuilder configBuilder)
{
    var config =
        configBuilder
        .SetAnalyticsEnabled()
        .SetStreamEnabled(false)
        .SetStore(new FileMapStore("file_cache"))
        .ConfigUrl("https://config.feature-flags.uat.harness.io/api/1.0")
        .EventUrl("https://event.feature-flags.uat.harness.io/api/1.0")
        .Build();

    var client = new CfClient(API_KEY, config);
    Subscribe(client);
    await client.InitializeAndWait();
    return client;
}

[System.Diagnostics.CodeAnalysis.DoesNotReturn]
async Task<ICfClient> MultipleClientExample(ConfigBuilder configBuilder)
{
    var r = Random.Shared;

    var stores = new[]
    {
        ("Freemium", "45d2a13a-c62f-4116-a1a7-86f25d715a2e"),
        ("Freemium-2", "44255167-bc3e-4831-a79a-640234fdced8"),
        ("Non-Freemium", "9ecc4ced-afc1-45af-9b54-c899cbff4b62"),
        ("Non-Freemium-2", "32ba37eb-2c12-4143-9d05-fb4d6782b083"),
    };

    foreach (var (storeName, apiKey) in stores)
    {
        var fileMapStore = new FileMapStore(storeName);
        using var client = new CfClient(apiKey, configBuilder.SetStore(fileMapStore).Build());

        var targetName = $"Target_{r.Next()}";
        var target =
            io.harness.cfsdk.client.dto.Target.builder()
            .Name(targetName)
            .IsPrivate(false)
            .Attributes(new Dictionary<string, string>
            {
                { $"Test_key_{r.Next()}", r.Next().ToString() },
                { $"Test_key_{r.Next()}", r.Next().ToString() },
                { $"Test_key_{r.Next()}", r.Next().ToString() }
            })
            .Identifier(targetName)
            .build();

        await client.InitializeAndWait();

        bool bResult = client.boolVariation("flag1", target, false);
        Log.Information("Client: {StoreName} Bool Variation value ----> {Result}", storeName, bResult);

        double nResult = client.numberVariation("flag2", target, -1);
        Log.Information("Client: {StoreName} Number Variation value ----> {Result}", storeName, nResult);

        string sResult = client.stringVariation("flag3", target, "NO VALUE!!!!");
        Log.Information("Client: {StoreName} String Variation value ----> {Result}", storeName, sResult);

        JObject jResult = client.jsonVariation("flag4", target, new JObject());
        Log.Information("Client: {StoreName} Json Variation value ----> {Result}", storeName, jResult);
    }

    throw new TaskCanceledException();
}
