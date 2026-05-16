using Newtonsoft.Json;

namespace Translation.Utils
{
    internal static class SafeJson
    {
        private static readonly JsonSerializerSettings ExternalDeserializeSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            MaxDepth = 64,
        };

        public static T DeserializeExternal<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, ExternalDeserializeSettings);
        }
    }
}
