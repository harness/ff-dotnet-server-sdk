using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace io.harness.cfsdk.client.api
{
    internal class DefaultApi
    {
    
        private string basePath = "http://localhost/api/1.0";
        public string jwttoken { get; set; }

        public  HttpClient httpClient { get; set; }

        public DefaultApi()
        {
            httpClient = new HttpClient();
        }

        public DefaultApi setConnectTimeout(int connectionTimeout)
        {
            httpClient.Timeout = TimeSpan.FromSeconds(connectionTimeout);
            return this;
        }

        public DefaultApi setBasePath(string basePath)
        {
            this.basePath = basePath;
           
            return this;
        }
        public string getBasePath()
        {
            return this.basePath; 

        }

        public void SetJWT(string jwt)
        {
            jwttoken = jwt;
            httpClient.DefaultRequestHeaders.Authorization
              = new AuthenticationHeaderValue("Bearer", jwt);
        }

    }
}
