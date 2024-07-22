using System;

namespace io.harness.cfsdk.client.api
{
    public class CfClientException : Exception
    {
        public CfClientException(string  errorMessage) : base(errorMessage)
        {
          
        }

        public CfClientException(string  errorMessage, Exception innerException) : base(errorMessage, innerException)
        {
        }
    }
    
    public class CfClientUnrecoverableException : Exception
    {
        public CfClientUnrecoverableException(string  errorMessage) : base(errorMessage)
        {
          
        }

        public CfClientUnrecoverableException(string  errorMessage, Exception innerException) : base(errorMessage, innerException)
        {
        }
    }
}
