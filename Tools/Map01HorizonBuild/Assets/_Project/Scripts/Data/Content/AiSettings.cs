namespace ShadowVale.Data.Content
{
    /// <summary>
    /// Runtime knobs of the coordination engine. <see cref="DefaultSolverVariant"/> is the
    /// telemetry label (greedy | ga | sa | sqa | qiea | qaoa) and selects the solver behind
    /// <c>ISquadSolver</c> for the whole session (UC-13).
    /// </summary>
    public sealed class AiSettings
    {
        public string DefaultSolverVariant { get; set; } = "greedy";
        public int LatencyBudgetMs { get; set; } = 120;
        public int ReplanCooldownMs { get; set; } = 800;
        public float WeightCoverage { get; set; } = 1.0f;
        public float WeightRedundancy { get; set; } = 0.5f;
        public float WeightFlanking { get; set; } = 1.0f;
        public float WeightCover { get; set; } = 0.5f;
        public float WeightDistance { get; set; } = 0.1f;
        public int VisionHops { get; set; } = 2;
    }
}
