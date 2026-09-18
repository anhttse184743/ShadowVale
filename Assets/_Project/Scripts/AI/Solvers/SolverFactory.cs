using ShadowVale.AI.Coordination;
using ShadowVale.AI.Solvers.Classical;

namespace ShadowVale.AI.Solvers
{
    /// <summary>
    /// Chooses the solver for the session from the bundle's ai_settings (UC-13). Until the
    /// RemoteSolverClient lands (P4, W8) every non-greedy id resolves to greedy and reports why,
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
            if (variant.IsInProcess())
                return new GreedySolver(latencyBudgetMs);

            // TODO(P4/W8): return new RemoteSolverClient(variant, latencyBudgetMs, profile) here.
            fallbackReason = $"remote solver '{variantId}' not wired yet (P4), using greedy";
            return new GreedySolver(latencyBudgetMs);
        }
    }
}
