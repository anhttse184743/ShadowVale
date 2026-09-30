using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Contracts;
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
