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
        public IEnumerator BackquoteTogglesTheObjectivePanelAndAutomaticPreviewExpires()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            for (int i = 0; i < 6; i++) yield return null;
            var mission = UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
            var hud = mission.GetComponent<Map01Hud>();
            foreach (var e in mission.Enemies) e.enabled = false;
            SetPreviewResolution(1600, 900); // A Game View to take the screenshots from.
            mission.Say(null, -1f);
            var routing = RouteInputToGame();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Assert.IsTrue(hud.ObjectiveShown, "The objective is on show from the start.");
                Directory.CreateDirectory("Logs/HudPreview");
                ScreenCapture.CaptureScreenshot("Logs/HudPreview/objective-shown.png");
                for (int i = 0; i < 4; i++) yield return null;
                yield return Press(keyboard, Key.Backquote);
                Assert.IsFalse(hud.ObjectiveShown, "Backquote tucks the objective panel away.");
                ScreenCapture.CaptureScreenshot("Logs/HudPreview/objective-hidden.png");
                for (int i = 0; i < 4; i++) yield return null;
                yield return Press(keyboard, Key.Backquote);
                Assert.IsTrue(hud.ObjectiveShown, "Backquote brings it back.");
                // Keep story presentation from pausing this gameplay timer check.
                mission.Cinematic = false;
                Assert.AreEqual(5f, (float)typeof(Map01Hud).GetField("objectiveSecondsLeft", Private).GetValue(hud), .1f);
                yield return WaitGameSeconds(5.2f);
                Assert.IsFalse(hud.ObjectiveShown, "Manually reopened objectives also hide after five seconds.");
                yield return Press(keyboard, Key.Backquote);
                Assert.IsTrue(hud.ObjectiveShown, "Backquote reopens the timed preview.");
            }
            finally { InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SprintCostsStaminaOnlyWhileRunning()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
            foreach (var e in mission.Enemies) e.enabled = false;
            var routing = RouteInputToGame();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return Press(keyboard, Key.LeftShift); // Sprint is a toggle.
                Assert.IsTrue(mission.ModernPlayer.IsSprinting);
                float full = mission.Stamina;
                yield return WaitGameSeconds(1.5f);
                Assert.AreEqual(full, mission.Stamina, .01f, "Standing still with sprint on costs no stamina.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); // Held down.
                yield return WaitGameSeconds(2f);
                Assert.IsTrue(mission.ModernPlayer.IsSprinting && mission.ModernPlayer.IsMoving, "Nam is running.");
                float spent = full - mission.Stamina;
                Assert.Greater(spent, mission.Settings.staminaDrain * .5f, "Running costs stamina.");
                Assert.LessOrEqual(spent, mission.Settings.staminaDrain * 2f + 1f, "No faster than the drain rate.");
                Assert.Greater(mission.Stamina, full * .6f, "Two seconds of running leaves most of the stamina.");
            }
            finally { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
            yield return new ExitPlayMode();
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
                // The approved loadout: 1 long gun, 2 pistol, 3 knife, 4 throwable. No bare hands any more.
                yield return Press(keyboard, Key.Digit3);
                Assert.IsTrue(inventory.IsEquipped("knife"), "3 draws the knife.");
                yield return Press(keyboard, Key.Digit2);
                Assert.IsTrue(inventory.IsEquipped("knife"), "2 with no pistol in its slot leaves the knife in hand.");
                // 6–8 were the weapon keys while 1–5 belonged to the removed shortcut bar.
                yield return Press(keyboard, Key.Digit6);
                Assert.IsTrue(inventory.IsEquipped("knife"), "6 no longer switches to the rifle.");
                yield return Press(keyboard, Key.Digit1);
                Assert.IsTrue(inventory.IsEquipped("rifle_standard"), "1 draws the rifle.");
                yield return Press(keyboard, Key.Digit1);
                Assert.IsTrue(inventory.IsEquipped("rifle_standard"), "Pressing the key of what is in hand puts nothing away: there are no bare hands.");
                Assert.AreNotEqual(ShadowVale.Gameplay.Combat.WeaponKind.Unarmed, mission.ModernCombat.EquippedKind);

                // Bandages: only H. 4 selects stones instead.
                inventory.Add("medkit_small", 2);
                inventory.Add("smoke_bomb", 1);
                mission.Damage(40);
                yield return Press(keyboard, Key.Digit4);
                Assert.AreEqual(2, inventory.Count("medkit_small"), "4 no longer uses a bandage.");
                Assert.AreEqual("stone", inventory.Throwable, "4 selects stones even when smoke bombs are available.");
                Assert.IsTrue(inventory.ThrowableEquipped);
                yield return Press(keyboard, Key.Digit4);
                Assert.AreEqual("smoke_bomb", inventory.Throwable, "Pressing 4 again selects the next available throwable.");
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
                foreach (var (id, n) in new[] { ("ammo_rifle", 125), ("medkit_small", 5), ("cloth", 12), ("herb", 8), ("supplies", 1), ("river_documents", 1), ("ammo_sniper", 6), ("scrap_metal", 4) })
                    inventory.Add(id, n);
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
                // Saving is deliberately blocked during the real throw animation. Let it finish
                // before pausing; otherwise this test freezes the action and can never save.
                var actions = mission.player.GetComponent<Map01NamActions>();
                double actionDeadline = Time.realtimeSinceStartupAsDouble + 5;
                while (actions != null && actions.Busy && Time.realtimeSinceStartupAsDouble < actionDeadline) yield return null;
                Assert.IsFalse(actions != null && actions.Busy);
                mission.SetPaused(true);
                SetPrivate(inventory, "nextQuickUse", 0f);
                Assert.IsFalse(inventory.UseItem("medkit_small"), "Pause must block consumables.");
                // The bandage take is still playing (Map01NamActions.Busy): no save mid-action. Let it finish first.
                mission.SetPaused(false);
                double actionEnd = Time.realtimeSinceStartupAsDouble + 8;
                while (mission.GetComponent<Map01Rescue>().NonSaveable && Time.realtimeSinceStartupAsDouble < actionEnd) yield return null;
                mission.SetPaused(true);
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
                // The bag is 24 cells; what it has no room for is put on the ground, not lost.
                inventory.Receive("ammo_rifle", 2700);
                Assert.LessOrEqual(inventory.InventoryStacks().Count, Map01Inventory.BagSize, "The bag never holds more than its 24 cells.");
                Assert.Greater(Map01Drops.For(mission).Piles.Count(), 0, "What did not fit lies at Nam's feet.");
                for (int i = 0; i < 3; i++) yield return null;
                ScreenCapture.CaptureScreenshot("Logs/HudPreview/scrolled.png");
                for (int i = 0; i < 10; i++) yield return null;
                mission.SetInventoryOpen(false);
                inventory.Spend("ammo_rifle", inventory.Count("ammo_rifle") - 125);
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
