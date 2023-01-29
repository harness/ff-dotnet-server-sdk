using System;
using System.Collections.Generic;
using io.harness.cfsdk.HarnessOpenAPIService;

namespace io.harness.cfsdk.client.dto
{
    internal sealed class Analytics : IEquatable<Analytics>
    {
        public FeatureConfig FeatureConfig { get; set; }
        public Target Target { get; set; }
        public Variation Variation { get; set; }
        public EventType EventType { get; set; }

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
                   EqualityComparer<FeatureConfig>.Default.Equals(FeatureConfig, other.FeatureConfig) &&
                   EqualityComparer<Target>.Default.Equals(Target, other.Target) &&
                   EqualityComparer<Variation>.Default.Equals(Variation, other.Variation);
        }

        public override int GetHashCode()
        {
            int hashCode = -1526478203;
            hashCode = hashCode * -1521134295 + EqualityComparer<FeatureConfig>.Default.GetHashCode(FeatureConfig);
            hashCode = hashCode * -1521134295 + EqualityComparer<Target>.Default.GetHashCode(Target);
            hashCode = hashCode * -1521134295 + EqualityComparer<Variation>.Default.GetHashCode(Variation);
            return hashCode;
        }
    }
}
