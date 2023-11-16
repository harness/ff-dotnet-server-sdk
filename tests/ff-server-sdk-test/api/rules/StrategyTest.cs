using io.harness.cfsdk.client.api.rules;
using NUnit.Framework;

namespace ff_server_sdk_test
{
    public class StrategyTest
    {
        [Test]
        public void CheckBucket57IsMatchingCorrectly()
        {
            Strategy strategy = new Strategy("test", "identifier");
            Assert.AreEqual(57, strategy.loadNormalizedNumber());
        }
    }
}