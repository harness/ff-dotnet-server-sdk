using io.harness.cfsdk.HarnessOpenAPIService;
using System;
using System.Collections.Generic;
using System.Text;

namespace io.harness.cfsdk.client.cache
{
    public interface IMyCache<K,V> : ICache<K, V>
    {
        Dictionary<K, V> CacheMap { get; set; }

        Dictionary<K, V> GetAllElements();
    }
}
