using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using io.harness.cfsdk.client.api.rules;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleToAttribute("ff-server-sdk-test")]

namespace io.harness.cfsdk.client.api
{
    interface IEvaluatorCallback
    {
        void EvaluationProcessed(FeatureConfig featureConfig, dto.Target target, Variation variation);
    }
    interface IEvaluator
    {
        bool BoolVariation(string key, dto.Target target, bool defaultValue);
        string StringVariation(string key, dto.Target target, string defaultValue);
        double NumberVariation(string key, dto.Target target, double defaultValue);
        JObject JsonVariation(string key, dto.Target target, JObject defaultValue);
    }

    internal class Evaluator : IEvaluator
    {
        private readonly ILogger<Evaluator> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly IRepository repository;
        private readonly IEvaluatorCallback callback;
        private readonly bool IsAnalyticsEnabled;

        public Evaluator(IRepository repository, IEvaluatorCallback callback, ILoggerFactory loggerFactory, bool isAnalyticsEnabled)
        {
            this.repository = repository;
            this.callback = callback;
            this.logger = loggerFactory.CreateLogger<Evaluator>();
            this.loggerFactory = loggerFactory;
            this.IsAnalyticsEnabled = isAnalyticsEnabled;
        }
        private Variation EvaluateVariation(string key, dto.Target target, FeatureConfigKind kind)
        {
            FeatureConfig featureConfig = this.repository.GetFlag(key);
            if (featureConfig == null || featureConfig.Kind != kind)
                return null;

            ICollection<Prerequisite> prerequisites = featureConfig.Prerequisites;
            if (prerequisites != null && prerequisites.Count > 0)
            {
                bool prereq = CheckPreRequisite(featureConfig, target);
                if( !prereq)
                {
                    return featureConfig.Variations.FirstOrDefault(v => v.Identifier.Equals(featureConfig.OffVariation));
                }
            }

            Variation var = Evaluate(featureConfig, target);
            if(IsAnalyticsEnabled && var != null && callback != null)
            {
                this.callback.EvaluationProcessed(featureConfig, target, var);
            }
            return var;
        }

        public bool BoolVariation(string key, dto.Target target, bool defaultValue)
        {
            Variation variation = EvaluateVariation(key, target, FeatureConfigKind.Boolean);
            bool res;
            if (variation != null && Boolean.TryParse(variation.Value, out res))
            {
                return res;
            }
            else
            {
                LogEvaluationFailureError(FeatureConfigKind.Boolean, key, target, defaultValue.ToString());
                return defaultValue;
            }
        }

        public JObject JsonVariation(string key, dto.Target target, JObject defaultValue)
        {
            Variation variation = EvaluateVariation(key, target, FeatureConfigKind.Json);
            if (variation != null)
            {
                return JObject.Parse(variation.Value);
            }
            else
            {
                LogEvaluationFailureError(FeatureConfigKind.String, key, target, defaultValue.ToString());
                return defaultValue;
            }
        }

        public double NumberVariation(string key, dto.Target target, double defaultValue)
        {
            Variation variation = EvaluateVariation(key, target, FeatureConfigKind.Int);
            double res;
            if (variation != null && Double.TryParse(variation.Value, out res))
            {
                return res;
            }
            else
            {
                LogEvaluationFailureError(FeatureConfigKind.String, key, target, defaultValue.ToString());
                return defaultValue;
            }
        }

        public string StringVariation(string key, dto.Target target, string defaultValue)
        {
            Variation variation = EvaluateVariation(key, target, FeatureConfigKind.String);
            if (variation != null)
            {
                return variation.Value;
            }
            else
            {
                LogEvaluationFailureError(FeatureConfigKind.String, key, target, defaultValue);
                return defaultValue;
            }
        }

        private void LogEvaluationFailureError(FeatureConfigKind kind, string featureKey, dto.Target target, string defaultValue)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "SDKCODE(eval:6001): Failed to evaluate {kind} variation for {targetId}, flag {featureId} and the default variation {defaultValue} is being returned",
                    kind, target?.Identifier ?? "null target", featureKey, defaultValue);
            }
        }

        private bool CheckPreRequisite(FeatureConfig parentFeatureConfig, dto.Target target)
        {
            if (parentFeatureConfig.Prerequisites != null && parentFeatureConfig.Prerequisites.Count > 0)
            {
                List<Prerequisite> prerequisites = parentFeatureConfig.Prerequisites.ToList();

                foreach (Prerequisite pqs in prerequisites)
                {
                    FeatureConfig preReqFeatureConfig = this.repository.GetFlag(pqs.Feature);
                    if (preReqFeatureConfig == null)
                    {
                        return true;
                    }

                    // Pre requisite variation value evaluated below
                    Variation preReqEvaluatedVariation = Evaluate(preReqFeatureConfig, target);
                    if(preReqEvaluatedVariation == null)
                    {
                        return true;
                    }

                    List<string> validPreReqVariations = pqs.Variations.ToList();
                    if (!validPreReqVariations.Contains(preReqEvaluatedVariation.Identifier))
                    {
                        return false;
                    }

                    if (!CheckPreRequisite(preReqFeatureConfig, target))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        private Variation Evaluate(FeatureConfig featureConfig, dto.Target target)
        {
            string variation = featureConfig.OffVariation;
            if (featureConfig.State == FeatureState.On)
            {
                variation = null;
                if (featureConfig.VariationToTargetMap != null)
                {
                    variation = EvaluateVariationMap(target, featureConfig.VariationToTargetMap);
                    if (variation != null)
                    {
                        logger.LogDebug("Specific targeting matched: Target({Target}) Flag({Flag})",
                            target.ToString(), ToStringHelper.FeatureConfigToString(featureConfig));
                    }
                }
                if (variation == null)
                {
                    variation = EvaluateRules(featureConfig, target);
                }
                if (variation == null)
                {
                    variation = EvaluateDistribution(featureConfig, target);
                }
                if (variation == null)
                {
                    variation = featureConfig.DefaultServe.Variation;
                }
            }

            if(variation != null && featureConfig.Variations != null)
            {
                return featureConfig.Variations.FirstOrDefault(var => var.Identifier.Equals(variation));
            }
            return null;

        }

        private string EvaluateVariationMap(dto.Target target, ICollection<VariationMap> variationMaps)
        {
            if (variationMaps == null || target == null)
            {
                return null;
            }
            foreach (VariationMap variationMap in variationMaps)
            {
                if (variationMap.Targets != null && variationMap.Targets.ToList().Any(t => t != null && t.Identifier.Equals(target.Identifier)) )
                {
                    return variationMap.Variation;
                }
                if( variationMap.TargetSegments != null && IsTargetIncludedOrExcludedInSegment(variationMap.TargetSegments.ToList(), target))
                {
                    return variationMap.Variation;
                }
            }
            return null;
        }

        private string EvaluateRules(FeatureConfig featureConfig, dto.Target target)
        {
            if (featureConfig.Rules == null || target == null)
            {
                return null;
            }

            foreach (ServingRule servingRule in featureConfig.Rules.ToList().OrderBy(sr => sr.Priority))
            {
                if (servingRule.Clauses != null && servingRule.Clauses.ToList().Any(c => EvaluateClause(c, target) == false))
                {
                    continue;
                }

                if( servingRule.Serve != null)
                {
                    if(servingRule.Serve.Distribution != null)
                    {
                        DistributionProcessor distributionProcessor = new DistributionProcessor(servingRule.Serve, loggerFactory);
                        return distributionProcessor.loadKeyName(target);
                    }
                    if( servingRule.Serve.Variation != null)
                    {
                        return servingRule.Serve.Variation;
                    }
                }
            }
            return null;
        }

       

        private string EvaluateDistribution(FeatureConfig featureConfig, dto.Target target)
        {
            if (featureConfig.Rules == null || target == null)
            {
                return null;
            }

            DistributionProcessor distributionProcessor = new DistributionProcessor(featureConfig.DefaultServe, loggerFactory);
            return distributionProcessor.loadKeyName(target);
        }
        private bool IsTargetIncludedOrExcludedInSegment(List<string> segmentList, dto.Target target)
        {
            foreach (string segmentIdentifier in segmentList)
            {
                Segment segment = this.repository.GetSegment(segmentIdentifier);
                if (segment != null)
                {
                    // check exclude list
                    if (segment.Excluded != null && segment.Excluded.Any(t => t.Identifier.Equals(target.Identifier)))
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug("Group excluded rule matched: Target({targetName}) Group({segmentName})",
                                target.ToString(), ToStringHelper.SegmentToString(segment));
                        }
                        return false;
                    }

                    // check include list
                    if (segment.Included != null && segment.Included.Any(t => t.Identifier.Equals(target.Identifier)))
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug("Group included rule matched: Target({targetName}) Group({segmentName})",
                                target.ToString(), ToStringHelper.SegmentToString(segment));
                        }

                        return true;
                    }

                    // if we have rules, at least one should pass
                    if (segment.Rules != null)
                    {
                        Clause firstSuccess = segment.Rules.FirstOrDefault(r => EvaluateClause(r, target));
                        if (firstSuccess != null)
                        {
                            if (logger.IsEnabled(LogLevel.Debug))
                            {
                                logger.LogDebug("Group condition rule matched: Target({targetName}) Group({segmentName})",
                                    target.ToString(), ToStringHelper.SegmentToString(segment));
                            }
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private bool EvaluateClause(Clause clause, dto.Target target)
        {
            // operator is mandatory
            if (clause == null || String.IsNullOrEmpty(clause.Op))
            {
                return false;
            }

            if (clause.Values == null || clause.Values.Count == 0)
            {
                return false;
            }

            if (clause.Op == "segmentMatch")
            {
                return IsTargetIncludedOrExcludedInSegment(clause.Values.ToList(), target);
            }

            object attrValue = GetAttrValue(target, clause.Attribute);
            if(attrValue == null)
            {
                return false;
            }

            string attrStr = attrValue.ToString();
            string value = clause.Values.First();

            switch (clause.Op)
            {
                case "starts_with":
                    return attrStr.StartsWith(value);
                case "ends_with":
                    return attrStr.EndsWith(value);
                case "match":
                    Regex rgx = new Regex(value);
                    return rgx.IsMatch(attrStr);
                case "contains":
                    return attrStr.Contains(value);
                case "equal":
                    return attrStr.ToLower().Equals(value.ToLower());
                case "equal_sensitive":
                    return attrStr.Equals(value);
                case "in":
                    return clause.Values.Contains(attrStr);
                default:
                    return false;
            }
        }

        public static string GetAttrValue(dto.Target target, string attribute)
        {
            switch (attribute)
            {
                case "identifier":
                    return target.Identifier;
                case "name":
                    return target.Name;
                default:
                    if (target.Attributes != null & target.Attributes.ContainsKey(attribute))
                    {
                        return target.Attributes[attribute];
                    }
                    return null;
            }

        }
    //     
    //
    //     public string SegmentToString(Segment segment)
    //     {
    //         var tagsStr = segment.Tags != null ? string.Join(", ", segment.Tags.Select(t => t.ToString())) : "None";
    //         var includedTargetsStr = segment.Included != null ? string.Join(", ", segment.Included.Select(t => t.Identifier)) : "None";
    //         var excludedTargetsStr = segment.Excluded != null ? string.Join(", ", segment.Excluded.Select(t => t.Identifier)) : "None";
    //         var rulesStr = segment.Rules != null ? string.Join(", ", segment.Rules.Select(ClauseToString)) : "None";
    //
    //         return $"Identifier: {segment.Identifier}, Name: {segment.Name}, Environment: {segment.Environment}, " +
    //                $"Tags: [{tagsStr}], Included Targets: [{includedTargetsStr}], Excluded Targets: [{excludedTargetsStr}], " +
    //                $"Rules: [{rulesStr}], Created At: {segment.CreatedAt}, Modified At: {segment.ModifiedAt}, Version: {segment.Version}";
    //     }
    //
    //     
    //     private string ClauseToString(Clause clause)
    //     {
    //         var valuesStr = clause.Values != null ? string.Join(", ", clause.Values) : "None";
    //         return $"Id: {clause.Id}, Attribute: {clause.Attribute}, Operation: {clause.Op}, Values: [{valuesStr}], Negate: {clause.Negate}";
    //     }
    //     
    //     private string FeatureConfigToString(FeatureConfig featureConfig)
    //     {
    //         var variationsStr = featureConfig.Variations != null 
    //             ? string.Join(", ", featureConfig.Variations.Select(v => $"{{Id: {v.Identifier}, Value: {v.Value}}}")) 
    //             : "None";
    //         
    //         var rulesStr = featureConfig.Rules != null 
    //             ? string.Join(", ", featureConfig.Rules.Select(ServingRuleToString)) // You might need to implement ToString for ServingRule or create a helper for it
    //             : "None";
    //         
    //         var prerequisitesStr = featureConfig.Prerequisites != null 
    //             ? string.Join(", ", featureConfig.Prerequisites.Select(p => p.Feature)) // Assuming Feature is a meaningful identifier for prerequisites
    //             : "None";
    //
    //         var variationMapsStr = featureConfig.VariationToTargetMap != null 
    //             ? string.Join(", ", featureConfig.VariationToTargetMap.Select(vm => vm.Variation)) // Simplified, consider expanding based on VariationMap's structure
    //             : "None";
    //     
    //         return $"Project: {featureConfig.Project}, Environment: {featureConfig.Environment}, Feature: {featureConfig.Feature}, " +
    //                $"State: {featureConfig.State}, Kind: {featureConfig.Kind}, Variations: [{variationsStr}], " +
    //                $"Rules: [{rulesStr}], Default Serve: {featureConfig.DefaultServe.Variation}, Off Variation: {featureConfig.OffVariation}, " +
    //                $"Prerequisites: [{prerequisitesStr}], VariationToTargetMap: [{variationMapsStr}], Version: {featureConfig.Version}";
    //     }
    //     
    //     private string ServingRuleToString(ServingRule servingRule)
    //     {
    //         // Assuming ClauseToString(Clause clause) is a method that converts a Clause object into a string.
    //         var clausesStr = servingRule.Clauses != null 
    //             ? string.Join(", ", servingRule.Clauses.Select(ClauseToString)) 
    //             : "None";
    //
    //         // Assuming Serve has meaningful properties to be printed out. Adjust according to actual structure.
    //         // This example simplifies Serve representation; you might need to expand it based on Serve's structure.
    //         var serveStr = servingRule.Serve != null ? $"Variation: {servingRule.Serve.Variation}" : "None";
    //     
    //         return $"RuleId: {servingRule.RuleId}, Priority: {servingRule.Priority}, Clauses: [{clausesStr}], Serve: [{serveStr}]";
    //     }
    }
}
