using System;
namespace io.harness.cfsdk.client.api
{
    public class Message
    {
        public string Event { get; set; }
        public string Domain { get; set; }
        public string Identifier { get; set; }
        public long Version { get; set; }
    }
}
