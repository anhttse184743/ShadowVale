using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Solvers.Classical;
using ShadowVale.AI.Tactical;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.AI
{
    /// <summary>One re-plan (M4): map graph → local QUBO instance → plan → roles and coordination score.</summary>
    public class CoordinationTests
    {
        private static readonly Vector3 Origin = new Vector3(0, 7000, 0);

        private static TacticalGraph Grid(int w, int h, float step = 10f)
        {
            var asset = ScriptableObject.CreateInstance<TacticalGraphAsset>();
            asset.sceneName = "Test";
            asset.nodes = new TacticalGraphAsset.Node[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var nb = new List<int>();
                if (x > 0) nb.Add(y * w + x - 1);
                if (x < w - 1) nb.Add(y * w + x + 1);
                if (y > 0) nb.Add((y - 1) * w + x);
                if (y < h - 1) nb.Add((y + 1) * w + x);
                asset.nodes[y * w + x] = new TacticalGraphAsset.Node
                {
                    position = Origin + new Vector3(x * step, 0, y * step), neighbors = nb.ToArray(),
                    cover = (x + y) % 3 == 0, coverFacing = 90f * ((x + y) % 4), chokepoint = x == 0 && y == h / 2,
                };
            }
            var g = new TacticalGraph(asset);
            g.SetVisibility(VisibilityCache.BuildNow(g, 25f, null));
            return g;
        }

        private static CoordinationInstance Build(TacticalGraph g, Vector3 player, params Vector3[] agents)
            => CoordinationInstance.Build(g, player, agents, ContainPlayerTask.Instance, CoordinationWeights.Default,
                maxNodes: 20, instanceId: "test", seed: 7, budgetMs: 120, variant: "greedy");

        [Test]
        public void InstanceIsTheLocalNeighbourhoodWithAValidQubo()
        {
            var g = Grid(9, 9);
            var inst = Build(g, Origin + new Vector3(40, 0, 40),
                Origin + new Vector3(0, 0, 0), Origin + new Vector3(80, 0, 80), Origin + new Vector3(80, 0, 0));
            var r = inst.Request;
            Assert.AreEqual(3, r.AgentCount);
            Assert.LessOrEqual(r.NodeCount, 20);
            Assert.AreEqual(r.VariableCount() * r.VariableCount(), r.QMatrix.Length);
            Assert.AreEqual(g.NearestNode(Origin + new Vector3(40, 0, 40)), inst.Sub.Nodes[inst.Sub.PlayerLocal]);
            Assert.IsFalse(inst.Features.Escape.Contains(inst.Sub.PlayerLocal), "The player's own node is not an escape.");
            Assert.IsNotEmpty(inst.Features.Escape);
            Assert.AreEqual(inst.Lambda * 3, r.Offset, 1e-9);
            for (int i = 0; i < r.VariableCount(); i++)
            for (int j = 0; j < i; j++)
                Assert.AreEqual(0.0, r.QMatrix[i * r.VariableCount() + j], "Upper-triangular.");
        }

        [Test]
        public void GreedyPlanIsFeasibleAndInterpretable()
        {
            var g = Grid(9, 9);
            var inst = Build(g, Origin + new Vector3(40, 0, 40),
                Origin + new Vector3(10, 0, 40), Origin + new Vector3(70, 0, 40), Origin + new Vector3(40, 0, 70), Origin + new Vector3(40, 0, 10));
            var plan = new GreedySolver().Solve(inst.Request);
            Assert.IsTrue(plan.IsFeasible(inst.Request.AgentCount, inst.Request.NodeCount));
            Assert.AreEqual(4, plan.AgentToTarget.Distinct().Count(), "Four agents, four different nodes.");
            foreach (int t in plan.AgentToTarget)
                CollectionAssert.Contains(new[] { "block", "cover", "flank" }, inst.RoleOf(t));
            double score = inst.CoordinationScore(plan.AgentToTarget);
            Assert.That(score, Is.InRange(0.0, 1.0));
            Assert.Greater(score, 0.0, "A four-agent plan covers some of the ways out.");
        }

        [Test]
        public void EnergyOfThePlanIsMinusTheObjective()
        {
            var g = Grid(7, 7);
            var inst = Build(g, Origin + new Vector3(30, 0, 30), Origin, Origin + new Vector3(60, 0, 60));
            var plan = new GreedySolver().Solve(inst.Request);
            Assert.AreEqual(-plan.Energy, plan.ObjectiveValue, 1e-9);
            Assert.Less(plan.Energy, 0.0, "Feasible plans sit below zero after the λ offset.");
        }

        [Test]
        public void TooFewNodesForTheAgentsGivesNoInstance()
        {
            var g = Grid(1, 2);
            Assert.IsNull(Build(g, Origin, Origin, Origin, Origin + new Vector3(0, 0, 10)));
        }
    }
}
