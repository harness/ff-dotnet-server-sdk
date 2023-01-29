using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using io.harness.cfsdk.client.dto;

namespace io.harness.cfsdk.client.cache
{
    internal interface IMyCache<K, V> : ICache<K, V>
    {
        IDictionary<K, V> GetAllElements();
    }

    internal class MemoryCache<K, V> : IMyCache<K, V>
    {
        private ConcurrentDictionary<K, V> cacheMap;

        public MemoryCache()
        {
            cacheMap = new ConcurrentDictionary<K, V>();
        }
        public void Put(K key, V value)
        {
            _ = cacheMap.AddOrUpdate(key, value, (k, v) => value);
        }
        public V getIfPresent(K key)
        {
            V value;
            _ = cacheMap.TryGetValue(key, out value);
            return value;
        }
        public IDictionary<K, V> GetAllElements()
        {
            return new ReadOnlyDictionary<K, V>(cacheMap);
        }

        public void Delete(K key)
        {
            V value;
            cacheMap.TryRemove(key, out value);
        }

        internal void resetCache()
        {
            cacheMap = new ConcurrentDictionary<K, V>();
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
    internal sealed class AnalyticsCache : MemoryCache<Analytics, int>
    {
    }


    /// <summary>
    /// In memory cache wrapper.
    /// </summary>
    internal sealed class FeatureSegmentCache : ICache
    {
        private readonly MemoryCache<string, object> memCache = new MemoryCache<string, object>();

        public void Delete(string key)
        {
            memCache.Delete(key);
        }

        public object Get(string key, Type t)
        {
            return memCache.getIfPresent(key);
        }

        public void Set(string key, object value)
        {
            memCache.Put(key, value);
        }
        public ICollection<string> Keys()
        {
            return memCache.GetAllElements().Keys;
        }
    }
}
