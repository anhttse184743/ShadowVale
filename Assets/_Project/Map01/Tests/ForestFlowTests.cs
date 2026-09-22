using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class ForestFlowTests : ForestSceneTestBase
    {
        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrDestroyedContentStopsInitializationWithoutGuardCascade(bool destroyed)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Mission initialization regression");
            var guardRoot = new GameObject("Guard initialization regression");
            var balance = new TextAsset("{}");
            // Edit Mode does not invoke these runtime lifecycle methods automatically.
            var mission = root.AddComponent<ForestMission>();
            var guard = guardRoot.AddComponent<ForestGuard>();
            try
            {
                mission.balanceJson = balance;
                if (destroyed)
                {
                    mission.contentBundle = new TextAsset("{}");
                    Object.DestroyImmediate(mission.contentBundle);
                }
                LogAssert.Expect(LogType.Error, "ForestMission could not initialize. Assign valid balanceJson and contentBundle TextAssets in the Inspector.");
                typeof(ForestMission).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(mission, null);
                Assert.IsFalse(mission.IsInitialized);
                Assert.IsFalse(mission.enabled);
                typeof(ForestGuard).GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(guard, null);
                Assert.IsFalse(guard.enabled);
                Assert.DoesNotThrow(() => guard.Hear(Vector3.zero, 10));
                Assert.DoesNotThrow(() => guard.Hit(10));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(guardRoot);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(balance);
            }
        }

        [UnityTest]
        public IEnumerator NonCombatRouteCompletesWithSuppliesAndEvidence()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<ForestMission>();
            Assert.IsTrue(mission.IsInitialized, "The serialized mission content must resolve before guards start.");
            var points = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None);
            mission.Interact(points.Single(p => p.id == "river_exit"));
            Assert.AreEqual(0, mission.Stage, "Exit must not skip the mission");
            mission.Interact(points.Single(p => p.id == "documents"));
            Assert.AreEqual(0, mission.Stage, "Evidence is gated by the first objective");
            mission.Interact(points.Single(p => p.id == "supplies"));
            Assert.AreEqual(1, mission.Stage);
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.position = mission.encounterExit.position; controller.enabled = true;
            yield return null;
            Assert.AreEqual(2, mission.Stage);
            mission.Interact(points.Single(p => p.id == "documents"));
            Assert.AreEqual(3, mission.Stage);
            Assert.AreEqual(1, mission.Count("river_documents"));
            var exitPosition = points.Single(p => p.id == "river_exit").transform.position;
            controller.enabled = false; mission.player.position = exitPosition; controller.enabled = true;
            mission.hung.GetComponent<NavMeshAgent>().Warp(exitPosition);
            mission.Interact(points.Single(p => p.id == "river_exit"));
            Assert.AreEqual(4, mission.Stage);
            Assert.IsFalse(mission.Alarmed, "Stealth completion must not force a battle");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator NoiseIsLocalAndLootCannotBeDuplicated()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<ForestMission>();
            var guards = Object.FindObjectsByType<ForestGuard>(FindObjectsSortMode.None);
            mission.EmitNoise(mission.player.position, 7);
            Assert.IsTrue(guards.All(g => g.state == ForestGuardState.Patrol));
            mission.EmitNoise(guards[0].transform.position, 8);
            Assert.AreEqual(ForestGuardState.Investigate, guards[0].state);
            var loot = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None).Single(p => p.id == "tutorial_loot");
            mission.Interact(loot); int cloth = mission.Count("cloth");
            mission.Interact(loot); Assert.AreEqual(cloth, mission.Count("cloth"));
            yield return new ExitPlayMode();
        }
    }
}
