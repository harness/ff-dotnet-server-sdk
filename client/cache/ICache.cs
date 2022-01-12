using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace io.harness.cfsdk.client.cache
{
    internal interface ICache<K, V>
    {

        void PutAll(ICollection<KeyValuePair<K, V>> keyValuePairs);

        void Put(K key, V value);
        void Put(KeyValuePair<K, V> keyValuePair);
       
        V getIfPresent(K  key);
    }

    public interface ICache
    {
        void Set(string key, Object value);
        Object Get(string key);
        void Delete(string key);
        ICollection<string> Keys();
    }
    public interface IStore : ICache
    {
        void Close();
    }
}
