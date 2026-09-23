using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.UI.HUD;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
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
            var balance = new TextAsset("{}");
            // Edit Mode does not invoke these runtime lifecycle methods automatically.
            var mission = root.AddComponent<Map01Mission>();
            try
            {
                mission.balanceJson = balance;
                if (destroyed)
                {
                    mission.contentBundle = new TextAsset("{}");
                    Object.DestroyImmediate(mission.contentBundle);
                }
                LogAssert.Expect(LogType.Error, "Map01Mission could not initialize. Assign valid balanceJson and contentBundle TextAssets in the Inspector.");
                typeof(Map01Mission).GetMethod("Awake", Private).Invoke(mission, null);
                Assert.IsFalse(mission.IsInitialized);
                Assert.IsFalse(mission.enabled);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
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
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            Assert.IsTrue(mission.IsInitialized, "The serialized mission content must resolve before guards start.");
            Assert.AreEqual(0, quest.Stage);
            var points = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None);
            var controller = mission.player.GetComponent<CharacterController>();

            interaction.Interact(points.Single(p => p.id == "supplies"));
            Assert.AreEqual(0, quest.Stage, "Reporting to base is gated until Hùng is treated.");

            inventory.Add("herb", 1);
            controller.enabled = false; mission.player.position = mission.hung.position; controller.enabled = true;
            yield return null;
            quest.TryRescueHung();
            Assert.AreEqual(1, quest.Stage, "Treating the wounded Hùng with herb must send Nam back to base.");
            Assert.AreEqual(0, inventory.Count("herb"), "The herb must be spent on the treatment.");

            interaction.Interact(points.Single(p => p.id == "supplies"));
            Assert.AreEqual(2, quest.Stage);
            Assert.AreEqual(1, inventory.Count("supplies"));

            quest.OnMapOpened();
            Assert.AreEqual(3, quest.Stage, "Opening the map must clear the terrain-scouting objective.");

            var outposts = Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None)
                .Where(e => e.name.StartsWith("Outpost guard ")).ToArray();
            Assert.GreaterOrEqual(outposts.Length, 1, "Map 1's three built-in enemy outposts must still be present.");
            foreach (var guard in outposts) guard.GetComponent<Health>().TakeDamage(9999, guard.transform.position, null);
            yield return null;
            Assert.AreEqual(Map01Quest.BossStage, quest.Stage, "Clearing every outpost must summon the commander.");

            var boss = Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None).Single(e => e.name == "Chỉ huy địch");
            Assert.IsTrue(boss.IsBoss);
            boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
            Assert.AreEqual(Map01Quest.CompleteStage, quest.Stage, "Defeating the commander must complete Map 1.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator InteractKeyTreatsHungWhenInRangeElseFallsBackToNearbyPoint()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var loot = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None).Single(p => p.id == "tutorial_loot");
            var controller = mission.player.GetComponent<CharacterController>();

            // Map01PlayerInteraction.Update() (which computes Nearby) bails out with no Keyboard
            // device present, and batchmode Play Mode tests do not attach a real one.
            var testKeyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                // Stage 0, standing at a point that is not Hùng: [E] must still trigger it — before
                // this fix, stage 0 routed every [E] press into the rescue check alone, so the herb
                // the rescue itself needs could never be picked up.
                Assert.AreEqual(0, quest.Stage);
                controller.enabled = false; mission.player.position = loot.transform.position; controller.enabled = true;
                yield return null;
                typeof(Map01PlayerInteraction).GetMethod("HandleInteractKey", Private).Invoke(interaction, null);
                Assert.Greater(inventory.Count("herb"), 0, "Standing at a point other than Hùng must still interact with it.");

                // Standing at Hùng instead: [E] treats him rather than doing nothing.
                controller.enabled = false; mission.player.position = mission.hung.position; controller.enabled = true;
                yield return null;
                typeof(Map01PlayerInteraction).GetMethod("HandleInteractKey", Private).Invoke(interaction, null);
                Assert.AreEqual(1, quest.Stage, "[E] near Hùng must treat him, not silently do nothing.");
            }
            finally { InputSystem.RemoveDevice(testKeyboard); }
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
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var guards = Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None);
            Assert.IsNotEmpty(guards);
            mission.EmitNoise(mission.player.position, 7);
            Assert.IsTrue(guards.All(g => !g.Alerted));
            mission.EmitNoise(guards[0].transform.position, 8);
            Assert.IsTrue(guards[0].Alerted);
            var loot = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None).Single(p => p.id == "tutorial_loot");
            interaction.Interact(loot); int cloth = inventory.Count("cloth");
            interaction.Interact(loot); Assert.AreEqual(cloth, inventory.Count("cloth"));
            yield return new ExitPlayMode();
        }
    }
}
