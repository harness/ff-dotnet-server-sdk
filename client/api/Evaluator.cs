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
            if (variation != null && bool.TryParse(variation.Value, out res))
            {
                return res;
            }

            LogEvaluationFailureError(FeatureConfigKind.Boolean, key, target, defaultValue.ToString());
            return defaultValue;
        }

        public JObject JsonVariation(string key, Target target, JObject defaultValue)
        {
            var variation = EvaluateVariation(key, target, FeatureConfigKind.Json);
            if (variation != null)
            {
                return JObject.Parse(variation.Value);
            }

            LogEvaluationFailureError(FeatureConfigKind.String, key, target, defaultValue.ToString());
            return defaultValue;
        }

        public double NumberVariation(string key, Target target, double defaultValue)
        {
            var variation = EvaluateVariation(key, target, FeatureConfigKind.Int);
            double res;
            if (variation != null && double.TryParse(variation.Value, out res))
            {
                return res;
            }

            LogEvaluationFailureError(FeatureConfigKind.String, key, target, defaultValue.ToString());
            return defaultValue;
        }

        public string StringVariation(string key, Target target, string defaultValue)
        {
            var variation = EvaluateVariation(key, target, FeatureConfigKind.String);
            if (variation != null)
            {
                return variation.Value;
            }

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
                    "SDKCODE(eval:6001): Failed to evaluate {kind} variation for {targetId}, flag {featureId} and the default variation {defaultValue} is being returned",
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
            var variation = featureConfig.OffVariation;
            if (featureConfig.State == FeatureState.On)
            {
                variation = null;
                if (featureConfig.VariationToTargetMap != null)
                {
                    variation = EvaluateVariationMap(target, featureConfig.VariationToTargetMap);
                    if (variation != null)
                        logger.LogDebug("Specific targeting matched: Target({Target}) Flag({Flag})",
                            target.ToString(), ToStringHelper.FeatureConfigToString(featureConfig));
                }

                if (variation == null) variation = EvaluateRules(featureConfig, target);
                if (variation == null) variation = EvaluateDistribution(featureConfig, target);
                if (variation == null) variation = featureConfig.DefaultServe.Variation;
            }

            if (variation != null && featureConfig.Variations != null)
                return featureConfig.Variations.FirstOrDefault(var => var.Identifier.Equals(variation));
            return null;
        }

        private string EvaluateVariationMap(Target target, ICollection<VariationMap> variationMaps)
        {
            if (variationMaps == null || target == null) return null;
            foreach (var variationMap in variationMaps)
            {
                if (variationMap.Targets != null && variationMap.Targets.ToList()
                        .Any(t => t != null && t.Identifier.Equals(target.Identifier))) return variationMap.Variation;
                if (variationMap.TargetSegments != null &&
                    IsTargetIncludedOrExcludedInSegment(variationMap.TargetSegments.ToList(), target))
                    return variationMap.Variation;
            }

            return null;
        }

        private string EvaluateRules(FeatureConfig featureConfig, Target target)
        {
            if (featureConfig.Rules == null || target == null) return null;

            foreach (var servingRule in featureConfig.Rules.ToList().OrderBy(sr => sr.Priority))
            {
                if (servingRule.Clauses != null &&
                    servingRule.Clauses.ToList().Any(c => EvaluateClause(c, target) == false)) continue;

                if (servingRule.Serve != null)
                {
                    if (servingRule.Serve.Distribution != null)
                    {
                        var distributionProcessor = new DistributionProcessor(servingRule.Serve, loggerFactory);
                        return distributionProcessor.loadKeyName(target);
                    }

                    if (servingRule.Serve.Variation != null) return servingRule.Serve.Variation;
                }
            }

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
                if (segment != null)
                {
                    // check exclude list
                    if (segment.Excluded != null && segment.Excluded.Any(t => t.Identifier.Equals(target.Identifier)))
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                            logger.LogDebug("Group excluded rule matched: Target({targetName}) Group({segmentName})",
                                target.ToString(), ToStringHelper.SegmentToString(segment));
                        return false;
                    }

                    // check include list
                    if (segment.Included != null && segment.Included.Any(t => t.Identifier.Equals(target.Identifier)))
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                            logger.LogDebug("Group included rule matched: Target({targetName}) Group({segmentName})",
                                target.ToString(), ToStringHelper.SegmentToString(segment));

                        return true;
                    }

                    // if we have rules, at least one should pass
                    if (segment.Rules != null)
                    {
                        var firstSuccess = segment.Rules.FirstOrDefault(r => EvaluateClause(r, target));
                        if (firstSuccess != null)
                        {
                            if (logger.IsEnabled(LogLevel.Debug))
                                logger.LogDebug(
                                    "Group condition rule matched: Target({targetName}) Group({segmentName})",
                                    target.ToString(), ToStringHelper.SegmentToString(segment));
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool EvaluateClause(Clause clause, Target target)
        {
            // operator is mandatory
            if (clause == null || string.IsNullOrEmpty(clause.Op)) return false;

            if (clause.Values == null || clause.Values.Count == 0) return false;

            if (clause.Op == "segmentMatch") return IsTargetIncludedOrExcludedInSegment(clause.Values.ToList(), target);

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
                    if ((target.Attributes != null) & target.Attributes.ContainsKey(attribute))
                        return target.Attributes[attribute];
                    return null;
            }
        }
    }
}