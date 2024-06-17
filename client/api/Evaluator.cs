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
using System.Collections.Generic;
using Newtonsoft.Json;

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
        JToken JsonVariationToken(string key, Target target, JToken defaultValue); 
        JObject JsonVariation(string key, Target target, JObject defaultValue);

    }

    internal class Evaluator : IEvaluator
    {
        private readonly IEvaluatorCallback callback;
        private readonly bool IsAnalyticsEnabled;
        private readonly ILogger<Evaluator> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly IRepository repository;
        private readonly IPollingProcessor poller;
        private readonly Config config;


        public Evaluator(IRepository repository, IEvaluatorCallback callback, ILoggerFactory loggerFactory,
            bool isAnalyticsEnabled, IPollingProcessor poller, Config config)
        {
            this.repository = repository;
            this.callback = callback;
            logger = loggerFactory.CreateLogger<Evaluator>();
            this.loggerFactory = loggerFactory;
            IsAnalyticsEnabled = isAnalyticsEnabled;
            this.poller = poller;
            this.config = config;
        }

        public bool BoolVariation(string key, Target target, bool defaultValue)
        {
            var variation = EvaluateVariation(key, target, FeatureConfigKind.Boolean);
            bool res;
            if (variation != null && bool.TryParse(variation.Value, out res)) return res;

            LogEvaluationFailureError(FeatureConfigKind.Boolean, key, target, defaultValue.ToString());
            return defaultValue;
        }
        public JToken JsonVariationToken(string key, Target target, JToken defaultValue)
        {
            var variation = EvaluateVariation(key, target, FeatureConfigKind.Json);
            if (variation != null)
            {
                try
                {
                    return JToken.Parse(variation.Value);
                }
                catch (JsonReaderException ex)
                {
                    logger.LogWarning("Failed to parse JSON variation");
                    LogEvaluationFailureError(FeatureConfigKind.Json, key, target, defaultValue.ToString());
                }
            }

            LogEvaluationFailureError(FeatureConfigKind.Json, key, target, defaultValue.ToString());
            return defaultValue;
        }
        
        public JObject JsonVariation(string key, Target target, JObject defaultValue)
        {
            var variation = EvaluateVariation(key, target, FeatureConfigKind.Json);
            if (variation != null)
            {
                try
                {
                    var token = JToken.Parse(variation.Value);
                    if (token.Type == JTokenType.Object) return (JObject)token;
                    if (logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning("JSON variation is not an object. Returning default value. Use JsonVariation(string key, Target target, JToken defaultValue) which is available since version 1.7.0");
                        LogEvaluationFailureError(FeatureConfigKind.Json, key, target, defaultValue.ToString());
                    }
                    return defaultValue;
                }
                catch (JsonReaderException ex)
                {
                    // Log the error if parsing fails
                    LogEvaluationFailureError(FeatureConfigKind.Json, key, target, ex.Message);
                }
            }

            LogEvaluationFailureError(FeatureConfigKind.Json, key, target, defaultValue.ToString());
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
            if (featureConfig == null)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning(
                    "Unable to find flag {Key} in cache, refreshing flag cache and retrying evaluation ",
                     key);

                if (poller != null)
                {
                    var refreshResult = poller.RefreshFlags(TimeSpan.FromSeconds(config.CacheRecoveryTimeoutInMs));

                    if (refreshResult != RefreshOutcome.Success)
                        return null;
                }

                // Re-attempt to fetch the feature config after the refresh
                featureConfig = repository.GetFlag(key);

                // If still not found or doesn't match the kind, return null to indicate failure
                if (featureConfig == null)
                {
                    if (logger.IsEnabled(LogLevel.Error))
                        logger.LogError("Failed to find flag {Key} in cache even after attempting a refresh. Check flag exists in project", key);
                    return null;
                }
            }

            if (featureConfig.Kind != kind)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning(
                    "Requested variation {Kind} does not match flag {Key} which is of type {featureConfigKind}",
                    kind,  key, featureConfig.Kind);
                return null;
            }

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
                var prerequisites = parentFeatureConfig.Prerequisites;

                foreach (var pqs in prerequisites)
                {
                    var preReqFeatureConfig = repository.GetFlag(pqs.Feature);
                    if (preReqFeatureConfig == null) return true;

                    // Pre requisite variation value evaluated below
                    var preReqEvaluatedVariation = Evaluate(preReqFeatureConfig, target);
                    if (preReqEvaluatedVariation == null) return true;

                    var validPreReqVariations = pqs.Variations;
                    if (!validPreReqVariations.Contains(preReqEvaluatedVariation.Identifier)) return false;

                    if (!CheckPreRequisite(preReqFeatureConfig, target)) return false;
                }
            }

            return true;
        }

        private Variation Evaluate(FeatureConfig featureConfig, Target target)
        {
            if (logger.IsEnabled(LogLevel.Debug))

                logger.LogDebug("Evaluating: Flag({@FeatureFlag}) Target({@Target})", new { FeatureFlag = featureConfig}, new { Target = target});


            if (featureConfig.State == FeatureState.Off)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("Flag is off: Flag({@Flag})", new { FeatureFlag = featureConfig});
                return GetVariation(featureConfig.Variations, featureConfig.OffVariation);
            }

            // Check for specific targeting match
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug(
                    "Evaluating specific targeting: Flag({@Flag})",
                    new { FeatureFlag = featureConfig});

            var specificTargetingVariation =
                EvaluateVariationMap(target, featureConfig.VariationToTargetMap, featureConfig.Feature);
            if (specificTargetingVariation != null)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("Specific targeting matched: Flag({@Flag}) Target({@Target})",
                        new { FeatureFlag = featureConfig}, new { Target = target});

                return GetVariation(featureConfig.Variations, specificTargetingVariation);
            }

            // Evaluate rules
            var rulesVariation = EvaluateRules(featureConfig, target);
            if (rulesVariation != null) return GetVariation(featureConfig.Variations, rulesVariation);

            // Use default serve variation
            var defaultVariation = featureConfig.DefaultServe.Variation;
            if (defaultVariation == null)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("Default serve variation not found: Flag({@Flag})",
                    new { Flag = featureConfig});
                return null;
            }

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Default on rule matched: Target({@Target}) Flag({@Flag})",
                    new { Target = target}, new { Flag = featureConfig});

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
                if (variationMap.Targets != null && variationMap.Targets
                        .Any(t => t != null && t.Identifier.Equals(target.Identifier))) return variationMap.Variation;
                // Legacy: the variation to target map no longer contains TargetSegments. These are stored in group rules.

                if (variationMap.TargetSegments != null &&
                    IsTargetIncludedOrExcludedInSegment(variationMap.TargetSegments, target))
                    return variationMap.Variation;
            }

            return null;
        }

        private string EvaluateRules(FeatureConfig featureConfig, Target target)
        {
            // No rules to evaluate or target is not supplied
            if (featureConfig.Rules == null || target == null) return null;

            foreach (var servingRule in featureConfig.Rules)
            {
                // Invalid state: Log if Clauses are null 
                if (servingRule.Clauses == null)
                {
                    if (logger.IsEnabled(LogLevel.Warning))
                        logger.LogWarning("Clauses are null for servingRule {RuleId} in FeatureConfig {@FeatureConfigId}",
                        servingRule.RuleId, new { Flag = featureConfig});

                    return null;
                }

                // Proceed if any clause evaluation fails
                if (servingRule.Clauses.Any(c => !EvaluateClause(c, target))) continue;

                // Invalid state: Log if Serve is null
                if (servingRule.Serve == null)
                {
                    if (logger.IsEnabled(LogLevel.Warning))
                        logger.LogWarning("Serve is null for rule ID {Rule} in FeatureConfig {@FeatureConfig}",
                        servingRule.RuleId, new { Flag = featureConfig});

                    return null;
                }

                // Check if percentage rollout applies
                if (servingRule.Serve.Distribution != null)
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug(
                            "Percentage rollout applies to group rule, evaluating distribution: Target({@Target}) Flag({@Flag})",
                            new { Target = target}, new { Flag = featureConfig});
                            

                    var distributionProcessor = new DistributionProcessor(servingRule.Serve, loggerFactory);
                    return distributionProcessor.loadKeyName(target);
                }

                // Invalid state: Log if the variation is null
                if (servingRule.Serve.Variation == null)
                {
                    if (logger.IsEnabled(LogLevel.Warning))
                        logger.LogWarning("Serve.Variation is null for a rule in Flag({@FeatureConfig})",
                             new { Flag = featureConfig});
                        
                    return null;
                }

                return servingRule.Serve.Variation;
            }

            // Log if no applicable rule was found
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("No applicable rule found for Target({@Target})  Flag({@FeatureConfig})",
                    new { Target = target}, new { Flag = featureConfig});
            return null;
        }
        
        private bool IsTargetIncludedOrExcludedInSegment(ICollection<string> segmentList, Target target)
        {
            foreach (var segmentIdentifier in segmentList)
            {
                var segment = repository.GetSegment(segmentIdentifier);
                if (segment == null)
                    throw new InvalidCacheStateException(
                        $"Segment with identifier {segmentIdentifier} could not be found in the cache despite belonging to the flag.");

                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("Evaluating group rule: Group({@Segment} Target({@Target}))",
            new { Segment = segment}, new { Target = target});

                // check exclude list
                if (segment.Excluded != null && segment.Excluded.Any(t => t.Identifier.Equals(target.Identifier)))
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug("Group excluded rule matched: Target({@TargetName}) Group({@SegmentName})",
                            new { Target = target}, new { Segment = segment});
                    return false;
                }

                // check include list
                if (segment.Included != null && segment.Included.Any(t => t.Identifier.Equals(target.Identifier)))
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug("Group included rule matched: Target({@TargetName}) Group({@SegmentName})",
                            new { Target = target}, new { Segment = segment});

                    return true;
                }

                var servingRules = segment.ServingRules;
                if (servingRules != null && servingRules.Count > 0)
                {
                    // Use enhanced rules first if they're available

                    if (servingRules.Any(r => r.Clauses.Count > 0 && r.Clauses.All(c => EvaluateClause(c, target))))
                    {
                        return true;
                    }
                }
                else
                {
                    // Fall back to legacy rules
                    // Check custom rules
                    if (segment.Rules == null)
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                            logger.LogDebug("No group rules found in group: Group({@SegmentName})",
                                new { Segment = segment });
                        return false;
                    }

                    var firstSuccess = segment.Rules.FirstOrDefault(r => EvaluateClause(r, target));
                    if (firstSuccess != null)
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                            logger.LogDebug(
                                "Group condition rule matched: Condition({@Condition}) Target({@TargetName}) Group({@SegmentName})",
                                new { Condition = firstSuccess }, new { Target = target }, new { Segment = segment });
                        return true;
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


            if (clause.Op == "segmentMatch")
                return IsTargetIncludedOrExcludedInSegment(clause.Values, target);

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
                    if (config.UseMapForInClause && clause.AdditionalProperties.TryGetValue(StorageRepository.AdditionalPropertyValueAsSet, out var valuesObj))
                    {
                        return ((HashSet<string>)valuesObj).Contains(attrStr);  // O(1) lookup
                    }
                    return clause.Values.Contains(attrStr); // O(n) lookup
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