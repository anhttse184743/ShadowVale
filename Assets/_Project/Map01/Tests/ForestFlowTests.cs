using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.UI.HUD;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class ForestFlowTests : ForestSceneTestBase
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
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
        public IEnumerator BriefingRouteRescuesHungClearsOutpostsAndDefeatsCommander()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<ForestMission>();
            Assert.IsTrue(mission.IsInitialized, "The serialized mission content must resolve before guards start.");
            Assert.AreEqual(0, mission.Stage);
            var points = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None);
            var controller = mission.player.GetComponent<CharacterController>();

            mission.Interact(points.Single(p => p.id == "supplies"));
            Assert.AreEqual(0, mission.Stage, "Reporting to base is gated until Hùng is treated.");

            var inventory = (Dictionary<string, int>)typeof(ForestMission).GetField("inventory", Private).GetValue(mission);
            inventory["herb"] = 1;
            controller.enabled = false; mission.player.position = mission.hung.position; controller.enabled = true;
            typeof(ForestMission).GetMethod("TryRescueHung", Private).Invoke(mission, null);
            Assert.AreEqual(1, mission.Stage, "Treating the wounded Hùng with herb must send Nam back to base.");
            Assert.AreEqual(0, mission.Count("herb"), "The herb must be spent on the treatment.");

            mission.Interact(points.Single(p => p.id == "supplies"));
            Assert.AreEqual(2, mission.Stage);
            Assert.AreEqual(1, mission.Count("supplies"));

            typeof(ForestMission).GetMethod("OnMapOpened", Private).Invoke(mission, null);
            Assert.AreEqual(3, mission.Stage, "Opening the map must clear the terrain-scouting objective.");

            var outposts = Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None)
                .Where(e => e.name.StartsWith("Outpost guard ")).ToArray();
            Assert.GreaterOrEqual(outposts.Length, 1, "Map 1's three built-in enemy outposts must still be present.");
            foreach (var guard in outposts) guard.GetComponent<Health>().TakeDamage(9999, guard.transform.position, null);
            yield return null;
            Assert.AreEqual(ForestMission.BossStage, mission.Stage, "Clearing every outpost must summon the commander.");

            var boss = Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None).Single(e => e.name == "Chỉ huy địch");
            Assert.IsTrue(boss.IsBoss);
            boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
            Assert.AreEqual(ForestMission.CompleteStage, mission.Stage, "Defeating the commander must complete Map 1.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator InteractKeyTreatsHungWhenInRangeElseFallsBackToNearbyPoint()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<ForestMission>();
            var loot = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None).Single(p => p.id == "tutorial_loot");

            // Stage 0, standing at a point that is not Hùng: [E] must still trigger it — before
            // this fix, stage 0 routed every [E] press into the rescue check alone, so the herb the
            // rescue itself needs could never be picked up.
            Assert.AreEqual(0, mission.Stage);
            typeof(ForestMission).GetField("nearby", Private).SetValue(mission, loot);
            typeof(ForestMission).GetMethod("HandleInteractKey", Private).Invoke(mission, null);
            Assert.Greater(mission.Count("herb"), 0, "Standing at a point other than Hùng must still interact with it.");

            // Standing at Hùng instead: [E] treats him rather than doing nothing.
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.position = mission.hung.position; controller.enabled = true;
            yield return null;
            typeof(ForestMission).GetMethod("HandleInteractKey", Private).Invoke(mission, null);
            Assert.AreEqual(1, mission.Stage, "[E] near Hùng must treat him, not silently do nothing.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator WeaponHotbarHidesOnlyItsOwnRowNotTheCrosshairCanvas()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null; yield return null;
            var hotbar = Object.FindFirstObjectByType<WeaponHotbar>();
            Assert.IsNotNull(hotbar, "Map 1's migrated HUD must include the weapon hotbar.");
            // HudBuilder always puts WeaponHotbar directly on the HUD canvas GameObject.
            var canvasRoot = hotbar.GetComponent<Canvas>().transform;
            Assert.IsNotNull(canvasRoot.Find("Crosshair"), "The HUD canvas must still have its crosshair.");
            var rootGroup = canvasRoot.GetComponent<CanvasGroup>();
            Assert.IsTrue(rootGroup == null || rootGroup.alpha > 0,
                "Hiding the redundant hotbar must not hide the whole HUD canvas (and the crosshair with it).");
            var rowGroup = (CanvasGroup)typeof(WeaponHotbar).GetField("hotbarGroup", Private).GetValue(hotbar);
            Assert.IsNotNull(rowGroup, "hotbarGroup must resolve, wired or by falling back to the row named \"Hotbar\".");
            Assert.AreEqual(0f, rowGroup.alpha, "The hotbar row itself is still hidden — Map 1's own HUD already covers it.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator NoiseIsLocalAndLootCannotBeDuplicated()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<ForestMission>();
            var guards = Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None);
            Assert.IsNotEmpty(guards);
            mission.EmitNoise(mission.player.position, 7);
            Assert.IsTrue(guards.All(g => !g.Alerted));
            mission.EmitNoise(guards[0].transform.position, 8);
            Assert.IsTrue(guards[0].Alerted);
            var loot = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None).Single(p => p.id == "tutorial_loot");
            mission.Interact(loot); int cloth = mission.Count("cloth");
            mission.Interact(loot); Assert.AreEqual(cloth, mission.Count("cloth"));
            yield return new ExitPlayMode();
        }
    }
}
