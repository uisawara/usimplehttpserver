#nullable enable
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace UnityPackages.mmzkworks.SimpleHttpServer.Runtime
{
    public sealed class JsonNetSerializer : IJsonSerializer
    {
        private readonly JsonSerializerSettings _settings;

        public JsonNetSerializer(JsonSerializerSettings? settings = null)
        {
            _settings = settings ?? new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                DateParseHandling = DateParseHandling.None, // ISOは明示パース
                Formatting = Formatting.None
            };
        }

        public string Serialize(object? value)
        {
            return JsonConvert.SerializeObject(value, _settings);
        }

        public object? Deserialize(string json, Type type)
        {
            return JsonConvert.DeserializeObject(json, type, _settings);
        }
    }
}