using ShadowVale.AI.Coordination;
using ShadowVale.AI.Solvers.Classical;

namespace ShadowVale.AI.Solvers
{
    /// <summary>
    /// Chooses local Greedy or QIEA for the session. Until the
    /// RemoteSolverClient lands, unsupported remote ids resolve to greedy and report why,
    /// so telemetry can mark the session as not valid for comparison.
    /// </summary>
    public static class SolverFactory
    {
        public static ISquadSolver Create(string variantId, int latencyBudgetMs, out string fallbackReason)
        {
            fallbackReason = null;
            if (!SolverVariantIds.TryParse(variantId, out var variant))
            {
                fallbackReason = $"unknown solver variant '{variantId}', using greedy";
                return new GreedySolver(latencyBudgetMs);
            }
            if (variant == SolverVariant.Qiea) return new QieaSolver(latencyBudgetMs);
            if (variant.IsInProcess())
                return new GreedySolver(latencyBudgetMs);

            // TODO(P4/W8): return new RemoteSolverClient(variant, latencyBudgetMs, profile) here.
            fallbackReason = $"remote solver '{variantId}' not wired yet (P4), using greedy";
            return new GreedySolver(latencyBudgetMs);
        }
    }
}
