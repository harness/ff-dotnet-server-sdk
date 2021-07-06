using System;

namespace io.harness.cfsdk.client.api
{
    public class CfClientException : Exception
    {
        public CfClientException(string  errorMessage) : base(errorMessage)
        {
          
        }
    }
}
