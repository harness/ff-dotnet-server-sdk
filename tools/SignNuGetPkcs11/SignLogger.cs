using NuGet.Common;

namespace SignNuGetPkcs11;

internal class SignLogger : NuGet.Common.ILogger
{
    public void LogDebug(string data)
    {
        Console.WriteLine($"DEBUG: {data}");
    }

    public void LogError(string data)
    {
        Console.WriteLine($"ERROR: {data}");
    }

    public void LogInformationSummary(string data)
    {
        Console.WriteLine($"LogInformationSummary: {data}");
    }

    public void Log(LogLevel level, string data)
    {
        Console.WriteLine($"Log: {data}");
    }

    public Task LogAsync(LogLevel level, string data)
    {
        Console.WriteLine($"LogAsync: {data}");
        return Task.CompletedTask;
    }

    public Task LogAsync(ILogMessage message)
    {
        Console.WriteLine($"LogAsync: {message.Message}");
        return Task.CompletedTask;
    }

    public void Log(ILogMessage message)
    {
        Console.WriteLine($"Log: {message.Message}");
    }

    public void LogInformation(string data)
    {
        Console.WriteLine($"INFO: {data}");
    }

    public void LogMinimal(string data)
    {
        Console.WriteLine($"MINIMAL: {data}");
    }

    public void LogVerbose(string data)
    {
        Console.WriteLine($"VERBOSE: {data}");
    }

    public void LogWarning(string data)
    {
        Console.WriteLine($"WARNING: {data}");
    }
}
