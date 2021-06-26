using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.api.rules;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.HarnessOpenAPIService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace io.harness.cfsdk.client
{
    public class Evaluator
    {
        private SegmentCache segmentCache;

        public Evaluator(SegmentCache segmentCache)
        {
            this.segmentCache = segmentCache;
        }

        public Variation evaluate(FeatureConfig featureConfig, dto.Target target)
        {
            string servedVariation = featureConfig.OffVariation;
            if (featureConfig.State == FeatureState.Off)
            {
                return getVariation(featureConfig.Variations, servedVariation);
            }

            servedVariation = processVariationMap(target, featureConfig.VariationToTargetMap);
            if (servedVariation != null)
            {
                return getVariation(featureConfig.Variations, servedVariation);
            }


            servedVariation = processRules(featureConfig, target);
            if (servedVariation != null)
            {
                return getVariation(featureConfig.Variations, servedVariation);
            }

            Serve defaultServe = featureConfig.DefaultServe;
            servedVariation = processDefaultServe(defaultServe, target);

            return getVariation(featureConfig.Variations, servedVariation);
        }
        private string processDefaultServe(Serve defaultServe, dto.Target target)
        {
            if (defaultServe == null)
            {
                throw new CfClientException("The serving rule is missing default serve.");
            }
            string servedVariation;
            if (defaultServe.Variation != null)
            {
                servedVariation = defaultServe.Variation;
            }
            else if (defaultServe.Distribution != null)
            {
                DistributionProcessor distributionProcessor = new DistributionProcessor(defaultServe);
                servedVariation = distributionProcessor.loadKeyName(target);
            }
            else
            {
                throw new CfClientException("The default serving rule is invalid.");
            }
            return servedVariation;
        }

        private Variation getVariation(ICollection<Variation> variations, string variationIdentifier)
        {
            foreach (Variation variation in variations)
            {
                if (variationIdentifier == variation.Identifier)
                {
                    return variation;
                }
            }
            throw new CfClientException("Invalid variation identifier " + variationIdentifier + ".");
        }


        private string processVariationMap(dto.Target target, ICollection<VariationMap> variationMaps)
        {
            if (variationMaps == null)
            {
                return null;
            }
            foreach (VariationMap variationMap in variationMaps)
            {
                ICollection<TargetMap> targets = variationMap.Targets;

                if (targets != null)
                {
                    foreach (TargetMap targetMap in targets)
                    {
                        if (targetMap.Identifier.Contains(target.Identifier))
                        {
                            return variationMap.Variation;
                        }
                    }
                }

                ICollection<string> segmentIdentifiers = variationMap.TargetSegments;
                if (segmentIdentifiers != null)
                {
                    foreach (string segmentIdentifier in segmentIdentifiers)
                    {
                        Segment segment = segmentCache.getIfPresent(segmentIdentifier);
                        if (segment != null)
                        {
                            ICollection<HarnessOpenAPIService.Target> includedTargets = segment.Included;
                            if (includedTargets != null)
                            {
                                foreach (HarnessOpenAPIService.Target includedTarget in includedTargets)
                                {
                                    if (includedTarget.Identifier.Contains(target.Identifier))
                                    {
                                        return variationMap.Variation;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }


        private string processRules(FeatureConfig featureConfig, dto.Target target)
        {
            ICollection<ServingRule> originalServingRules = featureConfig.Rules;
            List<ServingRule> servingRules = ((List<ServingRule>)originalServingRules).OrderBy(sr => sr.Priority).ToList();


            string servedVariation = null;
            foreach (ServingRule servingRule in servingRules)
            {
                servedVariation = processServingRule(servingRule, target);
                if (servedVariation != null)
                {
                    return servedVariation;
                }
            }
            return null;
        }

        private string processServingRule(ServingRule servingRule, dto.Target target)
        {
            foreach (Clause clause in servingRule.Clauses.Where(cl => cl != null))
            {
                if (!process(clause, target))
                { // check if the target match the clause
                    return null;
                }
            }

            Serve serve = servingRule.Serve;
            string servedVariation;
            if (serve.Variation != null)
            {
                servedVariation = serve.Variation;
            }
            else
            {
                DistributionProcessor distributionProcessor =
                    new DistributionProcessor(servingRule.Serve);
                servedVariation = distributionProcessor.loadKeyName(target);
            }
            return servedVariation;
        }

        private bool process(Clause clause, dto.Target target)
        {
            bool result = compare(clause.Values.ToList(), target, clause);
            return result != clause.Negate != null;
        }

        private bool compare(List<string> value, dto.Target target, Clause clause)
        {
            string Operator = clause.Op;
            string Object = null;
            object attrValue = null;
            try
            {
                attrValue = getAttrValue(target, clause.Attribute);
            }
            catch (CfClientException e)
            {
                attrValue = "";
            }
            Object = (String)attrValue;

            if (clause.Values == null)
            {
                throw new CfClientException("The clause is missing values");
            }

            string v = value[0];
            switch (Operator)
            {
                case "starts_with":
                    return Object.StartsWith(v);
                case "ends_with":
                    return Object.EndsWith(v);
                case "match":
                    Regex rgx = new Regex(v);
                    return rgx.IsMatch(Object);
                case "contains":
                    return Object.Contains(v);
                case "equal":
                    return Object.ToLower().Equals(v.ToLower());
                case "equal_sensitive":
                    return Object.Equals(v);
                case "in":
                    return value.Contains(Object);
                case "segmentMatch":
                    foreach (string segmentIdentifier in value)
                    {
                        Segment segment = segmentCache.getIfPresent(segmentIdentifier);
                        if (segment != null)
                        {
                            List<HarnessOpenAPIService.Target> includedTargets = segment.Included.ToList();
                            if (includedTargets != null)
                            {
                                foreach (HarnessOpenAPIService.Target includedTarget in includedTargets)
                                {
                                    if (includedTarget.Identifier.Contains(target.Identifier))
                                    {
                                        return true;
                                    }
                                }
                            }
                            if (segment.Rules != null)
                            {
                                foreach (Clause rule in segment.Rules)
                                {
                                    try
                                    {
                                        Object = (string)getAttrValue(target, rule.Attribute);
                                    }
                                    catch (CfClientException e)
                                    {
                                        Object = "";
                                    }
                                    if (Object != null)
                                    {
                                        List<string> values = new List<string>();
                                        values.Add(Object);
                                        bool returnValue = compare(values, target, rule);
                                        if (returnValue)
                                        {
                                            return returnValue;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    return false;
                default:
                    return false;
            }
        }

        public static object getAttrValue(dto.Target target, string attribute)
        {
            Type t = target.GetType();
            PropertyInfo[] props = t.GetProperties();

            var field = props.FirstOrDefault(f => f.Name == attribute);

            if (field != null)
            {
                return field;
            }
            else
            {
                if (target.Attributes != null & target.Attributes.ContainsKey(attribute))
                {
                    return target.Attributes[attribute];
                }
            }

            throw new CfClientException("The attribute"+ attribute+" does not exist");
        }

    }
}
