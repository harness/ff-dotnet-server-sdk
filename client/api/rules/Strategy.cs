using Murmur;
using System;
using System.Security.Cryptography;
using System.Text;

namespace io.harness.cfsdk.client.api.rules
{
    public class Strategy
    {
        private readonly string value;
        private readonly string bucketBy;

        public Strategy(String value, String bucketBy)
        {
            this.value = value;
            this.bucketBy = bucketBy;
        }

        public int loadNormalizedNumber()
        {
            return loadNormalizedNumberWithNormalizer(100);
        }

        public int loadNormalizedNumberWithNormalizer(int normalizer)
        {
            byte[] valueBytes = Encoding.ASCII.GetBytes(bucketBy + ":" + value);
            HashAlgorithm hasher = MurmurHash.Create32(seed: 0);
            var hashcode = (uint)BitConverter.ToInt32(hasher.ComputeHash(valueBytes), 0);
            return (int)(hashcode % normalizer) + 1;
        }
    }
}
