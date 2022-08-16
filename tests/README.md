## Instructions to installing and using VSCode Debugger

Here are some helpful steps to installing and using the VS debugger on VSCode for the examples in this SDK.

- Install the [C# extension](https://code.visualstudio.com/docs/languages/csharp)
- Run the debugger on VSCode
    - You will know it was successfully installed when you see a new directory created named `.vscode` after running it.
    - Within the directory, you will see the files `launch.json` and `tasks.json`
    - Please see [this documentation](https://docs.microsoft.com/en-us/dotnet/core/tutorials/debugging-with-visual-studio-code?pivots=dotnet-6-0) for further defails and instructions.

When running the example app locally, to use the local sources, add the line `<RestoreSources>$(RestoreSources);../../examples/getting_started/bin/Debug;https://api.nuget.org/v3/index.json</RestoreSources>` to `examples/getting_started/getting_started.csproj`