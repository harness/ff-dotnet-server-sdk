using io.harness.cfsdk.HarnessOpenAPIService;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;

namespace io.harness.cfsdk.client.cache
{
    public interface IMyCache<K,V> : ICache<K, V>
    {
        IDictionary<K, V> GetAllElements();
    }

    public class MemoryCache<K,V> : IMyCache<K, V>
    {
        private ConcurrentDictionary<K, V> CacheMap { get; set; }

        public MemoryCache()
        {
            CacheMap = new ConcurrentDictionary<K, V>();
        }
        public void Put(K key, V value)
        {
            _ = CacheMap.AddOrUpdate(key, value, (k, v) => value);
        }
        public V getIfPresent(K key)
        {
            V value;
            _ = CacheMap.TryGetValue(key, out value);
            return value;
        }
        public IDictionary<K, V> GetAllElements()
        {
            return CacheMap;
        }

        public void Delete(K key)
        {
            V value;
            CacheMap.TryRemove(key, out value);
        }

        internal void resetCache()
        {
            CacheMap = new ConcurrentDictionary<K, V>();
        }

        public void Put(KeyValuePair<K, V> keyValuePair)
        {
            Put(keyValuePair.Key, keyValuePair.Value);
        }
        public void PutAll(ICollection<KeyValuePair<K, V>> keyValuePairs)
        {
            foreach (var item in keyValuePairs)
            {
                Put(item);
            }
        }
    }
}
