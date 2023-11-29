.NET SDK For Harness Feature Flags
========================

## Table of Contents
**[Intro](#Intro)**<br>
**[Requirements](#Requirements)**<br>
**[Quickstart](#Quickstart)**<br>
**[Further Reading](docs/further_reading.md)**<br>
**[Build Instructions](docs/build.md)**<br>


## Intro
Use this README to get started with our Feature Flags (FF) SDK for .NET. This guide outlines the basics of getting started with the SDK and provides a full code sample for you to try out.
This sample doesn’t include configuration options, for in depth steps and configuring the SDK, for example, disabling streaming or using our Relay Proxy, see the  [.NET SDK Reference](https://ngdocs.harness.io/article/c86rasy39v-net-sdk-reference).

For a sample FF .NET SDK project, see our [test .NET project](examples/getting_started/).

![FeatureFlags](https://github.com/harness/ff-python-server-sdk/raw/main/docs/images/ff-gui.png)

## Requirements
The library is packaged as multi-target supporting  `net461`,`netstandard2.0`, `net5.0`, `net6.0` and `net7.0`.

## Build Requirements
If building from source you will need [.Net 7.0.404](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) or newer (dotnet --version)<br>

## Quickstart
To follow along with our test code sample, make sure you’ve:

- [Created a Feature Flag on the Harness Platform](https://ngdocs.harness.io/article/1j7pdkqh7j-create-a-feature-flag) called harnessappdemodarkmode
- [Created a server SDK key and made a copy of it](https://ngdocs.harness.io/article/1j7pdkqh7j-create-a-feature-flag#step_3_create_an_sdk_key)



### Install the SDK
Add the sdk using dotnet
```bash
dotnet add package ff-dotnet-server-sdk
```

### Code Sample
The following is a complete code example that you can use to test the `harnessappdemodarkmode` Flag you created on the Harness Platform. When you run the code it will:
- Connect to the FF service.
- Report the value of the Flag every 10 seconds until the connection is closed. Every time the `harnessappdemodarkmode` Flag is toggled on or off on the Harness Platform, the updated value is reported. 
- Close the SDK.


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
            // Configure your logger
            var loggerFactory = new SerilogLoggerFactory(
                new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.Console()
                    .CreateLogger());

            // Create a feature flag client
            CfClient.Instance.Initialize(apiKey, Config.Builder().LoggerFactory(loggerFactory).Build());

            // Create a target (different targets can get different results based on rules)
            Target target = Target.builder()
                            .Name("Harness_Target_1")
                            .Identifier("HT_1")
                            .Attributes(new Dictionary<string, string>(){{"email", "demo@harness.io"}})
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

```

### Running the example

```bash
$ export FF_API_KEY=<your key here>
$ dotnet run --project examples/getting_started/
```

### Running the example with Docker
If you dont have the right version of dotnet installed locally, or dont want to install the dependancies you can
use docker to quicky get started

```bash
docker run -v $(pwd):/app -w /app -e FF_API_KEY=$FF_API_KEY mcr.microsoft.com/dotnet/sdk:6.0 dotnet run --framework net6.0 --project examples/getting_started/
```

### Additional Reading


For further examples and config options, see the [.NET SDK Reference](https://ngdocs.harness.io/article/c86rasy39v-net-sdk-reference#).

For more information about Feature Flags, see our [Feature Flags documentation](https://ngdocs.harness.io/article/0a2u2ppp8s-getting-started-with-feature-flags).


-------------------------
[Harness](https://www.harness.io/) is a feature management platform that helps teams to build better software and to
test features quicker.

-------------------------
