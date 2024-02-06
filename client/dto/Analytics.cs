using System;
using System.Collections.Generic;
using io.harness.cfsdk.HarnessOpenAPIService;

namespace io.harness.cfsdk.client.dto
{
// Base Analytics class
    public abstract class Analytics : IEquatable<Analytics>
    {
        protected readonly Target target;

        protected Analytics(Target target)
        {
            this.target = target;
        }

        public Target Target => target;
        public abstract bool Equals(Analytics other);

        public override bool Equals(object obj)
        {
            return Equals(obj as Analytics);
        }

        public abstract override int GetHashCode();
    }

// EvaluationAnalytics subclass
    public class EvaluationAnalytics : Analytics
    {
        // The global target is used when we don't want to use the actual target in the evaluation metrics
        // payload.  Since 1.4.2 the global target has been used.
        public static readonly string GlobalTargetIdentifier = "__global__cf_target";
        public static readonly string GlobalTargetName = "Global Target";

        public EvaluationAnalytics(FeatureConfig featureConfig, Variation variation, Target target)
            : base(target)
        {
            this.FeatureConfig = featureConfig;
            this.Variation = variation;
        }
        
        public FeatureConfig FeatureConfig { get; }

        public Variation Variation { get; }


        public override bool Equals(Analytics other)
        {
            return other is EvaluationAnalytics otherEvaluation
                   && EqualityComparer<FeatureConfig>.Default.Equals(FeatureConfig, otherEvaluation.FeatureConfig)
                   && EqualityComparer<Variation>.Default.Equals(Variation, otherEvaluation.Variation);
        }

        public override int GetHashCode()
        {
            var hashCode = -1526478203;
            hashCode = hashCode * -1521134295 + EqualityComparer<FeatureConfig>.Default.GetHashCode(FeatureConfig);
            hashCode = hashCode * -1521134295 + EqualityComparer<Variation>.Default.GetHashCode(Variation);
            return hashCode;
        }

        // Additional methods...
    }

// TargetAnalytics subclass
    public class TargetAnalytics : Analytics
    {
        private readonly Target target;

        // Properties...

        public TargetAnalytics(Target target)
            : base(target)
        {
            // Initialization...
        }

        public override bool Equals(Analytics other)
        {
            return other is TargetAnalytics otherTarget
                   && EqualityComparer<Target>.Default.Equals(target, otherTarget.target);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<Target>.Default.GetHashCode(target);
        }

        // Additional methods...
    }

// Usage in caches and other operations remains the same


    // public class Analytics : IEquatable<Analytics>
    // {
    //     private readonly FeatureConfig featureConfig;
    //     private readonly Target target;
    //     private readonly Variation variation;
    //
    //     public FeatureConfig FeatureConfig { get => featureConfig; }
    //     public Target Target { get => target; }
    //     public Variation Variation { get => variation; }
    //
    //     public Analytics(FeatureConfig featureConfig, Target target, Variation variation, EventType eventType)
    //     {
    //         this.featureConfig = featureConfig;
    //         this.target = target;
    //         this.variation = variation;
    //     }
    //
    //     public override bool Equals(object obj)
    //     {
    //         return Equals(obj as Analytics);
    //     }
    //
    //     public bool Equals(Analytics other)
    //     {
    //         return other != null &&
    //                EqualityComparer<FeatureConfig>.Default.Equals(featureConfig, other.featureConfig) &&
    //                EqualityComparer<Target>.Default.Equals(target, other.target) &&
    //                EqualityComparer<Variation>.Default.Equals(variation, other.variation);
    //     }
    //
    //     public override int GetHashCode()
    //     {
    //         int hashCode = -1526478203;
    //         hashCode = hashCode * -1521134295 + EqualityComparer<FeatureConfig>.Default.GetHashCode(featureConfig);
    //         hashCode = hashCode * -1521134295 + EqualityComparer<Target>.Default.GetHashCode(target);
    //         hashCode = hashCode * -1521134295 + EqualityComparer<Variation>.Default.GetHashCode(variation);
    //         return hashCode;
    //     }
    // }
}