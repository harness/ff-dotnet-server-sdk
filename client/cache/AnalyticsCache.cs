using io.harness.cfsdk.client.dto;
using System.Collections.Generic;

namespace io.harness.cfsdk.client.cache
{
    public class AnalyticsCache : IMyCache<Analytics, int>
    {
        public Dictionary<Analytics, int> CacheMap { get; set; }

        public AnalyticsCache()
        {
            CacheMap = new Dictionary<Analytics, int>();
        }
        public void Put(Analytics key, int value)
        {
            if (CacheMap.ContainsKey(key))
            {
                CacheMap.Remove(key);
            }
            CacheMap.Add(key, value);
        }
        public int getIfPresent(Analytics key)
        {
            if (!CacheMap.ContainsKey(key))
            {
                return 0;
            }
            return CacheMap[key];
        }
        public Dictionary<Analytics, int> GetAllElements()
        {
            return CacheMap;
        }

        public void Delete(Analytics key)
        {
            CacheMap.Remove(key);
        }

        internal void resetCache()
        {
            CacheMap = new Dictionary<Analytics, int>();
        }

        public void Put(KeyValuePair<Analytics, int> keyValuePair)
        {
            Put(keyValuePair.Key, keyValuePair.Value);
        }
        public void PutAll(ICollection<KeyValuePair<Analytics, int>> keyValuePairs)
        {
            foreach (var item in keyValuePairs)
            {
                Put(item);
            }
        }
    }
}
