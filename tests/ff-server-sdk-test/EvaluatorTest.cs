using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ff_server_sdk_test
{
    public class EvaluatorListener : IEvaluatorCallback
    {
        public void evaluationProcessed(FeatureConfig featureConfig, io.harness.cfsdk.client.dto.Target target, Variation variation)
        {
            var targetName = target != null ? target.Name : "_no_target";
            Serilog.Log.Information($"processEvaluation {featureConfig.Feature}, {targetName}, {variation.Value} ");
        }
    }
    [TestFixture]
    public class EvaluatorTest
    {
        private static List<TestModel> testData = new List<TestModel>();
        private static IRepository repository;
        private static ICache cache;

        private static Evaluator evaluator;
        private string noTarget = "_no_target";

        // Initial Evaluator test setup
        static EvaluatorTest()
        {
            var listener = new EvaluatorListener();
            cache = new FeatureSegmentCache();
            repository = new StorageRepository(cache, null, null);
            evaluator = new Evaluator(repository, listener);

            Assert.DoesNotThrow(() =>
            {
                foreach (string fileName in Directory.GetFiles("./ff-test-cases/tests", "*.json"))
                {
                    var testModel = JsonConvert.DeserializeObject<TestModel>(File.ReadAllText(fileName));
                    Assert.NotNull(testModel);

                    string name = Path.GetFileName(fileName);
                    string feature = testModel.flag.Feature + name;
                    testModel.flag.Feature = feature;
                    testModel.testFile = name;

                    testData.Add(testModel);

                    repository.SetFlag(testModel.flag.Feature, testModel.flag);
                    if (testModel.segments != null)
                    {
                        testModel.segments.ForEach(s =>
                        {
                            repository.SetSegment(s.Identifier, s);
                        });
                    }
                }
            });
        }

        private static IEnumerable<TestCaseData> GenerateTestCases()
        {

            foreach (var test in testData)
            {
                foreach (var item in test.expected)
                {
                    yield return new TestCaseData(test.flag.Feature, item.Key, item.Value, test);
                }
            }
        }

        [Test, Category("Evaluation Testing"), TestCaseSource("GenerateTestCases")]
        public void ExecuteTestCases(string f, string identifier, bool result, TestModel test)
        {
            io.harness.cfsdk.client.dto.Target target = null;
            if (!identifier.Equals(noTarget))
            {
                if (test.targets != null)
                {
                    target = test.targets.Find(t => { return t.Identifier == identifier; });
                }
            }
            string feature = test.flag.Feature;
            switch (test.flag.Kind)
            {
                case FeatureConfigKind.Boolean:
                    bool res = evaluator.BoolVariation(feature, target, false);
                    Assert.AreEqual(res, result, $"Expected result for {feature} was {result}");
                    break;
                case FeatureConfigKind.Int:
                    double resInt = evaluator.NumberVariation(feature, target, 0);
                    break;
                case FeatureConfigKind.String:
                    string resStr = evaluator.StringVariation(feature, target, "");
                    break;
                case FeatureConfigKind.Json:
                    JObject resObj = evaluator.JsonVariation(feature, target, JObject.Parse("{val: 'default value'}"));
                    break;
            }
        }

    }
}
