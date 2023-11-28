using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.IdentityModel.Logging;
using Newtonsoft.Json;
using WireMock.ResponseBuilders;
using Response = WireMock.ResponseBuilders.Response;

namespace ff_server_sdk_test.api
{

    using static System.Text.Encoding;

    public static class CannedResponses
    {
        public static string MakeEmptyBody()
        {
            return "[]";
        }

        public static string MakeFeatureConfigBodyWithVariationToTargetMapSetToNull()
        {
            return
                "[{\"variationToTargetMap\":null,\"project\":\"Project\",\"environment\":\"Environment\",\"feature\":\"FeatureWithVariationToTargetMapSetAsNull\",\"state\":\"on\",\"kind\":\"string\",\"variations\":[ { \"identifier\": \"on\", \"name\": \"on\", \"value\": \"on\" } ],\"rules\":[],\"defaultServe\":{  \"variation\":\"on\"  },\"offVariation\":\"off\",\"prerequisites\":[],\"version\":1}]";
        }

        public static string MakeMultiFeatureConfigBody(int times)
        {
            var flags = new List<FeatureConfig>();

            for (int i = 0; i < times; i++)
            {
                var flag = new FeatureConfig
                {
                    Feature = "Feature"+i,
                    Environment = "Environment",
                    Kind = FeatureConfigKind.Boolean,
                    Prerequisites = new List<Prerequisite>(),
                    Project = "Project",
                    Rules = new List<ServingRule>(),
                    State = FeatureState.On,
                    Variations = new List<Variation>(),
                    Version = 1,
                    AdditionalProperties = new Dictionary<string, object>(),
                    DefaultServe = new Serve(),
                    OffVariation = "off"
                };
                flags.Add(flag);
            }
            
            var body = JsonConvert.SerializeObject(flags, new JsonSerializerSettings());
            Console.WriteLine("feature config to return: " + body);
            return body;
        }

        public static string MakeFeatureConfigBody()
        {
            var flag = new FeatureConfig
            {
                Feature = "Feature",
                Environment = "Environment",
                Kind = FeatureConfigKind.Boolean,
                Prerequisites = new List<Prerequisite>(),
                Project = "Project",
                Rules = new List<ServingRule>(),
                State = FeatureState.On,
                Variations = new List<Variation>(),
                Version = 1,
                AdditionalProperties = new Dictionary<string, object>(),
                DefaultServe = new Serve(),
                OffVariation = "off"
            };
            var flags = new List<FeatureConfig> { flag };

            var body = JsonConvert.SerializeObject(flags, new JsonSerializerSettings());
            Console.WriteLine("feature config to return: " + body);
            return body;
        }

        public static string MakeTargetSegmentsBody()
        {
            var segment = new Segment
            {
                Environment = "Environment",
                Rules = new List<Clause>(),
                Identifier = "Identifier",
                Name = "Name",
                Tags = new List<Tag>(),
                Version = 1
            };
            var segments = new List<Segment>();
            segments.Add(segment);
            return JsonConvert.SerializeObject(segments);
        }

        public static IResponseBuilder MakeAuthResponse()
        {
            return Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"authToken\": \"" + MakeDummyJwtToken() + "\"}");
        }

        public static string MakeDummyJwtToken()
        {
            return MakeDummyJwtToken(
                "00000000-0000-0000-0000-000000000000", "Production", "aaaaa_BBBBB-cccccccccc");
        }

        private static string MakeDummyJwtToken(string envUuid, string env, string accountId)
        {
            const string header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
            var payload = "{";

            if (envUuid != null)
            {
                payload += "\"environment\":\"" + envUuid + "\",";
            }

            if (env != null)
            {
                payload += "\"environmentIdentifier\":\"" + env + "\",";
            }

            if (accountId != null)
            {
                payload += "\"accountID\":\"" + accountId + "\",";
            }

            payload +=
                "\"project\":\"00000000-0000-0000-0000-000000000000\","
                + "\"projectIdentifier\":\"dev\","
                + "\"organization\":\"00000000-0000-0000-0000-000000000000\","
                + "\"organizationIdentifier\":\"default\","
                + "\"clusterIdentifier\":\"1\","
                + "\"key_type\":\"Server\""
                + "}";

            var hmac256 = new byte[32];

            Console.WriteLine("payload=" + payload);

            var token = System.Convert.ToBase64String(UTF8.GetBytes(header))
                        + "."
                        + System.Convert.ToBase64String(UTF8.GetBytes(payload))
                        + "."
                        + System.Convert.ToBase64String(hmac256);

            // https://www.rfc-editor.org/rfc/rfc7515.html#appendix-C
            token = token.Split('=')[0]; // Remove any trailing '='s
            token = token.Replace('+', '-'); // 62nd char of encoding
            token = token.Replace('/', '_'); // 63rd char of encoding

            // check if it can be decoded ok
            IdentityModelEventSource.ShowPII = true;
            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.ReadToken(token);

            return token;
        }
    }
}