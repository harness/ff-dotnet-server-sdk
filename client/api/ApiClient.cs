using System;
using System.Collections.Generic;
using System.Net.Http;

namespace io.harness.cfsdk.client.api
{
    public class ApiClient
    {

        private string basePath = "http://localhost/api/1.0";
        private bool debugging = false;
        private Dictionary<string, string> defaultHeaderMap = new Dictionary<string, string>();
        private Dictionary<string, string> defaultCookieMap = new Dictionary<string, string>();
        private string tempFolderPath = null;
        public string jwttoken;

        HttpClient httpClient;

        public ApiClient setConnectTimeout(int connectionTimeout)
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(connectionTimeout);
            return this;
        }

        public ApiClient setBasePath(String basePath)
        {
            this.basePath = basePath;
            return this;
        }
    }
}
