using System.Collections.Generic;
using ShadowVale.Data.Coordination;

namespace ShadowVale.Data.Telemetry
{
    /// <summary>
    /// Batch uploaded once at session end to <c>POST /api/v1/telemetry/sessions</c>. A session is one
    /// play of one map, from entering it until it is left (restart, menu, quit).
    /// <see cref="SessionId"/> is a random GUID created per session, never tied to a user; the
    /// backend stores the batch idempotently by it, so a retried upload is harmless.
    /// Wire format: snake_case JSON (see <c>Docs/Contracts/telemetry_session.sample.json</c>).
    /// </summary>
    public sealed class TelemetrySessionDto
    {
        /// <summary>Bumped whenever a field changes meaning or is removed.</summary>
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string SessionId { get; set; }
        public string ContentVersion { get; set; }
        /// <summary>Solver variant the session played with (the most used one if it was switched).</summary>
        public string AiVariant { get; set; }
        public string BuildVersion { get; set; }
        /// <summary>windows | macos | linux | editor — never a machine name.</summary>
        public string Platform { get; set; }
        /// <summary>ISO-8601 UTC.</summary>
        public string StartedAt { get; set; }
        public string EndedAt { get; set; }
        public string MapId { get; set; }
        public double DurationSeconds { get; set; }
        /// <summary>completed | failed | died | quit.</summary>
        public string MissionResult { get; set; }
        /// <summary>Mean escape time over the encounters the player escaped (0 if none).</summary>
        public double EscapeTimeSeconds { get; set; }
        public SolverMetrics Solver { get; set; }
        public TelemetrySummaryDto Summary { get; set; }
        public List<EncounterDto> Encounters { get; set; } = new();
        public List<TelemetryEventDto> Events { get; set; } = new();
    }
}
