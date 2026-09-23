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
        public IEnumerator HudQuickUseAndSaveRestoreUseRealInventory()
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
                Assert.IsNotNull(Resources.Load<Texture2D>("Hud/Items"));
                Assert.IsNotNull(Resources.Load<Texture2D>("Hud/HealthFrame"));
                var stock = (Dictionary<string, int>)typeof(Map01Inventory).GetField("items", Private).GetValue(inventory);
                stock["ammo_rifle"] = 125; stock["medkit_small"] = 5; stock["cloth"] = 12;
                stock["herb"] = 8; stock["supplies"] = 1; stock["river_documents"] = 1;
                stock["ammo_sniper"] = 6; stock["scrap_metal"] = 4;
                CollectionAssert.AreEqual(new[] { 90, 35 }, inventory.InventoryStacks().Where(s => s.Id == "ammo_rifle").Select(s => s.Count));
                Assert.IsFalse(inventory.AssignQuickSlot(2, "ammo_rifle"));
                Assert.IsFalse(inventory.AssignQuickSlot(2, "river_documents"));
                Assert.IsFalse(inventory.AssignQuickSlot(9, "medkit_small"));
                Assert.IsTrue(inventory.AssignQuickSlot(2, "medkit_small"));
                Assert.AreEqual(5, inventory.Count("medkit_small"), "Assigning must not move stock.");
                Assert.IsFalse(inventory.UseQuickSlot(2), "Full health must not consume a bandage.");
                Assert.AreEqual(5, inventory.Count("medkit_small"));
                mission.Damage(50);
                float before = mission.PlayerHealth;
                Assert.IsTrue(inventory.UseQuickSlot(2));
                Assert.AreEqual(4, inventory.Count("medkit_small"));
                Assert.AreEqual(Mathf.Min(mission.Settings.playerHP, before + mission.Settings.medkitHeal), mission.PlayerHealth);
                Assert.IsFalse(inventory.UseQuickSlot(2), "The same frame must not consume twice.");
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
                Assert.IsFalse(inventory.UseQuickSlot(2), "Pause must block consumables.");
                Assert.IsTrue(inventory.AssignQuickSlot(4, "stone"));
                Assert.IsTrue(inventory.AssignQuickSlot(0, null));
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
                Assert.IsNull(inventory.QuickItem(0));
                Assert.AreEqual("medkit_small", inventory.QuickItem(2));
                Assert.AreEqual("stone", inventory.QuickItem(4));
                Assert.AreEqual(4, inventory.Count("medkit_small"));
                Assert.AreEqual(125, inventory.Count("ammo_rifle"));
                mission.SetInventoryOpen(true);
                {
                    // Release a dragged bandage over the third shortcut, then outside the bar.
                    inventory.AssignQuickSlot(2, null);
                    SetPrivate(hud, "dragItem", "medkit_small"); SetPrivate(hud, "dragging", true);
                    var release = new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(426, 804) };
                    typeof(Map01Hud).GetMethod("FinishHudDrag", Private).Invoke(hud, new object[] { release, 1600f, 900f });
                    Assert.AreEqual("medkit_small", inventory.QuickItem(2));
                    Assert.IsNull(typeof(Map01Hud).GetField("dragItem", Private).GetValue(hud));
                    SetPrivate(hud, "dragItem", "stone"); SetPrivate(hud, "dragging", true);
                    release = new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(700, 400) };
                    typeof(Map01Hud).GetMethod("FinishHudDrag", Private).Invoke(hud, new object[] { release, 1600f, 900f });
                    Assert.AreEqual("medkit_small", inventory.QuickItem(2), "Dropping outside a shortcut must not change its binding.");
                    Assert.AreEqual(4, inventory.Count("medkit_small"), "Dragging must not consume inventory.");
                }
                var testKeyboard = InputSystem.AddDevice<Keyboard>();
                try {
                    mission.SetInventoryOpen(true); inventory.SelectedItem = "medkit_small";
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Digit4)); InputSystem.Update();
                    inventory.HandleQuickKeys(testKeyboard);
                    Assert.AreEqual("medkit_small", inventory.QuickItem(3), "Number keys assign while the bag is open.");
                    Assert.AreEqual(4, inventory.Count("medkit_small"));
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState()); InputSystem.Update();
                    mission.SetInventoryOpen(false); SetPrivate(inventory, "nextQuickUse", 0f); mission.RestoreHealthFromSave(20f);
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Digit4)); InputSystem.Update();
                    inventory.HandleQuickKeys(testKeyboard);
                    Assert.AreEqual(3, inventory.Count("medkit_small"), "Number keys consume while playing.");
                } finally { InputSystem.RemoveDevice(testKeyboard); }
                // Also preserve default shortcuts when loading pre-HUD saves.
                inventory.RestoreQuickSlots(null);
                Assert.AreEqual("medkit_small", inventory.QuickItem(0));
                Assert.AreEqual("stone", inventory.QuickItem(1));

                // A real rendered screenshot with deliberately seeded QA inventory; no demo stock ships in gameplay.
                mission.SetPaused(true); mission.SetInventoryOpen(true);
                SetPreviewResolution(1600, 900);
                SetPrivate(hud, "selectedStack", 2); mission.Say(null, -1f); mission.RestoreHealthFromSave(75f);
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
