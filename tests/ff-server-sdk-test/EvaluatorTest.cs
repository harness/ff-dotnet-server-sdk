using System;
using System.Collections.Generic;
using System.IO;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ff_server_sdk_test
{
    public class EvaluatorTest: IEvaluatorCallback
    {
        private List<TestModel> testData = new List<TestModel>();
        private List<TestResult> testResults = new List<TestResult>();
        private IRepository repository;
        private ICache cache;

        private Evaluator evaluator;
        private string noTarget = "_no_target";
        [SetUp]
        public void Setup()
        {

            this.cache = new FeatureSegmentCache();
            this.repository = new StorageRepository(this.cache, null, null);
            this.evaluator = new Evaluator(this.repository, this);

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
                }
           });
        }

        [Test]
        public void ProccessEvaluations()
        {
            testData.ForEach(t => ProcessTest(t));
        }

        private void ProcessTest(TestModel model)
        {
            Assert.DoesNotThrow(() =>
            {
                this.repository.SetFlag(model.flag.Feature, model.flag);
                if (model.segments != null)
                {
                    model.segments.ForEach(s =>
                    {
                        this.repository.SetSegment(s.Identifier, s);
                    });
                }

                foreach (var item in model.expected)
                {
                    this.testResults.Add(new TestResult() { Identifier = item.Key, TestModel = model, Result = item.Value });
                }


                foreach (var testResult in this.testResults)
                {

                    io.harness.cfsdk.client.dto.Target target = null;
                    if (!testResult.Identifier.Equals(noTarget))
                    {
                        if (testResult.TestModel.targets != null)
                        {
                            target = testResult.TestModel.targets.Find(t => { return t.Identifier == testResult.Identifier; });
                        }
                    }
                    string feature = testResult.TestModel.flag.Feature;
                    switch (testResult.TestModel.flag.Kind)
                    {
                        case FeatureConfigKind.Boolean:
                            bool res = evaluator.BoolVariation(feature, target, false);
                            Assert.AreEqual(res, testResult.Result, $"Expected result for {feature} was {testResult.Result}");
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
            });
        }

        public void evaluationProcessed(FeatureConfig featureConfig, io.harness.cfsdk.client.dto.Target target, Variation variation)
        {
            var targetName = target != null ? target.Name : noTarget;
            Serilog.Log.Information( $"processEvaluation {featureConfig.Feature}, {targetName}, {variation.Value} ");
        }
    }
}
