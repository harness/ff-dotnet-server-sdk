using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.connector;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Logging;
using WireMock.Settings;

namespace ff_server_sdk_test.connector
{
    [TestFixture]
    public class EventSourceTest
    {
        private WireMockServer server;

        [SetUp]
        public void StartMockServer()
        {
            server = WireMockServer.Start(new WireMockServerSettings());
        }

        [TearDown]
        public void StopMockServer()
        {
            server.Stop();
        }

        class TestCallback : IUpdateCallback
        {
            public int UpdateCount { get; set; }
            public int ConnectCount { get; set; }
            public int DisconnectCount { get; set; }

            public List<Message> Events { get; set; } = new();

            private CountdownEvent disconnectLatch = new CountdownEvent(1);

            public void Update(Message message, bool manual)
            {
               Console.WriteLine("Test got stream update " + message);
                Events.Add(message);
                UpdateCount++;
            }

            public void OnStreamConnected()
            {
                Console.WriteLine("SDKCODE(stream:5000): SSE stream connected ok");
                ConnectCount++;
            }

            public void OnStreamDisconnected()
            {
                Console.WriteLine("SDKCODE(stream:5001): SSE stream disconnected");
                DisconnectCount++;
                disconnectLatch.Signal();
            }

            public void WaitForDisconnect()
            {
                disconnectLatch.Wait(TimeSpan.FromMinutes(2));
            }
        }

        [Test]
        public void ShouldParseEventsCorrectly()
        {
            server
                .Given(Request.Create().WithPath("/api/1.0/stream").UsingGet())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithBody(@"data: { ""domain"": ""flag"",  ""event"": ""patch"",  ""identifier"": ""flagid"",  ""version"": ""0""}, 
                                    data: { ""domain"": ""flag"",  ""event"": ""patch"",  ""identifier"": ""flagid"",  ""version"": ""1""}
                                    ")
                );

            var callback = new TestCallback();
            Config config = new ConfigBuilder().ConfigUrl(server.Url + "/api/1.0").Build();
            var httpClient = SseHttpClient(config, "dummyapikey");
            var eventSource = new EventSource(httpClient, "stream", config, callback, new NullLoggerFactory());
            eventSource.Start();

            callback.WaitForDisconnect();

            Assert.That(callback.ConnectCount, Is.EqualTo(1));
            Assert.That(callback.UpdateCount, Is.EqualTo(2));
            Assert.That(callback.DisconnectCount, Is.EqualTo(1));

            for (int i = 0; i < 2; i++)
            {
                Assert.That(callback.Events[i].Event, Is.EqualTo("patch"));
                Assert.That(callback.Events[i].Domain, Is.EqualTo("flag"));
                Assert.That(callback.Events[i].Identifier, Is.EqualTo("flagid"));
                Assert.That(callback.Events[i].Version, Is.EqualTo(i));
            }
        }
        
        [Test]
        public void ShouldNotHangWhenStreamIsDisconnected()
        {
            server
                .Given(Request.Create().WithPath("/api/1.0/stream").UsingGet())
                .RespondWith(
                    Response.Create()
                        .WithStatusCode(200)
                        .WithBody(@"data: { ""domain"": ""flag"",  ""event"": ""patch"",  ""identifier"": ""flagid"",  ""version"": ""0""}, 
                                    data: { ""domain"": ""flag"",  ""event"": ""patch"",  ""identifier"": ""flagid"",  ""version"": ""1""}
                                    ")
                        
                );

            server
                .Given(Request.Create().WithPath("/api/1.0/stream").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithFault(FaultType.MALFORMED_RESPONSE_CHUNK));

            var callback = new TestCallback();
            Config config = new ConfigBuilder().ConfigUrl(server.Url + "/api/1.0").Build();
            var httpClient = SseHttpClient(config, "dummyapikey");
            var eventSource = new EventSource(httpClient, "stream", config, callback, new NullLoggerFactory());
            eventSource.Start();

            callback.WaitForDisconnect();

            Assert.That(callback.ConnectCount, Is.EqualTo(1));
            Assert.That(callback.UpdateCount, Is.EqualTo(0));
            Assert.That(callback.DisconnectCount, Is.EqualTo(1));
        }

        private static HttpClient SseHttpClient(Config config, string apiKey)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(config.ConfigUrl.EndsWith("/") ? config.ConfigUrl : config.ConfigUrl + "/" );
            client.DefaultRequestHeaders.Add("API-Key", apiKey);
            client.DefaultRequestHeaders.Add("Accept", "text /event-stream");
            client.Timeout = TimeSpan.FromMinutes(1);
            return client;
        }
    }
}