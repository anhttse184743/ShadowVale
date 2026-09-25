namespace ShadowVale.Data.Coordination
{
    /// <summary>Aggregated per-session coordination metrics (RQ3), uploaded with the telemetry batch.</summary>
    public sealed class SolverMetrics
    {
        public string SolverVariantId { get; set; }
        public int SolveCount { get; set; }
        public int TimeoutCount { get; set; }
        public int FallbackCount { get; set; }
        public double LatencyP50Ms { get; set; }
        public double LatencyP95Ms { get; set; }
        public double LatencyMaxMs { get; set; }
        public double ObjectiveMean { get; set; }
        /// <summary>Fraction of escape routes covered at the moment of each re-plan, averaged.</summary>
        public double CoordinationScore { get; set; }
    }
}
