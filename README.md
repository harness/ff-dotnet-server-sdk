Harness CF .NET Server SDK
========================

## Overview

-------------------------
[Harness](https://www.harness.io/) is a feature management platform that helps teams to build better software and to
test features quicker.

## Packaging

Library is packaged as multi-target supporting `netstandard2.0` set of API's and additionaly target `net461` for older frameworks.


## Setup

You can reference the SDK in your project using NuGet package. Package is published to default package repository (nuget.org).
Package name: `ff-netF48-server-sdk --version 1.1.0`

More information can be found here https://docs.microsoft.com/en-us/nuget/quickstart/install-and-use-a-package-using-the-dotnet-cli

After dependency has been added, the SDK elements, primarily `CfClient` should be accessible in the main application.

## Initialization

`CfClient` is a main class that provides all features of SDK.

Class can be accessed as Singleton instance.

```
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.client.api;

/**
 * Put the API Key here from your environment
 */
String API_KEY = "YOUR_API_KEY";

config = Config.Builder()
                .SetPollingInterval(60000)
                .SetAnalyticsEnabled()
                .SetStreamEnabled(true)
                .Build();

/** 
 * Call to Initialize will start authentication, while await will pause execution
 * until initiazition is completed.
 */
await CfClient.Instance.Initialize(API_KEY, config);

/**
 * Define you target on which you would like to evaluate 
 * the featureFlag
 */
Target target = io.harness.cfsdk.client.dto.Target.builder()
                .Name("User1") //can change with your target name
                .Identifier("user1@example.com") //can change with your target identifier
                .build();
```

Alternativly user can directly instantiate `CfClient`, pass required configration parameters, and initiate authentication process
```
// Creates instance of a client
var client = new CfClient(API_KEY, Config.Builder().Build());

// Starts authentication and asynchronously wait for initialisation to complete
await client.InitializeAndWait();
```


`target` represents the desired target for which we want features to be evaluated.

`"YOUR_API_KEY"` is an authentication key, needed for access to Harness services.

**Your Harness SDK is now initialized. Congratulations!**

## Connector

This feature allows you to create or use other connectors.
Connector is just a proxy to your data. Currently supported connectors:
* Harness (Used by default)
* Local (used only in development)

```
LocalConnector connector = new LocalConnector("local");
client = new CfClient(connector);
```

## Storage

For offline support and asynchronous startup of SDK user should use storage interface.
When SDK is used without waiting on async methods, and configuration is provided with file storage, then all flags are loaded from last saved configurations.
If there is no flag in a storage then they will be evaluated from defaultValue argument.

```
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

```
client.InitializationCompleted += (sender, e) =>
{
    // fired when authentication is completed and recent configuration is fetched from server
    Console.WriteLine("Notification Initialization Completed");
};
client.EvaluationChanged += (sender, identifier) =>
{
    // Fired when fleg value changes.
    Console.WriteLine($"Flag changed for {identifier}");
};
```

### Bool variation

* `public bool boolVariation(string key, dto.Target target, bool defaultValue)`

### Number variation

* `public double numberVariation(string key, dto.Target target, int defaultValue)`

### String variation

* `public string stringVariation(string key, dto.Target target, string defaultValue)`

### JSON variation

* `public JObject jsonVariation(string key, dto.Target target, JObject defaultValue)`


## Using feature flags metrics

Metrics API endpoint can be changed like this:

```
Config.builder()
              .EventUrl("METRICS_API_EVENTS_URL")
              .build();
```

Otherwise, the default metrics endpoint URL will be used.


