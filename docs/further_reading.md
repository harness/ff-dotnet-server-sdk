## Initialization

The user can alternatively directly instantiate `CfClient`, pass required configration parameters, and initiate authentication process
```c#
// Creates instance of a client
var client = new CfClient(API_KEY, Config.Builder().Build());

// Starts authentication and asynchronously wait for initialisation to complete
await client.InitializeAndWait();
```

## Other Variation Types

### Bool variation

* `public bool boolVariation(string key, dto.Target target, bool defaultValue)`

### Number variation

* `public double numberVariation(string key, dto.Target target, int defaultValue)`

### String variation

* `public string stringVariation(string key, dto.Target target, string defaultValue)`

### JSON variation

* `public JObject jsonVariation(string key, dto.Target target, JObject defaultValue)`

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
client.EvaluationChanged += (sender, identifier) =>
{
    // Fired when flag value changes.
    Console.WriteLine($"Flag changed for {identifier}");
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

