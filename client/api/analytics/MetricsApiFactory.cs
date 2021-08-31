using System;
using System.Net.Http.Headers;

namespace io.harness.cfsdk.client.api.analytics
{
    internal class MetricsApiFactory
    {
        internal static DefaultApi create(String jwtToken, Config config)
        {
            DefaultApi metricsAPI = new DefaultApi();

            if (!string.IsNullOrEmpty(config.eventUrl))
            {
                metricsAPI.setBasePath(config.eventUrl);
                metricsAPI.httpClient.BaseAddress = new Uri(config.eventUrl);
                metricsAPI.setConnectTimeout(config.connectionTimeout);
                metricsAPI.SetJWT(jwtToken);
            }
            return metricsAPI;
        }

    }
}
