using System.Collections.Generic;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.api.analytics;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using io.harness.cfsdk.HarnessOpenMetricsAPIService;
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
                new AnalyticsPublisherService(connectorMock.Object, evaluationAnalyticsCacheMock, targetAnalyticsCacheMock, new NullLoggerFactory());

            var variation = new Variation();
            var target = new Target();

            var featureConfig1 = CreateFeatureConfig("feature1");
            var evaluationAnalytics = new EvaluationAnalytics(featureConfig1, variation, target);
            var targetAnalytics = new TargetAnalytics(target);

            var sut = new MetricsProcessor(new Config(), evaluationAnalyticsCacheMock,targetAnalyticsCacheMock, analyticsPublisherServiceMock,
                new NullLoggerFactory(), false);


            sut.PushToCache(target, featureConfig1, variation);

            // Ensure the cache totals are correct
            Assert.That(evaluationAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));
            Assert.That(targetAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));
            
            
            // Ensure the cache total and breakdown of the type of analytics is correct
            Assert.That(evaluationAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));

            // Ensure the counter is correct.
            Assert.That(evaluationAnalyticsCacheMock.getIfPresent(evaluationAnalytics), Is.EqualTo(1));
            Assert.That(targetAnalyticsCacheMock.getIfPresent(targetAnalytics), Is.EqualTo(1));
        }

        
        [Test]
        public void Should_add_multiple_evaluations_for_single_feature_to_analytics_cache()
        {
            var evaluationAnalyticsCacheMock = new EvaluationAnalyticsCache();
            var targetAnalyticsCacheMock = new TargetAnalyticsCache();            
            var connectorMock = new Mock<IConnector>();
            var analyticsPublisherServiceMock =
                new AnalyticsPublisherService(connectorMock.Object, evaluationAnalyticsCacheMock, targetAnalyticsCacheMock, new NullLoggerFactory());
        
            var target = new Target();
            var variation = new Variation();
        
            // simulate multiple evaluations for a single feature
            var featureConfig = CreateFeatureConfig("feature1");
            var evaluationAnalytics = new EvaluationAnalytics(featureConfig, variation, target);
            var targetAnalytics = new TargetAnalytics(target);
        
            var sut = new MetricsProcessor(new Config(), evaluationAnalyticsCacheMock, targetAnalyticsCacheMock, analyticsPublisherServiceMock,
                new NullLoggerFactory(), false);
        
            sut.PushToCache(target, featureConfig, variation);
            sut.PushToCache(target, featureConfig, variation);
            sut.PushToCache(target, featureConfig, variation);
            sut.PushToCache(target, featureConfig, variation);
            sut.PushToCache(target, featureConfig, variation);
        
        
            // Ensure the cache totals are correct
            Assert.That(evaluationAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));
            Assert.That(targetAnalyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));

        
            // Correct count
            Assert.That(evaluationAnalyticsCacheMock.getIfPresent(evaluationAnalytics), Is.EqualTo(5));
            Assert.That(targetAnalyticsCacheMock.getIfPresent(targetAnalytics), Is.EqualTo(1));
        }
        
        [Test]
        public void Should_add_single_evaluation_for_multiple_features_to_analytics_cache()
        {
            // Arrange
            var evaluationAnalyticsCacheMock = new EvaluationAnalyticsCache();
            var targetAnalyticsCacheMock = new TargetAnalyticsCache();                        var connectorMock = new Mock<IConnector>();
            var analyticsPublisherServiceMock =
                new AnalyticsPublisherService(connectorMock.Object, evaluationAnalyticsCacheMock, targetAnalyticsCacheMock, new NullLoggerFactory());
        
            var target = new Target();
            var variation = new Variation();
        
            // simulate an evaluation for multiple different features
            var featureConfig1 = CreateFeatureConfig("feature1");
            var featureConfig2 = CreateFeatureConfig("feature2");
            var evaluationAnalytics = new EvaluationAnalytics(featureConfig1, variation, target);
            var evaluationAnalytics2 = new EvaluationAnalytics(featureConfig2, variation, target);
            var targetAnalytics = new TargetAnalytics(target);
        
            var sut = new MetricsProcessor(new Config(), evaluationAnalyticsCacheMock, targetAnalyticsCacheMock, analyticsPublisherServiceMock,
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
            Assert.That(targetAnalyticsCacheMock.getIfPresent(targetAnalytics), Is.EqualTo(1));
        }
        
        
        // [Test]
        // public void Should_add_multiple_evaluations_for_multiple_features_to_analytics_cache()
        // {
        //     // Arrange
        //     var analyticsCacheMock = new EvaluationAnalyticsCache();
        //     var connectorMock = new Mock<IConnector>();
        //     var analyticsPublisherServiceMock =
        //         new AnalyticsPublisherService(connectorMock.Object, analyticsCacheMock, new NullLoggerFactory());
        //
        //     var target = new Target();
        //     // var target = new Target(EvaluationAnalytics.GlobalTargetIdentifier, EvaluationAnalytics.GlobalTargetName,
        //     //     null);
        //     var variation = new Variation();
        //
        //     // simulate an evaluation for multiple different features
        //     var featureConfig1 = CreateFeatureConfig("feature1");
        //     var featureConfig2 = CreateFeatureConfig("feature2");
        //
        //     var evaluationAnalytics = new EvaluationAnalytics(featureConfig1, variation, target);
        //     var evaluationAnalytics2 = new EvaluationAnalytics(featureConfig2, variation, target);
        //
        //     var sut = new MetricsProcessor(new Config(), analyticsCacheMock, analyticsPublisherServiceMock,
        //         new NullLoggerFactory(), false);
        //
        //     // Act
        //     sut.PushToCache(target, featureConfig1, variation);
        //     sut.PushToCache(target, featureConfig1, variation);
        //
        //     sut.PushToCache(target, featureConfig2, variation);
        //     sut.PushToCache(target, featureConfig2, variation);
        //     sut.PushToCache(target, featureConfig2, variation);
        //
        //     // Ensure the cache total and breakdown of the type of analytics is correct
        //     Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(3));
        //     var (evaluationCount, targetCount) = GetAnalyticsTypeCounts(analyticsCacheMock);
        //     Assert.That(evaluationCount, Is.EqualTo(2), "Incorrect number of EvaluationAnalytics");
        //     Assert.That(targetCount, Is.EqualTo(1), "Incorrect number of TargetAnalytics");
        //     
        //     // Ensure the counter is correct
        //     Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(3));
        //     Assert.That(analyticsCacheMock.getIfPresent(evaluationAnalytics), Is.EqualTo(2));
        //     Assert.That(analyticsCacheMock.getIfPresent(evaluationAnalytics2), Is.EqualTo(3));
        // }
        //
        // [Test]
        // public void Should_store_one_evaluation_when_global_target_is_used_for_multiple_evaluations()
        // {
        //     var analyticsCache = new EvaluationAnalyticsCache();
        //     var connectorMock = new Mock<IConnector>();
        //     var loggerFactory = new NullLoggerFactory();
        //     var analyticsPublisherService =
        //         new AnalyticsPublisherService(connectorMock.Object, analyticsCache, loggerFactory);
        //     
        //     // Pass true for global target
        //     var metricsProcessor =
        //         new MetricsProcessor(new Config(), analyticsCache, analyticsPublisherService, loggerFactory, true);
        //
        //
        //     var target = Target.builder()
        //         .Name("unique_name_1")
        //         .Identifier("unique_identifier_1")
        //         .Attributes(new Dictionary<string, string> { { "email", "demo1@harness.io" } })
        //         .build();
        //
        //     var sameAsTarget1 = Target.builder()
        //         .Name("unique_name_2")
        //         .Identifier("unique_identifier_2")
        //         .Attributes(new Dictionary<string, string> { { "email", "demo2@harness.io" } })
        //         .build();
        //
        //     var differentAttributesToTarget1 = Target.builder()
        //         .Name("unique_names_3")
        //         .Identifier("unique_identifier_3")
        //         .Attributes(new Dictionary<string, string> { { "email", "demo124563@harness.io" } })
        //         .build();
        //
        //     var differentIdentifierToTarget1 = Target.builder()
        //         .Name("unique_names_4")
        //         .Identifier("different_identifier_4")
        //         .Attributes(new Dictionary<string, string> { { "email", "demo4@harness.io" } })
        //         .build();
        //
        //     var differentIdentifierAndAttributesToTarget1 = Target.builder()
        //         .Name("unique_names_5")
        //         .Identifier("another_different_identifier_5")
        //         .Attributes(new Dictionary<string, string> { { "email", "12456demo5@harness.io" } })
        //         .build();
        //
        //
        //     var featureConfig = CreateFeatureConfig("feature");
        //     var variation = new Variation();
        //     metricsProcessor.PushToCache(target, featureConfig, variation);
        //     metricsProcessor.PushToCache(sameAsTarget1, featureConfig, variation);
        //     metricsProcessor.PushToCache(differentAttributesToTarget1, featureConfig, variation);
        //     metricsProcessor.PushToCache(differentIdentifierToTarget1, featureConfig, variation);
        //     metricsProcessor.PushToCache(differentIdentifierAndAttributesToTarget1, featureConfig, variation);
        //     
        //     Target globalTarget = new Target(EvaluationAnalytics.GlobalTargetIdentifier,
        //         EvaluationAnalytics.GlobalTargetName, null);
        //     var evaluationAnalytics = new EvaluationAnalytics(featureConfig, variation, globalTarget);
        //
        //     // Ensure the cache total and breakdown of the type of analytics is correct
        //     Assert.That(analyticsCache.GetAllElements().Count, Is.EqualTo(6));
        //     var (evaluationCount, targetCount) = GetAnalyticsTypeCounts(analyticsCache);
        //     Assert.That(evaluationCount, Is.EqualTo(1), "Incorrect number of EvaluationAnalytics");
        //     Assert.That(targetCount, Is.EqualTo(5), "Incorrect number of TargetAnalytics");
        //
        //     // Check the evaluation has a count of 5
        //     Assert.That(analyticsCache.getIfPresent(evaluationAnalytics), Is.EqualTo(5));
        // }
        //
        // [Test]
        // public void Should_force_push_metrics_and_clear_cache_when_analytics_cache_full()
        // {
        //     // Arrange
        //     var analyticsCacheMock = new EvaluationAnalyticsCache();
        //     var connectorMock = new Mock<IConnector>();
        //
        //     var bufferSize = 2;
        //     var configMock = new Config("", "", false, 10, true, 1, bufferSize, 10, 10, 10, false, 10000);
        //     var analyticsPublisherServiceMock =
        //         new AnalyticsPublisherService(connectorMock.Object, analyticsCacheMock, new NullLoggerFactory());
        //
        //     var target = new Target();
        //     var variation = new Variation();
        //
        //     var sut = new MetricsProcessor(configMock, analyticsCacheMock, analyticsPublisherServiceMock,
        //         new NullLoggerFactory(), false);
        //
        //     // Act - set cachesize > buffer
        //     sut.PushToCache(target, CreateFeatureConfig("feature1"), variation);
        //     sut.PushToCache(target, CreateFeatureConfig("feature2"), variation);
        //     sut.PushToCache(target, CreateFeatureConfig("feature3"), variation);
        //
        //     // Assert 
        //     connectorMock.Verify(a => a.PostMetrics(It.IsAny<Metrics>()), Times.Once);
        //     Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(0));
        // }
        //
        // [Test]
        // public void Should_Push_Targets_To_GlobalTargetSet_Using_MetricsProcessor()
        // {
        //     var analyticsCache = new EvaluationAnalyticsCache();
        //     var connectorMock = new Mock<IConnector>();
        //     var loggerFactory = new NullLoggerFactory();
        //     var analyticsPublisherService =
        //         new AnalyticsPublisherService(connectorMock.Object, analyticsCache, loggerFactory);
        //     var metricsProcessor =
        //         new MetricsProcessor(new Config(), analyticsCache, analyticsPublisherService, loggerFactory, false);
        //
        //     var target1 = Target.builder()
        //         .Name("unique_name_1")
        //         .Identifier("unique_identifier_1")
        //         .Attributes(new Dictionary<string, string> { { "email", "demo@harness.io" } })
        //         .build();
        //
        //     var sameAsTarget1 = Target.builder()
        //         .Name("unique_name_1")
        //         .Identifier("unique_identifier_1")
        //         .Attributes(new Dictionary<string, string> { { "email", "demo@harness.io" } })
        //         .build();
        //
        //
        //     var featureConfig1 = CreateFeatureConfig("feature1");
        //     var variation1 = new Variation();
        //
        //     metricsProcessor.PushToCache(target1, featureConfig1, variation1);
        //     metricsProcessor.PushToCache(sameAsTarget1, featureConfig1, variation1);
        //
        //     // Trigger the push to GlobalTargetSet
        //     analyticsPublisherService.SendDataAndResetCache();
        //
        //     Assert.IsTrue(AnalyticsPublisherService.SeenTargets.ContainsKey(target1),
        //         "Target should be pushed to GlobalTargetSet");
        // }
        //
        // [Test]
        // public void Should_Handle_Concurrent_Pushes_To_GlobalTargetSet_Correctly()
        // {
        //     var analyticsCache = new EvaluationAnalyticsCache();
        //     var connectorMock = new Mock<IConnector>();
        //     var loggerFactory = new NullLoggerFactory();
        //     var analyticsPublisherService =
        //         new AnalyticsPublisherService(connectorMock.Object, analyticsCache, loggerFactory);
        //     var metricsProcessor =
        //         new MetricsProcessor(new Config(), analyticsCache, analyticsPublisherService, loggerFactory, false);
        //
        //     const int numberOfThreads = 10;
        //     var tasks = new List<Task>();
        //
        //
        //     for (var i = 0; i < numberOfThreads; i++)
        //     {
        //         var target = Target.builder()
        //             .Name($"unique_name_{i}")
        //             .Identifier($"unique_identifier_{i}")
        //             .Attributes(new Dictionary<string, string> { { "email", $"demo{i}@harness.io" } })
        //             .build();
        //
        //         var sameAsTarget1 = Target.builder()
        //             .Name($"unique_name_{i}")
        //             .Identifier($"unique_identifier_{i}")
        //             .Attributes(new Dictionary<string, string> { { "email", $"demo{i}@harness.io" } })
        //             .build();
        //
        //         var differentAttributesToTarget1 = Target.builder()
        //             .Name($"unique_names_{i}")
        //             .Identifier($"unique_identifier_{i}")
        //             .Attributes(new Dictionary<string, string> { { "email", $"demo12456{i}@harness.io" } })
        //             .build();
        //
        //         var differentIdentifierToTarget1 = Target.builder()
        //             .Name($"unique_names_{i}")
        //             .Identifier($"different_identifier_{i}")
        //             .Attributes(new Dictionary<string, string> { { "email", $"demo{i}@harness.io" } })
        //             .build();
        //
        //         var differentIdentifierAndAttributesToTarget1 = Target.builder()
        //             .Name($"unique_names_{i}")
        //             .Identifier($"another_different_identifier_{i}")
        //             .Attributes(new Dictionary<string, string> { { "email", $"12456demo{i}@harness.io" } })
        //             .build();
        //
        //         var task = Task.Run(() =>
        //         {
        //             var featureConfig = CreateFeatureConfig($"feature{i}");
        //             var variation = new Variation();
        //             metricsProcessor.PushToCache(target, featureConfig, variation);
        //             metricsProcessor.PushToCache(sameAsTarget1, featureConfig, variation);
        //             metricsProcessor.PushToCache(differentAttributesToTarget1, featureConfig, variation);
        //             metricsProcessor.PushToCache(differentIdentifierToTarget1, featureConfig, variation);
        //             metricsProcessor.PushToCache(differentIdentifierAndAttributesToTarget1, featureConfig, variation);
        //         });
        //         tasks.Add(task);
        //     }
        //
        //     Task.WhenAll(tasks).Wait();
        //
        //     // Trigger the push to GlobalTargetSet
        //     analyticsPublisherService.SendDataAndResetCache();
        //     var count = AnalyticsPublisherService.SeenTargets.Count;
        //     Assert.IsTrue(AnalyticsPublisherService.SeenTargets.Count == 41);
        // }


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