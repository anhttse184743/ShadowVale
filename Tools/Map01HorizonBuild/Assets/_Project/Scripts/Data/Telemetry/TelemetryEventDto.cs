using System.Collections.Generic;

namespace ShadowVale.Data.Telemetry
{
    /// <summary>
    /// One anonymised gameplay event. <see cref="Type"/> is a snake_case id such as
    /// player_spotted, noise_emitted, shot_fired, takedown, coordination_replan, solver_timeout,
    /// mission_result. <see cref="Payload"/> holds type-specific fields; no personal data ever.
    /// </summary>
    public sealed class TelemetryEventDto
    {
        public string Type { get; set; }
        /// <summary>Seconds since session start.</summary>
        public double T { get; set; }
        public string MapId { get; set; }
        public Dictionary<string, object> Payload { get; set; } = new();
    }
}
