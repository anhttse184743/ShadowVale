using System.Collections.Generic;

namespace ShadowVale.AI.Tactical
{
    /// <summary>Unweighted-graph helpers shared by the baker and the runtime graph. Adjacency is per node.</summary>
    public static class GraphAlgorithms
    {
        public const int Unreachable = int.MaxValue;

        /// <summary>Hop distance from every node to every node, row-major n×n.</summary>
        public static int[] AllPairsHops(IReadOnlyList<int[]> adjacency)
        {
            int n = adjacency.Count;
            var hops = new int[n * n];
            for (int i = 0; i < hops.Length; i++) hops[i] = Unreachable;
            var queue = new Queue<int>();
            for (int s = 0; s < n; s++)
            {
                hops[s * n + s] = 0;
                queue.Enqueue(s);
                while (queue.Count > 0)
                {
                    int u = queue.Dequeue();
                    int du = hops[s * n + u];
                    foreach (int v in adjacency[u])
                    {
                        if (hops[s * n + v] != Unreachable) continue;
                        hops[s * n + v] = du + 1;
                        queue.Enqueue(v);
                    }
                }
            }
            return hops;
        }

        /// <summary>
        /// Betweenness centrality (Brandes 2001, unweighted, undirected, not normalised): how many
        /// shortest paths pass through each node. High values mark chokepoints such as bridges.
        /// </summary>
        public static float[] Betweenness(IReadOnlyList<int[]> adjacency)
        {
            int n = adjacency.Count;
            var cb = new float[n];
            var stack = new Stack<int>();
            var preds = new List<int>[n];
            for (int i = 0; i < n; i++) preds[i] = new List<int>();
            var sigma = new float[n];
            var dist = new int[n];
            var delta = new float[n];
            var queue = new Queue<int>();
            for (int s = 0; s < n; s++)
            {
                for (int i = 0; i < n; i++) { preds[i].Clear(); sigma[i] = 0; dist[i] = -1; delta[i] = 0; }
                sigma[s] = 1; dist[s] = 0;
                queue.Enqueue(s);
                while (queue.Count > 0)
                {
                    int v = queue.Dequeue();
                    stack.Push(v);
                    foreach (int w in adjacency[v])
                    {
                        if (dist[w] < 0) { dist[w] = dist[v] + 1; queue.Enqueue(w); }
                        if (dist[w] == dist[v] + 1) { sigma[w] += sigma[v]; preds[w].Add(v); }
                    }
                }
                while (stack.Count > 0)
                {
                    int w = stack.Pop();
                    foreach (int v in preds[w]) delta[v] += sigma[v] / sigma[w] * (1f + delta[w]);
                    if (w != s) cb[w] += delta[w];
                }
            }
            for (int i = 0; i < n; i++) cb[i] *= .5f; // Each undirected path was counted from both ends.
            return cb;
        }

        /// <summary>Nodes of the largest connected component, ascending.</summary>
        public static List<int> LargestComponent(IReadOnlyList<int[]> adjacency)
        {
            int n = adjacency.Count;
            var label = new int[n];
            for (int i = 0; i < n; i++) label[i] = -1;
            var best = new List<int>();
            var stack = new Stack<int>();
            for (int s = 0; s < n; s++)
            {
                if (label[s] >= 0) continue;
                var component = new List<int>();
                label[s] = s; stack.Push(s);
                while (stack.Count > 0)
                {
                    int u = stack.Pop();
                    component.Add(u);
                    foreach (int v in adjacency[u])
                        if (label[v] < 0) { label[v] = s; stack.Push(v); }
                }
                if (component.Count > best.Count) best = component;
            }
            best.Sort();
            return best;
        }
    }
}
