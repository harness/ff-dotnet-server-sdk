using System;
using io.harness.cfsdk.client.api;
using NUnit.Framework;

namespace ff_server_sdk_test
{
    public class ConfigTests
    {
        [Test]
        public void DefaultConfigAnalyticsSetCorrectly()
        {
            //Arrange
            //Config config = Config.Builder().Build();
            var config = new Config();

            //Check default values
            Assert.IsTrue(config.AnalyticsEnabled);
            Assert.IsTrue(config.StreamEnabled);
            Assert.IsFalse(config.Debug);
            Assert.IsTrue(config.ConfigUrl == "https://config.ff.harness.io/api/1.0");
            Assert.IsTrue(config.EventUrl == "https://events.ff.harness.io/api/1.0");

            //Check analytics setter
            Config configset = Config.Builder().SetAnalyticsEnabled(false).SetStreamEnabled(false).Build();

            Assert.IsFalse(configset.AnalyticsEnabled);
            Assert.IsFalse(configset.StreamEnabled);
        }
    }
}