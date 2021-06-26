using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace io.harness.cfsdk.client.cache
{
    public interface ICache<K, V>
    {

        void PutAll(ICollection<KeyValuePair<K, V>> keyValuePairs);

        void Put(K key, V value);
        void Put(KeyValuePair<K, V> keyValuePair);
       
        V getIfPresent(K  key);
    }
}
