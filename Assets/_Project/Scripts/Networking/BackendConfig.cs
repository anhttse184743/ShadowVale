using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ShadowVale.Networking
{
    /// <summary>
    /// Where the web backend is and the game key it wants. Environment variables win
    /// (<c>SVL_API_BASE_URL</c>, <c>SVL_GAME_KEY</c>), then <c>StreamingAssets/Config/backend.json</c>
    /// (<c>{ "api_base_url": "...", "game_key": "..." }</c>, gitignored; see <c>backend.example.json</c>).
    /// The key ships inside the build, so it only keeps spam out; it is not a secret of anyone.
    /// </summary>
    public static class BackendConfig
    {
        /// <summary>The backend's <c>http</c> launch profile (the <c>https</c> one redirects to a self-signed port).</summary>
        public const string DefaultApiBaseUrl = "http://localhost:5079";
        public const string ConfigRelativePath = "Config/backend.json";

        private static JObject _file;
        private static bool _fileRead;

        public static string ApiBaseUrl => Env("SVL_API_BASE_URL") ?? FileValue("api_base_url") ?? DefaultApiBaseUrl;

        /// <summary>Null when not configured: the game then plays offline on its cached or shipped bundle.</summary>
        public static string GameKey => Env("SVL_GAME_KEY") ?? FileValue("game_key");

        private static string FileValue(string key)
        {
            if (!_fileRead)
            {
                _fileRead = true;
                try
                {
                    var path = Path.Combine(Application.streamingAssetsPath, ConfigRelativePath);
                    if (File.Exists(path)) _file = JObject.Parse(File.ReadAllText(path));
                }
                catch (Exception e) { Debug.LogWarning($"[Backend] {ConfigRelativePath} unreadable: {e.Message}"); }
            }
            var value = _file?[key]?.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string Env(string key)
        {
            try
            {
                var v = Environment.GetEnvironmentVariable(key);
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }
            catch { return null; }
        }
    }
}
