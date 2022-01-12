using System;
namespace io.harness.cfsdk.client.connector
{
    public interface IService
    {
        void Start();
        void Stop();
        void Close();
    }
}
