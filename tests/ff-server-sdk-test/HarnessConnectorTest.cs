using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.connector;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using Client = io.harness.cfsdk.HarnessOpenAPIService.Client;

namespace ff_server_sdk_test {
    
    [TestFixture]
    public class HarnessConnectorTest
    {
        string fakeJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9" +
            ".eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyLCJlbnZpcm9ubWVudCI6InRlc3QiLCJjbHVzdGVySWRlbnRpZmllciI6InRlc3QiLCJhY2NvdW50SUQiOiJ0ZXN0In0" +
            ".MVFJ6Sd0AObZkg3LxKYU9EBMn-t40tPJ-tFd0Ch5EiU";

        public class TestCallback : IConnectionCallback
        {
            public virtual void OnReauthenticateRequested()
            {
            }
        }

        HttpClient MockedHttpClient(params HttpResponseMessage[] responses)
        {
            var mockMessageHandler = new Mock<HttpMessageHandler>();
            var sequence = mockMessageHandler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"authToken\": \"" + fakeJwt + "\"}")
                });
            responses.ToList().ForEach(response => sequence.ReturnsAsync(response));
            return new HttpClient(mockMessageHandler.Object);
        }
        
        
        
        [Test]
        public async Task ShouldReAuthWhenGetFlagReturns403()
        {
            //Arrange
            var mockHttpClient = MockedHttpClient(new HttpResponseMessage { StatusCode = HttpStatusCode.Forbidden });
            var client = new Client(mockHttpClient);
            client.BaseUrl = "http://dummy:1234";
            var mockCallback = new Mock<TestCallback>();
            var connector = new HarnessConnector("test", new Config(), mockCallback.Object, new HttpClient(), new HttpClient(), new HttpClient(), client, new NullLoggerFactory());
            await connector.Authenticate();
  
            //Act
            var exception = Assert.ThrowsAsync<CfClientException>(async () => await connector.GetFlag("test"));
            
            //Assert
            Assert.That(exception!.Message.Contains("The HTTP status code of the response was not expected (403)"));
            mockCallback.Verify(it => it.OnReauthenticateRequested());
        }
        
        [Test]
        public async Task ShouldReAuthWhenGetFlagsReturns403()
        {
            //Arrange
            var mockHttpClient = MockedHttpClient(new HttpResponseMessage { StatusCode = HttpStatusCode.Forbidden });
            var client = new Client(mockHttpClient);
            var mockCallback = new Mock<TestCallback>();
            var connector = new HarnessConnector("test", new Config(), mockCallback.Object, new HttpClient(), new HttpClient(), new HttpClient(), client, new NullLoggerFactory());
            await connector.Authenticate();

            //Act
            var exception = Assert.ThrowsAsync<CfClientException>(async () => await connector.GetFlags());

            //Assert
            Assert.That(exception!.Message.Contains("The HTTP status code of the response was not expected (403)"));
            mockCallback.Verify(it => it.OnReauthenticateRequested());
        }

        [Test]
        public async Task ShouldNotReAuthWhenGetFlagReturns400()
        {
            //Arrange
            var mockHttpClient = MockedHttpClient(new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest });
            var client = new Client(mockHttpClient);
            client.BaseUrl = "http://dummy:1234";
            var mockCallback = new Mock<TestCallback>();
            var connector = new HarnessConnector("test", new Config(), mockCallback.Object, new HttpClient(), new HttpClient(), new HttpClient(), client, new NullLoggerFactory());
            await connector.Authenticate();
        
            //Act
            var exception = Assert.ThrowsAsync<CfClientException>(async () => await connector.GetFlag("test"));
            
            //Assert
            Assert.That(exception!.Message.Contains("The HTTP status code of the response was not expected (400)"));
            mockCallback.Verify(it => it.OnReauthenticateRequested(), Times.Never);
        }
    }
}