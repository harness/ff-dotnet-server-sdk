using io.harness.cfsdk.HarnessOpenAPIService;
using Serilog;
using System.Threading.Tasks;

namespace io.harness.cfsdk.client.api
{
    internal class AuthService
    {
        private DefaultApi defaultApi;
        private string apiKey;
        private int pollIntervalInSec;

        public AuthService( DefaultApi defaultApi, string apiKey, int pollIntervalInSec)
        {
            this.defaultApi = defaultApi;
            this.apiKey = apiKey;
            this.pollIntervalInSec = pollIntervalInSec;
        }

        public async Task Authenticate()
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new CfClientException("SDK key cannot be empty");
            }

            try
            {

                AuthenticationRequest authenticationRequest = new AuthenticationRequest();
                authenticationRequest.ApiKey = apiKey;
                authenticationRequest.Target = new Target2 { Identifier = "" };

                Client client = new Client(defaultApi.httpClient);

                AuthenticationResponse response = await client.ClientAuthAsync(authenticationRequest);
                string authToken = response.AuthToken;
               
                Log.Information("Auth Token ---> {At}", authToken );

                defaultApi.SetJWT(authToken);


            }
            catch (ApiException apiException)
            {
                if (apiException.StatusCode == 401)
                {
                    string errorMsg = "Invalid apiKey "+ apiKey+". Serving default value. ";
                    Log.Error(errorMsg);
                    throw new CfClientException(errorMsg);
                }
                Log.Error("Failed to get auth token {}", apiException.Message);
            }
        }

    }
}
