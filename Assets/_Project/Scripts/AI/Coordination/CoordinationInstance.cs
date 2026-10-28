using System.Collections.Generic;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Tactical;
using ShadowVale.Data.Coordination;
using UnityEngine;

namespace ShadowVale.AI.Coordination
{
    /// <summary>
    /// One re-plan, from the map graph to the solver's input and back: the local subgraph around
    /// the player's last known position, the features, the Q matrix (<see cref="Request"/>), and the
    /// translation of a plan into world positions and roles. Main thread; cheap (a few hundred µs).
    /// </summary>
    public sealed class CoordinationInstance
    {
        public LocalSubgraph Sub;
        public InstanceFeatures Features;
        public QuboRequest Request;
        public CoordinationWeights Weights;
        public double Lambda;
        public int PlayerNode;
        public int[] AgentNodes;
        public double[] X, Z;
        public bool[] IsCover;
        public double[] CoverFacing;

        public static CoordinationInstance Build(IWorldQuery graph, Vector3 playerPosition, IReadOnlyList<Vector3> agentPositions,
            ICoordinationTask task, CoordinationWeights weights, int maxNodes, string instanceId, int seed, int budgetMs, string variant)
        {
            int agents = agentPositions.Count;
            int player = graph.NearestNode(playerPosition);
            if (player < 0 || agents == 0) return null;
            var agentNodes = new int[agents];
            for (int i = 0; i < agents; i++) agentNodes[i] = graph.NearestNode(agentPositions[i]);

            var sub = LocalSubgraph.Extract(graph, player, agentNodes, System.Math.Max(maxNodes, agents + 1));
            int n = sub.Count;
            if (n < agents) return null;

            var x = new double[n]; var z = new double[n]; var cover = new bool[n]; var facing = new double[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 p = graph.NodePosition(sub.Nodes[i]);
                x[i] = p.x; z[i] = p.z;
                cover[i] = graph.IsCoverNode(sub.Nodes[i]);
                facing[i] = graph.CoverFacing(sub.Nodes[i]);
            }
            var visible = new bool[n * n];
            for (int a = 0; a < n; a++)
            for (int b = 0; b < n; b++)
                visible[a * n + b] = a == b || graph.NodeSeesNode(sub.Nodes[a], sub.Nodes[b]);
            var dist = new double[agents * n];
            for (int a = 0; a < agents; a++)
            for (int j = 0; j < n; j++)
            {
                int h = graph.Hops(agentNodes[a], sub.Nodes[j]);
                dist[a * n + j] = h == int.MaxValue ? InstanceFeatures.UnreachableCost : h;
            }

            var escape = task.EscapeNodes(sub, graph);
            var features = InstanceFeatures.Compute(x, z, sub.PlayerLocal, cover, facing, escape, visible, dist, agents);
            var q = QuboBuilder.Build(features, weights, out double offset, out double lambda);

            var coverLocal = new List<int>();
            for (int i = 0; i < n; i++) if (cover[i]) coverLocal.Add(i);
            return new CoordinationInstance
            {
                Sub = sub, Features = features, Weights = weights, Lambda = lambda, PlayerNode = player, AgentNodes = agentNodes,
                X = x, Z = z, IsCover = cover, CoverFacing = facing,
                Request = new QuboRequest
                {
                    InstanceId = instanceId, Seed = seed, AgentCount = agents, NodeCount = n,
                    AgentNodes = new List<int>(sub.AgentLocal), CoverNodes = coverLocal, EscapeRoutes = new List<int>(features.Escape),
                    QMatrix = q, Offset = offset, WeightCoverage = weights.Coverage, WeightRedundancy = weights.Redundancy,
                    WeightFlanking = weights.Flanking, WeightCover = weights.Cover, WeightDistance = weights.Distance,
                    PenaltyConflict = lambda, LatencyBudgetMs = budgetMs, Variant = variant,
                },
            };
        }

        /// <summary>block if the node is itself an escape node, cover if it has cover facing the player, else flank.</summary>
        public string RoleOf(int local)
        {
            if (System.Array.BinarySearch(Features.Escape, local) >= 0) return "block";
            return Features.CoverQ[local] > 0 ? "cover" : "flank";
        }

        /// <summary>Share of escape nodes seen from at least one of the plan's nodes.</summary>
        public double CoordinationScore(int[] targets)
        {
            if (Features.Escape.Length == 0) return 0;
            int n = Features.NodeCount, covered = 0;
            foreach (int e in Features.Escape)
                foreach (int t in targets)
                    if (Features.Visible[t * n + e]) { covered++; break; }
            return covered / (double)Features.Escape.Length;
        }

        /// <summary>Angle (degrees) at the player between a node and the mean direction of all targets.</summary>
        public float FlankAngle(int local, int[] targets)
        {
            int p = Sub.PlayerLocal;
            double mx = 0, mz = 0;
            foreach (int t in targets) { mx += X[t] - X[p]; mz += Z[t] - Z[p]; }
            var mean = new Vector2((float)mx, (float)mz);
            var dir = new Vector2((float)(X[local] - X[p]), (float)(Z[local] - Z[p]));
            if (mean.sqrMagnitude < 1e-6f || dir.sqrMagnitude < 1e-6f) return 0f;
            return Vector2.Angle(mean, dir);
        }
    }
}
