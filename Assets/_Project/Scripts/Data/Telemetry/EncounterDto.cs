namespace ShadowVale.Data.Telemetry
{
    /// <summary>
    /// One fight: from the first guard's contact with the player until it is resolved (RQ4).
    /// <list type="bullet">
    /// <item><c>captured</c> — the player died while guards were fighting him.</item>
    /// <item><c>escaped</c> — every guard lost him (none left in SpotPlayer/TakeCover/Engage/Retreat).</item>
    /// <item><c>cleared</c> — the last guard fighting him was killed.</item>
    /// <item><c>unresolved</c> — the session ended mid-fight.</item>
    /// </list>
    /// </summary>
    public sealed class EncounterDto
    {
        public int Index { get; set; }
        /// <summary>Seconds since session start.</summary>
        public double StartT { get; set; }
        public double EndT { get; set; }
        public string Result { get; set; }
        /// <summary>Contact to resolution; for <c>escaped</c> this is the escape time.</summary>
        public double DurationSeconds { get; set; }
        /// <summary>Guards that fought in it.</summary>
        public int Agents { get; set; }
        public int Kills { get; set; }
        public int Replans { get; set; }
        public int Fallbacks { get; set; }
        /// <summary>Mean coordination score of the plans applied during the fight (0 without plans).</summary>
        public double CoordinationScore { get; set; }
        /// <summary>Solver variant(s) that planned it, "+"-joined.</summary>
        public string Variants { get; set; }
    }
}
