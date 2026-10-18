using ShadowVale.AI.Coordination;
using ShadowVale.AI.Solvers.Classical;
using ShadowVale.AI.Solvers.Remote;

namespace ShadowVale.AI.Solvers
{
    /// <summary>
    /// Chooses the session's solver. Greedy runs in-process; every other variant is a
    /// <see cref="RemoteSolverClient"/> to the Python sidecar, which falls back to greedy per solve
    /// (with a reason) whenever the sidecar is late or absent. Unknown ids resolve to greedy.
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

            return new RemoteSolverClient(variant.ToId(), latencyBudgetMs);
        }
    }
}
