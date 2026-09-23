using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShadowVale.Map01.Tests
{
    public sealed class ForestHudTests : ForestSceneTestBase
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void SetPrivate(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);

        private static void SetPreviewResolution(int width, int height)
        {
            var assembly = typeof(Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { Enum.Parse(modeType, "FixedResolution"), (object)width, height, "HUD validation" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, count - 1);
            view.Repaint();
        }

        [Test]
        public void SplittingPreservesAllStockAndOmitsEmptyItems()
        {
            var source = new Dictionary<string, int> { { "ammo_rifle", 125 }, { "medkit_small", 12 }, { "cloth", 0 } };
            var stacks = ForestInventory.Split(source, id => id == "ammo_rifle" ? 90 : 5);
            CollectionAssert.AreEqual(new[] { 90, 35 }, stacks.Where(s => s.Id == "ammo_rifle").Select(s => s.Count));
            CollectionAssert.AreEqual(new[] { 5, 5, 2 }, stacks.Where(s => s.Id == "medkit_small").Select(s => s.Count));
            Assert.AreEqual(137, stacks.Sum(s => s.Count));
            Assert.IsFalse(stacks.Any(s => s.Id == "cloth"));
        }

        [UnityTest]
        public IEnumerator ObjectiveGuidePreviewScreenshots()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var guide = mission.GetComponent<Map01ObjectiveGuide>();
            var controller = mission.player.GetComponent<CharacterController>();
            var cameraRig = mission.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>();
            foreach (var e in mission.Enemies) e.enabled = false; // A clean shot, not a firefight.
            SetPreviewResolution(1600, 900);
            Directory.CreateDirectory("Logs/GuidePreview");

            // Nam spawns on the base's floor — a known NavMesh spot to station Hùng for the report shot
            // (right at the supply point the floor is carved out under furniture).
            Vector3 baseFloor = mission.player.position;
            // The escort is shot from Hùng's side looking home (the base is where Nam starts); the
            // outposts from ~20 m out of the base; the report walking back to Hùng at the base.
            foreach (var (stage, file, along) in new[] {
                (Map01Quest.EscortStage, "escort-base", 3), (Map01Quest.CampsStage, "camps-outpost", 20), (Map01Quest.ReportCampsStage, "report-hung", 4) })
            {
                if (stage == Map01Quest.EscortStage) { controller.enabled = false; mission.player.position = mission.hung.position + Vector3.right * 2; controller.enabled = true; }
                if (stage == Map01Quest.ReportCampsStage) mission.hung.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(baseFloor);
                quest.RestoreStage(stage);
                mission.Say(null, -1f);
                for (float until = Time.time + .7f; Time.time < until;) yield return null;
                Assert.Greater(guide.Route.Count, along + 8, "Need a route long enough to look down.");
                // Walk Nam a little down the route and look along the next stretch of it.
                Vector3 stand = guide.Route[along], ahead = guide.Route[along + 8];
                float yaw = Mathf.Atan2(ahead.x - stand.x, ahead.z - stand.z) * Mathf.Rad2Deg;
                controller.enabled = false; mission.player.SetPositionAndRotation(stand, Quaternion.Euler(0, yaw, 0)); controller.enabled = true;
                typeof(ShadowVale.Gameplay.Player.ThirdPersonCamera).GetField("_yaw", Private).SetValue(cameraRig, yaw);
                for (float until = Time.time + .7f; Time.time < until;) yield return null;
                Assert.IsTrue(guide.HasTarget, "The compass must be on screen for the preview.");
                ScreenCapture.CaptureScreenshot($"Logs/GuidePreview/{file}.png");
                for (int i = 0; i < 10; i++) yield return null;
            }
            yield return new ExitPlayMode();
        }

        /// <summary>Press and release one key through the real input path; Update-driven readers
        /// see it on the frame after it is queued.</summary>
        private static IEnumerator Press(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator ItemsUseTheirOwnKeysWithNoShortcutBar()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            foreach (var e in mission.Enemies) e.enabled = false;
            var routing = RouteInputToGame();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Assert.IsTrue(inventory.IsEquipped("rifle_standard"), "PlayerCombat's starting weapon is the rifle.");
                // Weapons: 6 rifle, 7 knife — the keys the combat HUD shows next to them.
                yield return Press(keyboard, Key.Digit7);
                Assert.IsTrue(inventory.IsEquipped("knife"), "7 draws the knife.");
                // 1–5 were the removed shortcut bar; they must not quietly do anything any more.
                yield return Press(keyboard, Key.Digit1);
                Assert.IsTrue(inventory.IsEquipped("knife"), "1 no longer switches to the rifle.");
                yield return Press(keyboard, Key.Digit6);
                Assert.IsTrue(inventory.IsEquipped("rifle_standard"), "6 draws the rifle.");

                // Bandages: only H.
                inventory.Add("medkit_small", 2);
                mission.Damage(40);
                yield return Press(keyboard, Key.Digit3);
                Assert.AreEqual(2, inventory.Count("medkit_small"), "3 no longer uses a bandage.");
                yield return Press(keyboard, Key.H);
                Assert.AreEqual(1, inventory.Count("medkit_small"), "H uses a bandage.");
            }
            finally { InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HudItemUseAndSaveRestoreUseRealInventory()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var rootField = typeof(ForestSaveSlots).GetField("storageRoot", BindingFlags.Static | BindingFlags.NonPublic);
            string root = Path.Combine(Application.temporaryCachePath, "hud-test-" + Guid.NewGuid().ToString("N"));
            rootField.SetValue(null, root);
            try {
                var mission = UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
                var inventory = mission.GetComponent<Map01Inventory>();
                var hud = mission.GetComponent<Map01Hud>();
                var saveSystem = mission.GetComponent<Map01SaveSystem>();
                Assert.IsTrue(mission.IsInitialized);
                Assert.AreEqual(1, inventory.Count("rifle_standard"));
                Assert.AreEqual(1, inventory.Count("knife"));
                Assert.AreEqual(0, inventory.Count("ammo_rifle"), "Ammo must come from loot, not a free starting stock.");
                Assert.AreEqual(0, inventory.Count("herb"), "Herb must come from loot, not a free starting stock.");
                Assert.IsNotNull(Resources.Load<Texture2D>("Hud/StartingWeapons"));
                Assert.IsTrue(inventory.EquipItem("knife"));
                Assert.IsTrue(inventory.IsEquipped("knife"));
                Assert.IsTrue(inventory.EquipItem("rifle_standard"));
                Assert.IsTrue(inventory.IsEquipped("rifle_standard"));
                Assert.AreEqual(1, inventory.Count("knife"), "Equipping does not consume the weapon.");
                Assert.IsNotNull(Resources.Load<Texture2D>("Hud/Items"));
                Assert.IsNotNull(Resources.Load<Texture2D>("Hud/HealthFrame"));
                var stock = (Dictionary<string, int>)typeof(Map01Inventory).GetField("items", Private).GetValue(inventory);
                stock["ammo_rifle"] = 125; stock["medkit_small"] = 5; stock["cloth"] = 12;
                stock["herb"] = 8; stock["supplies"] = 1; stock["river_documents"] = 1;
                stock["ammo_sniper"] = 6; stock["scrap_metal"] = 4;
                CollectionAssert.AreEqual(new[] { 90, 35 }, inventory.InventoryStacks().Where(s => s.Id == "ammo_rifle").Select(s => s.Count));
                Assert.IsFalse(inventory.UseItem("ammo_rifle"), "Ammo is not used directly.");
                Assert.IsFalse(inventory.UseItem("river_documents"));
                Assert.IsFalse(inventory.UseItem("medkit_small"), "Full health must not consume a bandage.");
                Assert.AreEqual(5, inventory.Count("medkit_small"));
                mission.Damage(50);
                float before = mission.PlayerHealth;
                Assert.IsTrue(inventory.UseItem("medkit_small"));
                Assert.AreEqual(4, inventory.Count("medkit_small"));
                Assert.AreEqual(Mathf.Min(mission.Settings.playerHP, before + mission.Settings.medkitHeal), mission.PlayerHealth);
                Assert.IsFalse(inventory.UseItem("medkit_small"), "The same frame must not consume twice.");
                SetPrivate(inventory, "nextQuickUse", 0f);
                int stones = inventory.Count("stone");
                mission.SetInventoryOpen(true);
                Assert.IsFalse(inventory.UseItem("stone"), "Throwing must require closing the gameplay panel first.");
                Assert.AreEqual(stones, inventory.Count("stone"));
                Assert.IsTrue(mission.CloseGameplayPanel());
                Assert.IsFalse(mission.CloseGameplayPanel());
                Assert.IsTrue(inventory.UseItem("stone", mission.player.position + Vector3.forward * 3f));
                Assert.AreEqual(stones - 1, inventory.Count("stone"));
                mission.SetPaused(true);
                SetPrivate(inventory, "nextQuickUse", 0f);
                Assert.IsFalse(inventory.UseItem("medkit_small"), "Pause must block consumables.");
                Assert.IsTrue(saveSystem.SaveSlot(1, out var error), error);
                Map01SaveSystem.BeginGame(1);
                yield return null;
                var pending = typeof(Map01SaveSystem).GetField("pendingCheckpoint", BindingFlags.Static | BindingFlags.NonPublic);
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (pending.GetValue(null) != null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsNull(pending.GetValue(null));
                mission = UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
                inventory = mission.GetComponent<Map01Inventory>();
                hud = mission.GetComponent<Map01Hud>();
                Assert.AreEqual(4, inventory.Count("medkit_small"));
                Assert.AreEqual(125, inventory.Count("ammo_rifle"));
                Assert.AreEqual(1, inventory.Count("rifle_standard"));
                Assert.AreEqual(1, inventory.Count("knife"));

                // A real rendered screenshot with deliberately seeded QA inventory; no demo stock ships in gameplay.
                mission.SetPaused(true); mission.SetInventoryOpen(true);
                SetPreviewResolution(1600, 900);
                SetPrivate(hud, "selectedStack", 0); mission.Say(null, -1f); mission.RestoreHealthFromSave(75f);
                Directory.CreateDirectory("Logs/HudPreview");
                for (int i = 0; i < 5; i++) yield return null;
                ScreenCapture.CaptureScreenshot("Logs/HudPreview/inventory.png");
                for (int i = 0; i < 10; i++) yield return null;
                stock = (Dictionary<string, int>)typeof(Map01Inventory).GetField("items", Private).GetValue(inventory);
                stock["ammo_rifle"] = 2700;
                Assert.Greater(inventory.InventoryStacks().Count, 24, "Overflow must remain accessible beyond the visible grid.");
                SetPrivate(hud, "hudScroll", 444f);
                for (int i = 0; i < 3; i++) yield return null;
                ScreenCapture.CaptureScreenshot("Logs/HudPreview/scrolled.png");
                for (int i = 0; i < 10; i++) yield return null;
                mission.SetInventoryOpen(false);
                stock["ammo_rifle"] = 125;
                for (int i = 0; i < 3; i++) yield return null;
                ScreenCapture.CaptureScreenshot("Logs/HudPreview/gameplay.png");
                for (int i = 0; i < 10; i++) yield return null;
                mission.SetInventoryOpen(true);
                SetPreviewResolution(1280, 720);
                for (int i = 0; i < 5; i++) yield return null;
                ScreenCapture.CaptureScreenshot("Logs/HudPreview/inventory-720p.png");
                for (int i = 0; i < 10; i++) yield return null;
            } finally {
                rootField.SetValue(null, null); Time.timeScale = 1;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            yield return new ExitPlayMode();
        }
    }
}
