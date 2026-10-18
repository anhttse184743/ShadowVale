using System.Collections.Generic;

namespace ShadowVale.Data.Telemetry
{
    /// <summary>
    /// One anonymised gameplay event. <see cref="Type"/> is a snake_case id: player_spotted,
    /// enemy_state_changed, noise_emitted (footsteps at most 1/s), shot_fired, takedown, enemy_killed,
    /// player_death, player_position (every few seconds), objective_completed, mission_result,
    /// squad_alert, coordination_replan, solver_timeout, solver_fallback, plan_discarded,
    /// solver_changed, sidecar_status, boss_relocated, grenade_thrown, grenade_exploded,
    /// boss_reinforcements. <see cref="Payload"/> holds type-specific fields (positions
    /// are world x/z in metres); no personal data ever.
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
