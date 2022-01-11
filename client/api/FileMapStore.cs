using System;
using System.Collections.Generic;
using io.harness.cfsdk.client.cache;

namespace io.harness.cfsdk.client.api
{
    public class FileMapStore : IStore
    {
        public FileMapStore(string name)
        {
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Delete(string key)
        {
            throw new NotImplementedException();
        }

        public object Get(string key)
        {
            throw new NotImplementedException();
        }

        public ICollection<string> Keys()
        {
            throw new NotImplementedException();
        }

        public void Set(string key, object value)
        {
            throw new NotImplementedException();
        }
    }
}
