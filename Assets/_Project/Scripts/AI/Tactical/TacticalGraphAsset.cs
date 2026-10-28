using System;
using System.Collections.Generic;
using ShadowVale.Data.Content;
using UnityEngine;

namespace ShadowVale.AI.Tactical
{
    /// <summary>
    /// A map's baked tactical graph (ShadowVale ▸ AI ▸ Bake Tactical Graph). Lives in a Resources
    /// folder named after the scene so any map loads its own with <see cref="LoadForScene"/>.
    /// Converts to the content bundle's <see cref="MapDefinition"/> nav graph for the web platform.
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowVale/Tactical Graph", fileName = "TacticalGraph")]
    public sealed class TacticalGraphAsset : ScriptableObject
    {
        public const string ResourcesFolder = "TacticalGraphs";

        [Serializable]
        public struct Node
        {
            public Vector3 position;
            public bool cover;
            /// <summary>World yaw the cover protects from (degrees).</summary>
            public float coverFacing;
            /// <summary>A chokepoint the player escapes through: bridge, exit, narrow pass.</summary>
            public bool chokepoint;
            public int[] neighbors;
        }

        public string sceneName;
        [Tooltip("Grid spacing the graph was baked with (metres).")]
        public float spacing;
        public Node[] nodes = Array.Empty<Node>();

        public int NodeCount => nodes.Length;

        public static TacticalGraphAsset LoadForScene(string scene)
            => string.IsNullOrEmpty(scene) ? null : Resources.Load<TacticalGraphAsset>(ResourcesFolder + "/" + scene);

        /// <summary>The graph in the content bundle's schema (maps[].nav_graph_nodes / escape_routes).</summary>
        public MapDefinition ToMapDefinition(string mapId, string displayName)
        {
            var def = new MapDefinition { Id = mapId, DisplayName = displayName, SceneName = sceneName };
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                def.NavGraphNodes.Add(new NavGraphNode
                {
                    Id = i, X = n.position.x, Y = n.position.y, Z = n.position.z,
                    IsCover = n.cover, CoverFacing = n.coverFacing,
                    Neighbors = new List<int>(n.neighbors ?? Array.Empty<int>()),
                });
                if (n.chokepoint) def.EscapeRoutes.Add(i);
            }
            return def;
        }

        /// <summary>The graph from a content bundle map (what the web platform publishes).</summary>
        public static TacticalGraphAsset FromMapDefinition(MapDefinition def)
        {
            var asset = CreateInstance<TacticalGraphAsset>();
            asset.sceneName = def.SceneName;
            var escapes = new HashSet<int>(def.EscapeRoutes);
            asset.nodes = new Node[def.NavGraphNodes.Count];
            foreach (var n in def.NavGraphNodes)
                asset.nodes[n.Id] = new Node
                {
                    position = new Vector3(n.X, n.Y, n.Z), cover = n.IsCover, coverFacing = n.CoverFacing,
                    chokepoint = escapes.Contains(n.Id), neighbors = n.Neighbors.ToArray(),
                };
            return asset;
        }
    }
}
