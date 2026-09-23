using System.Collections.Generic;
using ShadowVale.Data.Coordination;

namespace ShadowVale.Data.Telemetry
{
    /// <summary>
    /// Batch uploaded once at session end to <c>POST /api/v1/telemetry/sessions</c>.
    /// <see cref="SessionId"/> is a random GUID created per session, never tied to a user.
    /// </summary>
    public sealed class TelemetrySessionDto
    {
        public string SessionId { get; set; }
        public string ContentVersion { get; set; }
        public string AiVariant { get; set; }
        public string BuildVersion { get; set; }
        public string StartedAt { get; set; }
        public string EndedAt { get; set; }
        public string MapId { get; set; }
        /// <summary>captured | escaped | killed_all | quit</summary>
        public string MissionResult { get; set; }
        public double EscapeTimeSeconds { get; set; }
        public SolverMetrics Solver { get; set; }
        public List<TelemetryEventDto> Events { get; set; } = new();
    }
}
