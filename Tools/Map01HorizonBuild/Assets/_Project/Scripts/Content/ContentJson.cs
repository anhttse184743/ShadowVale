using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ShadowVale.Content
{
    /// <summary>
    /// The one JSON configuration used for content bundles, save files, telemetry and solver
    /// requests: snake_case on the wire, PascalCase in C#. Keep it identical to the backend's
    /// <c>JsonNamingPolicy.SnakeCaseLower</c> and the solver's pydantic models.
    /// </summary>
    public static class ContentJson
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy(processDictionaryKeys: false, overrideSpecifiedNames: true)
            },
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Formatting = Formatting.None,
        };

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);

        public static string Serialize(object value, bool indented = false) =>
            JsonConvert.SerializeObject(value, indented ? Formatting.Indented : Formatting.None, Settings);
    }
}
