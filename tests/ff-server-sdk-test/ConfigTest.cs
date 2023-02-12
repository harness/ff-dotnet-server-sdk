using System;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.cache;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace ff_server_sdk_test
{
    public class ConfigTests
    {
        [Test]
        public void DefaultConfigIsCorrect()
        {
            // Arrange, Act
            var config = Config.Builder().Build();

            // Assert
            Assert.IsTrue(config.AnalyticsEnabled);
            Assert.IsTrue(config.StreamEnabled);
            Assert.IsFalse(config.Debug);
            Assert.AreEqual("https://config.ff.harness.io/api/1.0", config.ConfigUrl);
            Assert.AreEqual("https://events.ff.harness.io/api/1.0", config.EventUrl);
            Assert.AreEqual(10, config.MaxAuthRetries);
            Assert.AreEqual(1024, config.BufferSize);
            Assert.IsNotNull(config.Cache);
            Assert.AreEqual(10000, config.ConnectionTimeout);
            Assert.AreEqual(60, config.Frequency);
            Assert.AreEqual(10000, config.MetricsServiceAcceptableDuration);
            Assert.AreEqual(60, config.PollIntervalInSeconds);
            Assert.AreEqual(60000, config.PollIntervalInMiliSeconds);
            Assert.AreEqual(30000, config.ReadTimeout);
            Assert.IsNull(config.Store);
            Assert.AreEqual(10000, config.WriteTimeout);
            Assert.IsNull(config.LoggerFactory);
        }

        [Test]
        public void BuilderIsCorrect()
        {
            // Arrange
            var defCfg = Config.Builder().Build();
            var mockCache = new Mock<ICache>().Object;
            var mockStore = new Mock<IStore>().Object;
            var mockLoggerFactory = new Mock<ILoggerFactory>().Object;

            // Act
            var config = Config.Builder()
                .SetAnalyticsEnabled(!defCfg.AnalyticsEnabled)
                .SetStreamEnabled(!defCfg.StreamEnabled)
                .debug(!defCfg.Debug)
                .ConfigUrl("https://config.example.com")
                .EventUrl("https://events.example.com")
                //.SetMaxAuthRetries(defCfg.MaxAuthRetries + 1)
                //.SetBufferSize(defCfg.BufferSize * 2)
                .SetCache(mockCache)
                .connectionTimeout(defCfg.ConnectionTimeout + 1)
                //.SetFrequency(defCfg.Frequency + 1)
                .MetricsServiceAcceptableDuration(defCfg.MetricsServiceAcceptableDuration + 1)
                .SetPollingInterval(defCfg.PollIntervalInSeconds + 1)
                .readTimeout(defCfg.ReadTimeout + 1)
                .SetStore(mockStore)
                .writeTimeout(defCfg.WriteTimeout + 1)
                .SetLoggerFactory(mockLoggerFactory)
                .Build();

            // Assert
            Assert.AreEqual(!defCfg.AnalyticsEnabled, config.AnalyticsEnabled);
            Assert.AreEqual(!defCfg.StreamEnabled, config.StreamEnabled);
            Assert.AreEqual(!defCfg.Debug, config.Debug);
            Assert.AreEqual("https://config.example.com", config.ConfigUrl);
            Assert.AreEqual("https://events.example.com", config.EventUrl);
            //Assert.AreEqual(defCfg.MaxAuthRetries + 1, config.MaxAuthRetries);
            //Assert.AreEqual(defCfg.BufferSize * 2, config.BufferSize);
            Assert.AreSame(mockCache, config.Cache);
            Assert.AreEqual(defCfg.ConnectionTimeout + 1, config.ConnectionTimeout);
            //Assert.AreEqual(defCfg.Frequency + 1, config.Frequency);
            Assert.AreEqual(defCfg.MetricsServiceAcceptableDuration + 1, config.MetricsServiceAcceptableDuration);
            Assert.AreEqual(defCfg.PollIntervalInSeconds + 1, config.PollIntervalInSeconds);
            Assert.AreEqual(defCfg.PollIntervalInMiliSeconds + 1000, config.PollIntervalInMiliSeconds);
            Assert.AreEqual(defCfg.ReadTimeout + 1, config.ReadTimeout);
            Assert.AreSame(mockStore, config.Store);
            Assert.AreEqual(defCfg.WriteTimeout + 1, config.WriteTimeout);
            Assert.AreSame(mockLoggerFactory, config.LoggerFactory);
        }

        [Test]
        public void WithDefaultLoggerFactory()
        {
            // Arrange, Act
            var defCfg = Config.Builder().Build();
            var defLogger = defCfg.CreateLogger<ICache>();

            // Assert
            Assert.AreSame(Config.DefaultLogger, defLogger);
            Assert.IsFalse(defLogger.IsEnabled(LogLevel.Critical));
        }

        [Test]
        public void WithCustomLoggerFactory()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>(MockBehavior.Loose);
            var mockLoggerFactory = new Mock<ILoggerFactory>(MockBehavior.Strict);
            mockLoggerFactory
                .Setup(m => m.CreateLogger(It.IsAny<string>()))
                .Returns(mockLogger.Object);

            // Act
            var customCfg = Config.Builder()
                .SetLoggerFactory(mockLoggerFactory.Object)
                .Build();

            var customLogger = customCfg.CreateLogger<ICache>();

            // Assert
            mockLoggerFactory.Verify(m => m.CreateLogger(typeof(ICache).FullName), Times.Once);

            Assert.NotNull(customLogger);
            //Assert.AreSame(mockLogger.Object, customLogger);
            _ = customLogger.IsEnabled(LogLevel.Information);
            mockLogger.Verify(m => m.IsEnabled(LogLevel.Information), Times.Once);
        }
    }
}