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
        /// <summary>Greedy + local search and tabu search (exp-v2, sidecar): the classical baselines added after the review.</summary>
        Ls,
        Tabu,
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
            SolverVariant.Ls => "ls",
            SolverVariant.Tabu => "tabu",
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
                case "ls": v = SolverVariant.Ls; return true;
                case "tabu": v = SolverVariant.Tabu; return true;
                default: v = SolverVariant.Greedy; return false;
            }
        }

        /// <summary>
        /// Variants that run inside the game process (no sidecar needed). Only greedy: it is the
        /// classical baseline and the mandatory fallback. Every research solver lives in the Python
        /// sidecar so there is exactly one implementation of each algorithm to compare.
        /// </summary>
        public static bool IsInProcess(this SolverVariant v) => v == SolverVariant.Greedy;

        public static bool IsQuantumInspired(this SolverVariant v) =>
            v == SolverVariant.Sqa || v == SolverVariant.Qiea || v == SolverVariant.Qaoa;
    }
}
