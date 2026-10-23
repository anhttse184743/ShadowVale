using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ShadowVale.Data.Coordination;
using ShadowVale.AI.Coordination;

namespace ShadowVale.AI.Solvers.Classical
{
    /// <summary>
    /// Classical baseline and the in-process fallback. MUST mirror
    /// <c>svl_solver/solvers/greedy.py</c> exactly: agents in index order, each takes the free node
    /// with the lowest marginal energy given the agents already placed. Pure C#, no allocation
    /// beyond the two arrays, O(A²·N): 12 agents × 100 nodes solves in well under a millisecond.
    /// </summary>
    public sealed class GreedySolver : ISquadSolver
    {
        public string VariantId => "greedy";
        public int LatencyBudgetMs { get; }

        public GreedySolver(int latencyBudgetMs = 120) { LatencyBudgetMs = latencyBudgetMs; }

        public Task<CoordinationPlan> SolveAsync(QuboRequest request, CancellationToken ct)
            => Task.FromResult(Solve(request));

        public CoordinationPlan Solve(QuboRequest req)
        {
            var sw = Stopwatch.StartNew();
            var a = req.AgentCount;
            var n = req.NodeCount;
            if (a < 1 || n < a) throw new ArgumentException("Require 1 <= agents <= nodes");
            var size = a * n;
            var q = req.QMatrix;
            if (q == null || q.Length != size * size)
                throw new ArgumentException($"q_matrix must have {size * size} entries, got {q?.Length ?? 0}");

            var x = new bool[size];
            var taken = new bool[n];
            var target = new int[a];

            for (var agent = 0; agent < a; agent++)
            {
                var bestNode = -1;
                var bestGain = double.PositiveInfinity;
                for (var node = 0; node < n; node++)
                {
                    if (taken[node]) continue;
                    var i = agent * n + node;
                    var gain = MarginalGain(q, x, size, i);
                    if (gain < bestGain) { bestGain = gain; bestNode = node; }
                }
                target[agent] = bestNode;
                taken[bestNode] = true;
                x[agent * n + bestNode] = true;
            }

            var energy = Energy(q, x, size) + req.Offset;
            sw.Stop();
            var ms = sw.Elapsed.TotalMilliseconds;
            return new CoordinationPlan
            {
                AgentToTarget = target,
                Energy = energy,
                ObjectiveValue = -energy,
                SolveLatencyMs = ms,
                SolverVariantId = VariantId,
                HitLatencyBudget = ms > LatencyBudgetMs,
                FeasibleRaw = true,
                InstanceId = req.InstanceId,
            };
        }

        /// <summary>ΔE of setting x_i = 1 given current x: Q[i,i] + Σ_j (Q[i,j] + Q[j,i]) x_j. Works for upper-triangular or symmetric Q.</summary>
        private static double MarginalGain(double[] q, bool[] x, int size, int i)
        {
            var g = q[i * size + i];
            for (var j = 0; j < size; j++)
            {
                if (j == i || !x[j]) continue;
                g += q[i * size + j] + q[j * size + i];
            }
            return g;
        }

        public static double Energy(double[] q, bool[] x, int size)
        {
            double e = 0;
            for (var i = 0; i < size; i++)
            {
                if (!x[i]) continue;
                e += q[i * size + i];
                for (var j = i + 1; j < size; j++)
                    if (x[j]) e += q[i * size + j] + q[j * size + i];
            }
            return e;
        }
    }
}
