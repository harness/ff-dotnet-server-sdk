using io.harness.cfsdk.HarnessOpenAPIService;

namespace io.harness.cfsdk.client.dto
{
    public class Analytics
    {
        private FeatureConfig featureConfig;
        private Target target;
        private Variation variation;
        private EventType eventType = EventType.METRICS;

        public FeatureConfig FeatureConfig { get => featureConfig; set => featureConfig = value; }
        public Target Target { get => target; set => target = value; }
        public Variation Variation { get => variation; set => variation = value; }
        public EventType EventType { get => eventType; set => eventType = value; }

        public Analytics(FeatureConfig _featureConfig, Target _target, Variation _variation, EventType _eventType)
        {
            FeatureConfig = _featureConfig;
            Target = _target;
            Variation = _variation;
            EventType = _eventType;
        }

        public Analytics()
        {
        }

        public static AnalyticsBuilder Builder()
        {
            return new AnalyticsBuilder();
        }
    }

    public class AnalyticsBuilder
    {
        Analytics analyticstobuild;

        public AnalyticsBuilder()
        {
            analyticstobuild = new Analytics();
        }

        public AnalyticsBuilder featureConfig(FeatureConfig config)
        {
            analyticstobuild.FeatureConfig = config;
            return this;
        }
        public AnalyticsBuilder target(Target target)
        {
            analyticstobuild.Target = target;
            return this;
        }
        public AnalyticsBuilder variation(Variation variation)
        {
            analyticstobuild.Variation = variation;
            return this;
        }
        public AnalyticsBuilder eventType(EventType event_type)
        {
            analyticstobuild.EventType = event_type;
            return this;
        }
        public Analytics Build()
        {
            return analyticstobuild;
        }
    }
}
