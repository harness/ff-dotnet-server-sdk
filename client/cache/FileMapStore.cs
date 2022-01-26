using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using io.harness.cfsdk.client.cache;
using Newtonsoft.Json;

namespace io.harness.cfsdk.client.api
{
    public class FileMapStore : IStore
    {
        private string storeName;
        public FileMapStore(string name)
        {
            storeName = name;
            Directory.CreateDirectory(name);
            Array.ForEach(Directory.EnumerateFiles(name).ToArray(), f => File.Delete(f));
        }

        public void Close()
        {
            
        }

        public void Delete(string key)
        {
            File.Delete(Path.Combine(storeName, key));
        }

        public object Get(string key, Type t)
        {
            try
            {
                var filePath = Path.Combine(storeName, key);
                if (File.Exists(filePath))
                {
                    var str = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject(str, t);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public ICollection<string> Keys()
        {
            return Directory.EnumerateFiles(storeName).Select( f => Path.GetFileName(f)).ToList();
        }

        public void Set(string key, object value)
        {
            var str = JsonConvert.SerializeObject(value);
            File.WriteAllText(Path.Combine(storeName, key), str);
        }
    }
}
