using System;
using System.Collections.Generic;
using io.harness.cfsdk.HarnessOpenAPIService;

namespace io.harness.cfsdk.client.dto
{
    // We send two types of analytics to the metrics service
    // 1. Evaluation metrics.
    // 2. Target metrics.
    // Both types inherit from this base class.
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
        // payload. 
        public static readonly string GlobalTargetIdentifier = "__global__cf_target";
        public static readonly string GlobalTargetName = "Global Target";

        public EvaluationAnalytics(FeatureConfig featureConfig, Variation variation, Target target)
            : base(target)
        {
            FeatureConfig = featureConfig;
            Variation = variation;
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
    }

    // TargetAnalytics subclass
    public class TargetAnalytics : Analytics
    {
        public TargetAnalytics(Target target)
            : base(target)
        {
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
    }
}