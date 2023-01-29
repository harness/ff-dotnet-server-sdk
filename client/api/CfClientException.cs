namespace io.harness.cfsdk.client.api
{
    [System.Serializable]
    public class CfClientException : System.Exception
    {
        public CfClientException() { }
        public CfClientException(string message) : base(message) { }
        public CfClientException(string message, System.Exception inner) : base(message, inner) { }
        protected CfClientException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
