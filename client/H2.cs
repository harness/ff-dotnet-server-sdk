using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;

namespace io.harness.cfsdk.HarnessOpenAPIService
{
    partial class Client
    {
        partial void UpdateJsonSerializerSettings(Newtonsoft.Json.JsonSerializerSettings settings)
        {
         
            settings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;

            settings.DefaultValueHandling = DefaultValueHandling.Ignore;
        }

        public class RequireObjectPropertiesContractResolver : DefaultContractResolver
        {
            protected override JsonObjectContract CreateObjectContract(Type objectType)
            {
                var contract = base.CreateObjectContract(objectType);
                contract.ItemRequired = Required.AllowNull;
                return contract;
            }
        }
    }
}
