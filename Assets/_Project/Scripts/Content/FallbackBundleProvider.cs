using System;
using System.IO;
using ShadowVale.Data.Content;
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

        /// <summary>Parse + validate a bundle from JSON text. Shared by tests and the fetcher/cache.</summary>
        public static bool TryParse(string json, out ContentBundle bundle, out string error)
        {
            bundle = null;
            error = null;
            try
            {
                bundle = ContentJson.Deserialize<ContentBundle>(json);
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
