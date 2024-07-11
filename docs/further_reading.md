# Further Reading

Covers advanced topics (different config options and scenarios)

## Configuration Options
The following configuration options are available to control the behaviour of the SDK.
You can provide options by passing them in when the client is created e.g.

```c#
CfClient.Instance.Initialize(apiKey, Config.Builder()
    .ConfigUrl("https://config.ff.harness.io/api/1.0")
    .EventUrl("https://events.ff.harness.io/api/1.0")
    .SetPollingInterval(60)
    .SetStreamEnabled(true)
    .SetAnalyticsEnabled(true)
    .Build());
```

| Name            | Config Option                                     | Description                                                                                                                                      | default                              |
|-----------------|---------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------|
| baseUrl         | ConfigUrl("https://config.ff.harness.io/api/1.0") | the URL used to fetch feature flag evaluations. You should change this when using the Feature Flag proxy to http://localhost:7000                | https://config.ff.harness.io/api/1.0 |
| eventsUrl       | EventUrl("https://events.ff.harness.io/api/1.0")  | the URL used to post metrics data to the feature flag service. You should change this when using the Feature Flag proxy to http://localhost:7000 | https://events.ff.harness.io/api/1.0 |
| pollInterval    | SetPollingInterval(60)                            | when running in stream mode, the interval in seconds that we poll for changes.                                                                   | 60                                   |
| enableStream    | SetStreamEnabled(true)                            | Enable streaming mode.                                                                                                                           | true                                 |
| enableAnalytics | SetAnalyticsEnabled(true)                         | Enable analytics.  Metrics data is posted every 60s                                                                                              | true                                 |

# Anonymous Target

If you do not want a `Target` to be sent to Harness servers, you can use the `isPrivate` field.

```csharp
Target target1 = Target.builder()
                .Identifier("myIdentifier")
                .Name("myName")
                .IsPrivate(true)
                .Attributes(new Dictionary<string, string>(){{"email", "demo@harness.io"}})
                .build();
```            


## Logging Configuration
You can configure the logger using Serilog.
Add Serilog to your project with the following commands

```shell
dotnet add package Serilog
```

You can then configure the logger to write to the console with debug using the following
```c#
// Logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();
```

## Recommended reading

[Feature Flag Concepts](https://ngdocs.harness.io/article/7n9433hkc0-cf-feature-flag-overview)

[Feature Flag SDK Concepts](https://ngdocs.harness.io/article/rvqprvbq8f-client-side-and-server-side-sdks)

## Setting up your Feature Flags

[Feature Flags Getting Started](https://ngdocs.harness.io/article/0a2u2ppp8s-getting-started-with-feature-flags)

## Other Variation Types

### Bool variation

* `public bool boolVariation(string key, dto.Target target, bool defaultValue)`

### Number variation

* `public double numberVariation(string key, dto.Target target, double defaultValue)`

### String variation

* `public string stringVariation(string key, dto.Target target, string defaultValue)`

### JSON variation

* `public JObject jsonVariation(string key, dto.Target target, JObject defaultValue)`


## Waiting for Initialization

The user can call WaitForInitialization() to block and wait for the SDK
```c#
// Creates instance of a client
var client = new CfClient(API_KEY, Config.Builder().Build());

// Starts authentication and asynchronously wait for initialisation to complete
client.WaitForInitialization();
```

## Connector

This feature allows you to create or use other connectors.
Connector is just a proxy to your data. Currently supported connectors:
* Harness (Used by default)
* Local (used only in development)

```c#
LocalConnector connector = new LocalConnector("local");
client = new CfClient(connector);
```

## Storage

For offline support and asynchronous startup of SDK user should use storage interface.
When SDK is used without waiting on async methods, and configuration is provided with file storage, then all flags are loaded from last saved configurations.
If there is no flag in a storage then they will be evaluated from defaultValue argument.

```c#
FileMapStore fileStore = new FileMapStore("Non-Freemium");
LocalConnector connector = new LocalConnector("local");
client = new CfClient(connector, Config.builder().store(fileStore).build());
```

## Fetch evaluation's value

It is possible to fetch a value for a given evaluation. Evaluation is performed based on a different type. In case there
is no evaluation with provided id, the default value is returned.

Use the appropriate method to fetch the desired Evaluation of a certain type.

## Listen on events

Library exposes two events for user to subscribe on getting internal notifications.

```c#
client.InitializationCompleted += (sender, e) =>
{
    // fired when authentication is completed and recent configuration is fetched from server
    Console.WriteLine("Notification Initialization Completed");
};
client.EvaluationChanged += (sender, flagIdentifier) =>
{
    // Fired when flag value changes from streaming events.
    Console.WriteLine($"Flag changed for {flagIdentifier}");
};

client.FlagsLoaded += (sender, flagIdentifiers) =>
{
    // Fired once when the SDK has initiailized, and subsquently on each poll if streaming is disabled or has disconnected
    // and fallen back to polling
    foreach (var flagIdentifier in flagIdentifiers)
    {
        Console.WriteLine($"Flags loaded: Flag '{flagIdentifier}");
    }
};
```

## Using feature flags metrics

Metrics API endpoint can be changed like this:

```c#
Config.builder()
              .EventUrl("METRICS_API_EVENTS_URL")
              .build();
```

Otherwise, the default metrics endpoint URL will be used.

## Connect to Relay Proxy
 When using your Feature Flag SDKs with a [Harness Relay Proxy](https://ngdocs.harness.io/article/q0kvq8nd2o-relay-proxy) you need to change the default URL.
You can pass the URLs in when creating the client. i.e.

```c#
        CfClient.Instance.Initialize(apikey, Config.Builder()
            .ConfigUrl("http://localhost:7000")
            .EventUrl("http://localhost:7000")
            .Build());
```
