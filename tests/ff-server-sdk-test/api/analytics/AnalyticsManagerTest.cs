using System.Collections.Generic;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.api.analytics;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Target = io.harness.cfsdk.client.dto.Target;

namespace ff_server_sdk_test.api.analytics
{
    [TestFixture]
    public class AnalyticsManagerTests
    {
        [Test]
        public void Should_add_single_evaluation_and_target_for_single_feature_to_analytics_cache()
        {
            var evaluationAnalyticsCacheMock = new EvaluationAnalyticsCache();
            var targetAnalyticsCacheMock = new TargetAnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var analyticsPublisherServiceMock =
                new AnalyticsPublisherService(connectorMock.Object, evaluationAnalyticsCacheMock,
                    targetAnalyticsCacheMock, new NullLoggerFactory(), new Config());

            var variation = new Variation();
            var target = Target.builder()
                .Identifier("identifier")
                .Attributes(new Dictionary<string, string> { { "email", "demo@harness.io" } })
                .build(); 
            
            var featureConfig1 = CreateFeatureConfig("feature1");
            var evaluationAnalytics = new EvaluationAnalytics(featureConfig1, variation, target);
            var targetAnalytics = new TargetAnalytics(target);

            var sut = new MetricsProcessor(new Config(), evaluationAnalyticsCacheMock, targetAnalyticsCacheMock,
                analyticsPublisherServiceMock,
                new NullLoggerFactory(), false);


            sut.PushToCache(target, featureConfig1, variation);

            // Ensure the cache totals are correct
            Assert.That(evaluationAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));
            Assert.That(targetAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));


            // Ensure the cache total and breakdown of the type of analytics is correct
            Assert.That(evaluationAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));

            // Ensure the counter is correct.
            Assert.That(evaluationAnalyticsCacheMock.getIfPresent(evaluationAnalytics), Is.EqualTo(1));
            Assert.That(targetAnalyticsCacheMock.getIfPresent(targetAnalytics), Is.EqualTo(true));
        }
        
        [Test]
        public void Should_add_single_evaluation_and_no_target_for_null_target()
        {
            var evaluationAnalyticsCacheMock = new EvaluationAnalyticsCache();
            var targetAnalyticsCacheMock = new TargetAnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var analyticsPublisherServiceMock =
                new AnalyticsPublisherService(connectorMock.Object, evaluationAnalyticsCacheMock,
                    targetAnalyticsCacheMock, new NullLoggerFactory(), new Config());

            var variation = new Variation();

            var featureConfig1 = CreateFeatureConfig("feature1");
            var evaluationAnalytics = new EvaluationAnalytics(featureConfig1, variation, null);
            var targetAnalytics = new TargetAnalytics(null);

            var sut = new MetricsProcessor(new Config(), evaluationAnalyticsCacheMock, targetAnalyticsCacheMock,
                analyticsPublisherServiceMock,
                new NullLoggerFactory(), false);


            sut.PushToCache(null, featureConfig1, variation);

            // Ensure the cache totals are correct
            Assert.That(evaluationAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));
            Assert.That(targetAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(0));


            // Ensure the cache total and breakdown of the type of analytics is correct
            Assert.That(evaluationAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));

            // Ensure the counter is correct.
            Assert.That(evaluationAnalyticsCacheMock.getIfPresent(evaluationAnalytics), Is.EqualTo(1));
            Assert.That(targetAnalyticsCacheMock.getIfPresent(targetAnalytics), Is.EqualTo(false));
        }


        [Test]
        public void Should_add_multiple_evaluations_for_single_feature_to_analytics_cache()
        {
            var evaluationAnalyticsCacheMock = new EvaluationAnalyticsCache();
            var targetAnalyticsCacheMock = new TargetAnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var analyticsPublisherServiceMock =
                new AnalyticsPublisherService(connectorMock.Object, evaluationAnalyticsCacheMock,
                    targetAnalyticsCacheMock, new NullLoggerFactory(), new Config());

            var target = Target.builder()
                .Identifier("identifier")
                .Attributes(new Dictionary<string, string> { { "email", "demo@harness.io" } })
                .build();             var variation = new Variation();

            // simulate multiple evaluations for a single feature
            var featureConfig = CreateFeatureConfig("feature1");
            var evaluationAnalytics = new EvaluationAnalytics(featureConfig, variation, target);
            var targetAnalytics = new TargetAnalytics(target);

            var sut = new MetricsProcessor(new Config(), evaluationAnalyticsCacheMock, targetAnalyticsCacheMock,
                analyticsPublisherServiceMock,
                new NullLoggerFactory(), false);

            sut.PushToCache(target, featureConfig, variation);
            sut.PushToCache(target, featureConfig, variation);
            sut.PushToCache(target, featureConfig, variation);
            sut.PushToCache(target, featureConfig, variation);
            sut.PushToCache(target, featureConfig, variation);


            // Ensure the cache totals are correct
            Assert.That(evaluationAnalyticsCacheMock.Count, Is.EqualTo(1));
            Assert.That(targetAnalyticsCacheMock.Count, Is.EqualTo(1));


            // Correct count
            Assert.That(evaluationAnalyticsCacheMock.getIfPresent(evaluationAnalytics), Is.EqualTo(5));
            Assert.That(targetAnalyticsCacheMock.getIfPresent(targetAnalytics), Is.EqualTo(true));
        }

        [Test]
        public void Should_add_single_evaluation_for_multiple_features_to_analytics_cache()
        {
            // Arrange
            var evaluationAnalyticsCacheMock = new EvaluationAnalyticsCache();
            var targetAnalyticsCacheMock = new TargetAnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var analyticsPublisherServiceMock =
                new AnalyticsPublisherService(connectorMock.Object, evaluationAnalyticsCacheMock,
                    targetAnalyticsCacheMock, new NullLoggerFactory(), new Config());

            var target = Target.builder()
                .Identifier("identifier")
                .Attributes(new Dictionary<string, string> { { "email", "demo@harness.io" } })
                .build();             var variation = new Variation();

            // simulate an evaluation for multiple different features
            var featureConfig1 = CreateFeatureConfig("feature1");
            var featureConfig2 = CreateFeatureConfig("feature2");
            var evaluationAnalytics = new EvaluationAnalytics(featureConfig1, variation, target);
            var evaluationAnalytics2 = new EvaluationAnalytics(featureConfig2, variation, target);
            var targetAnalytics = new TargetAnalytics(target);

            var sut = new MetricsProcessor(new Config(), evaluationAnalyticsCacheMock, targetAnalyticsCacheMock,
                analyticsPublisherServiceMock,
                new NullLoggerFactory(), false);

            // Act
            sut.PushToCache(target, featureConfig1, variation);
            sut.PushToCache(target, featureConfig2, variation);

            // Ensure the cache totals are correct
            Assert.That(evaluationAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(2));
            Assert.That(targetAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));

            // Ensure the counter is correct
            Assert.That(evaluationAnalyticsCacheMock.getIfPresent(evaluationAnalytics), Is.EqualTo(1));
            Assert.That(evaluationAnalyticsCacheMock.getIfPresent(evaluationAnalytics2), Is.EqualTo(1));
            Assert.That(targetAnalyticsCacheMock.getIfPresent(targetAnalytics), Is.EqualTo(true));
        }


        [Test]
        public void Should_add_multiple_evaluations_for_multiple_features_to_analytics_cache()
        {
            // Arrange
            var evaluationAnalyticsCacheMock = new EvaluationAnalyticsCache();
            var targetAnalyticsCacheMock = new TargetAnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var analyticsPublisherServiceMock =
                new AnalyticsPublisherService(connectorMock.Object, evaluationAnalyticsCacheMock,
                    targetAnalyticsCacheMock, new NullLoggerFactory(), new Config());

            var target = Target.builder()
                .Identifier("identifier")
                .Attributes(new Dictionary<string, string> { { "email", "demo@harness.io" } })
                .build(); 
            var variation = new Variation();

            // simulate an evaluation for multiple different features
            var featureConfig1 = CreateFeatureConfig("feature1");
            var featureConfig2 = CreateFeatureConfig("feature2");

            var evaluationAnalytics = new EvaluationAnalytics(featureConfig1, variation, target);
            var evaluationAnalytics2 = new EvaluationAnalytics(featureConfig2, variation, target);

            var sut = new MetricsProcessor(new Config(), evaluationAnalyticsCacheMock, targetAnalyticsCacheMock,
                analyticsPublisherServiceMock,
                new NullLoggerFactory(), false);

            // Act
            sut.PushToCache(target, featureConfig1, variation);
            sut.PushToCache(target, featureConfig1, variation);

            sut.PushToCache(target, featureConfig2, variation);
            sut.PushToCache(target, featureConfig2, variation);
            sut.PushToCache(target, featureConfig2, variation);

            Assert.That(evaluationAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(2));
            Assert.That(targetAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));


            // Ensure the counter is correct
            Assert.That(evaluationAnalyticsCacheMock.getIfPresent(evaluationAnalytics), Is.EqualTo(2));
            Assert.That(evaluationAnalyticsCacheMock.getIfPresent(evaluationAnalytics2), Is.EqualTo(3));
        }

        [Test]
        public void Should_store_one_evaluation_when_global_target_is_used_for_multiple_evaluations()
        {
            var evaluationAnalyticsCacheMock = new EvaluationAnalyticsCache();
            var targetAnalyticsCacheMock = new TargetAnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var loggerFactory = new NullLoggerFactory();
            var analyticsPublisherService =
                new AnalyticsPublisherService(connectorMock.Object, evaluationAnalyticsCacheMock,
                    targetAnalyticsCacheMock, loggerFactory, new Config());

            // Pass true for global target
            var metricsProcessor =
                new MetricsProcessor(new Config(), evaluationAnalyticsCacheMock, targetAnalyticsCacheMock,
                    analyticsPublisherService, loggerFactory, true);


            var target = Target.builder()
                .Name("unique_name_1")
                .Identifier("unique_identifier_1")
                .Attributes(new Dictionary<string, string> { { "email", "demo1@harness.io" } })
                .build();

            var sameAsTarget1 = Target.builder()
                .Name("unique_name_2")
                .Identifier("unique_identifier_2")
                .Attributes(new Dictionary<string, string> { { "email", "demo2@harness.io" } })
                .build();

            var differentAttributesToTarget1 = Target.builder()
                .Name("unique_names_3")
                .Identifier("unique_identifier_3")
                .Attributes(new Dictionary<string, string> { { "email", "demo124563@harness.io" } })
                .build();

            var differentIdentifierToTarget1 = Target.builder()
                .Name("unique_names_4")
                .Identifier("different_identifier_4")
                .Attributes(new Dictionary<string, string> { { "email", "demo4@harness.io" } })
                .build();

            var differentIdentifierAndAttributesToTarget1 = Target.builder()
                .Name("unique_names_5")
                .Identifier("another_different_identifier_5")
                .Attributes(new Dictionary<string, string> { { "email", "12456demo5@harness.io" } })
                .build();


            var featureConfig = CreateFeatureConfig("feature");
            var variation = new Variation();
            metricsProcessor.PushToCache(target, featureConfig, variation);
            metricsProcessor.PushToCache(sameAsTarget1, featureConfig, variation);
            metricsProcessor.PushToCache(differentAttributesToTarget1, featureConfig, variation);
            metricsProcessor.PushToCache(differentIdentifierToTarget1, featureConfig, variation);
            metricsProcessor.PushToCache(differentIdentifierAndAttributesToTarget1, featureConfig, variation);

            var globalTarget = new Target(EvaluationAnalytics.GlobalTargetIdentifier,
                EvaluationAnalytics.GlobalTargetName, null);
            var evaluationAnalytics = new EvaluationAnalytics(featureConfig, variation, globalTarget);

            Assert.That(evaluationAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));
            Assert.That(targetAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(5));

            // Check the evaluation has a count of 5
            Assert.That(evaluationAnalyticsCacheMock.getIfPresent(evaluationAnalytics), Is.EqualTo(5));
        }


        [Test]
        public void Should_Push_Targets_To_GlobalTargetSet_Using_MetricsProcessor()
        {
            var evaluationAnalyticsCacheMock = new EvaluationAnalyticsCache();
            var targetAnalyticsCacheMock = new TargetAnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var loggerFactory = new NullLoggerFactory();
            var analyticsPublisherService =
                new AnalyticsPublisherService(connectorMock.Object, evaluationAnalyticsCacheMock,
                    targetAnalyticsCacheMock, loggerFactory, new Config());
            var metricsProcessor =
                new MetricsProcessor(new Config(), evaluationAnalyticsCacheMock, targetAnalyticsCacheMock,
                    analyticsPublisherService, loggerFactory, false);

            var target1 = Target.builder()
                .Name("unique_name_1")
                .Identifier("unique_identifier_1")
                .Attributes(new Dictionary<string, string> { { "email", "demo@harness.io" } })
                .build();

            var sameAsTarget1 = Target.builder()
                .Name("unique_name_1")
                .Identifier("unique_identifier_1")
                .Attributes(new Dictionary<string, string> { { "email", "demo@harness.io" } })
                .build();


            var featureConfig1 = CreateFeatureConfig("feature1");
            var variation1 = new Variation();

            metricsProcessor.PushToCache(target1, featureConfig1, variation1);
            metricsProcessor.PushToCache(sameAsTarget1, featureConfig1, variation1);

            // Trigger the push to GlobalTargetSet
            analyticsPublisherService.SendDataAndResetCache();

            Assert.IsTrue(analyticsPublisherService.SeenTargetsCache.getIfPresent(target1.Identifier),
                "Target should be pushed to GlobalTargetSet");
        }

        [Test]
        public void Should_Handle_Concurrent_Pushes_To_GlobalTargetSet_Correctly()
        {
            var evaluationAnalyticsCacheMock = new EvaluationAnalyticsCache();
            var targetAnalyticsCacheMock = new TargetAnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var loggerFactory = new NullLoggerFactory();
            var analyticsPublisherService =
                new AnalyticsPublisherService(connectorMock.Object, evaluationAnalyticsCacheMock,
                    targetAnalyticsCacheMock, loggerFactory, new Config());
            var metricsProcessor =
                new MetricsProcessor(new Config(), evaluationAnalyticsCacheMock, targetAnalyticsCacheMock,
                    analyticsPublisherService, loggerFactory, false);

            const int numberOfThreads = 10;
            var tasks = new List<Task>();


            for (var i = 0; i < numberOfThreads; i++)
            {
                var target = Target.builder()
                    .Name($"unique_name_{i}")
                    .Identifier($"unique_identifier_{i}")
                    .Attributes(new Dictionary<string, string> { { "email", $"demo{i}@harness.io" } })
                    .build();

                var sameAsTarget1 = Target.builder()
                    .Name($"unique_name_{i}")
                    .Identifier($"unique_identifier_{i}")
                    .Attributes(new Dictionary<string, string> { { "email", $"demo{i}@harness.io" } })
                    .build();

                var differentAttributesToTarget1 = Target.builder()
                    .Name($"unique_names_{i}")
                    .Identifier($"unique_identifier_{i}")
                    .Attributes(new Dictionary<string, string> { { "email", $"demo12456{i}@harness.io" } })
                    .build();

                var differentIdentifierToTarget1 = Target.builder()
                    .Name($"unique_names_{i}")
                    .Identifier($"different_identifier_{i}")
                    .Attributes(new Dictionary<string, string> { { "email", $"demo{i}@harness.io" } })
                    .build();

                var differentIdentifierAndAttributesToTarget1 = Target.builder()
                    .Name($"unique_names_{i}")
                    .Identifier($"another_different_identifier_{i}")
                    .Attributes(new Dictionary<string, string> { { "email", $"12456demo{i}@harness.io" } })
                    .build();

                var task = Task.Run(() =>
                {
                    var featureConfig = CreateFeatureConfig($"feature{i}");
                    var variation = new Variation();
                    metricsProcessor.PushToCache(target, featureConfig, variation);
                    metricsProcessor.PushToCache(sameAsTarget1, featureConfig, variation);
                    metricsProcessor.PushToCache(differentAttributesToTarget1, featureConfig, variation);
                    metricsProcessor.PushToCache(differentIdentifierToTarget1, featureConfig, variation);
                    metricsProcessor.PushToCache(differentIdentifierAndAttributesToTarget1, featureConfig, variation);
                });
                tasks.Add(task);
            }

            Task.WhenAll(tasks).Wait();

            // Trigger the push to GlobalTargetSet
            analyticsPublisherService.SendDataAndResetCache();
            var count = analyticsPublisherService.SeenTargetsCache.Count();
            Assert.IsTrue(analyticsPublisherService.SeenTargetsCache.Count() == 30);
        }


        private FeatureConfig CreateFeatureConfig(string feature)
        {
            return new FeatureConfig
            {
                Project = "DummyProject",
                Environment = "DummyEnvironment",
                Feature = feature,
                State = FeatureState.On,
                Kind = FeatureConfigKind.Boolean,
                Variations = new List<Variation>
                {
                    new()
                    {
                        /* Variation properties */
                    },
                    new()
                    {
                        /* Variation properties */
                    }
                },
                DefaultServe = new Serve
                {
                    /* Serve properties */
                },
                OffVariation = "DummyOffVariation",
                Prerequisites = new List<Prerequisite>
                {
                    new()
                    {
                        /* Prerequisite properties */
                    },
                    new()
                    {
                        /* Prerequisite properties */
                    }
                },
                VariationToTargetMap = new List<VariationMap>
                {
                    new()
                    {
                        /* VariationMap properties */
                    },
                    new()
                    {
                        /* VariationMap properties */
                    }
                },
                Version = 1,
                AdditionalProperties = new Dictionary<string, object>
                {
                    { "DummyProperty1", "Value1" },
                    { "DummyProperty2", 123 }
                }
            };
        }

        private (int evaluationCount, int targetCount) GetAnalyticsTypeCounts(EvaluationAnalyticsCache cache)
        {
            var evaluationCount = 0;
            var targetCount = 0;

            foreach (var entry in cache.GetAllElements())
                if (entry.Key is EvaluationAnalytics)
                    evaluationCount++;
                else if (entry.Key is TargetAnalytics)
                    targetCount++;

            return (evaluationCount, targetCount);
        }
    }
}