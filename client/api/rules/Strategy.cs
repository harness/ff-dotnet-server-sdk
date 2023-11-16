using Murmur;
using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api.rules
{
    public class Strategy
    {
        private readonly ILogger<Strategy> logger;
        private readonly string value;
        private readonly string bucketBy;

        public Strategy(String value, String bucketBy, ILoggerFactory loggerFactory)
        {
            this.value = value;
            this.bucketBy = bucketBy;
            this.logger = loggerFactory.CreateLogger<Strategy>();
        }

        public int loadNormalizedNumber()
        {
            return loadNormalizedNumberWithNormalizer(100);
        }

        public int loadNormalizedNumberWithNormalizer(int normalizer)
        {
            byte[] valueBytes = Encoding.ASCII.GetBytes(bucketBy + ":" + value);
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("MM3 input [{input}]", Encoding.UTF8.GetString(valueBytes));
            }
            HashAlgorithm hasher = MurmurHash.Create32(seed: 0);
            var hashcode = (uint)BitConverter.ToInt32(hasher.ComputeHash(valueBytes), 0);
            return (int)(hashcode % normalizer) + 1;
        }
    }
}
