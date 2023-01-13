using System;
using System.IO;
using io.harness.cfsdk.client.api;
using Serilog;

namespace io.harness.cfsdk.client.connector
{
    public class FileWatcher : IDisposable, IService
    {
        private readonly string domain;
        private readonly string path;
        private readonly IUpdateCallback callback;

        private FileSystemWatcher watcher;

        public FileWatcher(string domain, string path, IUpdateCallback callback)
        {
            this.domain = domain;
            this.callback = callback;
            this.path = path;
        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            callback.Update(new Message() { Domain = domain, Event = "delete", Identifier = Path.GetFileNameWithoutExtension(e.Name), Version = 0 }, false);
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            callback.Update(new Message() { Domain = domain, Event = "create", Identifier = Path.GetFileNameWithoutExtension(e.Name), Version = 0 }, false);
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            callback.Update(new Message() { Domain = domain, Event = "patch", Identifier = Path.GetFileNameWithoutExtension(e.Name), Version = 0 }, false);
        }

        public void Close()
        {
            Stop();
        }

        public void Dispose()
        {
            watcher.Dispose();
        }

        public void Start()
        {
            try
            {
                watcher = new FileSystemWatcher
                {
                    Path = path,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    Filter = "*.json",
                    EnableRaisingEvents = true
                };
                watcher.Changed += Watcher_Changed;
                watcher.Created += Watcher_Created;
                watcher.Deleted += Watcher_Deleted;
            }
            catch(Exception ex)
            {
                Log.Error($"Error creating fileWatcher at {path}", ex);
            }
        }

        public void Stop()
        {
            watcher.EnableRaisingEvents = false;
        }
    }
}
