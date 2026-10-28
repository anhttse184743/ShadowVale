using System;
using UnityEngine;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// What identifies a session — and all it says about the player: nothing. A random id per
    /// session, the random <see cref="InstallId"/> the backend requires (one per copy of the game, made
    /// from nothing about the machine or the person), the content and build versions, the map and the
    /// solver variant. No user name, machine name, IP or hardware id.
    /// </summary>
    public sealed class SessionContext
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string MapId { get; set; }
        public string ContentVersion { get; set; } = "unknown";
        /// <summary>Published content version (manifest.versionId), null for the bundle shipped with the game.</summary>
        public string ContentVersionId { get; set; }
        public string InstallId { get; set; }
        /// <summary>human | replay (the deterministic replay harness: SVL_SESSION_SOURCE=replay).</summary>
        public string Source { get; set; } = "human";
        public string BuildVersion { get; set; } = "unknown";
        public string Platform { get; set; } = "unknown";
        /// <summary>The variant the session was set up with; the one actually used comes from the plans.</summary>
        public string ConfiguredVariant { get; set; } = "greedy";
        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

        public static string PlatformName()
        {
            if (Application.isEditor) return "editor";
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer: return "windows";
                case RuntimePlatform.OSXPlayer: return "macos";
                case RuntimePlatform.LinuxPlayer: return "linux";
                default: return Application.platform.ToString().ToLowerInvariant();
            }
        }
    }
}
