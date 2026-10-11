using System.Collections.Generic;

namespace ShadowVale.Data.Telemetry
{
    /// <summary>
    /// The session's research metrics, computed in the client so they exist with or without a
    /// backend (RQ4, T01): capture rate, escape time, coordination score, plan diversity.
    /// </summary>
    public sealed class TelemetrySummaryDto
    {
        public int Encounters { get; set; }
        public int Captures { get; set; }
        public int Escapes { get; set; }
        public int Cleared { get; set; }
        /// <summary>Captures over resolved encounters (captured + escaped + cleared); 0 without any.</summary>
        public double CaptureRate { get; set; }
        public double EscapeTimeMeanSeconds { get; set; }
        public double EscapeTimeMedianSeconds { get; set; }
        /// <summary>Mean over every applied plan of the share of escape nodes covered.</summary>
        public double CoordinationScoreMean { get; set; }
        public int PlayerDeaths { get; set; }
        public int EnemiesKilled { get; set; }
        public int Takedowns { get; set; }
        public int PlayerAttacks { get; set; }
        public int ObjectivesCompleted { get; set; }

        /// <summary>Plans that went to the coordinator (fallback plans included).</summary>
        public int Plans { get; set; }
        public int DistinctPlans { get; set; }
        /// <summary>Shannon entropy (bits) of the whole-plan distribution: 0 = always the same plan.</summary>
        public double PlanEntropyBits { get; set; }
        /// <summary>Mean over guards of the entropy (bits) of where each was sent — how predictable a guard is.</summary>
        public double AgentPositionEntropyBits { get; set; }

        public double FallbackRate { get; set; }
        /// <summary>Variants the session used, "+"-joined; more than one means the variant was switched mid-session.</summary>
        public string Variants { get; set; }
        /// <summary>
        /// False when the session cannot stand for its variant in a comparison: the variant was
        /// switched, or too many plans fell back to greedy (so the session mostly measured greedy).
        /// </summary>
        public bool ValidForComparison { get; set; }
        /// <summary>File name of the matching coordination-instance log in <c>research/</c>, if any.</summary>
        public string CoordinationLog { get; set; }

        /// <summary>Every event type seen, with how often — including the ones sampled out of <c>events</c>.</summary>
        public Dictionary<string, int> EventCounts { get; set; } = new();
        /// <summary>Events not kept because the session hit its event cap.</summary>
        public int EventsDropped { get; set; }
    }
}
