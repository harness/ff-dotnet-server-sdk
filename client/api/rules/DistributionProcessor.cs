using io.harness.cfsdk.HarnessOpenAPIService;
using System;

namespace io.harness.cfsdk.client.api.rules
{
    public class DistributionProcessor
    {
        private  Distribution distribution;

        public DistributionProcessor(Serve serve)
        {
            this.distribution = serve.Distribution;

            if (distribution.Variations == null)
            {
                throw new ArgumentNullException("Variations are null");
            }
        }

        public string loadKeyName(dto.Target target)
        {
            string variation = "";
            foreach (WeightedVariation weightedVariation in distribution.Variations)
            {
                variation = weightedVariation.Variation;

                if (weightedVariation.Weight != null)
                {
                    if (isEnabled(target, weightedVariation.Weight))
                    {
                        return variation;
                    }
                    else
                    {
                        throw new ArgumentNullException(" weightedVariation.Weight is  null");
                    }
                }
            }
            // distance between last variation and total percentage
            return isEnabled(target, Strategy.ONE_HUNDRED) ? variation : "";
        }

        private bool isEnabled(dto.Target target, int percentage)
        {
            string bucketBy = distribution.BucketBy;
            object value = null;
            try
            {
                value = Evaluator.getAttrValue(target, distribution.BucketBy);
            }
            catch (CfClientException e)
            {
                if (e.StackTrace != null)
                {
                    Console.Error.WriteLine(e.StackTrace);
                }
            }

            string identifier = "";
            //java original String identifier = Objects.requireNonNull(value).toString();
            if (value != null)
            {
                identifier = value.ToString();
            }

            if (identifier.Equals(""))
            {
                return false;
            }

            Strategy strategy = new Strategy(identifier, bucketBy);
            int bucketId = strategy.loadNormalizedNumber();

            return percentage > 0 && bucketId <= percentage;
        }
    }
}
