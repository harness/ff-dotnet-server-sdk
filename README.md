Harness CF .NET Server SDK
========================

## Overview

-------------------------
[Harness](https://www.harness.io/) is a feature management platform that helps teams to build better software and to
test features quicker.

.NET Framework version required is 4.8.
-------------------------

## Setup

You can reference the SDK in your project using NuGet package. Package is published to default package repository (nuget.org).
Package name is ff-dotnet-server-sdk. More information can be found here https://docs.microsoft.com/en-us/nuget/quickstart/install-and-use-a-package-in-visual-studio

After dependency has been added, the SDK elements, primarily `CfClient` should be accessible in the main application.

## Initialization

`CfClient` is a base class that provides all features of SDK.

```
/**
 * Put the API Key here from your environment
 */
String API_KEY = "YOUR_API_KEY";

config = Config.Builder()
                .SetPollingInterval(10000)
                .SetAnalyticsEnabled()
                .SetStreamEnabled(true)
                .Build();

CfClient cfClient = await CfClient.getInstance(API_KEY, config);

/**
 * Define you target on which you would like to evaluate 
 * the featureFlag
 */
io.harness.cfsdk.client.dto.Target target =
                io.harness.cfsdk.client.dto.Target.builder()
                .Name("User1") //can change with your target name
                .Identifier("user1@example.com") //can change with your target identifier
                .build();
```

`target` represents the desired target for which we want features to be evaluated.

`"YOUR_API_KEY"` is an authentication key, needed for access to Harness services.

**Your Harness SDK is now initialized. Congratulations!**

### Public API Methods ###

The Public API exposes a few methods that you can utilize:

* `public async Task<bool> boolVariation(string key, dto.Target target, bool defaultValue)`

* `public async Task<string> stringVariation(string key, dto.Target target, string defaultValue)`

* `public async Task<double> numberVariation(string key, dto.Target target, int defaultValue)`

* `public async Task<JObject> jsonVariation(string key, dto.Target target, JObject defaultValue)`


## Fetch evaluation's value

It is possible to fetch a value for a given evaluation. Evaluation is performed based on a different type. In case there
is no evaluation with provided id, the default value is returned.

Use the appropriate method to fetch the desired Evaluation of a certain type.

### Bool variation

```
boolean result = await cfClient.boolVariation("sample_boolean_flag", target, false);  
```

### Number variation

```
double result =  awaitcfClient.numberVariation("sample_number_flag", target, 0);  
```

### String variation

```
string result = await cfClient.stringVariation("sample_string_flag", target, "");  
```

## Using feature flags metrics

Metrics API endpoint can be changed like this:

```
Config.builder()
              .EventUrl("METRICS_API_EVENTS_URL")
              .build();
```

Otherwise, the default metrics endpoint URL will be used.


