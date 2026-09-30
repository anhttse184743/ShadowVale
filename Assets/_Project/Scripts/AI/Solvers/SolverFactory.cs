using ShadowVale.AI.Coordination;
using ShadowVale.AI.Solvers.Classical;

namespace ShadowVale.AI.Solvers
{
    /// <summary>
    /// Chooses the session's solver. Greedy runs in-process; every other variant belongs to the
    /// Python sidecar. Until the RemoteSolverClient lands, remote ids resolve to greedy and report
    /// why, so telemetry can mark the session as not valid for comparison.
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
            if (variant.IsInProcess())
                return new GreedySolver(latencyBudgetMs);

            // TODO(M5): return new RemoteSolverClient(variant, latencyBudgetMs, profile) here.
            fallbackReason = $"remote solver '{variantId}' not wired yet (M5), using greedy";
            return new GreedySolver(latencyBudgetMs);
        }
    }
}
