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
Package name: `ff-netF48-server-sdk --version 1.1.1`

More information can be found here https://docs.microsoft.com/en-us/nuget/quickstart/install-and-use-a-package-using-the-dotnet-cli

After dependency has been added, the SDK elements, primarily `CfClient` should be accessible in the main application.

## Initialization

`CfClient` is a main class that provides all features of SDK.

Class can be accessed as Singleton instance.

```c#
using System;
using System.Collections.Generic;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.client.api;
using System.Threading;

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
}
```

Alternativly user can directly instantiate `CfClient`, pass required configration parameters, and initiate authentication process
```c#
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

```c#
Config.builder()
              .EventUrl("METRICS_API_EVENTS_URL")
              .build();
```

Otherwise, the default metrics endpoint URL will be used.


