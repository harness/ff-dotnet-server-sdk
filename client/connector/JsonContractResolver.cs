using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace io.harness.cfsdk.client.connector
{
    /// <summary>
    /// This ContractResolver will adjust some JSON properties to make them less strict.
    /// Currently client-v1.yaml does not require certain attributes however the code generator still adds
    /// Newtonsoft.Json.Required.DisallowNull which will throw an exception if the property is present but null.
    /// There does not seem to be any way to configuring the generator to remove the DisallowNull at code generation
    /// time, so instead we do it here dynamically setting each to Newtonsoft.Json.Required.Default.
    /// 
    /// https://github.com/RicoSuter/NSwag/issues/850
    /// https://www.newtonsoft.com/json/help/html/t_newtonsoft_json_required.htm
    /// </summary>
    internal class JsonContractResolver : DefaultContractResolver
    {
        private readonly ILogger<JsonContractResolver> logger;

        internal JsonContractResolver(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<JsonContractResolver>();
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            OverrideRequiredProperty(ref property);
            return property;
        }

        protected override JsonObjectContract CreateObjectContract(Type objectType)
        {
            var contract = base.CreateObjectContract(objectType);
            OverrideRequiredProperty(ref contract);
            return contract;
        }

        protected override JsonProperty CreatePropertyFromConstructorParameter(JsonProperty matchingMemberProperty, ParameterInfo parameterInfo)
        {
            var property = base.CreatePropertyFromConstructorParameter(matchingMemberProperty, parameterInfo);
            OverrideRequiredProperty(ref property);
            return property;
        }

        private void OverrideRequiredProperty(ref JsonProperty property)
        {
            if (property.NullValueHandling != NullValueHandling.Ignore ||
                property.Required != Required.DisallowNull) return;
            logger.LogDebug("Changing JSON property '{PropertyName}' from Required.DisallowNull to Required.Default", property.PropertyName);
            property.Required = Required.Default;
        }
        
        private void OverrideRequiredProperty(ref  JsonObjectContract contract)
        {
            if (contract.ItemNullValueHandling != NullValueHandling.Ignore ||
                contract.ItemRequired != Required.DisallowNull) return;
            logger.LogDebug("Changing JSON object contract '{contract}' from Required.DisallowNull to Required.Default", contract);
            contract.ItemRequired = Required.Default;
        }
    }
}