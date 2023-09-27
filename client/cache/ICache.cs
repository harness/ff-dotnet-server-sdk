using System;
using System.Collections.Generic;

namespace io.harness.cfsdk.client.cache
{
    internal interface ICache<TK, TV>
    {

        void PutAll(ICollection<KeyValuePair<TK, TV>> keyValuePairs);

        void Put(TK key, TV value);
        void Put(KeyValuePair<TK, TV> keyValuePair);
       
        TV getIfPresent(TK  key);
    }

    public interface ICache
    {
        void Set(string key, Object value);
        Object Get(string key, Type type);
        void Delete(string key);
        ICollection<string> Keys();
    }
    public interface IStore : ICache
    {
        void Close();
    }
}
