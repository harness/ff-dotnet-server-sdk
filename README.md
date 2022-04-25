Harness CF .NET Server SDK
========================

## Table of Contents
**[Intro](#Intro)**<br>
**[Requirements](#Requirements)**<br>
**[Quickstart](#Quickstart)**<br>
**[Further Reading](docs/further_reading.md)**<br>
**[Build Instructions](docs/build.md)**<br>


## Intro

Harness Feature Flags (FF) is a feature management solution that enables users to change the software’s functionality, without deploying new code. FF uses feature flags to hide code or behaviours without having to ship new versions of the software. A feature flag is like a powerful if statement.
* For more information, see https://harness.io/products/feature-flags/
* To read more, see https://ngdocs.harness.io/category/vjolt35atg-feature-flags
* To sign up, https://app.harness.io/auth/#/signup/

![FeatureFlags](https://github.com/harness/ff-python-server-sdk/raw/main/docs/images/ff-gui.png)

## Requirements
[.NET Framework >= 4.8]<br> 
or<br>
[.Net 5.0.104](https://docs.microsoft.com/en-us/nuget/quickstart/install-and-use-a-package-using-the-dotnet-cli) or newer (dotnet --version)<br>
The library is packaged as multi-target supporting `netstandard2.0` set of API's and additionaly targets `net461` for older frameworks.


## Quickstart
The Feature Flag SDK provides a client that connects to the feature flag service, and fetches the value
of featue flags.   The following section provides an example of how to install the SDK and initalize it from
an application.
This quickstart assumes you have followed the instructions to [setup a Feature Flag project and have created a flag called `harnessappdemodarkmode` and created a server API Key](https://ngdocs.harness.io/article/1j7pdkqh7j-create-a-feature-flag#step_1_create_a_project).


### Add the SDK to your project
Add the sdk using dotnet
```bash
dotnet add package ff-netF48-server-sdk .
```

### A Simple Example
Here is a complete example that will connect to the feature flag service and report the flag value every 10 seconds until the connection is closed.  
Any time a flag is toggled from the feature flag service you will receive the updated value.

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

```

### Running the example

```bash
$ export FF_API_KEY=<your key here>
$ dotnet run --project examples/getting_started/
```

### Running with docker
If you dont have the right version of dotnet installed locally, or dont want to install the dependancies you can
use docker to quicky get started

```bash
docker run -v $(pwd):/app -w /app -e FF_API_KEY=$FF_API_KEY mcr.microsoft.com/dotnet/sdk:5.0 dotnet run --project examples/getting_started/
```

### Additional Reading

Further examples and config options are in the further reading section:

[Further Reading](docs/further_reading.md)


-------------------------
[Harness](https://www.harness.io/) is a feature management platform that helps teams to build better software and to
test features quicker.

-------------------------
