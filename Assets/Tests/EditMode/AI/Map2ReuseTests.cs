using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.AI.Tactical;
using ShadowVale.Content;
using ShadowVale.Core.Events;
using ShadowVale.Core.Services;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Tests.EditMode.AI
{
    /// <summary>
    /// M10 (reuse across maps): Map 2 — a different map with no mission code at all — runs the shared
    /// AI as it is: its tactical graph baked by the same tool, a squad of generic
    /// <see cref="EnemyAgent"/>s from the content bundle's grunt archetype, the same noise system and
    /// squad coordinator. Nothing of Map 1 is involved.
    /// </summary>
    public class Map2ReuseTests
    {
        [UnityTest]
        public IEnumerator TheSharedAiRunsASquadOnMapTwoWithoutAnyMapOneCode()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
            // The project's menu tooling points Play back at Boot whenever a scene opens; this test plays Map 2 itself.
            var start = EditorSceneManager.playModeStartScene;
            EditorSceneManager.playModeStartScene = null;
            yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
            EditorSceneManager.playModeStartScene = start;
        }

        private static IEnumerator InPlayMode()
        {
            yield return null;
            var host = new GameObject("Map 2 AI (test)");
            var noise = new NoiseSystem();
            ServiceLocator.Register(noise);
            TacticalGraphHost.Attach(host, ~0, null);
            SquadCoordinator.Attach(host, CoordinationSettings.ForScene("Map 2"));
            GameObject player = null;
            var plans = new List<PlanAppliedEvent>();
            var contacts = new List<PlayerContactEvent>();
            Action<PlanAppliedEvent> onPlan = plans.Add;
            Action<PlayerContactEvent> onContact = contacts.Add;
            EventBus.Subscribe(onPlan); EventBus.Subscribe(onContact);
            try
            {
                yield return null;
                yield return null;
                Assert.IsTrue(ServiceLocator.TryResolve<IWorldQuery>(out var graph), "Map 2 has a baked tactical graph (Resources/TacticalGraphs/Map 2).");
                Assert.Greater(graph.NodeCount, 50);
                Assert.Greater(graph.CoverNodes.Count, 0, "The sandbag positions and houses give cover nodes.");
                foreach (var name in new[] { "Map01Mission", "Map01EnemyController" })
                    Assert.IsFalse(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(m => m.GetType().Name == name),
                        name + " has no business on Map 2.");

                // A squad of three at a cover node, the player 12–20 m away.
                int post = graph.CoverNodes.OrderBy(c => c).First(c => Nodes(graph).Count(n => Flat(graph.NodePosition(n) - graph.NodePosition(c)) < 8f) >= 3
                                                                      && Nodes(graph).Any(n => Range(graph, c, n, 12f, 20f)));
                Vector3 centre = graph.NodePosition(post);
                Vector3 playerAt = graph.NodePosition(Nodes(graph).First(n => Range(graph, post, n, 12f, 20f)));

                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Test player";
                player.transform.position = playerAt + Vector3.up;
                var nam = player.AddComponent<Health>();
                nam.SetMaxHealth(10000);
                ServiceLocator.Register<IPlayerObservable>(PlayerStealthAdapter.For(player));

                Assert.IsTrue(new FallbackBundleProvider().TryLoad(out var bundle, out var error), error);
                var grunt = bundle.EnemyArchetypes.First(e => e.Id == "grunt");
                var weapon = bundle.Weapons.FirstOrDefault(w => w.Id == grunt.WeaponId);
                var squad = new List<EnemyAgent>();
                for (int i = 0; i < 3; i++)
                {
                    Vector3 at = centre + Quaternion.Euler(0, i * 120f, 0) * Vector3.forward * 2.5f;
                    if (NavMesh.SamplePosition(at, out var hit, 4f, NavMesh.AllAreas)) at = hit.position;
                    var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.name = "village_guard_" + i;
                    go.transform.position = at;
                    squad.Add(go.AddComponent<EnemyAgent>().Configure(grunt, "V1", weapon));
                }
                yield return null;
                Assert.AreEqual(3, AgentRegistry.All.Count(a => a.SquadId == "V1"), "Registered for the coordinator.");

                // Hearing: a stone next to one guard sends him to look.
                int heard = noise.Emit(squad[2].Position + Vector3.right * 3f, 8f, NoiseKind.Stone);
                Assert.GreaterOrEqual(heard, 1);
                yield return null;
                Assert.AreEqual(EnemyStateId.Investigate, squad[2].State);

                // Shot at: contact, the squad is alerted, one plan places all three on distinct nodes.
                squad[0].DebugDamage(.1f);
                for (float until = Time.time + 6f; plans.Count == 0 && Time.time < until;) yield return null;
                Assert.IsTrue(contacts.Any(c => c.AgentId == squad[0].Id), "Contact reported by the generic agent.");
                var plan = plans.FirstOrDefault(p => p.Group.Split('+').Contains("V1"));
                Assert.IsNotNull(plan.Assignments, "The squad coordinator planned Map 2's squad.");
                Assert.AreEqual(3, plan.Assignments.Length);
                Assert.AreEqual(3, plan.Assignments.Select(a => a.Node).Distinct().Count(), "One node per guard.");
                Assert.IsNull(plan.FallbackReason);
                yield return null;
                foreach (var g in squad)
                {
                    Assert.IsTrue(g.Brain.Memory.Engaged(Time.time), g.Id + " fights.");
                    Assert.AreNotEqual(EnemyStateId.Patrol, g.State);
                }
            }
            finally
            {
                EventBus.Unsubscribe(onPlan); EventBus.Unsubscribe(onContact);
                ServiceLocator.Unregister<NoiseSystem>();
                ServiceLocator.Unregister<IPlayerObservable>();
            }
        }

        private static IEnumerable<int> Nodes(IWorldQuery g) => Enumerable.Range(0, g.NodeCount);

        private static bool Range(IWorldQuery g, int a, int b, float min, float max)
        {
            float d = Flat(g.NodePosition(a) - g.NodePosition(b));
            return d >= min && d <= max;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
