using System;
using System.Collections.Concurrent;
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

    /// <summary>
    /// Proves the SDK's sync-over-async paths do not deadlock when the calling thread owns a
    /// SynchronizationContext (ASP.NET Framework, WPF, WinForms).
    /// </summary>
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

            private readonly int ownerThreadId = Thread.CurrentThread.ManagedThreadId;

            public override void Post(SendOrPostCallback d, object state)
            {
                if (!queue.IsAddingCompleted)
                {
                    try { queue.Add((d, state)); }
                    catch (InvalidOperationException) { /* queue completed concurrently */ }
                }
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                if (Thread.CurrentThread.ManagedThreadId == ownerThreadId)
                {
                    d(state);
                    return;
                }

                using (var done = new ManualResetEventSlim(false))
                {
                    Exception error = null;
                    Post(_ =>
                    {
                        try { d(state); }
                        catch (Exception e) { error = e; }
                        finally { done.Set(); }
                    }, null);
                    done.Wait();
                    if (error != null) throw error;
                }
            }

            public override SynchronizationContext CreateCopy()
            {
                return this;
            }

            // Returns true if the task completed, false if it timed out. Must be called on the owner thread.
            public bool Run(Func<Task> work, int timeoutMs)
            {
                var previous = Current;
                SetSynchronizationContext(this);
                try
                {
                    using (var cts = new CancellationTokenSource())
                    {
                        var task = work();
                        var sentinel = Task.Delay(timeoutMs, cts.Token);
                        Task.WhenAny(task, sentinel).ContinueWith(_ => queue.CompleteAdding(),
                            TaskScheduler.Default);

                        foreach (var item in queue.GetConsumingEnumerable())
                        {
                            item.d(item.state);
                        }

                        cts.Cancel();
                        if (!task.IsCompleted) return false;
                        task.GetAwaiter().GetResult(); // propagate exceptions
                        return true;
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
            Exception error = null;
            var completed = false;

            var thread = new Thread(() =>
            {
                try
                {
                    var ctx = new SingleThreadedSynchronizationContext();
                    completed = ctx.Run(work, timeoutMs);
                }
                catch (Exception e)
                {
                    error = e;
                }
            }) { IsBackground = true };

            thread.Start();

            if (!thread.Join(timeoutMs + 1000) || (error == null && !completed))
            {
                Assert.Fail($"Deadlock detected: task did not complete within {timeoutMs}ms under SingleThreadedSynchronizationContext");
            }

            if (error != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
            }
        }

        private void SetupEndpoints(string featureConfigBody)
        {
            server
                .Given(Request.Create().WithPath("/api/1.0/client/auth").UsingPost())
                .RespondWith(MakeAuthResponse());

            server
                .Given(Request.Create().WithPath(EnvPath + "/feature-configs").UsingGet())
                .RespondWith(Response.Create().WithStatusCode(200).WithBody(featureConfigBody));

            server
                .Given(Request.Create().WithPath(EnvPath + "/target-segments").UsingGet())
                .RespondWith(Response.Create().WithStatusCode(200).WithBody(MakeTargetSegmentsBody()));
        }

        private Config BuildConfig()
        {
            return Config.Builder()
                .SetStreamEnabled(false)
                .SetAnalyticsEnabled(false)
                .ConfigUrl(server.Url + "/api/1.0")
                .Build();
        }

        private static string MakeFlag(string name, string kind, string value)
        {
            return "{\"variationToTargetMap\":null,\"project\":\"Project\",\"environment\":\"Environment\"," +
                   $"\"feature\":\"{name}\",\"state\":\"on\",\"kind\":\"{kind}\"," +
                   $"\"variations\":[{{\"identifier\":\"on\",\"name\":\"on\",\"value\":{value}}}]," +
                   "\"rules\":[],\"defaultServe\":{\"variation\":\"on\"},\"offVariation\":\"on\"," +
                   "\"prerequisites\":[],\"version\":1}";
        }

        private static string MakeAllKindsBody()
        {
            return "[" +
                   MakeFlag("BoolFlag", "boolean", "\"true\"") + "," +
                   MakeFlag("StringFlag", "string", "\"hello\"") + "," +
                   MakeFlag("NumberFlag", "int", "\"42\"") + "," +
                   MakeFlag("JsonFlag", "json", "\"{\\\"a\\\":1}\"") +
                   "]";
        }

        [Test, Timeout(10000)]
        public void Initialize_DoesNotDeadlock_UnderSingleThreadedContext()
        {
            SetupEndpoints(MakeEmptyBody());
            var config = BuildConfig();
            var initialized = false;

            RunWithDeadlockDetection(() =>
            {
                var client = new CfClient("dummy", config);
                try
                {
                    initialized = client.WaitForInitialization(4000);
                }
                finally
                {
                    client.Close();
                }
                return Task.CompletedTask;
            });

            Assert.IsTrue(initialized, "client did not initialise");
        }

        [Test, Timeout(10000)]
        public void WaitForInitialization_DoesNotDeadlock_UnderSingleThreadedContext()
        {
            SetupEndpoints(MakeEmptyBody());
            var config = BuildConfig();
            var initialized = false;

            RunWithDeadlockDetection(() =>
            {
                var client = new CfClient("dummy", config);
                try
                {
                    initialized = client.WaitForInitialization(4000);
                }
                finally
                {
                    client.Close();
                }
                return Task.CompletedTask;
            });

            Assert.IsTrue(initialized, "WaitForInitialization returned false");
        }

        [Test, Timeout(10000)]
        public void VariationMethods_CalledSynchronouslyInsideAsyncMethod_DoNotDeadlock()
        {
            SetupEndpoints(MakeAllKindsBody());
            var config = BuildConfig();
            var target = Target.builder().Name("AsyncDeadlockTest").Identifier("AsyncDeadlockTest").build();

            bool boolResult = false;
            string stringResult = null;
            double numberResult = 0;
            JObject jsonResult = null;

            RunWithDeadlockDetection(async () =>
            {
                var client = new CfClient("dummy", config);
                try
                {
                    Assert.IsTrue(client.WaitForInitialization(4000), "client did not initialise");

                    // Deliberately synchronous (no await) calls while a SynchronizationContext is active.
                    boolResult = client.boolVariation("BoolFlag", target, false);
                    stringResult = client.stringVariation("StringFlag", target, "default");
                    numberResult = client.numberVariation("NumberFlag", target, -1);
                    jsonResult = client.jsonVariation("JsonFlag", target, new JObject());
                }
                finally
                {
                    client.Close();
                }
                await Task.Yield();
            });

            Assert.IsTrue(boolResult);
            Assert.AreEqual("hello", stringResult);
            Assert.AreEqual(42d, numberResult);
            Assert.AreEqual(1, jsonResult.Value<int>("a"));
        }

        [Test, Timeout(10000)]
        public void PollingProcessor_Start_DoesNotDeadlock_UnderSingleThreadedContext()
        {
            // NOTE: this test would hang indefinitely on the current main branch without PR #147's
            // ConfigureAwait(false) additions: PollingProcessor.Start() blocks on
            // Task.WhenAll(ProcessFlags(), ProcessSegments()).Wait() (PollingProcessor.cs:94) while the
            // continuations try to resume on the single-threaded context owned by the blocked thread.
            SetupEndpoints(MakeAllKindsBody());
            var config = BuildConfig();
            var initialized = false;

            RunWithDeadlockDetection(() =>
            {
                var client = new CfClient("dummy", config);
                try
                {
                    initialized = client.WaitForInitialization(4000);
                }
                finally
                {
                    client.Close();
                }
                return Task.CompletedTask;
            });

            Assert.IsTrue(initialized, "client did not initialise");
        }

        [Test, Timeout(10000)]
        [Ignore("Pending PR #147 merge — add boolVariationAsync call here once ICfClient exposes it")]
        public void InitializeAsync_ThenVariationAsync_DoesNotDeadlock()
        {
        }
    }
}
