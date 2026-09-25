using System.Threading;
using System.Threading.Tasks;
using ShadowVale.Data.Coordination;

namespace ShadowVale.AI.Coordination
{
    /// <summary>
    /// The single boundary between the game and the Enemy Coordination Engine (research module).
    /// Classical baselines run in-process; quantum-inspired variants go over HTTP to the Python
    /// sidecar. Gameplay never knows which one is behind this interface (UC-13).
    /// </summary>
    public interface ISquadSolver
    {
        /// <summary>Telemetry label: greedy | ga | sa | sqa | qiea | qaoa.</summary>
        string VariantId { get; }

        /// <summary>Upper bound the solver may spend (NFR: bounded latency).</summary>
        int LatencyBudgetMs { get; }

        /// <summary>
        /// Must return a feasible plan even on timeout or failure (fall back to greedy and set
        /// <c>HitLatencyBudget</c> / <c>FallbackReason</c>). Never throws for solver reasons.
        /// </summary>
        Task<CoordinationPlan> SolveAsync(QuboRequest request, CancellationToken ct);
    }
}
