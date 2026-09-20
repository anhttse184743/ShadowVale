using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class ForestFlowTests
    {
        [UnityTest]
        public IEnumerator NonCombatRouteCompletesWithSuppliesAndEvidence()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map01_ForestFootprints.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<ForestMission>();
            var points = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None);
            mission.Interact(points.Single(p => p.id == "river_exit"));
            Assert.AreEqual(0, mission.Stage, "Exit must not skip the mission");
            mission.Interact(points.Single(p => p.id == "documents"));
            Assert.AreEqual(0, mission.Stage, "Evidence is gated by the first objective");
            mission.Interact(points.Single(p => p.id == "supplies"));
            Assert.AreEqual(1, mission.Stage);
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.position = new Vector3(0,0,31); controller.enabled = true;
            yield return null;
            Assert.AreEqual(2, mission.Stage);
            mission.Interact(points.Single(p => p.id == "documents"));
            Assert.AreEqual(3, mission.Stage);
            Assert.AreEqual(1, mission.Count("river_documents"));
            controller.enabled = false; mission.player.position = new Vector3(27,0,80); controller.enabled = true;
            mission.hung.GetComponent<NavMeshAgent>().Warp(new Vector3(26,0,79));
            mission.Interact(points.Single(p => p.id == "river_exit"));
            Assert.AreEqual(4, mission.Stage);
            Assert.IsFalse(mission.Alarmed, "Stealth completion must not force a battle");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator NoiseIsLocalAndLootCannotBeDuplicated()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map01_ForestFootprints.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<ForestMission>();
            var guards = Object.FindObjectsByType<ForestGuard>(FindObjectsSortMode.None);
            mission.EmitNoise(new Vector3(0,0,-52), 7);
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
