using System;
namespace io.harness.cfsdk.client.api
{
    [Flags]
    public enum NotificationType
    {
        READY = 0,
        FAILED = 1,
        CHANGED = 2,
        ALL = READY | FAILED | CHANGED
    }
    public struct Event
    {
        public string identifier;
        public NotificationType type;
    }
}
