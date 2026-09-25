using System.Collections.Generic;

namespace ShadowVale.Data.Coordination
{
    /// <summary>
    /// Wire format sent to the solver sidecar (<c>POST /solve</c>) and consumed by the in-process
    /// solvers. Mirrors <c>svl_solver/api/schemas.py::QuboRequest</c> field for field.
    /// Variable layout: x[a, n] ↔ flat index a * NodeCount + n. <see cref="QMatrix"/> is the
    /// row-major flattening of an upper-triangular (or symmetric) NodeCount·AgentCount square matrix.
    /// </summary>
    public sealed class QuboRequest
    {
        public string InstanceId { get; set; } = "";
        public int Seed { get; set; }
        public int AgentCount { get; set; }
        public int NodeCount { get; set; }
        public List<int> AgentNodes { get; set; } = new();
        public List<int> CoverNodes { get; set; } = new();
        public List<int> EscapeRoutes { get; set; } = new();
        public double[] QMatrix { get; set; }
        public double Offset { get; set; }
        public double WeightCoverage { get; set; } = 1.0;
        public double WeightFlanking { get; set; } = 1.0;
        public double PenaltyConflict { get; set; }
        public int LatencyBudgetMs { get; set; } = 120;
        public string Variant { get; set; } = "greedy";

        public int VariableCount => AgentCount * NodeCount;
    }
}
