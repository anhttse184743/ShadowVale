using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShadowVale.Data.Content;
using ShadowVale.Data.Content.V1;
using UnityEngine;

namespace ShadowVale.Content
{
    /// <summary>
    /// Reads <c>StreamingAssets/Content/fallback_bundle.json</c>. This is the bundle shipped with the
    /// build so the game always starts, even with no backend (NFR reliability, UC-15).
    /// </summary>
    public sealed class FallbackBundleProvider : IBundleSource
    {
        public const string RelativePath = "Content/fallback_bundle.json";

        public string SourceName => "fallback";

        public static string AbsolutePath => Path.Combine(Application.streamingAssetsPath, RelativePath);

        public bool TryLoad(out ContentBundle bundle, out string error)
        {
            bundle = null;
            error = null;
            try
            {
                var path = AbsolutePath;
                if (!File.Exists(path))
                {
                    error = $"fallback bundle not found at {path}";
                    return false;
                }
                return TryParse(File.ReadAllText(path), out bundle, out error);
            }
            catch (Exception e)
            {
                error = $"fallback bundle read failed: {e.Message}";
                return false;
            }
        }

        /// <summary>
        /// Parse + validate a bundle from JSON text. Shared by tests and the fetcher/cache. A web backend
        /// bundle (<c>"schema_version": "1.0"</c>) is mapped onto the runtime model with the coordination
        /// knobs of <see cref="AiSettingsFile"/>; the older game-side format (<c>"schema_version": 1</c>)
        /// is still read as it is.
        /// </summary>
        public static bool TryParse(string json, out ContentBundle bundle, out string error, string contentVersionId = null, string checksum = null)
        {
            bundle = null;
            error = null;
            try
            {
                // Dates stay text, as in the model
                var root = JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
                if (root["schema_version"]?.Type == JTokenType.String)
                {
                    var v1 = root.ToObject<BundleV1>(JsonSerializer.Create(ContentJson.Settings));
                    var problems = BundleV1Mapper.Check(v1);
                    if (problems.Count > 0)
                    {
                        error = $"bundle failed validation ({problems.Count} error(s)): {string.Join("; ", problems)}";
                        return false;
                    }
                    bundle = BundleV1Mapper.Map(v1, AiSettingsFile.Load(), contentVersionId, checksum);
                }
                else
                    bundle = root.ToObject<ContentBundle>(JsonSerializer.Create(ContentJson.Settings));
            }
            catch (Exception e)
            {
                error = $"bundle JSON malformed: {e.Message}";
                return false;
            }
            if (bundle == null)
            {
                error = "bundle JSON is empty";
                return false;
            }
            var errors = SchemaValidator.Validate(bundle);
            if (errors.Count > 0)
            {
                error = $"bundle failed validation ({errors.Count} error(s)): {string.Join("; ", errors)}";
                bundle = null;
                return false;
            }
            return true;
        }
    }
}
