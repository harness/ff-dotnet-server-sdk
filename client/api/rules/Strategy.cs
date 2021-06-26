
using Murmur;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace io.harness.cfsdk.client.api.rules
{
    public class Strategy
    {
        public static int ONE_HUNDRED = 100;

        private string identifier;
        private string bucketBy;

        public Strategy(String identifier, String bucketBy)
        {
            this.identifier = identifier;
            this.bucketBy = bucketBy;
        }

        public int loadNormalizedNumber()
        {
            return loadNormalizedNumberWithNormalizer(ONE_HUNDRED);
        }

        public int loadNormalizedNumberWithNormalizer(int normalizer)
        {
            byte[] value = Encoding.ASCII.GetBytes(bucketBy + ":" + identifier);
            HashAlgorithm hasher = MurmurHash.Create32(seed: 4294967295);
            long hashcode = hasher.ComputeHash(value).GetHashCode();
            return (int)(hashcode % normalizer) + 1;
        }
    }
}
