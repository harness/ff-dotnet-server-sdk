using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using NUnit.Framework;
using Moq;
using io.harness.cfsdk.client.api.analytics;
using io.harness.cfsdk.client.api;
using System.Collections.Generic;
using System.Threading.Tasks;
using io.harness.cfsdk.HarnessOpenMetricsAPIService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ff_server_sdk_test.api.analytics
{
    [TestFixture]
    public class AnalyticsManagerTests
    {
        [Test]
        public void Should_add_single_evaluation_and_target_for_single_feature_to_analytics_cache()
        {
            // Arrange
            var analyticsCacheMock = new AnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var analyticsPublisherServiceMock = new AnalyticsPublisherService(connectorMock.Object, analyticsCacheMock, new NullLoggerFactory());
        
            var variation = new Variation();
            var target = new io.harness.cfsdk.client.dto.Target();
        
            var featureConfig1 = CreateFeatureConfig("feature1");
            var evaluationAnalytics = new EvaluationAnalytics(featureConfig1, variation, target);
            var targetAnalytics = new TargetAnalytics(target);
        
            var sut = new MetricsProcessor(new Config(), analyticsCacheMock, analyticsPublisherServiceMock, new NullLoggerFactory());
        
            // Act
            sut.PushToCache(target, featureConfig1, variation);
        
            // Assert
            Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(2)); ;
            Assert.That(analyticsCacheMock.getIfPresent(evaluationAnalytics), Is.EqualTo(1));
            Assert.That(analyticsCacheMock.getIfPresent(targetAnalytics), Is.EqualTo(1));
        }
        //
        // [Test]
        // public void Should_add_multiple_evaluations_for_single_feature_to_analytics_cache()
        // {
        //     // Arrange
        //     var analyticsCacheMock = new AnalyticsCache();
        //     var connectorMock = new Mock<IConnector>();
        //     var analyticsPublisherServiceMock = new AnalyticsPublisherService(connectorMock.Object, analyticsCacheMock, new NullLoggerFactory());
        //
        //     var target = new io.harness.cfsdk.client.dto.Target();
        //     var variation = new Variation();
        //
        //     // simulate multiple evaluations for a single feature
        //     var featureConfig = CreateFeatureConfig("feature1");
        //     var analytics = new Analytics(featureConfig, target, variation, EventType.METRICS);
        //
        //     var sut = new MetricsProcessor(new Config(), analyticsCacheMock, analyticsPublisherServiceMock, new NullLoggerFactory());
        //
        //     // Act
        //     sut.PushToCache(target, featureConfig, variation);
        //     sut.PushToCache(target, featureConfig, variation);
        //     sut.PushToCache(target, featureConfig, variation);
        //     sut.PushToCache(target, featureConfig, variation);
        //     sut.PushToCache(target, featureConfig, variation);
        //
        //     // Assert
        //     Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));
        //     Assert.That(analyticsCacheMock.getIfPresent(analytics), Is.EqualTo(5));
        // }
        //
        // [Test]
        // public void Should_add_single_evaluation_for_multiple_features_to_analytics_cache()
        // {
        //     // Arrange
        //     var analyticsCacheMock = new AnalyticsCache();
        //     var connectorMock = new Mock<IConnector>();
        //     var analyticsPublisherServiceMock = new AnalyticsPublisherService(connectorMock.Object, analyticsCacheMock, new NullLoggerFactory());
        //
        //     var target = new io.harness.cfsdk.client.dto.Target();
        //     var variation = new Variation();
        //
        //     // simulate an evaluation for multiple different features
        //     var featureConfig1 = CreateFeatureConfig("feature1");
        //     var featureConfig2 = CreateFeatureConfig("feature2");
        //
        //     var analytics1 = new Analytics(featureConfig1, target, variation, EventType.METRICS);
        //     var analytics2 = new Analytics(featureConfig2, target, variation, EventType.METRICS);
        //
        //     var sut = new MetricsProcessor(new Config(), analyticsCacheMock, analyticsPublisherServiceMock, new NullLoggerFactory());
        //
        //     // Act
        //     sut.PushToCache(target, featureConfig1, variation);
        //     sut.PushToCache(target, featureConfig2, variation);
        //
        //     // Assert
        //     Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(2));
        //     Assert.That(analyticsCacheMock.getIfPresent(analytics1), Is.EqualTo(1));
        //     Assert.That(analyticsCacheMock.getIfPresent(analytics2), Is.EqualTo(1));
        // }
        //
        //
        // [Test]
        // public void Should_add_multiple_evaluations_for_multiple_features_to_analytics_cache()
        // {
        //     // Arrange
        //     var analyticsCacheMock = new AnalyticsCache();
        //     var connectorMock = new Mock<IConnector>();
        //     var analyticsPublisherServiceMock = new AnalyticsPublisherService(connectorMock.Object, analyticsCacheMock, new NullLoggerFactory());
        //
        //     var target = new io.harness.cfsdk.client.dto.Target();
        //     var variation = new Variation();
        //
        //     // simulate an evaluation for multiple different features
        //     var featureConfig1 = CreateFeatureConfig("feature1");
        //     var featureConfig2 = CreateFeatureConfig("feature2");
        //
        //     var analytics1 = new Analytics(featureConfig1, target, variation, EventType.METRICS);
        //     var analytics2 = new Analytics(featureConfig2, target, variation, EventType.METRICS);
        //
        //     var sut = new MetricsProcessor(new Config(), analyticsCacheMock, analyticsPublisherServiceMock, new NullLoggerFactory());
        //
        //     // Act
        //     sut.PushToCache(target, featureConfig1, variation);
        //     sut.PushToCache(target, featureConfig1, variation);
        //
        //     sut.PushToCache(target, featureConfig2, variation);
        //     sut.PushToCache(target, featureConfig2, variation);
        //     sut.PushToCache(target, featureConfig2, variation);
        //
        //     // Assert
        //     Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(2));
        //     Assert.That(analyticsCacheMock.getIfPresent(analytics1), Is.EqualTo(2));
        //     Assert.That(analyticsCacheMock.getIfPresent(analytics2), Is.EqualTo(3));
        // }
        //
        // [Test]
        // public void Should_force_push_metrics_and_clear_cache_when_analytics_cache_full()
        // {
        //     // Arrange
        //     var analyticsCacheMock = new AnalyticsCache();
        //     var connectorMock = new Mock<IConnector>();
        //
        //     var bufferSize = 2;
        //     var configMock = new Config("", "", false, 10, true, 1, bufferSize, 10, 10, 10, false, 10000);
        //     var analyticsPublisherServiceMock = new AnalyticsPublisherService(connectorMock.Object, analyticsCacheMock, new NullLoggerFactory());
        //
        //     var target = new io.harness.cfsdk.client.dto.Target();
        //     var variation = new Variation();
        //
        //     var sut = new MetricsProcessor(configMock, analyticsCacheMock, analyticsPublisherServiceMock, new NullLoggerFactory());
        //
        //     // Act - set cachesize > buffer
        //     sut.PushToCache(target, CreateFeatureConfig("feature1"), variation);
        //     sut.PushToCache(target, CreateFeatureConfig("feature2"), variation);
        //     sut.PushToCache(target, CreateFeatureConfig("feature3"), variation);
        //     sut.PushToCache(target, CreateFeatureConfig("feature4"), variation);
        //
        //     // Assert 
        //     connectorMock.Verify(a => a.PostMetrics(It.IsAny<Metrics>()), Times.Once);
        //     Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(0));
        // }
        //
        // [Test]
        // public void Should_Push_Targets_To_GlobalTargetSet_Using_MetricsProcessor()
        // {
        //     var analyticsCache = new AnalyticsCache();
        //     var connectorMock = new Mock<IConnector>();
        //     var loggerFactory = new NullLoggerFactory();
        //     var analyticsPublisherService = new AnalyticsPublisherService(connectorMock.Object, analyticsCache, loggerFactory);
        //     var metricsProcessor = new MetricsProcessor(new Config(), analyticsCache, analyticsPublisherService, loggerFactory);
        //
        //     var target1 = io.harness.cfsdk.client.dto.Target.builder()
        //         .Name("unique_name_1")
        //         .Identifier("unique_identifier_1")
        //         .Attributes(new Dictionary<string, string>(){{"email", "demo@harness.io"}})
        //         .build();
        //     
        //     var sameAsTarget1 = io.harness.cfsdk.client.dto.Target.builder()
        //         .Name("unique_name_1")
        //         .Identifier("unique_identifier_1")
        //         .Attributes(new Dictionary<string, string>(){{"email", "demo@harness.io"}})
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
        //     Assert.IsTrue(AnalyticsPublisherService.SeenTargets.ContainsKey(target1), "Target should be pushed to GlobalTargetSet");
        // }

        [Test]
        public void Should_Handle_Concurrent_Pushes_To_GlobalTargetSet_Correctly()
        {
            var analyticsCache = new AnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var loggerFactory = new NullLoggerFactory();
            var analyticsPublisherService = new AnalyticsPublisherService(connectorMock.Object, analyticsCache, loggerFactory);
            var metricsProcessor = new MetricsProcessor(new Config(), analyticsCache, analyticsPublisherService, loggerFactory);
        
            const int numberOfThreads = 10;
            var tasks = new List<Task>();
            
        
            for (int i = 0; i < numberOfThreads; i++)
            {
                var target = io.harness.cfsdk.client.dto.Target.builder()
                    .Name($"unique_name_{i}")
                    .Identifier($"unique_identifier_{i}")
                    .Attributes(new Dictionary<string, string>(){{"email", $"demo{i}@harness.io"}})
                    .build();
                
                var sameAsTarget1 = io.harness.cfsdk.client.dto.Target.builder()
                    .Name($"unique_name_{i}")
                    .Identifier($"unique_identifier_{i}")
                    .Attributes(new Dictionary<string, string>(){{"email", $"demo{i}@harness.io"}})
                    .build();
                
                var differentAttributesToTarget1 = io.harness.cfsdk.client.dto.Target.builder()
                    .Name($"unique_names_{i}")
                    .Identifier($"unique_identifier_{i}")
                    .Attributes(new Dictionary<string, string>(){{"email", $"demo12456{i}@harness.io"}})
                    .build();
                
                var differentIdentifierToTarget1 = io.harness.cfsdk.client.dto.Target.builder()
                    .Name($"unique_names_{i}")
                    .Identifier($"different_identifier_{i}")
                    .Attributes(new Dictionary<string, string>(){{"email", $"demo{i}@harness.io"}})
                    .build();
                
                var differentIdentifierAndAttributesToTarget1 = io.harness.cfsdk.client.dto.Target.builder()
                    .Name($"unique_names_{i}")
                    .Identifier($"another_different_identifier_{i}")
                    .Attributes(new Dictionary<string, string>(){{"email", $"12456demo{i}@harness.io"}})
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
            
            Assert.IsTrue(AnalyticsPublisherService.SeenTargets.Count == 41);
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
                    new Variation { /* Variation properties */ },
                    new Variation { /* Variation properties */ }
                },
                DefaultServe = new Serve { /* Serve properties */ },
                OffVariation = "DummyOffVariation",
                Prerequisites = new List<Prerequisite>
                {
                    new Prerequisite { /* Prerequisite properties */ },
                    new Prerequisite { /* Prerequisite properties */ }
                },
                VariationToTargetMap = new List<VariationMap>
                {
                    new VariationMap { /* VariationMap properties */ },
                    new VariationMap { /* VariationMap properties */ }
                },
                Version = 1,
                AdditionalProperties = new Dictionary<string, object>
                {
                    { "DummyProperty1", "Value1" },
                    { "DummyProperty2", 123 }
                }
            };
        }
    }


}

