using System;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.connector;
using Moq;
using NUnit.Framework;

namespace ff_server_sdk_test
{
    internal class FakeAuth : IAuthCallback
    {
        public void OnAuthenticationSuccess()
        {

        }
    }
    [TestFixture]
    public class InnerClient
    {
        [Test]
        public async Task shouldOnlyAuthenticateOnceIfSuccessful()
        {
            // Assert
            var mockConnector = new Mock<IConnector>();
            mockConnector
                .Setup(a => a.Authenticate())
                .ReturnsAsync("Done");
            // Act
            var a = new AuthService(mockConnector.Object, new Config { PollIntervalInMiliSeconds = 1000 }, new FakeAuth());
            a.Start();

            // Verify
            await Task.Delay(3000);
            mockConnector.Verify(it => it.Authenticate(), Times.Exactly(1));
        }

        [Test]
        public async Task shouldOnlyAuthenticateThreeTimesUntilSuccessful()
        {
            // Assert
            var mockConnector = new Mock<IConnector>();
            mockConnector
                .SetupSequence(a => a.Authenticate())
                .ThrowsAsync(new Exception("ONE"))
                .ThrowsAsync(new Exception("ONE"))
                .ReturnsAsync("DONE");

            // Act
            var a = new AuthService(mockConnector.Object, new Config { PollIntervalInMiliSeconds = 1000 }, new FakeAuth());
            a.Start();

            // Verify
            await Task.Delay(5000);
            mockConnector.Verify(it => it.Authenticate(), Times.Exactly(3));
        }

        [Test]
        public async Task shouldFailToAuthenticateWhenMaxRetriesReached()
        {
            // Assert
            var mockConnector = new Mock<IConnector>();
            mockConnector
                .SetupSequence(a => a.Authenticate())
                .ThrowsAsync(new Exception("ONE"))
                .ThrowsAsync(new Exception("ONE"))
                .ReturnsAsync("DONE");

            // Act
            var a = new AuthService(mockConnector.Object, new Config { PollIntervalInMiliSeconds = 1000, MaxAuthRetries = 1 }, new FakeAuth());
            a.Start();

            // Verify
            await Task.Delay(5000);
            mockConnector.Verify(it => it.Authenticate(), Times.Exactly(2));
        }
    }
}