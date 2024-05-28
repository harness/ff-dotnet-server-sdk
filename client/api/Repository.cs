using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleToAttribute("ff-server-sdk-test")]

namespace io.harness.cfsdk.client.api
{
    internal interface IRepositoryCallback
    {
        void OnFlagStored(string identifier);
        void OnFlagDeleted(string identifier);
        void OnSegmentStored(string identifier);
        void OnSegmentDeleted(string identifier);
    }
    internal interface IRepository
    {
        void SetFlag(string identifier, FeatureConfig featureConfig);
        void SetSegment(string identifier, Segment segment);

        void SetFlags(IEnumerable<FeatureConfig> flags);
        void SetSegments(IEnumerable<Segment> segments);

        FeatureConfig GetFlag(string identifier);
        Segment GetSegment(string identifier);
        IEnumerable<string> FindFlagsBySegment(string identifier);

        void DeleteFlag(string identifier);
        void DeleteSegment(string identifier);

        void Close();
    }

    internal class StorageRepository : IRepository
    {
        internal static readonly string AdditionalPropertyValueAsSet = "harness-values-as-set";

        private readonly ReaderWriterLockSlim rwLock;
        private readonly ILogger logger;
        private readonly ICache cache;
        private readonly IStore store;
        private readonly IRepositoryCallback callback; // avoid calling callbacks inside rwLocks!
        private readonly Config config;

        public StorageRepository(ICache cache, IStore store, IRepositoryCallback callback, ILoggerFactory loggerFactory, Config config)
        {
            this.rwLock = new ReaderWriterLockSlim();
            this.cache = cache;
            this.store = store;
            this.callback = callback;
            this.config = config;
            this.logger = loggerFactory.CreateLogger<StorageRepository>();
        }

        private string FlagKey(string identifier) {  return "flags_" + identifier; }
        private string SegmentKey(string identifier) { return "segments_" + identifier; }

        public FeatureConfig GetFlag(string identifier)
        {
            return GetFlag(identifier, true);
        }
        public Segment GetSegment(string identifier)
        {
            return GetSegment(identifier, true);
        }
        IEnumerable<string> IRepository.FindFlagsBySegment(string segment)
        {
            rwLock.EnterReadLock();
            try
            {
                List<string> features = new List<string>();
                ICollection<string> keys = this.store != null ? this.store.Keys() : this.cache.Keys();
                foreach (string key in keys)
                {
                    FeatureConfig flag = GetFlag(key);
                    if (flag != null && flag.Rules != null)
                    {
                        foreach (ServingRule rule in flag.Rules)
                        {
                            foreach (Clause clause in rule.Clauses)
                            {
                                if (clause.Op.Equals("segmentMatch") && clause.Values.Contains(segment))
                                {
                                    features.Add(flag.Feature);
                                }
                            }
                        }
                    }
                }
                return features;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }
        public void DeleteFlag(string identifier)
        {
            rwLock.EnterWriteLock();
            try
            {
                string key = FlagKey(identifier);
                if (store != null)
                {
                    logger.LogDebug("Flag {identifier} successfully deleted from store", identifier);
                    store.Delete(key);
                }

                this.cache.Delete(key);
                logger.LogDebug("Flag {identifier} successfully deleted from cache", identifier);

            }
            finally
            {
                rwLock.ExitWriteLock();
            }

            this.callback?.OnFlagDeleted(identifier);
        }

        public void DeleteSegment(string identifier)
        {
            rwLock.EnterWriteLock();
            try
            {
                string key = SegmentKey(identifier);
                if (store != null)
                {
                    logger.LogDebug("Segment {identifier} successfully deleted from store", identifier);
                    store.Delete(key);
                }

                this.cache.Delete(key);
                logger.LogDebug("Segment {identifier} successfully deleted from cache", identifier);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }

            this.callback?.OnSegmentDeleted(identifier);
        }
        private T GetCache<T>(string key, bool updateCache)
        {
            Object item = this.cache.Get(key, typeof(T));
            if (item != null)
            {
                return (T)item;
            }
            if (this.store != null)
            {
                item = this.store.Get(key, typeof(T));
                if (updateCache && item != null)
                {
                    this.cache.Set(key, item);
                }
            }
            return (T)item;
        }

        private FeatureConfig GetFlag( string identifer, bool updateCache)
        {
            string key = FlagKey(identifer);
            return GetCache<FeatureConfig>(key, updateCache);
        }
        private Segment GetSegment(string identifer, bool updateCache)
        {
            string key = SegmentKey(identifer);
            return GetCache<Segment>(key, updateCache);
        }
        void IRepository.SetFlag(string identifier, FeatureConfig featureConfig)
        {
            rwLock.EnterWriteLock();
            try
            {
                FeatureConfig current = GetFlag(identifier, false);
                // Update stored value in case if server returned newer version,
                // or if version is equal 0 (or doesn't exist)
                if (current != null && featureConfig.Version != 0 && current.Version >= featureConfig.Version)
                {
                    logger.LogTrace("Flag {identifier} already exists", identifier);
                    return;
                }

                Update(identifier, FlagKey(identifier), featureConfig);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }

            this.callback?.OnFlagStored(identifier);
        }

        private void CacheClauseValues(Segment segment)
        {
            if (!config.UseMapForInClause || segment == null || segment.Rules == null)
                return;

            // The generated API code uses a List which can be inefficient if a lot of values are used
            // This function will cache values as a HashSet in AdditionalProperties
            foreach (var clause in segment.Rules)
            {
                if (!clause.Op.Equals("in")) continue;
                HashSet<string> set = new();
                set.UnionWith(clause.Values);
                clause.AdditionalProperties.Remove(AdditionalPropertyValueAsSet);
                clause.AdditionalProperties.Add(AdditionalPropertyValueAsSet, set);
            }
        }

        private void SortServingGroups(Segment segment)
        {
            if (segment == null || segment.ServingRules == null || segment.ServingRules.Count <= 1)
            {
                return;
            }
            // Keep the ServingRules sorted by priority, we will always short-circuit on the first true
            segment.ServingRules = segment.ServingRules.OrderBy(r => r.Priority).ToList();
        }

        void IRepository.SetSegment(string identifier, Segment segment)
        {
            rwLock.EnterWriteLock();
            SortServingGroups(segment);
            try
            {
                Segment current = GetSegment(identifier, false);
                // Update stored value in case if server returned newer version,
                // or if version is equal 0 (or doesn't exist)
                if (current != null && segment.Version != 0 && current.Version >= segment.Version)
                {
                    logger.LogTrace("Segment {identifier} already exists", identifier);
                    return;
                }

                CacheClauseValues(segment);
                Update(identifier, SegmentKey(identifier), segment);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }

            this.callback?.OnSegmentStored(identifier);
        }

        public void SetFlags(IEnumerable<FeatureConfig> flags)
        {
            rwLock.EnterWriteLock();
            try
            {
                foreach (var item in flags)
                {
                    Update(item.Feature, FlagKey(item.Feature), item);
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public void SetSegments(IEnumerable<Segment> segments)
        {
            rwLock.EnterWriteLock();
            try
            {
                foreach (var item in segments)
                {
                    CacheClauseValues(item);
                    SortServingGroups(item);
                    Update(item.Identifier, SegmentKey(item.Identifier), item);
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        private void Update(string identifier, string key, Object value)
        {
            if (this.store == null)
            {
                logger.LogDebug("Item {identifier} successfully cached, identifier", identifier);
                cache.Set(key, value);
            }
            else
            {
                logger.LogDebug("Item {identifier} successfully stored and cache invalidated", identifier);
                store.Set(key, value);
                cache.Delete(key);
            }
        }

        public void Close()
        {
            this.store?.Close();
        }
    }
}
