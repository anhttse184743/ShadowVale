using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ShadowVale.Networking.Dto
{
    /// <summary>
    /// <c>GET /api/game/content/manifest</c>: the published version. <see cref="Checksum"/> is the SHA-256
    /// (hex) of the exact bundle bytes the backend serves, also sent as its ETag.
    /// </summary>
    public sealed class ContentManifest
    {
        public string VersionId { get; set; }
        public long VersionNo { get; set; }
        public string Label { get; set; }
        public string SchemaVersion { get; set; }
        public string Checksum { get; set; }
        public string PublishedAt { get; set; }

        /// <summary>The backend's JSON is camelCase; null when the text is not a manifest.</summary>
        public static ContentManifest Parse(string json)
        {
            try
            {
                // publishedAt stays the backend's ISO 8601 text
                var o = JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
                var manifest = new ContentManifest
                {
                    VersionId = o.Value<string>("versionId"),
                    VersionNo = o.Value<long?>("versionNo") ?? 0,
                    Label = o.Value<string>("label"),
                    SchemaVersion = o.Value<string>("schemaVersion"),
                    Checksum = o.Value<string>("checksum")?.ToLowerInvariant(),
                    PublishedAt = o["publishedAt"]?.ToString(),
                };
                return string.IsNullOrEmpty(manifest.VersionId) || string.IsNullOrEmpty(manifest.Checksum) ? null : manifest;
            }
            catch (Exception) { return null; }
        }
    }
}
