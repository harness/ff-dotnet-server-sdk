using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api.rules
{
    public class DistributionProcessor
    {
        private readonly ILogger<Evaluator> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly Distribution distribution;

        public DistributionProcessor(Serve serve, ILoggerFactory loggerFactory)
        {
            this.distribution = serve.Distribution;
            this.logger = loggerFactory.CreateLogger<Evaluator>();
            this.loggerFactory = loggerFactory;
        }

        public string loadKeyName(dto.Target target)
        {

            if (distribution == null || distribution.Variations == null)
            {
                return null;
            }

            string variation = "";
            int totalPercentage = 0;
            foreach (WeightedVariation weightedVariation in distribution.Variations)
            {
                variation = weightedVariation.Variation;
                totalPercentage += weightedVariation.Weight;
                if (isEnabled(target, totalPercentage))
                {
                    return variation;
                }
            }
            return variation;
        }

        private bool isEnabled(dto.Target target, int percentage)
        {
            string bucketBy = distribution.BucketBy;
            string value = Evaluator.GetAttrValue(target, bucketBy);
            if (string.IsNullOrEmpty(value))
            {
                string oldBB = bucketBy;
                bucketBy = "identifier";
                value = Evaluator.GetAttrValue(target, bucketBy);

                if (string.IsNullOrEmpty(value))
                {
                    return false;
                }

                logger.LogWarning("SDKCODE(eval:6002): BucketBy attribute not found in target attributes, falling back to 'identifier'");
            }

            Strategy strategy = new Strategy(value, bucketBy, loggerFactory);
            int bucketId = strategy.loadNormalizedNumber();
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "MM3 percentage_check={percentage} bucket_by={bucket_by} bucket={bucket}",
                    percentage,
                    bucketBy,
                    bucketId);
            }
            return percentage > 0 && bucketId <= percentage;
        }
    }
}
