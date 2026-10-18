using System.Collections.Generic;
using ShadowVale.AI.Contracts;

namespace ShadowVale.AI.Tactical
{
    /// <summary>
    /// The piece of the map graph one re-plan works on: the nodes nearest the player by hops (up to
    /// <c>maxNodes</c>, which keeps the QUBO at agents × maxNodes variables) plus every agent's current
    /// node. Local index i ↔ global node <see cref="Nodes"/>[i]; the player is local 0.
    /// </summary>
    public sealed class LocalSubgraph
    {
        public int[] Nodes;
        public int PlayerLocal;
        public int[] AgentLocal;
        /// <summary>Local nodes on the edge of the player's neighbourhood: a neighbour lies outside it.</summary>
        public int[] BoundaryLocal;

        public int Count => Nodes.Length;

        public static LocalSubgraph Extract(IWorldQuery graph, int playerNode, IReadOnlyList<int> agentNodes, int maxNodes)
        {
            var nodes = new List<int>();
            var local = new Dictionary<int, int>();
            void Add(int g) { if (!local.ContainsKey(g)) { local[g] = nodes.Count; nodes.Add(g); } }

            var agents = new List<int>();
            foreach (int a in agentNodes) if (a >= 0 && !agents.Contains(a)) agents.Add(a);
            int reserve = 0;
            foreach (int a in agents) if (a != playerNode) reserve++;
            int neighbourhood = System.Math.Max(1, maxNodes - reserve);

            // Breadth-first from the player: nearest by hops first, ties by graph order (deterministic).
            var queue = new Queue<int>();
            var seen = new HashSet<int> { playerNode };
            queue.Enqueue(playerNode);
            var ring = new List<int>();
            while (queue.Count > 0 && nodes.Count < neighbourhood)
            {
                int u = queue.Dequeue();
                Add(u);
                ring.Clear();
                foreach (int v in graph.Neighbors(u)) if (seen.Add(v)) ring.Add(v);
                ring.Sort();
                foreach (int v in ring) queue.Enqueue(v);
            }
            var neighbourhoodSet = new HashSet<int>(nodes);
            foreach (int a in agents) Add(a);

            var boundary = new List<int>();
            foreach (int g in neighbourhoodSet)
            {
                if (g == playerNode) continue;
                foreach (int v in graph.Neighbors(g))
                    if (!neighbourhoodSet.Contains(v)) { boundary.Add(local[g]); break; }
            }
            boundary.Sort();

            var agentLocal = new int[agentNodes.Count];
            for (int i = 0; i < agentNodes.Count; i++) agentLocal[i] = agentNodes[i] >= 0 ? local[agentNodes[i]] : -1;
            return new LocalSubgraph { Nodes = nodes.ToArray(), PlayerLocal = 0, AgentLocal = agentLocal, BoundaryLocal = boundary.ToArray() };
        }
    }
}
