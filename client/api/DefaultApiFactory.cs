namespace io.harness.cfsdk.client.api
{
    internal class DefaultApiFactory
    {
        internal static DefaultApi create(string basePath, int connectionTimeout, int readTimeout, int writeTimeout)
        {
            return create(basePath, connectionTimeout, readTimeout, writeTimeout, false);
        }
         
        internal static DefaultApi create(string basePath, int connectionTimeout, int readTimeout, int writeTimeout, bool debug)
        {
            DefaultApi defaultApi = new DefaultApi();
            if (!string.IsNullOrEmpty(basePath))
            {
                defaultApi.setConnectTimeout(connectionTimeout);
                defaultApi.setBasePath(basePath);
            }

            return defaultApi;
        }
    }
}
