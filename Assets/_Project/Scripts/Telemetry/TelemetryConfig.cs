using System;
using System.IO;
using ShadowVale.Networking;
using UnityEngine;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// Where telemetry goes and whether it is collected. Environment overrides, the same ones
    /// <c>AppConfig</c> documents: <c>SVL_TELEMETRY</c> (0/1), <c>SVL_API_BASE_URL</c>.
    /// Unset, telemetry is on in players and the editor and off in batch mode, so command-line
    /// test runs do not fill the queue; the replay harness turns it on with <c>SVL_TELEMETRY=1</c>.
    /// </summary>
    public sealed class TelemetryConfig
    {
        public const string DefaultApiBaseUrl = BackendConfig.DefaultApiBaseUrl;

        public bool Enabled { get; set; } = true;
        /// <summary>False: sessions are still queued and exported, just never sent (offline research runs).</summary>
        public bool UploadEnabled { get; set; } = true;
        public string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;
        /// <summary>Holds <c>pending/</c>, <c>rejected/</c>; research exports go to its sibling <c>research/</c>.</summary>
        public string RootDirectory { get; set; }
        public string ResearchDirectory { get; set; }
        /// <summary>Seconds between player-position samples (heatmap).</summary>
        public float PositionSampleSeconds { get; set; } = 5f;
        public int MaxEvents { get; set; } = 4000;
        /// <summary>Queued sessions kept at most; the oldest go first.</summary>
        public int MaxQueuedSessions { get; set; } = 50;
        /// <summary>Above this share of fallback plans a session is not valid for comparing variants.</summary>
        public double MaxFallbackRateForComparison { get; set; } = 0.2;

        public static TelemetryConfig FromEnvironment()
        {
            string flag = Env("SVL_TELEMETRY");
            string root = Path.Combine(Application.persistentDataPath, "telemetry");
            return new TelemetryConfig
            {
                Enabled = flag != null ? flag != "0" : !Application.isBatchMode,
                ApiBaseUrl = BackendConfig.ApiBaseUrl,
                RootDirectory = root,
                ResearchDirectory = Path.Combine(Application.persistentDataPath, "research"),
            };
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
