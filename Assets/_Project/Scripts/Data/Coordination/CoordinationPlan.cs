namespace ShadowVale.Data.Coordination
{
    /// <summary>
    /// Result of one coordination solve: which nav node each agent should move to.
    /// Mirrors <c>svl_solver/api/schemas.py::CoordinationPlan</c>. The three fields
    /// <see cref="SolverVariantId"/>, <see cref="SolveLatencyMs"/>, <see cref="HitLatencyBudget"/>
    /// are written to telemetry for RQ2/RQ3.
    /// </summary>
    public sealed class CoordinationPlan
    {
        /// <summary>Target node index per agent, same order as <c>QuboRequest.AgentNodes</c>.</summary>
        public int SamplesEvaluated { get; set; }
        public int RawFeasibleSamples { get; set; }
        public int[] AgentToTarget { get; set; }
        /// <summary>−(energy + offset); equals the reference objective F for feasible plans.</summary>
        public double ObjectiveValue { get; set; }
        public double SolveLatencyMs { get; set; }
        public string SolverVariantId { get; set; }
        /// <summary>true when the solve exceeded the budget and the plan comes from the greedy fallback.</summary>
        public bool HitLatencyBudget { get; set; }
        public bool FeasibleRaw { get; set; } = true;
        public double Energy { get; set; }
        public string FallbackReason { get; set; }
        public string InstanceId { get; set; } = "";
    }
}
