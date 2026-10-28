using System;

namespace ShadowVale.AI.Coordination
{
    /// <summary>
    /// The coordination QUBO (research objective O1), a line-for-line mirror of
    /// <c>svl_solver/qubo/formulation.py::build_qubo</c> and <c>penalties.py::compute_lambda</c>.
    /// Energy to minimise over x[a,n] (agent a at local node n, flat index a·N + n):
    /// <code>
    /// E(x) = −F(x) + λ Σ_a (Σ_n x[a,n] − 1)² + λ Σ_n Σ_{a&lt;b} x[a,n]·x[b,n]
    /// F    = w_cov Σ cov + w_cover Σ coverQ − w_dist Σ dist − w_red Σ_{a&lt;b} overlap + w_flank Σ_{a&lt;b} flank
    /// </code>
    /// Q is upper-triangular, row-major; E(x) + offset = −F(x) on every feasible x.
    /// </summary>
    public static class QuboBuilder
    {
        /// <summary>λ = 1 + L + 2(A−1)·P⁺ + D + (A−1)·P⁻ — every infeasible x is strictly worse than the best feasible one.</summary>
        public static double Lambda(InstanceFeatures f, CoordinationWeights w)
        {
            int a = f.AgentCount, n = f.NodeCount;
            double l = double.NegativeInfinity, d = 0, pPlus = 0, pMinus = 0;
            for (int i = 0; i < n; i++) l = Math.Max(l, w.Coverage * f.Cov[i] + w.Cover * f.CoverQ[i]);
            foreach (double v in f.Dist) d = Math.Max(d, v);
            d *= w.Distance;
            if (a > 1)
            {
                double fl = 0, ov = 0;
                foreach (double v in f.Flank) fl = Math.Max(fl, v);
                foreach (double v in f.Overlap) ov = Math.Max(ov, v);
                pPlus = w.Flanking * fl; pMinus = w.Redundancy * ov;
            }
            return 1.0 + l + 2.0 * (a - 1) * pPlus + d + (a - 1) * pMinus;
        }

        public static double[] Build(InstanceFeatures f, CoordinationWeights w, out double offset, out double lambda)
        {
            int agents = f.AgentCount, n = f.NodeCount, size = agents * n;
            lambda = Lambda(f, w);
            var q = new double[size * size];

            // Linear: −(objective linear) − λ (from expanding the one-node-per-agent constraint).
            for (int a = 0; a < agents; a++)
            for (int node = 0; node < n; node++)
            {
                int i = a * n + node;
                double linear = w.Coverage * f.Cov[node] + w.Cover * f.CoverQ[node] - w.Distance * f.Dist[a * n + node];
                q[i * size + i] = -linear - lambda;
            }
            // Same agent, two nodes: +2λ.
            for (int a = 0; a < agents; a++)
            for (int u = 0; u < n; u++)
            for (int v = u + 1; v < n; v++)
                q[(a * n + u) * size + a * n + v] += 2.0 * lambda;
            // Different agents: −(objective pair term), plus λ when they share a node.
            for (int a = 0; a < agents; a++)
            for (int b = a + 1; b < agents; b++)
            for (int u = 0; u < n; u++)
            for (int v = 0; v < n; v++)
            {
                double pair = w.Redundancy * f.Overlap[u * n + v] - w.Flanking * f.Flank[u * n + v];
                if (u == v) pair += lambda;
                q[(a * n + u) * size + b * n + v] += pair;
            }
            offset = lambda * agents;
            return q;
        }
    }
}
