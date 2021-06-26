using io.harness.cfsdk.HarnessOpenAPIService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace io.harness.cfsdk.client.cache
{
    public class FeatureConfigCache : IMyCache<string, FeatureConfig>
    {
        public Dictionary<string, FeatureConfig> CacheMap { get; set; }

        public FeatureConfigCache()
        {
            CacheMap = new Dictionary<string, FeatureConfig>();
        }

        public void Put(string key, FeatureConfig value)
        {
            if (CacheMap.ContainsKey(key))
            {
                CacheMap.Remove(key);
            }
            CacheMap.Add(key, value);
        }

        public FeatureConfig getIfPresent(string key)
        {
            if (!CacheMap.ContainsKey(key))
            {
                return null;
            }
            return CacheMap[key];
        }

        public Dictionary<string,FeatureConfig> GetAllElements()
        {
            return CacheMap;
        }

        public void Delete(String key)
        {
            CacheMap.Remove(key);
        }

        public void Put(KeyValuePair<string, FeatureConfig> keyValuePair)
        {
            Put(keyValuePair.Key, keyValuePair.Value);
        }
        public void PutAll(ICollection<KeyValuePair<string, FeatureConfig>> keyValuePairs)
        {
            foreach (var item in keyValuePairs)
            {
                Put(item);
            }
        }
    }
}
