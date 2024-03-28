using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        int GetFlagsCount();
        int GetSegmentsCount();


        FeatureConfig GetFlag(string identifier);
        Segment GetSegment(string identifier);
        IEnumerable<string> FindFlagsBySegment(string identifier);

        void DeleteFlag(string identifier);
        void DeleteSegment(string identifier);

        void Close();
    }

    internal class StorageRepository : IRepository
    {
        private readonly ILogger logger;
        private readonly ICache cache;
        private readonly IStore store;
        private readonly IRepositoryCallback callback;
        public StorageRepository(ICache cache, IStore store, IRepositoryCallback callback, ILoggerFactory loggerFactory)
        {
            this.cache = cache;
            this.store = store;
            this.callback = callback;
            this.logger = loggerFactory.CreateLogger<StorageRepository>();
        }

        private string FlagKey(string identifier) {  return "flags_" + identifier; }
        private string SegmentKey(string identifier) { return "segments_" + identifier; }
        
        public int GetFlagsCount()
        {
            return cache.Keys().Count(key => key.StartsWith("flags_"));
        }

        public int GetSegmentsCount()
        {
            return cache.Keys().Count(key => key.StartsWith("segments_"));
        }

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
            List<string> features = new List<string>();
            ICollection<string> keys = this.store != null ? this.store.Keys() : this.cache.Keys();
            foreach( string key in keys)
            {
                FeatureConfig flag = GetFlag(key);
                if(flag != null && flag.Rules != null)
                {
                    foreach( ServingRule rule in flag.Rules)
                    {
                        foreach (Clause clause in rule.Clauses)
                        {
                            if(clause.Op.Equals("segmentMatch") && clause.Values.Contains(segment))
                            {
                                features.Add(flag.Feature);
                            }
                        }
                    }
                }

            }
            return features;
        }
        public void DeleteFlag(string identifier)
        {
            string key = FlagKey(identifier);
            if (store != null)
            {
                logger.LogDebug("Flag {identifier} successfully deleted from store", identifier);
                store.Delete(key);
            }
            this.cache.Delete(key);
            logger.LogDebug("Flag {identifier} successfully deleted from cache", identifier);
            if (this.callback != null)
            {
                this.callback.OnFlagDeleted(identifier);
            }
        }
        

        public void DeleteSegment(string identifier)
        {
            string key = SegmentKey(identifier);
            if (store != null)
            {
                logger.LogDebug("Segment {identifier} successfully deleted from store", identifier);
                store.Delete(key);
            }
            this.cache.Delete(key);
            logger.LogDebug("Segment {identifier} successfully deleted from cache", identifier);
            if (this.callback != null)
            {
                this.callback.OnSegmentDeleted(identifier);
            }
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
            FeatureConfig current = GetFlag(identifier, false);
            // Update stored value in case if server returned newer version,
            // or if version is equal 0 (or doesn't exist)
            if( current != null && featureConfig.Version != 0 && current.Version >= featureConfig.Version )
            {
                logger.LogTrace("Flag {identifier} already exists", identifier);
                return;
            }

            Update(identifier, FlagKey(identifier), featureConfig);

            if (this.callback != null)
            {
                this.callback.OnFlagStored(identifier);
            }
        }
        void IRepository.SetSegment(string identifier, Segment segment)
        {
            Segment current = GetSegment(identifier, false);
            // Update stored value in case if server returned newer version,
            // or if version is equal 0 (or doesn't exist)
            if (current != null && segment.Version != 0 && current.Version >= segment.Version)
            {
                logger.LogTrace("Segment {identifier} already exists", identifier);
                return;
            }

            Update(identifier, SegmentKey(identifier), segment);

            if (this.callback != null)
            {
                this.callback.OnSegmentStored(identifier);
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
            if(this.store != null)
            {
                this.store.Close();
            }
        }
    }
}
