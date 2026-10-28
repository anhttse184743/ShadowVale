using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Perception;
using ShadowVale.AI.Tactical;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.AI
{
    /// <summary>The tactical graph (M3): graph algorithms, runtime queries, visibility, local subgraph, content round trip.</summary>
    public class TacticalGraphTests
    {
        private static readonly Vector3 Origin = new Vector3(0, 6000, 0);

        /// <summary>A w×h grid, 10 m apart, 4-connected, far above the scene.</summary>
        private static TacticalGraphAsset Grid(int w, int h)
        {
            var asset = ScriptableObject.CreateInstance<TacticalGraphAsset>();
            asset.sceneName = "Test";
            asset.nodes = new TacticalGraphAsset.Node[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var nb = new System.Collections.Generic.List<int>();
                if (x > 0) nb.Add(y * w + x - 1);
                if (x < w - 1) nb.Add(y * w + x + 1);
                if (y > 0) nb.Add((y - 1) * w + x);
                if (y < h - 1) nb.Add((y + 1) * w + x);
                asset.nodes[y * w + x] = new TacticalGraphAsset.Node { position = Origin + new Vector3(x * 10, 0, y * 10), neighbors = nb.ToArray() };
            }
            return asset;
        }

        [Test]
        public void BetweennessFindsTheBridgeOfABarbell()
        {
            // Two triangles 0-1-2 and 4-5-6 joined through node 3.
            int[][] adj =
            {
                new[] { 1, 2 }, new[] { 0, 2 }, new[] { 0, 1, 3 }, new[] { 2, 4 }, new[] { 3, 5, 6 }, new[] { 4, 6 }, new[] { 4, 5 },
            };
            var b = GraphAlgorithms.Betweenness(adj);
            Assert.AreEqual(3, System.Array.IndexOf(b, b.Max()), "Node 3 carries every cross path.");
            var hops = GraphAlgorithms.AllPairsHops(adj);
            Assert.AreEqual(4, hops[0 * 7 + 6]);
            Assert.AreEqual(0, hops[5 * 7 + 5]);
        }

        [Test]
        public void LargestComponentDropsIslands()
        {
            int[][] adj = { new[] { 1 }, new[] { 0, 2 }, new[] { 1 }, new[] { 4 }, new[] { 3 } };
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, GraphAlgorithms.LargestComponent(adj));
            Assert.AreEqual(GraphAlgorithms.Unreachable, GraphAlgorithms.AllPairsHops(adj)[0 * 5 + 3]);
        }

        [Test]
        public void RuntimeGraphAnswersQueries()
        {
            var asset = Grid(4, 3);
            asset.nodes[5].cover = true; asset.nodes[5].coverFacing = 90f;
            asset.nodes[11].chokepoint = true;
            var g = new TacticalGraph(asset);
            Assert.AreEqual(12, g.NodeCount);
            CollectionAssert.AreEqual(new[] { 5 }, g.CoverNodes.ToArray());
            Assert.AreEqual(90f, g.CoverFacing(5));
            CollectionAssert.AreEqual(new[] { 11 }, g.EscapeRoutes.ToArray());
            Assert.AreEqual(5, g.Hops(0, 11));
            Assert.AreEqual(6, g.NearestNode(Origin + new Vector3(21, 0, 9)));
            Assert.IsFalse(g.VisibilityReady);
            Assert.IsTrue(g.NodeSeesNode(0, 2), "Before visibility: two hops.");
            Assert.IsFalse(g.NodeSeesNode(0, 3));
            Assert.Throws<System.ArgumentException>(() => g.SetVisibility(new bool[5]));
        }

        private sealed class BlocksRow : ISightOccluder
        {
            public bool Blocks(Vector3 eye, Vector3 target) => Mathf.Abs(eye.z - target.z) > 15f; // Nothing across two rows.
        }

        [Test]
        public void VisibilityRespectsWallsOccludersAndRange()
        {
            var g = new TacticalGraph(Grid(3, 3));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                // Between node 0 (0,0) and node 2 (20,0), at eye height.
                wall.transform.position = Origin + new Vector3(10, 1.2f, -3);
                wall.transform.localScale = new Vector3(.5f, 3f, 2f);
                Physics.SyncTransforms();
                var v = VisibilityCache.BuildNow(g, 25f, new BlocksRow());
                g.SetVisibility(v);
                Assert.IsTrue(g.VisibilityReady);
                Assert.IsTrue(g.NodeSeesNode(0, 1));
                Assert.IsTrue(g.NodeSeesNode(1, 0), "Symmetric.");
                Assert.IsFalse(g.NodeSeesNode(0, 8), "Beyond 25 m.");
                Assert.IsFalse(g.NodeSeesNode(0, 6), "The occluder hides two rows up.");
                Assert.IsTrue(g.NodeSeesNode(0, 3));
                wall.transform.position = Origin + new Vector3(5, 1.2f, 0);
                Physics.SyncTransforms();
                Assert.IsFalse(VisibilityCache.Sees(g, 0, 1, 25f, null), "A wall in between.");
            }
            finally { Object.DestroyImmediate(wall); }
        }

        [Test]
        public void LocalSubgraphIsTheNeighbourhoodPlusTheAgents()
        {
            var g = new TacticalGraph(Grid(7, 7));
            int player = 24, farAgent = 0, nearAgent = 25;
            var sub = LocalSubgraph.Extract(g, player, new[] { farAgent, nearAgent }, maxNodes: 10);
            Assert.AreEqual(player, sub.Nodes[sub.PlayerLocal]);
            Assert.LessOrEqual(sub.Count, 10);
            Assert.AreEqual(farAgent, sub.Nodes[sub.AgentLocal[0]], "A far agent is still in the instance.");
            Assert.AreEqual(nearAgent, sub.Nodes[sub.AgentLocal[1]]);
            Assert.AreEqual(sub.Nodes.Length, sub.Nodes.Distinct().Count());
            foreach (int local in sub.Nodes.Where(n => n != farAgent).Select(n => g.Hops(player, n)))
                Assert.LessOrEqual(local, 2, "Nearest by hops first.");
            Assert.IsNotEmpty(sub.BoundaryLocal);
            foreach (int b in sub.BoundaryLocal)
                Assert.IsTrue(g.Neighbors(sub.Nodes[b]).Any(n => !sub.Nodes.Contains(n)), "Boundary nodes lead out.");

            var again = LocalSubgraph.Extract(g, player, new[] { farAgent, nearAgent }, maxNodes: 10);
            CollectionAssert.AreEqual(sub.Nodes, again.Nodes, "Deterministic.");
        }

        [Test]
        public void GraphRoundTripsThroughTheContentSchema()
        {
            var asset = Grid(3, 2);
            asset.nodes[4].cover = true; asset.nodes[4].coverFacing = 180f; asset.nodes[2].chokepoint = true;
            var def = asset.ToMapDefinition("map01", "Map 1");
            Assert.AreEqual(6, def.NavGraphNodes.Count);
            CollectionAssert.AreEqual(new[] { 2 }, def.EscapeRoutes);
            var back = TacticalGraphAsset.FromMapDefinition(def);
            Assert.AreEqual(asset.nodes.Length, back.nodes.Length);
            for (int i = 0; i < asset.nodes.Length; i++)
            {
                Assert.AreEqual(asset.nodes[i].position, back.nodes[i].position);
                Assert.AreEqual(asset.nodes[i].cover, back.nodes[i].cover);
                Assert.AreEqual(asset.nodes[i].chokepoint, back.nodes[i].chokepoint);
                CollectionAssert.AreEqual(asset.nodes[i].neighbors, back.nodes[i].neighbors);
            }
        }
    }
}
