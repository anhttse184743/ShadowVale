using System;

namespace ShadowVale.AI.Solvers
{
    /// <summary>Solver ids as they appear in ai_settings.default_solver_variant and in telemetry.</summary>
    public enum SolverVariant
    {
        Greedy,
        Ga,
        Sa,
        Sqa,
        Qiea,
        Qaoa,
    }

    public static class SolverVariantIds
    {
        public static string ToId(this SolverVariant v) => v switch
        {
            SolverVariant.Greedy => "greedy",
            SolverVariant.Ga => "ga",
            SolverVariant.Sa => "sa",
            SolverVariant.Sqa => "sqa",
            SolverVariant.Qiea => "qiea",
            SolverVariant.Qaoa => "qaoa",
            _ => throw new ArgumentOutOfRangeException(nameof(v)),
        };

        public static bool TryParse(string id, out SolverVariant v)
        {
            switch ((id ?? "").Trim().ToLowerInvariant())
            {
                case "greedy": v = SolverVariant.Greedy; return true;
                case "ga": v = SolverVariant.Ga; return true;
                case "sa": v = SolverVariant.Sa; return true;
                case "sqa": v = SolverVariant.Sqa; return true;
                case "qiea": v = SolverVariant.Qiea; return true;
                case "qaoa": v = SolverVariant.Qaoa; return true;
                default: v = SolverVariant.Greedy; return false;
            }
        }

        /// <summary>Variants that run inside the game process (no sidecar needed).</summary>
        public static bool IsInProcess(this SolverVariant v) => v == SolverVariant.Greedy || v == SolverVariant.Qiea;

        public static bool IsQuantumInspired(this SolverVariant v) =>
            v == SolverVariant.Sqa || v == SolverVariant.Qiea || v == SolverVariant.Qaoa;
    }
}
