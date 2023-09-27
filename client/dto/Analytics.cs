using System;
using System.Collections.Generic;
using io.harness.cfsdk.HarnessOpenAPIService;

namespace io.harness.cfsdk.client.dto
{
    public class Analytics : IEquatable<Analytics>
    {
        private readonly FeatureConfig featureConfig;
        private readonly Target target;
        private readonly Variation variation;

        public FeatureConfig FeatureConfig { get => featureConfig; }
        public Target Target { get => target; }
        public Variation Variation { get => variation; }

        public Analytics(FeatureConfig featureConfig, Target target, Variation variation, EventType eventType)
        {
            this.featureConfig = featureConfig;
            this.target = target;
            this.variation = variation;
        }

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
