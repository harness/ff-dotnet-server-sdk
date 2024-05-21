using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

[SetUpFixture]
public class SetupTracing
{
    [OneTimeSetUp]
    public void StartTest()
    {
        Trace.Listeners.Add(new ConsoleTraceListener());
    }

    [OneTimeTearDown]
    public void EndTest()
    {
        Trace.Flush();
    }
}

namespace ff_server_sdk_test
{
    public class EvaluatorListener : IEvaluatorCallback
    {
        public void EvaluationProcessed(FeatureConfig featureConfig, io.harness.cfsdk.client.dto.Target target,
            Variation variation)
        {
            var targetName = target != null ? target.Name : "_no_target";
            Console.WriteLine($"processEvaluation {featureConfig.Feature}, {targetName}, {variation.Value} ");
        }
    }



    [TestFixture]
    public class EvaluatorTest
    {
        private static IRepository repository;
        private static ICache cache;

        private static Evaluator evaluator;
        private string noTarget = "_no_target";

        // Initial Evaluator test setup
        static EvaluatorTest()
        {
            var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
            var listener = new EvaluatorListener();
            var config = Config.Builder().UseMapForInClause(true).Build();
            cache = new FeatureSegmentCache();
            repository = new StorageRepository(cache, null, null, loggerFactory, config);
            evaluator = new Evaluator(repository, listener, loggerFactory, true, null, null);
        }

        private static void LoadSegments(List<Segment> segments)
        {
            if (segments != null)
            {
                segments.ForEach(segment => { repository.SetSegment(segment.Identifier, segment); });
            }
        }

        private static void LoadFlags(List<FeatureConfig> flags)
        {
            if (flags != null)
            {
                flags.ForEach(flag => { repository.SetFlag(flag.Feature, flag); });
            }
        }
        
        
        private static FeatureConfig FindFeatureConfig(string flagName, List<FeatureConfig> flags)
        {
            foreach (FeatureConfig nextFlag in flags)
            {
                if (nextFlag.Feature.Equals(flagName))
                {
                    return nextFlag;
                }
            }
            Assert.Fail("Could not find feature flag " + flagName);
            return null;
        }


        private static List<string> GetTree(string path, string searchPattern)
        {
            List<string> found = new List<string>();
            foreach (string file in Directory.GetFiles(path, searchPattern))
            {
                found.Add(file);
            }

            foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
            {
                List<string> subFound = GetTree(dir, searchPattern);
                found.AddRange(subFound);
            }

            return found;
        }

        private static IEnumerable<TestCaseData> GenerateTestCases()
        {
            string baseTestPath = Path.GetFullPath("./ff-test-cases/tests/");

            foreach (string fileName in GetTree(baseTestPath, "*"))
            {
                Console.WriteLine("Processing " + fileName);

                var testModel = JsonConvert.DeserializeObject<TestModel>(File.ReadAllText(fileName),
                    new JsonSerializerSettings
                    {
                        ContractResolver = new LenientContractResolver()
                    });

                Assert.NotNull(testModel);
                Assert.NotNull(testModel.tests);

                foreach (Dictionary<string, object> nextTest in testModel.tests)
                {
                    object expected = nextTest.GetValueOrDefault("expected");
                    string target = (string)nextTest.GetValueOrDefault("target", null); // May be null
                    string flag = (string)nextTest.GetValueOrDefault("flag");
                    FeatureConfig feature = FindFeatureConfig(flag, testModel.flags);

                    LoadSegments(testModel.segments);
                    LoadFlags(testModel.flags);

                    string nUnitTestName = fileName.Replace(baseTestPath, "ff-test-cases ").Replace(".json", "");

                    nUnitTestName += "__with_flag_" + flag;
                    if (target != null)
                    {
                        nUnitTestName += "__with_target_" + target;
                    }

                    yield return new TestCaseData(nUnitTestName, target, expected, flag, feature.Kind, testModel);
                }
            }
        }

        [Test, Category("Evaluation Testing"), TestCaseSource("GenerateTestCases")]
        public void ExecuteTestCases(string testName, string targetIdentifier, object expected, string featureFlag, FeatureConfigKind kind, TestModel testModel)
        {
            io.harness.cfsdk.client.dto.Target target = null;
            if (!noTarget.Equals(targetIdentifier))
            {
                if (testModel.targets != null)
                {
                    target = testModel.targets.Find(t => { return t.Identifier == targetIdentifier; });
                }
            }

            object got = null;
            
            switch (kind)
            {
                case FeatureConfigKind.Boolean:
                    got = evaluator.BoolVariation(featureFlag, target, false);
                    break;
                case FeatureConfigKind.Int:
                    got = evaluator.NumberVariation(featureFlag, target, 0);
                    break;
                case FeatureConfigKind.String:
                    got = evaluator.StringVariation(featureFlag, target, "");
                    break;
                case FeatureConfigKind.Json:
                    got = evaluator.JsonVariation(featureFlag, target, JObject.Parse("{val: 'default value'}"));
                    break;
            }

            Debug.Print("    TEST : {0}", testName);
            Debug.Print("    FLAG : {0}", featureFlag);
            Debug.Print("  TARGET : {0} ", targetIdentifier ?? "(none)");
            Debug.Print("EXPECTED : {0} ({1})", expected, expected.GetType().Name);
            Debug.Print("     GOT : {0} ({1})", got.ToString().Replace("\n", ""), got.GetType().Name);

            if (kind == FeatureConfigKind.Json)
            {
                var expectedJson = JObject.Parse((string)expected);
                Assert.AreEqual(expectedJson, got, $"Expected result for {featureFlag} was {expected}");
            }
            else
            {
                Assert.AreEqual(expected, got, $"Expected result for {featureFlag} was {expected}");
            }
        }

        // if (target.attr.email endswith '@harness.io' && target.attr.role = 'developer')
        [TestCase("boolflag_and", "email_is_dev","user@harness.io","developer", true)]
        [TestCase("boolflag_and", "email_is_mgr","user@harness.io","manager", false)]
        [TestCase("boolflag_and", "external_email_is_dev","user@gmail.com","developer", false)]
        [TestCase("boolflag_and", "external_email_is_mgr","user@gmail.com","manager", false)]
        // if (target.attr.email endswith '@harness.io' || target.attr.email endswith '@somethingelse.com')
        [TestCase("boolflag_or", "email_is_harness","user@harness.io","n/a", true)]
        [TestCase("boolflag_or", "email_is_something_else","user@somethingelse.com","n/a", true)]
        [TestCase("boolflag_or", "email_is_gmail","user@gmail.com","n/a", false)]
        public void TestV2Rules_And(string flagName, string name, string email, string role, bool expected)
        {
            LoadFlags(repository, "./local-test-cases/v2-andor-flags.json");
            LoadSegments(repository, "./local-test-cases/v2-andor-segments.json");

            var target = io.harness.cfsdk.client.dto.Target.builder()
                .Name(name)
                .Identifier(name)
                .Attributes(new Dictionary<string, string> {{"email", email}, {"role", role}})
                .build();

            bool result = evaluator.BoolVariation(flagName, target, false);

            Assert.AreEqual(expected, result);
        }

        private void LoadFlags(IRepository repo, String fileName)
        {
            var json = File.ReadAllText(fileName);
            var testModel = JsonConvert.DeserializeObject<List<FeatureConfig>>(json,
                new JsonSerializerSettings
                {
                    ContractResolver = new LenientContractResolver()
                });

            repo.SetFlags(testModel);
        }

        private void LoadSegments(IRepository repo, String fileName)
        {
            var json = File.ReadAllText(fileName);
            var testModel = JsonConvert.DeserializeObject<List<Segment>>(json,
                new JsonSerializerSettings
                {
                    ContractResolver = new LenientContractResolver()
                });

            repo.SetSegments(testModel);
        }

    }
}
