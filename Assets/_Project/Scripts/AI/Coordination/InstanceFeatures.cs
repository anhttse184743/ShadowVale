using System;
using System.Collections.Generic;

namespace ShadowVale.AI.Coordination
{
    /// <summary>
    /// The numbers the coordination objective reads, computed exactly like
    /// <c>svl_solver/model/instance.py::_precompute</c> so a Q matrix built here and one built by the
    /// Python solver repo agree to machine precision (tested against exported fixtures):
    /// <code>
    /// cov[n]        escape nodes visible from n
    /// overlap[n,m]  escape nodes visible from both n and m (0 on the diagonal)
    /// flank[n,m]    (1 − cos θ)/2, θ the angle at the player between n and m (0 on the diagonal)
    /// coverQ[n]     is_cover · max(0, cos(facing[n], n→player)); is_cover when no facing
    /// dist[a,n]     graph hops from agent a to n (unreachable → 1000)
    /// </code>
    /// Everything is double and row-major so the arithmetic matches numpy's.
    /// </summary>
    public sealed class InstanceFeatures
    {
        public const double UnreachableCost = 1e3;

        public int AgentCount, NodeCount;
        public double[] Cov, Overlap, Flank, CoverQ, Dist;
        public int[] Escape;
        public bool[] Visible;

        /// <param name="x">Node x coordinates (world x).</param>
        /// <param name="z">Node z coordinates (world z; Python's second coordinate).</param>
        /// <param name="coverFacing">World yaw per node (degrees) or null to count every cover fully.</param>
        /// <param name="visible">Row-major N×N, true on the diagonal.</param>
        /// <param name="dist">Row-major A×N hop distances (already mapped to <see cref="UnreachableCost"/>).</param>
        public static InstanceFeatures Compute(double[] x, double[] z, int player, IReadOnlyList<bool> isCover,
            IReadOnlyList<double> coverFacing, IReadOnlyList<int> escape, bool[] visible, double[] dist, int agentCount)
        {
            int n = x.Length;
            if (z.Length != n || isCover.Count != n || visible.Length != n * n || dist.Length != agentCount * n)
                throw new ArgumentException("inconsistent instance sizes");
            var f = new InstanceFeatures
            {
                AgentCount = agentCount, NodeCount = n, Visible = visible, Dist = dist,
                Escape = new List<int>(escape).ToArray(),
                Cov = new double[n], Overlap = new double[n * n], Flank = new double[n * n], CoverQ = new double[n],
            };
            Array.Sort(f.Escape);

            for (int a = 0; a < n; a++)
            {
                double c = 0;
                foreach (int e in f.Escape) if (visible[a * n + e]) c++;
                f.Cov[a] = c;
                for (int b = 0; b < n; b++)
                {
                    if (a == b) continue;
                    double o = 0;
                    foreach (int e in f.Escape) if (visible[a * n + e] && visible[b * n + e]) o++;
                    f.Overlap[a * n + b] = o;
                }
            }

            // Flank: unit vectors from the player; nodes on the player's own spot have none.
            var ux = new double[n]; var uz = new double[n]; var ok = new bool[n];
            for (int i = 0; i < n; i++)
            {
                double vx = x[i] - x[player], vz = z[i] - z[player];
                double norm = Math.Sqrt(vx * vx + vz * vz);
                ok[i] = norm > 1e-9;
                if (ok[i]) { ux[i] = vx / norm; uz[i] = vz / norm; }
            }
            for (int a = 0; a < n; a++)
            for (int b = 0; b < n; b++)
            {
                if (a == b || !ok[a] || !ok[b]) continue;
                double cos = ux[a] * ux[b] + uz[a] * uz[b];
                f.Flank[a * n + b] = Math.Min(1.0, Math.Max(0.0, (1.0 - cos) / 2.0));
            }

            for (int i = 0; i < n; i++)
            {
                double cover = isCover[i] ? 1.0 : 0.0;
                if (coverFacing == null) { f.CoverQ[i] = cover; continue; }
                double tx = x[player] - x[i], tz = z[player] - z[i];
                double norm = Math.Sqrt(tx * tx + tz * tz);
                double cos = 1.0;
                if (norm > 1e-9)
                {
                    double yaw = coverFacing[i] * Math.PI / 180.0;
                    cos = (Math.Sin(yaw) * tx + Math.Cos(yaw) * tz) / norm;
                }
                f.CoverQ[i] = cover * Math.Min(1.0, Math.Max(0.0, cos));
            }
            return f;
        }
    }
}
