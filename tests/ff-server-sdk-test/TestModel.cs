using System;
using System.Collections.Generic;
using System.Reflection;
using io.harness.cfsdk.HarnessOpenAPIService;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ff_server_sdk_test
{
    public class LenientContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
       {
           var property = base.CreateProperty(member, memberSerialization);
           property.Required = Required.Default;
           return property;
       }

       protected override JsonObjectContract CreateObjectContract(Type objectType)
       {
           var contract = base.CreateObjectContract(objectType);
           contract.ItemRequired = Required.Default;
           return contract;
       }

       protected override JsonProperty CreatePropertyFromConstructorParameter(JsonProperty matchingMemberProperty, ParameterInfo parameterInfo)
       {
           var property = base.CreatePropertyFromConstructorParameter(matchingMemberProperty, parameterInfo);
           property.Required = Required.Default;
           return property;
       }
    }

    public class TestModel
    {
        public string testFile;
        public List<FeatureConfig> flags;
        public List<io.harness.cfsdk.client.dto.Target> targets;
        public List<Segment> segments;
        public List<Dictionary<string, object>> tests;
        public TestModel()
        {
        }
    }
}
