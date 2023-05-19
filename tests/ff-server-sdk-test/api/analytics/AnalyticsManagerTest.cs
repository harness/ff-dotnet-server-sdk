using Disruptor;
using Disruptor.Dsl;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using NUnit.Framework;
using Serilog;
using System;
using System.Threading.Tasks;
using System.Timers;
using Moq;
using io.harness.cfsdk.client.api.analytics;
using io.harness.cfsdk.client.api;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using io.harness.cfsdk.HarnessOpenMetricsAPIService;
using System.Threading;

namespace ff_server_sdk_test.api.analytics
{
    [TestFixture]
    public class AnalyticsManagerTests
    {
        [Test]
        public void Should_add_single_evaluation_for_single_feature_to_analytics_cache()
        {
            // Arrange
            var analyticsCacheMock = new AnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var analyticsPublisherServiceMock = new AnalyticsPublisherService(connectorMock.Object, analyticsCacheMock);

            var variation = new Variation();
            var target = new io.harness.cfsdk.client.dto.Target();

            var featureConfig1 = CreateFeatureConfig("feature1");
            var analytics = new Analytics(featureConfig1, target, variation, EventType.METRICS);

            var sut = new MetricsProcessor(new LocalConnector("TEST"), new Config(), null, analyticsCacheMock, analyticsPublisherServiceMock);

            // Act
            sut.PushToCache(target, featureConfig1, variation);

            // Assert
            Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(1)); ;
            Assert.That(analyticsCacheMock.getIfPresent(analytics), Is.EqualTo(1));
        }

        [Test]
        public void Should_add_multiple_evaluations_for_single_feature_to_analytics_cache()
        {
            // Arrange
            var analyticsCacheMock = new AnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var analyticsPublisherServiceMock = new AnalyticsPublisherService(connectorMock.Object, analyticsCacheMock);

            var target = new io.harness.cfsdk.client.dto.Target();
            var variation = new Variation();

            // simulate multiple evaluations for a single feature
            var featureConfig = CreateFeatureConfig("feature1");
            var analytics = new Analytics(featureConfig, target, variation, EventType.METRICS);

            var sut = new MetricsProcessor(new LocalConnector("TEST"), new Config(), null, analyticsCacheMock, analyticsPublisherServiceMock);

            // Act
            sut.PushToCache(target, featureConfig, variation);
            sut.PushToCache(target, featureConfig, variation);
            sut.PushToCache(target, featureConfig, variation);
            sut.PushToCache(target, featureConfig, variation);
            sut.PushToCache(target, featureConfig, variation);

            // Assert
            Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(1));
            Assert.That(analyticsCacheMock.getIfPresent(analytics), Is.EqualTo(5));

        }

        [Test]
        public void Should_add_single_evaluation_for_multiple_features_to_analytics_cache()
        {
            // Arrange
            var analyticsCacheMock = new AnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var analyticsPublisherServiceMock = new AnalyticsPublisherService(connectorMock.Object, analyticsCacheMock);

            var target = new io.harness.cfsdk.client.dto.Target();
            var variation = new Variation();

            // simulate an evaluation for multiple different features
            var featureConfig1 = CreateFeatureConfig("feature1");
            var featureConfig2 = CreateFeatureConfig("feature2");

            var analytics1 = new Analytics(featureConfig1, target, variation, EventType.METRICS);
            var analytics2 = new Analytics(featureConfig2, target, variation, EventType.METRICS);

            var sut = new MetricsProcessor(new LocalConnector("TEST"), new Config(), null, analyticsCacheMock, analyticsPublisherServiceMock);

            // Act
            sut.PushToCache(target, featureConfig1, variation);
            sut.PushToCache(target, featureConfig2, variation);

            // Assert
            Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(2));
            Assert.That(analyticsCacheMock.getIfPresent(analytics1), Is.EqualTo(1));
            Assert.That(analyticsCacheMock.getIfPresent(analytics2), Is.EqualTo(1));
        }


        [Test]
        public void Should_add_multiple_evaluations_for_multiple_features_to_analytics_cache()
        {
            // Arrange
            var analyticsCacheMock = new AnalyticsCache();
            var connectorMock = new Mock<IConnector>();
            var analyticsPublisherServiceMock = new AnalyticsPublisherService(connectorMock.Object, analyticsCacheMock);

            var target = new io.harness.cfsdk.client.dto.Target();
            var variation = new Variation();

            // simulate an evaluation for multiple different features
            var featureConfig1 = CreateFeatureConfig("feature1");
            var featureConfig2 = CreateFeatureConfig("feature2");

            var analytics1 = new Analytics(featureConfig1, target, variation, EventType.METRICS);
            var analytics2 = new Analytics(featureConfig2, target, variation, EventType.METRICS);

            var sut = new MetricsProcessor(new LocalConnector("TEST"), new Config(), null, analyticsCacheMock, analyticsPublisherServiceMock);

            // Act
            sut.PushToCache(target, featureConfig1, variation);
            sut.PushToCache(target, featureConfig1, variation);

            sut.PushToCache(target, featureConfig2, variation);
            sut.PushToCache(target, featureConfig2, variation);
            sut.PushToCache(target, featureConfig2, variation);

            // Assert
            Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(2));
            Assert.That(analyticsCacheMock.getIfPresent(analytics1), Is.EqualTo(2));
            Assert.That(analyticsCacheMock.getIfPresent(analytics2), Is.EqualTo(3));
        }

        [Test]
        public void Should_force_push_metrics_and_clear_cache_when_analytics_cache_full()
        {
            // Arrange
            var analyticsCacheMock = new AnalyticsCache();
            var connectorMock = new Mock<IConnector>();

            var bufferSize = 2;
            var configMock = new Config("", "", false, 10, true, 1, bufferSize, 10, 10, 10, false, 10000);
            var analyticsPublisherServiceMock = new AnalyticsPublisherService(connectorMock.Object, analyticsCacheMock);

            var target = new io.harness.cfsdk.client.dto.Target();
            var variation = new Variation();

            var sut = new MetricsProcessor(connectorMock.Object, configMock, null, analyticsCacheMock, analyticsPublisherServiceMock);

            // Act - set cachesize > buffer
            sut.PushToCache(target, CreateFeatureConfig("feature1"), variation);
            sut.PushToCache(target, CreateFeatureConfig("feature2"), variation);
            sut.PushToCache(target, CreateFeatureConfig("feature3"), variation);
            sut.PushToCache(target, CreateFeatureConfig("feature4"), variation);

            // Assert 
            connectorMock.Verify(a => a.PostMetrics(It.IsAny<Metrics>()), Times.Once);
            Assert.That(analyticsCacheMock.GetAllElements().Count, Is.EqualTo(0));
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

