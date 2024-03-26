using System.Linq;
using io.harness.cfsdk.HarnessOpenAPIService;

namespace io.harness.cfsdk.client.api
{
    public static class ToStringHelper
    {
        public static string SegmentToString(Segment segment)
        {
            var tagsStr = segment.Tags != null ? string.Join(", ", segment.Tags.Select(t => t.ToString())) : "None";
            var includedTargetsStr = segment.Included != null
                ? string.Join(", ", segment.Included.Select(t => t.Identifier))
                : "None";
            var excludedTargetsStr = segment.Excluded != null
                ? string.Join(", ", segment.Excluded.Select(t => t.Identifier))
                : "None";
            var rulesStr = segment.Rules != null ? string.Join(", ", segment.Rules.Select(ClauseToString)) : "None";

            return $"Identifier: {segment.Identifier}, Name: {segment.Name}, Environment: {segment.Environment}, " +
                   $"Tags: [{tagsStr}], Included Targets: [{includedTargetsStr}], Excluded Targets: [{excludedTargetsStr}], " +
                   $"Rules: [{rulesStr}], Created At: {segment.CreatedAt}, Modified At: {segment.ModifiedAt}, Version: {segment.Version}";
        }

        public static string ClauseToString(Clause clause)
        {
            var valuesStr = clause.Values != null ? string.Join(", ", clause.Values) : "None";
            return
                $"Id: {clause.Id}, Attribute: {clause.Attribute}, Operation: {clause.Op}, Values: [{valuesStr}], Negate: {clause.Negate}";
        }

        public static string FeatureConfigToString(FeatureConfig featureConfig)
        {
            var variationsStr = featureConfig.Variations != null
                ? string.Join(", ", featureConfig.Variations.Select(v => $"{{Id: {v.Identifier}, Value: {v.Value}}}"))
                : "None";

            var rulesStr = featureConfig.Rules != null
                ? string.Join(", ", featureConfig.Rules.Select(r => ServingRuleToString(r)))
                : "None";

            var prerequisitesStr = featureConfig.Prerequisites != null
                ? string.Join(", ", featureConfig.Prerequisites.Select(p => p.Feature))
                : "None";

            var variationMapsStr = featureConfig.VariationToTargetMap != null && featureConfig.VariationToTargetMap.Any()
                ? string.Join(", ", featureConfig.VariationToTargetMap.Select(VariationMapToString))
                : "None";

            return
                $"Project: {featureConfig.Project}, Environment: {featureConfig.Environment}, Feature: {featureConfig.Feature}, " +
                $"State: {featureConfig.State}, Kind: {featureConfig.Kind}, Variations: [{variationsStr}], " +
                $"Rules: [{rulesStr}], Default Serve: {featureConfig.DefaultServe.Variation}, Off Variation: {featureConfig.OffVariation}, " +
                $"Prerequisites: [{prerequisitesStr}], VariationToTargetMap: [{variationMapsStr}], Version: {featureConfig.Version}";
        }

        public static string ServingRuleToString(ServingRule servingRule)
        {
            var clausesStr = servingRule.Clauses != null
                ? string.Join(", ", servingRule.Clauses.Select(clause => ClauseToString(clause)))
                : "None";

            var serveStr = servingRule.Serve != null ? $"Variation: {servingRule.Serve.Variation}" : "None";

            return
                $"RuleId: {servingRule.RuleId}, Priority: {servingRule.Priority}, Clauses: [{clausesStr}], Serve: [{serveStr}]";
        }

        public static string VariationMapToString(VariationMap variationMap)
        {
            var targetsStr = variationMap.Targets != null && variationMap.Targets.Any()
                ? string.Join(", ", variationMap.Targets.Select(t => TargetMapToString(t)))
                : "None";

            var targetSegmentsStr = variationMap.TargetSegments != null && variationMap.TargetSegments.Any()
                ? string.Join(", ", variationMap.TargetSegments)
                : "None";

            return
                $"Variation: {variationMap.Variation}, Targets: [{targetsStr}], TargetSegments: [{targetSegmentsStr}]";
        }

        public static string TargetMapToString(TargetMap targetMap)
        {
            return $"Identifier: {targetMap.Identifier}";
        }
    }
}