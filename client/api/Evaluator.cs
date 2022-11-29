using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using io.harness.cfsdk.client.api.rules;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using Newtonsoft.Json.Linq;
using Serilog;

[assembly: InternalsVisibleToAttribute("ff-server-sdk-test")]

namespace io.harness.cfsdk.client.api
{
    interface IEvaluatorCallback
    {
        void evaluationProcessed(FeatureConfig featureConfig, dto.Target target, Variation variation);
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
        private IRepository repository;
        private IEvaluatorCallback callback;
        public Evaluator(IRepository repository, IEvaluatorCallback callback)
        {
            this.repository = repository;
            this.callback = callback;
        }
        private Variation EvaluateVariation(string key, dto.Target target, FeatureConfigKind kind)
        {
            FeatureConfig featureConfig = this.repository.GetFlag(key);
            if (featureConfig == null || featureConfig.Kind != kind)
                return null;

            ICollection<Prerequisite> prerequisites = featureConfig.Prerequisites;
            if (prerequisites != null && prerequisites.Count > 0)
            {
                bool prereq = checkPreRequisite(featureConfig, target);
                if( !prereq)
                {
                    return featureConfig.Variations.FirstOrDefault(v => v.Identifier.Equals(featureConfig.OffVariation));
                }
            }

            Variation var = Evaluate(featureConfig, target);
            if(var != null && callback != null)
            {
                this.callback.evaluationProcessed(featureConfig, target, var);
            }
            return var;
        }

        public bool BoolVariation(string key, dto.Target target, bool defaultValue)
        {
            Variation variation = EvaluateVariation(key, target, FeatureConfigKind.Boolean);
            bool res;
            return (variation != null && Boolean.TryParse(variation.Value, out res)) ? res : defaultValue;
        }

        public JObject JsonVariation(string key, dto.Target target, JObject defaultValue)
        {
            Variation variation = EvaluateVariation(key, target, FeatureConfigKind.Json);
            return variation != null ? JObject.Parse(variation.Value) : defaultValue;
        }

        public double NumberVariation(string key, dto.Target target, double defaultValue)
        {
            Variation variation = EvaluateVariation(key, target, FeatureConfigKind.Int);
            double res;
            return (variation != null && Double.TryParse(variation.Value, out res)) ? res : defaultValue;
        }

        public string StringVariation(string key, dto.Target target, string defaultValue)
        {
            Variation variation = EvaluateVariation(key, target, FeatureConfigKind.String);
            return variation != null ? variation.Value : defaultValue;
        }

        private bool checkPreRequisite(FeatureConfig parentFeatureConfig, dto.Target target)
        {
            bool result = true;
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
                    else
                    {
                        result = checkPreRequisite(preReqFeatureConfig, target);
                    }
                }
            }
            return result;
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
                        DistributionProcessor distributionProcessor = new DistributionProcessor(servingRule.Serve);
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
            DistributionProcessor distributionProcessor = new DistributionProcessor(featureConfig.DefaultServe);
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
                        Log.Debug($"Target {target.Name} excluded from segment {segment.Name} via exclude list");
                        return false;
                    }

                    // check include list
                    if (segment.Included != null && segment.Included.Any(t => t.Identifier.Equals(target.Identifier)))
                    {
                        Log.Debug($"Target {target.Name} included in segment {segment.Name} via include list");
                        return true;
                    }

                    // if we have rules, at least one should pass
                    if (segment.Rules != null)
                    {
                        Clause firstSuccess = segment.Rules.FirstOrDefault(r => EvaluateClause(r, target));
                        return firstSuccess != null;
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

            object attrValue = getAttrValue(target, clause.Attribute);
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
                case "attrStr":
                    return attrStr.Equals(value);
                case "in":
                    return clause.Values.Contains(attrStr);
                default:
                    return false;
            }
        }

        public static object getAttrValue(dto.Target target, string attribute)
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
