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

        public Evaluator(IRepository repository, IEvaluatorCallback callback, ILoggerFactory loggerFactory)
        {
            this.repository = repository;
            this.callback = callback;
            this.logger = loggerFactory.CreateLogger<Evaluator>();
            this.loggerFactory = loggerFactory;
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
            if(var != null && callback != null)
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
                            logger.LogDebug("Target {targetName} excluded from segment {segmentName} via exclude list",
                                target.Name, segment.Name);
                        }
                        return false;
                    }

                    // check include list
                    if (segment.Included != null && segment.Included.Any(t => t.Identifier.Equals(target.Identifier)))
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug("Target {targetName} included in segment {segmentName} via include list",
                                target.Name, segment.Name);
                        }

                        return true;
                    }

                    // if we have rules, at least one should pass
                    if (segment.Rules != null)
                    {
                        Clause firstSuccess = segment.Rules.FirstOrDefault(r => EvaluateClause(r, target));
                        if (firstSuccess != null)
                        {
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
    }
}
