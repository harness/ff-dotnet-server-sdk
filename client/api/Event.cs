using System;
namespace io.harness.cfsdk.client.api
{
    public enum NotificationType
    {
        READY,
        FAILED,
        CHANGED
    }
    public struct Event
    {
        public string identifier;
        public NotificationType type;
    }
}
