using io.harness.cfsdk.client.api.rules;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace ff_server_sdk_test
{
    public class StrategyTest
    {
        [Test]
        public void CheckBucket57IsMatchingCorrectly()
        {
            Strategy strategy = new Strategy("test", "identifier", new NullLoggerFactory());
            Assert.AreEqual(57, strategy.loadNormalizedNumber());
        }
    }
}