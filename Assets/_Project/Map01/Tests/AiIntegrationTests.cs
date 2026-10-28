using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Fsm;
using ShadowVale.Core.Events;
using ShadowVale.Core.Services;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>Map 1's guards inside the shared AI module: registry, squads, events, debug console.</summary>
    public sealed class AiIntegrationTests : ForestSceneTestBase
    {
        [UnityTest]
        public IEnumerator GuardsJoinTheRegistryInTheirSquadsAndReportContacts()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            // Entering play mode reloads the domain and the test framework restores this iterator
            // without its closures, so everything that uses lambdas runs in an iterator made after it.
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TheBakedTacticalGraphLoadsWithCoverAtTheCampsAndBuildsVisibility()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return GraphInPlayMode();
            yield return new ExitPlayMode();
        }

        private static IEnumerator GraphInPlayMode()
        {
            yield return null;
            Assert.IsTrue(ServiceLocator.TryResolve<IWorldQuery>(out var graph), "Map 1 registers its tactical graph.");
            Assert.That(graph.NodeCount, Is.InRange(100, 600));
            for (int i = 1; i < graph.NodeCount; i++)
                Assert.AreNotEqual(int.MaxValue, graph.Hops(0, i), $"Node {i} is reachable (river crossings bridged).");
            Assert.GreaterOrEqual(graph.EscapeRoutes.Count, 3, "At least the three bridges.");
            foreach (var camp in new[] { "Enemy outpost 1", "Enemy outpost 2", "Enemy outpost 3" })
            {
                var centre = GameObject.Find(camp).transform.position;
                bool cover = false;
                foreach (int c in graph.CoverNodes) if ((graph.NodePosition(c) - centre).sqrMagnitude < 12f * 12f) cover = true;
                Assert.IsTrue(cover, camp + " has cover nodes (sandbags, tent walls).");
            }
            for (float until = Time.realtimeSinceStartup + 20f; !graph.VisibilityReady && Time.realtimeSinceStartup < until;) yield return null;
            Assert.IsTrue(graph.VisibilityReady, "Visibility is computed within a few seconds of loading.");
        }

        [UnityTest]
        public IEnumerator ACampUnderAttackIsPlannedAsOneSquadWithDistinctPositions()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return SquadPlanInPlayMode();
            yield return new ExitPlayMode();
        }

        private static IEnumerator SquadPlanInPlayMode()
        {
            yield return null;
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            Assert.IsTrue(ServiceLocator.TryResolve<SquadCoordinator>(out var coordinator), "Map 1 runs a squad coordinator.");
            var squad = mission.Enemies.Where(e => e.SquadId == "C2" && e.Alive).ToArray();
            Assert.GreaterOrEqual(squad.Length, 2);

            var alerts = new List<SquadAlertEvent>();
            var replans = new List<ReplanStartedEvent>();
            var plans = new List<PlanAppliedEvent>();
            System.Action<SquadAlertEvent> onAlert = alerts.Add;
            System.Action<ReplanStartedEvent> onReplan = replans.Add;
            System.Action<PlanAppliedEvent> onPlan = plans.Add;
            EventBus.Subscribe(onAlert); EventBus.Subscribe(onReplan); EventBus.Subscribe(onPlan);
            try
            {
                squad[0].DebugDamage(.1f); // Shot at: he knows where Nam is and radios the camp.
                for (float until = Time.time + 5f; plans.Count == 0 && Time.time < until;) yield return null;

                Assert.IsTrue(alerts.Any(a => a.SourceSquad == "C2" && a.SquadMatesJoined >= squad.Length - 1), "The whole camp is alerted.");
                Assert.IsTrue(squad.All(g => g.Engaged), "Every C2 guard is fighting.");
                Assert.IsNotEmpty(replans, "A re-plan started.");
                Assert.AreEqual("first_contact", replans[0].Trigger);
                var plan = plans.First(p => p.Group.Split('+').Contains("C2"));
                Assert.GreaterOrEqual(plan.Assignments.Length, squad.Length);
                Assert.AreEqual(plan.Assignments.Length, plan.Assignments.Select(a => a.Node).Distinct().Count(), "One node per guard.");
                Assert.IsNull(plan.FallbackReason);
                Assert.That(plan.CoordinationScore, Is.InRange(0.0, 1.0));
                foreach (var guard in squad)
                {
                    Assert.IsTrue(guard.Brain.Memory.HasAssignment, guard.name + " got a position.");
                    Assert.IsTrue(plan.Assignments.Any(a => a.AgentId == guard.LegacyId));
                }
            }
            finally
            {
                EventBus.Unsubscribe(onAlert); EventBus.Unsubscribe(onReplan); EventBus.Unsubscribe(onPlan);
            }
        }

        [UnityTest]
        public IEnumerator WithQieaTheCampIsPlannedByThePythonSidecar()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return SidecarPlanInPlayMode("qiea");
            yield return new ExitPlayMode();
        }

        /// <summary>Tabu is the bundle's default since exp-v2: it must plan in Python within the budget too.</summary>
        [UnityTest]
        public IEnumerator WithTabuTheCampIsPlannedByThePythonSidecar()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return SidecarPlanInPlayMode("tabu");
            yield return new ExitPlayMode();
        }

        private static IEnumerator SidecarPlanInPlayMode(string variant)
        {
            string url = System.Environment.GetEnvironmentVariable("SVL_TEST_SIDECAR_URL") ?? "http://127.0.0.1:8011";
            var health = ShadowVale.AI.Solvers.Remote.SidecarLauncher.IsHealthyAsync(url);
            while (!health.IsCompleted) yield return null;
            if (!health.Result) Assert.Ignore($"No sidecar at {url} (start: svl-api --port 8011).");
            string previous = System.Environment.GetEnvironmentVariable("SVL_SOLVER_URL");
            System.Environment.SetEnvironmentVariable("SVL_SOLVER_URL", url);
            var plans = new List<PlanAppliedEvent>();
            System.Action<PlanAppliedEvent> onPlan = plans.Add;
            EventBus.Subscribe(onPlan);
            try
            {
                yield return null;
                var mission = Object.FindFirstObjectByType<Map01Mission>();
                ServiceLocator.TryResolve<SquadCoordinator>(out var coordinator);
                coordinator.SetVariant(variant);
                for (float until = Time.realtimeSinceStartup + 5f; coordinator.SidecarOnline == null && Time.realtimeSinceStartup < until;) yield return null;
                Assert.AreEqual(true, coordinator.SidecarOnline, "The coordinator found the sidecar.");

                mission.Enemies.First(e => e.SquadId == "C2" && e.Alive).DebugDamage(.1f);
                for (float until = Time.realtimeSinceStartup + 5f; plans.Count == 0 && Time.realtimeSinceStartup < until;) yield return null;
                Assert.IsNotEmpty(plans, "A plan came back.");
                var plan = plans[0];
                Assert.IsNull(plan.FallbackReason, plan.FallbackReason);
                Assert.AreEqual(variant, plan.Variant, "Solved in Python, not by the local fallback.");
                Assert.AreEqual(plan.Assignments.Length, plan.Assignments.Select(a => a.Node).Distinct().Count());
                Assert.Less(plan.LatencyMs, 120 + ShadowVale.AI.Solvers.Remote.RemoteSolverClient.NetworkMarginMs, "Within budget.");
            }
            finally
            {
                EventBus.Unsubscribe(onPlan);
                System.Environment.SetEnvironmentVariable("SVL_SOLVER_URL", previous);
            }
        }

        /// <summary>A solver that takes <see cref="DelayMs"/> of real time, then answers greedily.</summary>
        private sealed class SlowSolver : ISquadSolver
        {
            public const int DelayMs = 300;
            public string VariantId => "slow";
            public int LatencyBudgetMs => 120;
            public System.Threading.Tasks.Task<ShadowVale.Data.Coordination.CoordinationPlan> SolveAsync(
                ShadowVale.Data.Coordination.QuboRequest request, System.Threading.CancellationToken ct) =>
                System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(DelayMs);
                    var plan = new ShadowVale.AI.Solvers.Classical.GreedySolver(LatencyBudgetMs).Solve(request);
                    plan.SolverVariantId = VariantId;
                    plan.SolveLatencyMs = DelayMs;
                    return plan;
                });
        }

        [UnityTest]
        public IEnumerator InLockstepAPlanLandsAfterItsRealLatencyInGameTime()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return LockstepInPlayMode();
            yield return new ExitPlayMode();
        }

        private static IEnumerator LockstepInPlayMode()
        {
            // The replay harness's fixed step: game time no longer follows the wall clock.
            int previousCapture = Time.captureFramerate;
            Time.captureFramerate = 30;
            var replans = new List<float>();
            var plans = new List<(PlanAppliedEvent plan, float at)>();
            System.Action<ReplanStartedEvent> onReplan = _ => replans.Add(Time.time);
            System.Action<PlanAppliedEvent> onPlan = e => plans.Add((e, Time.time));
            EventBus.Subscribe(onReplan); EventBus.Subscribe(onPlan);
            try
            {
                yield return null;
                yield return null;
                var mission = Object.FindFirstObjectByType<Map01Mission>();
                ServiceLocator.TryResolve<SquadCoordinator>(out var coordinator);
                coordinator.UseSolver(new SlowSolver());
                coordinator.LockstepLatency = true;

                float realStart = Time.realtimeSinceStartup;
                mission.Enemies.First(e => e.SquadId == "C2" && e.Alive).DebugDamage(.1f);
                for (int frames = 0; plans.Count == 0 && frames < 120; frames++) yield return null;

                Assert.IsNotEmpty(plans, "The slow solver's plan was applied.");
                Assert.AreEqual("slow", plans[0].plan.Variant);
                float gameDelay = plans[0].at - replans[0];
                // Lower bound less a float ulp: two game-time stamps subtracted can land 1e-8 s short of it.
                Assert.That(gameDelay, Is.InRange(SlowSolver.DelayMs / 1000f - 1e-4f, SlowSolver.DelayMs / 1000f + 2f / 30f),
                    "The plan lands after the solve's real latency, measured in game time.");
                Assert.GreaterOrEqual(Time.realtimeSinceStartup - realStart, SlowSolver.DelayMs / 1000f, "The game waited for the solver.");
            }
            finally
            {
                EventBus.Unsubscribe(onReplan); EventBus.Unsubscribe(onPlan);
                Time.captureFramerate = previousCapture;
            }
        }

        [UnityTest]
        public IEnumerator WithPlanningOffTheCampStillRadiosButNobodyPlans()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return NoPlanningInPlayMode();
            yield return new ExitPlayMode();
        }

        private static IEnumerator NoPlanningInPlayMode()
        {
            var alerts = new List<SquadAlertEvent>();
            var replans = new List<ReplanStartedEvent>();
            System.Action<SquadAlertEvent> onAlert = alerts.Add;
            System.Action<ReplanStartedEvent> onReplan = replans.Add;
            EventBus.Subscribe(onAlert); EventBus.Subscribe(onReplan);
            try
            {
                yield return null;
                yield return null;
                var mission = Object.FindFirstObjectByType<Map01Mission>();
                ServiceLocator.TryResolve<SquadCoordinator>(out var coordinator);
                coordinator.PlanningEnabled = false;
                var squad = mission.Enemies.Where(e => e.SquadId == "C2" && e.Alive).ToArray();
                squad[0].DebugDamage(.1f);
                for (float until = Time.time + 2f; Time.time < until;) yield return null;

                Assert.IsTrue(alerts.Any(a => a.SourceSquad == "C2"), "The camp is still alerted over the radio.");
                Assert.IsTrue(squad.All(g => g.Engaged), "Every C2 guard fights.");
                Assert.IsEmpty(replans, "Nobody plans: the FSM-only control (each guard finds his own cover).");
            }
            finally
            {
                EventBus.Unsubscribe(onAlert); EventBus.Unsubscribe(onReplan);
            }
        }

        private static IEnumerator InPlayMode()
        {
            yield return null;
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            Assert.IsNotNull(mission);

            foreach (var enemy in mission.Enemies)
                Assert.IsTrue(AgentRegistry.All.Contains(enemy), enemy.name + " is registered.");
            Assert.IsTrue(mission.Enemies.All(e => e.SquadId.Length > 0), "Every guard belongs to a squad: " +
                string.Join(", ", mission.Enemies.Where(e => e.SquadId.Length == 0).Select(e => e.name)));
            var squads = AgentRegistry.All.Select(a => a.SquadId).Distinct().ToArray();
            CollectionAssert.IsSubsetOf(new[] { "B", "C1", "C2", "C3" }, squads, "Jetty squad and the three camps.");
            Assert.IsNotNull(GameObject.Find("[AI Debug]"), "The debug console is added in the editor.");

            var guard = mission.Enemies.First(e => e.SquadId == "C1" && e.Alive);
            var contacts = new List<PlayerContactEvent>();
            var states = new List<EnemyStateChangedEvent>();
            System.Action<PlayerContactEvent> onContact = contacts.Add;
            System.Action<EnemyStateChangedEvent> onState = states.Add;
            EventBus.Subscribe(onContact);
            EventBus.Subscribe(onState);
            try
            {
                guard.DebugDamage(.1f);
                yield return null;
                yield return null;
                Assert.IsTrue(contacts.Any(c => c.AgentId == guard.LegacyId && !c.Seen), "Shot at: a contact report.");
                Assert.IsTrue(guard.Engaged);
                Assert.IsTrue(states.Any(s => s.AgentId == guard.LegacyId), "Its FSM left Patrol.");
                Assert.AreNotEqual(EnemyStateId.Patrol, guard.State);
            }
            finally
            {
                EventBus.Unsubscribe(onContact);
                EventBus.Unsubscribe(onState);
            }
        }
    }
}
