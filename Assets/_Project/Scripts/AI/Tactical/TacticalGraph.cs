using System;
using System.Collections.Generic;
using ShadowVale.AI.Contracts;
using UnityEngine;

namespace ShadowVale.AI.Tactical
{
    /// <summary>
    /// The runtime tactical graph (<see cref="IWorldQuery"/>): 20–150 nodes over the walkable map,
    /// with cover flags, chokepoints, all-pairs hop distances and — once <see cref="VisibilityCache"/>
    /// has run — node-to-node visibility. Immutable apart from that visibility matrix.
    /// </summary>
    public sealed class TacticalGraph : IWorldQuery
    {
        private readonly Vector3[] _positions;
        private readonly int[][] _adjacency;
        private readonly bool[] _cover;
        private readonly float[] _coverFacing;
        private readonly int[] _coverNodes, _chokepoints;
        private readonly int[] _hops;
        private bool[] _visible;

        public int ObstructionMask { get; set; } = ~0;
        public string SceneName { get; }

        public TacticalGraph(TacticalGraphAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            SceneName = asset.sceneName;
            int n = asset.nodes.Length;
            _positions = new Vector3[n]; _adjacency = new int[n][]; _cover = new bool[n]; _coverFacing = new float[n];
            var cover = new List<int>(); var choke = new List<int>();
            for (int i = 0; i < n; i++)
            {
                var node = asset.nodes[i];
                _positions[i] = node.position;
                _adjacency[i] = node.neighbors ?? Array.Empty<int>();
                _cover[i] = node.cover; _coverFacing[i] = node.coverFacing;
                if (node.cover) cover.Add(i);
                if (node.chokepoint) choke.Add(i);
            }
            _coverNodes = cover.ToArray(); _chokepoints = choke.ToArray();
            _hops = GraphAlgorithms.AllPairsHops(_adjacency);
        }

        public int NodeCount => _positions.Length;
        public Vector3 NodePosition(int node) => _positions[node];
        public IReadOnlyList<int> Neighbors(int node) => _adjacency[node];
        public bool IsCoverNode(int node) => _cover[node];
        public float CoverFacing(int node) => _coverFacing[node];
        public IReadOnlyList<int> CoverNodes => _coverNodes;
        public IReadOnlyList<int> EscapeRoutes => _chokepoints;
        public int Hops(int a, int b) => _hops[a * _positions.Length + b];

        public int NearestNode(Vector3 position)
        {
            int best = -1; float bestD = float.PositiveInfinity;
            for (int i = 0; i < _positions.Length; i++)
            {
                Vector3 d = _positions[i] - position;
                d.y *= 2f; // Prefer the node on the same level (bridge vs river bed below it).
                float sq = d.sqrMagnitude;
                if (sq < bestD) { bestD = sq; best = i; }
            }
            return best;
        }

        public bool HasLineOfSight(Vector3 from, Vector3 to)
            => !Physics.Linecast(from, to, ObstructionMask, QueryTriggerInteraction.Ignore);

        public bool VisibilityReady => _visible != null;

        /// <summary>Visible pairs once computed; before that, a two-hop radius (the solver repo's v1 rule).</summary>
        public bool NodeSeesNode(int a, int b) => _visible != null ? _visible[a * _positions.Length + b] : Hops(a, b) <= 2;

        public void SetVisibility(bool[] visible)
        {
            if (visible != null && visible.Length != _positions.Length * _positions.Length)
                throw new ArgumentException("visibility matrix size");
            _visible = visible;
        }
    }
}
