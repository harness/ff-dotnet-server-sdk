using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using io.harness.cfsdk.client.api.rules;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Target = io.harness.cfsdk.client.dto.Target;

[assembly: InternalsVisibleToAttribute("ff-server-sdk-test")]

namespace io.harness.cfsdk.client.api
{
    internal interface IEvaluatorCallback
    {
        void EvaluationProcessed(FeatureConfig featureConfig, Target target, Variation variation);
    }

    internal interface IEvaluator
    {
        bool BoolVariation(string key, Target target, bool defaultValue);
        string StringVariation(string key, Target target, string defaultValue);
        double NumberVariation(string key, Target target, double defaultValue);
        JObject JsonVariation(string key, Target target, JObject defaultValue);
    }

    internal class Evaluator : IEvaluator
    {
        private readonly IEvaluatorCallback callback;
        private readonly bool IsAnalyticsEnabled;
        private readonly ILogger<Evaluator> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly IRepository repository;

        public Evaluator(IRepository repository, IEvaluatorCallback callback, ILoggerFactory loggerFactory,
            bool isAnalyticsEnabled)
        {
            this.repository = repository;
            this.callback = callback;
            logger = loggerFactory.CreateLogger<Evaluator>();
            this.loggerFactory = loggerFactory;
            IsAnalyticsEnabled = isAnalyticsEnabled;
        }

        public bool BoolVariation(string key, Target target, bool defaultValue)
        {
            var variation = EvaluateVariation(key, target, FeatureConfigKind.Boolean);
            bool res;
            if (variation != null && bool.TryParse(variation.Value, out res)) return res;

            LogEvaluationFailureError(FeatureConfigKind.Boolean, key, target, defaultValue.ToString());
            return defaultValue;
        }

        public JObject JsonVariation(string key, Target target, JObject defaultValue)
        {
            var variation = EvaluateVariation(key, target, FeatureConfigKind.Json);
            if (variation != null) return JObject.Parse(variation.Value);

            LogEvaluationFailureError(FeatureConfigKind.String, key, target, defaultValue.ToString());
            return defaultValue;
        }

        public double NumberVariation(string key, Target target, double defaultValue)
        {
            var variation = EvaluateVariation(key, target, FeatureConfigKind.Int);
            double res;
            if (variation != null && double.TryParse(variation.Value, out res)) return res;

            LogEvaluationFailureError(FeatureConfigKind.String, key, target, defaultValue.ToString());
            return defaultValue;
        }

        public string StringVariation(string key, Target target, string defaultValue)
        {
            var variation = EvaluateVariation(key, target, FeatureConfigKind.String);
            if (variation != null) return variation.Value;

            LogEvaluationFailureError(FeatureConfigKind.String, key, target, defaultValue);
            return defaultValue;
        }

        private Variation EvaluateVariation(string key, Target target, FeatureConfigKind kind)
        {
            var featureConfig = repository.GetFlag(key);
            if (featureConfig == null || featureConfig.Kind != kind)
                return null;

            var prerequisites = featureConfig.Prerequisites;
            if (prerequisites != null && prerequisites.Count > 0)
            {
                var prereq = CheckPreRequisite(featureConfig, target);
                if (!prereq)
                    return featureConfig.Variations.FirstOrDefault(v =>
                        v.Identifier.Equals(featureConfig.OffVariation));
            }

            var var = Evaluate(featureConfig, target);
            if (IsAnalyticsEnabled && var != null && callback != null)
                callback.EvaluationProcessed(featureConfig, target, var);
            return var;
        }

        private void LogEvaluationFailureError(FeatureConfigKind kind, string featureKey, Target target,
            string defaultValue)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning(
                    "SDKCODE(eval:6001): Failed to evaluate {Kind} variation for {TargetId}, flag {FeatureId} and the default variation {DefaultValue} is being returned",
                    kind, target?.Identifier ?? "null target", featureKey, defaultValue);
        }

        private bool CheckPreRequisite(FeatureConfig parentFeatureConfig, Target target)
        {
            if (parentFeatureConfig.Prerequisites != null && parentFeatureConfig.Prerequisites.Count > 0)
            {
                var prerequisites = parentFeatureConfig.Prerequisites.ToList();

                foreach (var pqs in prerequisites)
                {
                    var preReqFeatureConfig = repository.GetFlag(pqs.Feature);
                    if (preReqFeatureConfig == null) return true;

                    // Pre requisite variation value evaluated below
                    var preReqEvaluatedVariation = Evaluate(preReqFeatureConfig, target);
                    if (preReqEvaluatedVariation == null) return true;

                    var validPreReqVariations = pqs.Variations.ToList();
                    if (!validPreReqVariations.Contains(preReqEvaluatedVariation.Identifier)) return false;

                    if (!CheckPreRequisite(preReqFeatureConfig, target)) return false;
                }
            }

            return true;
        }

        private Variation Evaluate(FeatureConfig featureConfig, Target target)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Evaluating: Flag({Flag}) Target({Target})",
                    ToStringHelper.FeatureConfigToString(featureConfig), target.ToString());

            if (featureConfig.State == FeatureState.Off)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("Flag is off: Flag({Flag})", ToStringHelper.FeatureConfigToString(featureConfig));
                return GetVariation(featureConfig.Variations, featureConfig.OffVariation);
            }

            // Check for specific targeting match
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug(
                    "Evaluating specific targeting: Flag({Flag})",
                    ToStringHelper.FeatureConfigToString(featureConfig));
            var specificTargetingVariation =
                EvaluateVariationMap(target, featureConfig.VariationToTargetMap, featureConfig.Feature);
            if (specificTargetingVariation != null)
            {
                logger.LogDebug("Specific targeting matched: Flag({Flag}) Target({Target})",
                    ToStringHelper.FeatureConfigToString(featureConfig), target.ToString());
                return GetVariation(featureConfig.Variations, specificTargetingVariation);
            }

            // Evaluate rules
            var rulesVariation = EvaluateRules(featureConfig, target);
            if (rulesVariation != null) return GetVariation(featureConfig.Variations, rulesVariation);

            // Use default serve variation
            var defaultVariation = featureConfig.DefaultServe.Variation;
            if (defaultVariation == null)
            {
                logger.LogWarning("Default serve variation not found: Flag({Flag})",
                    ToStringHelper.FeatureConfigToString(featureConfig));
                return null;
            }

            logger.LogDebug("Default on rule matched: Target({Target}) Flag({Flag})",
                target.ToString(), ToStringHelper.FeatureConfigToString(featureConfig));
            return GetVariation(featureConfig.Variations, defaultVariation);
        }

        private Variation GetVariation(ICollection<Variation> variations, string variationIdentifier)
        {
            return variations.FirstOrDefault(var => var.Identifier.Equals(variationIdentifier));
        }

        private string EvaluateVariationMap(Target target, ICollection<VariationMap> variationMaps,
            string featureIdentifier)
        {
            if (variationMaps == null)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug(
                        "No specific targeting rules found in flag ({FeatureIdentifier})", featureIdentifier);
                return null;
            }

            if (target == null) return null;
            foreach (var variationMap in variationMaps)
            {
                if (variationMap.Targets != null && variationMap.Targets.ToList()
                        .Any(t => t != null && t.Identifier.Equals(target.Identifier))) return variationMap.Variation;
                // Legacy: the variation to target map no longer contains TargetSegments. These are stored in group rules.
                if (variationMap.TargetSegments != null &&
                    IsTargetIncludedOrExcludedInSegment(variationMap.TargetSegments.ToList(), target))
                    return variationMap.Variation;
            }

            return null;
        }

        private string EvaluateRules(FeatureConfig featureConfig, Target target)
        {
            // No rules to evaluate or target is not supplied
            if (featureConfig.Rules == null || target == null) return null;

            foreach (var servingRule in featureConfig.Rules.OrderBy(sr => sr.Priority))
            {
                // Invalid state: Log if Clauses are null 
                if (servingRule.Clauses == null)
                {
                    logger.LogWarning("Clauses are null for servingRule {RuleId} in FeatureConfig {FeatureConfigId}",
                        servingRule.RuleId, ToStringHelper.FeatureConfigToString(featureConfig));
                    return null;
                }

                // Proceed if any clause evaluation fails
                if (servingRule.Clauses.Any(c => !EvaluateClause(c, target))) continue;

                // Invalid state: Log if Serve is null
                if (servingRule.Serve == null)
                {
                    logger.LogWarning("Serve is null for rule ID {Rule} in FeatureConfig {FeatureConfig}",
                        servingRule.RuleId, ToStringHelper.FeatureConfigToString(featureConfig));
                    return null;
                }

                // Check if percentage rollout applies
                if (servingRule.Serve.Distribution != null)
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug(
                            "Percentage rollout applies to group rule, evaluating distribution: Target({Target}) Flag({Flag})",
                            target.ToString(), ToStringHelper.FeatureConfigToString(featureConfig));

                    var distributionProcessor = new DistributionProcessor(servingRule.Serve, loggerFactory);
                    return distributionProcessor.loadKeyName(target);
                }

                // Invalid state: Log if the variation is null
                if (servingRule.Serve.Variation == null)
                {
                    logger.LogWarning("Serve.Variation is null for a rule in FeatureConfig {FeatureConfig}",
                        ToStringHelper.FeatureConfigToString(featureConfig));
                    return null;
                }

                return servingRule.Serve.Variation;
            }

            // Log if no applicable rule was found
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("No applicable rule found for Target({Target})  Flag({FeatureConfig})",
                    target.ToString(), ToStringHelper.FeatureConfigToString(featureConfig));

            return null;
        }


        private string EvaluateDistribution(FeatureConfig featureConfig, Target target)
        {
            if (featureConfig.Rules == null || target == null) return null;

            var distributionProcessor = new DistributionProcessor(featureConfig.DefaultServe, loggerFactory);
            return distributionProcessor.loadKeyName(target);
        }

        private bool IsTargetIncludedOrExcludedInSegment(List<string> segmentList, Target target)
        {
            foreach (var segmentIdentifier in segmentList)
            {
                var segment = repository.GetSegment(segmentIdentifier);
                if (segment == null)
                    throw new InvalidCacheStateException(
                        $"Segment with identifier {segmentIdentifier} could not be found in the cache. This might indicate a cache inconsistency or missing data.");

                logger.LogDebug("Evaluating group rule: Group({Segment} Target({Target}))",
                    ToStringHelper.SegmentToString(segment), target.ToString());

                // check exclude list
                if (segment.Excluded != null && segment.Excluded.Any(t => t.Identifier.Equals(target.Identifier)))
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug("Group excluded rule matched: Target({TargetName}) Group({SegmentName})",
                            target.ToString(), ToStringHelper.SegmentToString(segment));
                    return false;
                }

                // check include list
                if (segment.Included != null && segment.Included.Any(t => t.Identifier.Equals(target.Identifier)))
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug("Group included rule matched: Target({TargetName}) Group({SegmentName})",
                            target.ToString(), ToStringHelper.SegmentToString(segment));

                    return true;
                }

                // Check custom rules
                if (segment.Rules == null)
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug("No group rules found in group: Group({SegmentName})",
                            ToStringHelper.SegmentToString(segment));
                    return false;
                }

                var firstSuccess = segment.Rules.FirstOrDefault(r => EvaluateClause(r, target));
                if (firstSuccess != null)
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug(
                            "Group condition rule matched: Target({TargetName}) Group({SegmentName})",
                            target.ToString(), ToStringHelper.SegmentToString(segment));
                    return true;
                }
            }

            return false;
        }

        private bool EvaluateClause(Clause clause, Target target)
        {
            // operator is mandatory
            if (clause == null || string.IsNullOrEmpty(clause.Op)) return false;

            if (clause.Values == null || clause.Values.Count == 0) return false;

            try
            {
                if (clause.Op == "segmentMatch")
                    return IsTargetIncludedOrExcludedInSegment(clause.Values.ToList(), target);
            }
            catch (InvalidCacheStateException ex)
            {
                logger.LogError(ex, "Invalid cache state detected while evaluating group rule {Clause}",
                    ToStringHelper.ClauseToString(clause));
                return false;
            }

            object attrValue = GetAttrValue(target, clause.Attribute);
            if (attrValue == null) return false;

            var attrStr = attrValue.ToString();
            var value = clause.Values.First();

            switch (clause.Op)
            {
                case "starts_with":
                    return attrStr.StartsWith(value);
                case "ends_with":
                    return attrStr.EndsWith(value);
                case "match":
                    var rgx = new Regex(value);
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

        public static string GetAttrValue(Target target, string attribute)
        {
            switch (attribute)
            {
                case "identifier":
                    return target.Identifier;
                case "name":
                    return target.Name;
                default:
                    if (target.Attributes != null && target.Attributes.ContainsKey(attribute))
                        return target.Attributes[attribute];
                    return null;
            }
        }
    }

    public class InvalidCacheStateException : Exception
    {
        public InvalidCacheStateException()
        {
        }

        public InvalidCacheStateException(string message)
            : base(message)
        {
        }

        public InvalidCacheStateException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}