using System;
using System.Threading;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.connector;
using Moq;
using NUnit.Framework;

namespace ff_server_sdk_test
{
    internal class FakeAuth : IAuthCallback
    {
        private SemaphoreSlim doneEvent = new SemaphoreSlim(0);

        public async Task<bool> WaitForSuccess(int timeout)
        {
            return await doneEvent.WaitAsync(timeout);
        }

        public void OnAuthenticationSuccess()
        {
            doneEvent.Release();
        }
    }

    [TestFixture, Timeout(100), Parallelizable()]
    public class InnerClient
    {
        [Test, Timeout(1000)]
        public async Task shouldOnlyAuthenticateOnceIfSuccessful()
        {
            // Arrange
            var callback = new FakeAuth();
            var mockConnector = new Mock<IConnector>();
            mockConnector
                .Setup(a => a.Authenticate())
                .ReturnsAsync("Done");

            // Act
            var a = new AuthService(mockConnector.Object, new Config { PollIntervalInMiliSeconds = 50 }, callback);
            a.Start();

            // Assert
            Assert.IsTrue(await callback.WaitForSuccess(1000));
            mockConnector.Verify(it => it.Authenticate(), Times.Exactly(1));
        }

        [Test, Timeout(1000)]
        public async Task shouldOnlyAuthenticateThreeTimesUntilSuccessful()
        {
            // Assert
            var callback = new FakeAuth();
            var mockConnector = new Mock<IConnector>();
            mockConnector
                .SetupSequence(a => a.Authenticate())
                .ThrowsAsync(new Exception("ONE"))
                .ThrowsAsync(new Exception("ONE"))
                .ReturnsAsync("DONE");

            // Act
            var a = new AuthService(mockConnector.Object, new Config { PollIntervalInMiliSeconds = 50 }, callback);
            a.Start();

            // Verify
            Assert.IsTrue(await callback.WaitForSuccess(1000));
            mockConnector.Verify(it => it.Authenticate(), Times.Exactly(3));
        }

        [Test, Timeout(1000)]
        public async Task shouldFailToAuthenticateWhenMaxRetriesReached()
        {
            // Assert
            var callback = new FakeAuth();
            var mockConnector = new Mock<IConnector>();
            mockConnector
                .SetupSequence(a => a.Authenticate())
                .ThrowsAsync(new Exception("ONE"))
                .ThrowsAsync(new Exception("ONE"))
                .ReturnsAsync("DONE");

            // Act
            var a = new AuthService(mockConnector.Object, new Config { PollIntervalInMiliSeconds = 50, MaxAuthRetries = 1 }, callback);
            a.Start();

            // Verify
            Assert.IsFalse(await callback.WaitForSuccess(200));
            mockConnector.Verify(it => it.Authenticate(), Times.Exactly(2));
        }
    }
}