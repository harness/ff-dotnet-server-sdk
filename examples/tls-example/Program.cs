
using System.Security.Cryptography.X509Certificates;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.client.dto;
using Serilog;
using Serilog.Extensions.Logging;

namespace io.harness.tls_example
{
    class Program
    {
        private static string certAuthority1Pem =
            "-----BEGIN CERTIFICATE-----\n<<ADD YOUR CA CERTS HERE>>\n-----END CERTIFICATE-----";
        
        
        static async Task Main(string[] args)
        {
            var apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
            if (apiKey == null) throw new ArgumentNullException("FF_API_KEY","FF_API_KEY env variable is not set");
            var flagName = Environment.GetEnvironmentVariable("FF_FLAG_NAME") ?? "test";
            var pem = Environment.GetEnvironmentVariable("FF_TLS_TRUSTED_CERT_PEM") ?? certAuthority1Pem;
            
            var loggerFactory = new SerilogLoggerFactory(
                new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.Console()
                    .CreateLogger());

            var cert1 = pemToX509Cert(pem);

            var trustedCerts = new List<X509Certificate2> { cert1 };
            
            var config = Config.Builder()
                .ConfigUrl("https://ffserver:8001/api/1.0")
                .EventUrl("https://ffserver:8000/api/1.0")
                .TlsTrustedCAs(trustedCerts)
                .LoggerFactory(loggerFactory).Build();
            var client = new CfClient(apiKey, config);

            client.WaitForInitialization();

            Target target = Target.builder()
                .Name("DotNET SDK TLS")
                .Identifier("dotnetsdktls")
                .build();

            while (true)
            {
                var resultBool = client.boolVariation(flagName, target, false);
                Console.WriteLine($"Flag '{flagName}' = " + resultBool);
                Thread.Sleep(2 * 1000);
            }
            
        }

        static X509Certificate2 pemToX509Cert(string pem)
        {
            pem = pem.Replace("\\n", "");
            pem = pem.Replace("\n", "");
            pem = pem
                .Replace("-----BEGIN CERTIFICATE-----",null)
                .Replace("-----END CERTIFICATE-----",null);

            return new X509Certificate2(Convert.FromBase64String(pem));
        }
    }
}
