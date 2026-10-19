using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Boss;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Tactical;
using ShadowVale.Gameplay.Enemies;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.AI
{
    /// <summary>
    /// 18/10/2026: the commander's men look after him (the squad plan keeps watch on the way to him, the
    /// men he calls rally to him first), and a wounded guard left alone recovers.
    /// </summary>
    public class CommanderGuardTests
    {
        private sealed class Line : IWorldQuery
        {
            public readonly List<Vector3> Nodes = new List<Vector3>();
            public int NodeCount => Nodes.Count;
            public Vector3 NodePosition(int node) => Nodes[node];
            public IReadOnlyList<int> Neighbors(int node) => new int[0];
            public bool IsCoverNode(int node) => false;
            public float CoverFacing(int node) => 0f;
            public IReadOnlyList<int> CoverNodes => new int[0];
            public IReadOnlyList<int> EscapeRoutes => new int[0];
            public int NearestNode(Vector3 p) => Enumerable.Range(0, Nodes.Count).OrderBy(i => (Nodes[i] - p).sqrMagnitude).First();
            public int Hops(int a, int b) => 1;
            public bool HasLineOfSight(Vector3 from, Vector3 to) => true;
            public bool NodeSeesNode(int a, int b) => true;
            public bool VisibilityReady => true;
        }

        private static (Line graph, LocalSubgraph sub) PlayerAndCommander(params Vector3[] others)
        {
            var g = new Line();
            g.Nodes.Add(Vector3.zero); // The player's node: local 0.
            g.Nodes.AddRange(others);
            var sub = new LocalSubgraph
            {
                Nodes = Enumerable.Range(0, g.Nodes.Count).ToArray(), PlayerLocal = 0,
                AgentLocal = new int[0], BoundaryLocal = new[] { 3 },
            };
            return (g, sub);
        }

        [Test]
        public void UnderHisCommandTheSquadAlsoWatchesTheWayToHim()
        {
            var commander = new Vector3(0, 0, 20);
            var (g, sub) = PlayerAndCommander(
                new Vector3(0, 0, 12),   // 1: on the way to him
                new Vector3(1, 0, 15),   // 2: on the way to him
                new Vector3(10, 0, 14),  // 3: the edge of the player's neighbourhood, well off the line
                new Vector3(0, 0, 6),    // 4: on the line, but the player's half
                new Vector3(3, 0, 19));  // 5: right beside him
            var contain = ContainPlayerTask.Instance.EscapeNodes(sub, g);
            CollectionAssert.AreEquivalent(new[] { 3 }, contain, "Without him: only the ways out round the player.");

            var task = new GuardCommanderTask(ContainPlayerTask.Instance, commander);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 5 }, task.EscapeNodes(sub, g),
                "With him: the ways out, plus the way from the player to him and the ground beside him.");
            Assert.AreEqual("contain+guard_commander", task.Id);
            Assert.AreEqual(0f, GuardCommanderTask.OffLine(new Vector3(0, 0, 12), Vector3.zero, commander), 1e-5f);
            Assert.AreEqual(3f, GuardCommanderTask.OffLine(new Vector3(0, 0, 23), Vector3.zero, commander), 1e-5f, "Past his end: from him.");
        }

        [Test]
        public void TheWayToHimNeverOutweighsTheRestOfThePlan()
        {
            var corridor = Enumerable.Range(0, 10).Select(i => new Vector3(i % 2, 0, 10 + i)).ToArray();
            var (g, sub) = PlayerAndCommander(corridor);
            var guard = GuardCommanderTask.GuardNodes(sub, g, new Vector3(0, 0, 20));
            Assert.AreEqual(GuardCommanderTask.MaxGuardNodes, guard.Count, "At most four nodes added: the ring round the player still counts.");
            CollectionAssert.DoesNotContain(guard, sub.PlayerLocal);
        }

        [Test]
        public void TheMenHeCallsGatherInFrontOfHim()
        {
            var commander = Vector3.zero;
            var player = new Vector3(0, 0, 20);
            var points = Enumerable.Range(0, 3).Select(i => NearbyAgentsReinforcements.RallyPoint(commander, player, i)).ToList();
            Assert.AreEqual(new Vector3(0, 0, 3), points[1], "Straight between him and the player.");
            foreach (var p in points)
            {
                Assert.AreEqual(NearbyAgentsReinforcements.RallyDistance, (p - commander).magnitude, 1e-4f);
                Assert.Less((p - player).magnitude, (commander - player).magnitude, "On the player's side of him.");
            }
            Assert.AreEqual(3, points.Distinct().Count(), "Fanned out, not on top of each other.");
        }

        [Test]
        public void AWoundedGuardLeftAloneForHalfAMinuteRecovers()
        {
            var r = new HealthRegen();
            Assert.AreEqual(30f, r.QuietSeconds);
            Assert.AreEqual(.05f, r.SharePerSecond, 1e-6f, "5% of his health a second.");
            Assert.AreEqual(50f, r.Tick(0f, .1f, 50f, 100f, out _));
            Assert.AreEqual(50f, r.Tick(29.9f, .1f, 50f, 100f, out bool early));
            Assert.IsFalse(early, "Not before 30 s without a hit.");
            Assert.AreEqual(55f, r.Tick(30f, 1f, 50f, 100f, out bool started), 1e-4f);
            Assert.IsTrue(started, "The recovery starts (once).");
            Assert.AreEqual(60f, r.Tick(31f, 1f, 55f, 100f, out started), 1e-4f);
            Assert.IsFalse(started);

            Assert.AreEqual(40f, r.Tick(32f, 1f, 40f, 100f, out _), "Hit again: no more recovery...");
            Assert.IsFalse(r.Recovering);
            Assert.AreEqual(40f, r.Tick(61.9f, 1f, 40f, 100f, out _), "...the wait starts over.");
            Assert.AreEqual(45f, r.Tick(62f, 1f, 40f, 100f, out _), 1e-4f);

            Assert.AreEqual(100f, r.Tick(63f, 1f, 99f, 100f, out _), 1e-4f, "Never past his maximum.");
            Assert.IsFalse(r.Recovering);
            Assert.AreEqual(0f, r.Tick(200f, 1f, 0f, 100f, out _), "The dead stay dead.");
        }

        [Test]
        public void ACommanderFiringOnTheMoveLosesLessAimThanAGuard()
        {
            var p = new ShotAccuracyProfile();
            ShotContext Shot(bool moving, bool boss) => new ShotContext(12f, 18f, 10f, false, false, false, false, moving, boss);
            Assert.AreEqual(p.movingShooter, ShotAccuracy.HitChance(p, Shot(true, false)) / ShotAccuracy.HitChance(p, Shot(false, false)), 1e-4f);
            Assert.AreEqual(.8f, ShotAccuracy.HitChance(p, Shot(true, true)) / ShotAccuracy.HitChance(p, Shot(false, true)), 1e-4f,
                "×0.8 on the move for him, ×0.6 for a guard.");
        }
    }
}
