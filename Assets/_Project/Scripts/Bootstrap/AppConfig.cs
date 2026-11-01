using System;
using ShadowVale.Networking;
using UnityEngine;

namespace ShadowVale.Bootstrap
{
    /// <summary>
    /// Environment-level configuration (where the backend is, build flags). Read once at boot.
    /// Override with environment variables so the same build can point at localhost, staging or
    /// production without recompiling: SVL_API_BASE_URL, SVL_GAME_KEY, SVL_SOLVER_URL, SVL_TELEMETRY (0/1).
    /// The backend address and key are read by <see cref="BackendConfig"/> (also from StreamingAssets/Config/backend.json).
    /// </summary>
    public static class AppConfig
    {
        public const string DefaultApiBaseUrl = BackendConfig.DefaultApiBaseUrl;
        public const string DefaultSolverUrl = "http://127.0.0.1:8001";

        public static string ApiBaseUrl => BackendConfig.ApiBaseUrl;
        public static string SolverUrl => Env("SVL_SOLVER_URL", DefaultSolverUrl);
        public static bool TelemetryEnabled => Env("SVL_TELEMETRY", "1") != "0";
        public static string BuildVersion => Application.version;

        public static bool IsDevelopment =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        private static string Env(string key, string fallback)
        {
            try
            {
                var v = Environment.GetEnvironmentVariable(key);
                return string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
            }
            catch { return fallback; }
        }
    }
}
