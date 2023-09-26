using io.harness.cfsdk.HarnessOpenAPIService;

namespace io.harness.cfsdk.client.api.rules
{
    public class DistributionProcessor
    {
        private readonly Distribution distribution;

        public DistributionProcessor(Serve serve)
        {
            this.distribution = serve.Distribution;
        }

        public string loadKeyName(dto.Target target)
        {

            if (distribution == null || distribution.Variations == null)
            {
                return null;
            }

            string variation = "";
            foreach (WeightedVariation weightedVariation in distribution.Variations)
            {
                variation = weightedVariation.Variation;
                if (isEnabled(target, weightedVariation.Weight))
                {
                    return variation;
                }
            }
            return variation;
        }

        private bool isEnabled(dto.Target target, int percentage)
        {
            object value = Evaluator.GetAttrValue(target, distribution.BucketBy);

            string identifier = "";
            if (value != null)
            {
                identifier = value.ToString();
            }

            if (identifier.Equals(""))
            {
                return false;
            }

            Strategy strategy = new Strategy(identifier, distribution.BucketBy);
            int bucketId = strategy.loadNormalizedNumber();

            return percentage > 0 && bucketId <= percentage;
        }
    }
}
