using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.dto;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace ff_server_sdk_test.api
{
    using static CannedResponses;

    [TestFixture]
    public class AsyncDeadlockTest
    {
        private const string EnvPath = "/api/1.0/client/env/00000000-0000-0000-0000-000000000000";
        private WireMockServer server;

        [SetUp]
        public void StartMockServer()
        {
            server = WireMockServer.Start(new WireMockServerSettings
            {
                Logger = new WireMockConsoleLogger()
            });
        }

        [TearDown]
        public void StopMockServer()
        {
            server.Stop();
        }

        private sealed class SingleThreadedSynchronizationContext : SynchronizationContext
        {
            private readonly BlockingCollection<(SendOrPostCallback d, object state)> queue =
                new BlockingCollection<(SendOrPostCallback d, object state)>();

            private readonly Thread ownerThread = Thread.CurrentThread;

            public bool TimedOut { get; private set; }

            public override void Post(SendOrPostCallback d, object state)
            {
                queue.Add((d, state));
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                if (Thread.CurrentThread == ownerThread)
                {
                    d(state);
                    return;
                }

                using (var done = new ManualResetEventSlim(false))
                {
                    Exception error = null;
                    queue.Add((s =>
                    {
                        try { d(s); }
                        catch (Exception e) { error = e; }
                        finally { done.Set(); }
                    }, null));
                    done.Wait();
                    if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
                }
            }

            public override SynchronizationContext CreateCopy() => this;

            // Pumps the queue on the calling thread until the task completes or timeoutMs elapses.
            // If work blocks this thread synchronously, the pump never runs and the caller's Join times out.
            public void Run(Func<Task> work, int timeoutMs)
            {
                var previous = Current;
                SetSynchronizationContext(this);
                try
                {
                    using (var cts = new CancellationTokenSource())
                    {
                        var task = work();
                        var sentinel = Task.Delay(timeoutMs, cts.Token);
                        Task.WhenAny(task, sentinel).ContinueWith(winner =>
                        {
                            if (winner.Result == sentinel) TimedOut = true;
                            queue.CompleteAdding();
                        }, TaskScheduler.Default);

                        foreach (var (d, state) in queue.GetConsumingEnumerable())
                        {
                            d(state);
                        }

                        cts.Cancel();
                        if (TimedOut) return;
                        task.GetAwaiter().GetResult();
                    }
                }
                finally
                {
                    SetSynchronizationContext(previous);
                }
            }
        }

        private static void RunWithDeadlockDetection(Func<Task> work, int timeoutMs = 5000)
        {
            ExceptionDispatchInfo failure = null;
            var context = new SingleThreadedSynchronizationContext();

            var thread = new Thread(() =>
            {
                try { context.Run(work, timeoutMs); }
                catch (Exception e) { failure = ExceptionDispatchInfo.Capture(e); }
            }) { IsBackground = true, Name = "SingleThreadedSyncContext" };

            thread.Start();

            var message = $"Deadlock detected: task did not complete within {timeoutMs}ms under SingleThreadedSynchronizationContext";
            if (!thread.Join(timeoutMs + 1000))
            {
                Assert.Fail(message);
            }

            failure?.Throw();
            if (context.TimedOut) Assert.Fail(message);
        }

        private void SetupEndpoints(string featureConfigBody = null)
        {
            server
                .Given(Request.Create().WithPath("/api/1.0/client/auth").UsingPost())
                .RespondWith(MakeAuthResponse());

            server
                .Given(Request.Create().WithPath(EnvPath + "/feature-configs").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody(featureConfigBody ?? MakeFeatureConfigBodyWithVariationToTargetMapSetToNull()));

            server
                .Given(Request.Create().WithPath(EnvPath + "/target-segments").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody(MakeTargetSegmentsBody()));
        }

        private Config BuildConfig()
        {
            return Config.Builder()
                .SetStreamEnabled(false)
                .SetAnalyticsEnabled(false)
                .ConfigUrl(server.Url + "/api/1.0")
                .Build();
        }

        private static string Flag(string name, string kind, string value)
        {
            return "{\"variationToTargetMap\":null,\"project\":\"Project\",\"environment\":\"Environment\"," +
                   "\"feature\":\"" + name + "\",\"state\":\"on\",\"kind\":\"" + kind + "\"," +
                   "\"variations\":[{\"identifier\":\"v1\",\"name\":\"v1\",\"value\":\"" + value + "\"}]," +
                   "\"rules\":[],\"defaultServe\":{\"variation\":\"v1\"},\"offVariation\":\"v1\"," +
                   "\"prerequisites\":[],\"version\":1}";
        }

        [Test, Timeout(10000)]
        public void Initialize_DoesNotDeadlock_UnderSingleThreadedContext()
        {
            SetupEndpoints();
            var initialized = false;

            RunWithDeadlockDetection(() =>
            {
                var client = new CfClient("dummy", BuildConfig());
                initialized = client.WaitForInitialization(4000);
                return Task.CompletedTask;
            });

            Assert.That(initialized, Is.True);
        }

        [Test, Timeout(10000)]
        public void WaitForInitialization_DoesNotDeadlock_UnderSingleThreadedContext()
        {
            SetupEndpoints();
            var initialized = false;

            RunWithDeadlockDetection(() =>
            {
                var client = new CfClient("dummy", BuildConfig());
                initialized = client.WaitForInitialization(4000);
                return Task.CompletedTask;
            });

            Assert.That(initialized, Is.True, "client did not initialise");
        }

        [Test, Timeout(10000)]
        public void VariationMethods_CalledSynchronouslyInsideAsyncMethod_DoNotDeadlock()
        {
            SetupEndpoints("[" +
                           Flag("boolFlag", "boolean", "true") + "," +
                           Flag("stringFlag", "string", "on") + "," +
                           Flag("numberFlag", "int", "42") + "," +
                           Flag("jsonFlag", "json", "{\\\"a\\\":1}") + "]");

            var target = Target.builder().Name("AsyncDeadlockTest").Identifier("AsyncDeadlockTest").build();
            bool boolResult = false;
            string stringResult = null;
            double numberResult = 0;
            JObject jsonResult = null;

            RunWithDeadlockDetection(async () =>
            {
                var client = new CfClient("dummy", BuildConfig());
                Assert.That(client.WaitForInitialization(4000), Is.True);

                // Synchronous calls inside an async method with a SynchronizationContext active (no await).
                boolResult = client.boolVariation("boolFlag", target, false);
                stringResult = client.stringVariation("stringFlag", target, "default");
                numberResult = client.numberVariation("numberFlag", target, -1);
                jsonResult = client.jsonVariation("jsonFlag", target, new JObject());
                await Task.CompletedTask;
            });

            Assert.That(boolResult, Is.True);
            Assert.That(stringResult, Is.EqualTo("on"));
            Assert.That(numberResult, Is.EqualTo(42));
            Assert.That((int)jsonResult["a"], Is.EqualTo(1));
        }

        [Test, Timeout(10000)]
        public void PollingProcessor_Start_DoesNotDeadlock_UnderSingleThreadedContext()
        {
            // Highest priority: PollingProcessor.Start() runs Task.WhenAll(ProcessFlags(), ProcessSegments()).Wait()
            // (PollingProcessor.cs:94). Without ConfigureAwait(false) on the awaits inside those methods, their
            // continuations are posted back to the blocked context thread and never run. This test would hang
            // indefinitely on the current main branch without PR #147's ConfigureAwait(false) additions.
            SetupEndpoints();
            var initialized = false;

            RunWithDeadlockDetection(() =>
            {
                var client = new CfClient("dummy", BuildConfig());
                initialized = client.WaitForInitialization(4000);
                return Task.CompletedTask;
            });

            Assert.That(initialized, Is.True);
        }

        [Test, Timeout(10000)]
        [Ignore("Pending PR #147 merge — add boolVariationAsync call here once ICfClient exposes it")]
        public void InitializeAsync_ThenVariationAsync_DoesNotDeadlock()
        {
        }
    }
}
