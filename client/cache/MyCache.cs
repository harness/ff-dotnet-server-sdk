﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using io.harness.cfsdk.client.dto;

namespace io.harness.cfsdk.client.cache
{
    internal interface IMyCache<TK,TV> : ICache<TK, TV>
    {
        IDictionary<TK, TV> GetAllElements();
    }

    internal class MemoryCache<TK,TV> : IMyCache<TK, TV>
    {
        protected ConcurrentDictionary<TK, TV> CacheMap { get; set; }

        public MemoryCache()
        {
            CacheMap = new ConcurrentDictionary<TK, TV>();
        }
        public void Put(TK key, TV value)
        {
            _ = CacheMap.AddOrUpdate(key, value, (_, _) => value);
        }
        public TV getIfPresent(TK key)
        {
            _ = CacheMap.TryGetValue(key, out var value);
            return value;
        }
        public IDictionary<TK, TV> GetAllElements()
        {
            return new ReadOnlyDictionary<TK, TV>(CacheMap);
        }

        public void Delete(TK key)
        {
            CacheMap.TryRemove(key, out _);
        }

        internal void resetCache()
        {
            CacheMap = new ConcurrentDictionary<TK, TV>();
        }

        public void Put(KeyValuePair<TK, TV> keyValuePair)
        {
            Put(keyValuePair.Key, keyValuePair.Value);
        }
        public void PutAll(ICollection<KeyValuePair<TK, TV>> keyValuePairs)
        {
            foreach (var item in keyValuePairs)
            {
                Put(item);
            }
        }
    }
    
    internal class MemoryCacheWithCounter<TK, TV> : MemoryCache<TK, TV>
    {
        private int itemCount = 0;
        
        public new void Put(TK key, TV value)
        {
            if (CacheMap.TryAdd(key, value))
            {
                Interlocked.Increment(ref itemCount);
            }
            else
            {
                _ = CacheMap.AddOrUpdate(key, value, (_, _) => value);
            }
        }

        public new void Delete(TK key)
        {
            if (CacheMap.TryRemove(key, out _))
            {
                Interlocked.Decrement(ref itemCount);
            }
        }

        public int Count()
        {
            return itemCount;
        }

        public new void resetCache()
        {
            CacheMap = new ConcurrentDictionary<TK, TV>();
            itemCount = 0; 
        }
    }

    internal class EvaluationAnalyticsCache : MemoryCacheWithCounter<EvaluationAnalytics, int>
    {
    }
    
    internal class TargetAnalyticsCache : MemoryCacheWithCounter<TargetAnalytics, int>
    {
    }


    /// <summary>
    /// In memory cache wrapper.
    /// </summary>
    internal class FeatureSegmentCache : ICache
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
