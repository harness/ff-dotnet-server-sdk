using System;
using System.Collections.Generic;
using io.harness.cfsdk.HarnessOpenAPIService;

namespace io.harness.cfsdk.client.dto
{
    // We send two types of analytics to the metrics service
    // 1. Evaluation metrics.
    // 2. Target metrics.

    public class EvaluationAnalytics : IEquatable<EvaluationAnalytics>
    {
        // The global target is used when we don't want to use the actual target in the evaluation metrics
        // payload. 
        public static readonly string GlobalTargetIdentifier = "__global__cf_target";
        public static readonly string GlobalTargetName = "Global Target";

        public EvaluationAnalytics(FeatureConfig featureConfig, Variation variation, Target target)
        {
            FeatureConfig = featureConfig;
            Variation = variation;
            Target = target;
        }

        public FeatureConfig FeatureConfig { get; }

        public Variation Variation { get; }
        public Target Target { get; }


        public bool Equals(EvaluationAnalytics other)
        {
            if (other == null) return false;

            return EqualityComparer<string>.Default.Equals(Target?.Identifier, other.Target?.Identifier)
                   && EqualityComparer<string>.Default.Equals(FeatureConfig?.Feature, other.FeatureConfig?.Feature)
                   && EqualityComparer<string>.Default.Equals(Variation?.Identifier, other.Variation?.Identifier)
                   && EqualityComparer<string>.Default.Equals(Variation?.Value, other.Variation?.Value);
        }


        public override int GetHashCode()
        {
            var hashCode = -1526478203;
            hashCode = hashCode * -1521134295 +
                       EqualityComparer<string>.Default.GetHashCode(Target?.Identifier ?? string.Empty);
            hashCode = hashCode * -1521134295 +
                       EqualityComparer<string>.Default.GetHashCode(FeatureConfig?.Feature ?? string.Empty);
            hashCode = hashCode * -1521134295 +
                       EqualityComparer<string>.Default.GetHashCode(Variation?.Identifier ?? string.Empty);
            hashCode = hashCode * -1521134295 +
                       EqualityComparer<string>.Default.GetHashCode(Variation?.Value ?? string.Empty);
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EvaluationAnalytics);
        }
    }

    public class TargetAnalytics : IEquatable<TargetAnalytics>
    {
        public TargetAnalytics(Target target)
        {
            Target = target;
        }

        public Target Target { get; }

        public bool Equals(TargetAnalytics other)
        {
            return other != null &&
                   EqualityComparer<string>.Default.Equals(Target?.Identifier, other.Target?.Identifier);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<string>.Default.GetHashCode(Target?.Identifier ?? string.Empty);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TargetAnalytics);
        }
    }
}