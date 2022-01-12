using System;
using System.Collections.Generic;
using io.harness.cfsdk.HarnessOpenAPIService;

namespace io.harness.cfsdk.client.dto
{
    public class Analytics : IEquatable<Analytics>
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
        public Analytics() { }

        public override bool Equals(object obj)
        {
            return Equals(obj as Analytics);
        }

        public bool Equals(Analytics other)
        {
            return other != null &&
                   EqualityComparer<FeatureConfig>.Default.Equals(featureConfig, other.featureConfig) &&
                   EqualityComparer<Target>.Default.Equals(target, other.target) &&
                   EqualityComparer<Variation>.Default.Equals(variation, other.variation);
        }

        public override int GetHashCode()
        {
            int hashCode = -1526478203;
            hashCode = hashCode * -1521134295 + EqualityComparer<FeatureConfig>.Default.GetHashCode(featureConfig);
            hashCode = hashCode * -1521134295 + EqualityComparer<Target>.Default.GetHashCode(target);
            hashCode = hashCode * -1521134295 + EqualityComparer<Variation>.Default.GetHashCode(variation);
            return hashCode;
        }
    }
}
