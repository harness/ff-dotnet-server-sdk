using System.Diagnostics;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.dto;
using Serilog;
using Serilog.Extensions.Logging;

var apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
if (apiKey == null) throw new ArgumentNullException("FF_API_KEY","FF_API_KEY env variable is not set");
var flagName = Environment.GetEnvironmentVariable("FF_FLAG_NAME") ?? "test";


var loggerFactory = new SerilogLoggerFactory(
    new LoggerConfiguration()
        .MinimumLevel.Error()
        .WriteTo.Console()
        .CreateLogger());

var config = Config.Builder()
    .LoggerFactory(loggerFactory)
    .SetAnalyticsEnabled(true)
    //.SetBufferSize(100)
    .Build();

var client = new CfClient(apiKey,
    Config.Builder()
        .ConfigUrl("https://config.feature-flags.qa.harness.io/api/1.0")
        .EventUrl("https://event.feature-flags.qa.harness.io/api/1.0")
        .SetStreamEnabled(true)
        .UseMapForInClause(true)
        .LoggerFactory(loggerFactory)
        .Build());

Console.WriteLine("Wait for SDK to init");
client.WaitForInitialization();

Thread[] threads = new Thread[1];

for (int i = 0; i < threads.Length; i++)
{
    threads[i] = new Thread(() => Run(i, client, flagName));
}

foreach (var t in threads)
    t.Start();

Console.WriteLine("Waiting for threads to complete");

foreach (var t in threads)
    t.Join();



static void Run(int threadNum, CfClient client, string flagName)
{
    Console.WriteLine("thread started: " + threadNum);
    var limit = 1_000_000_000;
    var sw = new Stopwatch();
    double avgTotal = 0;
    double avgTime = 0;
    long minTime = Int32.MaxValue;
    long maxTime = 0;

    var startTime = DateTime.UtcNow;
    var oneSecond = TimeSpan.FromSeconds(1);
    var numberCallsThisSecond = 0;

    var target = Target.builder()
        .Name("DotNETSDK_loadtest")
        .Identifier("dotnetsdk_loadtest")
        .Attributes(new Dictionary<string, string> { { "TenantId", "asdasd" } })
        .build();

    for (int i = 1; i < limit; i++)
    {
        sw.Restart();
        sw.Start();
        //_ = client.boolVariation(flagName, target, false);
        _ = client.stringVariation(flagName, target, "def");
        sw.Stop();

        numberCallsThisSecond++;
        minTime = Math.Min(minTime, sw.ElapsedMilliseconds);
        maxTime = Math.Max(maxTime, sw.ElapsedMilliseconds);
        avgTotal += sw.ElapsedMilliseconds;

        if (DateTime.UtcNow > startTime + oneSecond)
        {
            avgTime = avgTotal / numberCallsThisSecond;

            var ts = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            Console.WriteLine($"{ts}  threadnum: {threadNum} iteration: {i+1} min:{minTime}ms max:{maxTime}ms avg:{avgTime:N2}ms tps: {numberCallsThisSecond}");

            numberCallsThisSecond = 0;
            avgTotal = 0;
            startTime = DateTime.UtcNow;
            minTime = Int32.MaxValue;
            maxTime = 0;
        }
    }
}



