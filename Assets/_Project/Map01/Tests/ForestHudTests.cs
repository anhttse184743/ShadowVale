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
        private static void Set(ForestMission mission, string field, object value) => typeof(ForestMission).GetField(field, Private).SetValue(mission, value);

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
                var mission = UnityEngine.Object.FindFirstObjectByType<ForestMission>();
                Assert.IsTrue(mission.IsInitialized);
                Assert.IsNotNull(Resources.Load<Texture2D>("Hud/Items"));
                Assert.IsNotNull(Resources.Load<Texture2D>("Hud/HealthFrame"));
                var stock = (Dictionary<string, int>)typeof(ForestMission).GetField("inventory", Private).GetValue(mission);
                stock["ammo_rifle"] = 125; stock["medkit_small"] = 5; stock["cloth"] = 12;
                stock["herb"] = 8; stock["supplies"] = 1; stock["river_documents"] = 1;
                stock["ammo_sniper"] = 6; stock["scrap_metal"] = 4;
                CollectionAssert.AreEqual(new[] { 90, 35 }, mission.InventoryStacks().Where(s => s.Id == "ammo_rifle").Select(s => s.Count));
                Assert.IsFalse(mission.AssignQuickSlot(2, "ammo_rifle"));
                Assert.IsFalse(mission.AssignQuickSlot(2, "river_documents"));
                Assert.IsFalse(mission.AssignQuickSlot(9, "medkit_small"));
                Assert.IsTrue(mission.AssignQuickSlot(2, "medkit_small"));
                Assert.AreEqual(5, mission.Count("medkit_small"), "Assigning must not move stock.");
                Assert.IsFalse(mission.UseQuickSlot(2), "Full health must not consume a bandage.");
                Assert.AreEqual(5, mission.Count("medkit_small"));
                mission.Damage(50);
                float before = mission.Health;
                Assert.IsTrue(mission.UseQuickSlot(2));
                Assert.AreEqual(4, mission.Count("medkit_small"));
                Assert.AreEqual(Mathf.Min(mission.Settings.playerHP, before + mission.Settings.medkitHeal), mission.Health);
                Assert.IsFalse(mission.UseQuickSlot(2), "The same frame must not consume twice.");
                Set(mission, "nextQuickUse", 0f);
                int stones = mission.Count("stone");
                mission.SetInventoryOpen(true);
                Assert.IsFalse(mission.UseItem("stone"), "Throwing must require a gameplay aim.");
                Assert.AreEqual(stones, mission.Count("stone"));
                Assert.IsTrue(mission.CloseGameplayPanel());
                Assert.IsFalse(mission.CloseGameplayPanel());
                Assert.IsTrue(mission.UseItem("stone"));
                Assert.AreEqual(stones - 1, mission.Count("stone"));
                mission.SetPaused(true);
                Set(mission, "nextQuickUse", 0f);
                Assert.IsFalse(mission.UseQuickSlot(2), "Pause must block consumables.");
                Assert.IsTrue(mission.AssignQuickSlot(4, "stone"));
                Assert.IsTrue(mission.AssignQuickSlot(0, null));
                Assert.IsTrue(mission.SaveSlot(1, out var error), error);
                ForestMission.BeginGame(1);
                yield return null;
                var pending = typeof(ForestMission).GetField("pendingCheckpoint", BindingFlags.Static | BindingFlags.NonPublic);
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (pending.GetValue(null) != null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsNull(pending.GetValue(null));
                mission = UnityEngine.Object.FindFirstObjectByType<ForestMission>();
                Assert.IsNull(mission.QuickItem(0));
                Assert.AreEqual("medkit_small", mission.QuickItem(2));
                Assert.AreEqual("stone", mission.QuickItem(4));
                Assert.AreEqual(4, mission.Count("medkit_small"));
                Assert.AreEqual(125, mission.Count("ammo_rifle"));
                mission.SetInventoryOpen(true);
                {
                    // Release a dragged bandage over the third shortcut, then outside the bar.
                    mission.AssignQuickSlot(2, null);
                    Set(mission, "dragItem", "medkit_small"); Set(mission, "dragging", true);
                    var release = new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(426, 804) };
                    typeof(ForestMission).GetMethod("FinishHudDrag", Private).Invoke(mission, new object[] { release, 1600f, 900f });
                    Assert.AreEqual("medkit_small", mission.QuickItem(2));
                    Assert.IsNull(typeof(ForestMission).GetField("dragItem", Private).GetValue(mission));
                    Set(mission, "dragItem", "stone"); Set(mission, "dragging", true);
                    release = new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(700, 400) };
                    typeof(ForestMission).GetMethod("FinishHudDrag", Private).Invoke(mission, new object[] { release, 1600f, 900f });
                    Assert.AreEqual("medkit_small", mission.QuickItem(2), "Dropping outside a shortcut must not change its binding.");
                    Assert.AreEqual(4, mission.Count("medkit_small"), "Dragging must not consume inventory.");
                }
                var testKeyboard = InputSystem.AddDevice<Keyboard>();
                try {
                    mission.SetInventoryOpen(true); Set(mission, "selectedItem", "medkit_small");
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Digit4)); InputSystem.Update();
                    typeof(ForestMission).GetMethod("HandleQuickKeys", Private).Invoke(mission, new object[] { testKeyboard });
                    Assert.AreEqual("medkit_small", mission.QuickItem(3), "Number keys assign while the bag is open.");
                    Assert.AreEqual(4, mission.Count("medkit_small"));
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState()); InputSystem.Update();
                    mission.SetInventoryOpen(false); Set(mission, "nextQuickUse", 0f); Set(mission, "hp", 20f);
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Digit4)); InputSystem.Update();
                    typeof(ForestMission).GetMethod("HandleQuickKeys", Private).Invoke(mission, new object[] { testKeyboard });
                    Assert.AreEqual(3, mission.Count("medkit_small"), "Number keys consume while playing.");
                } finally { InputSystem.RemoveDevice(testKeyboard); }
                // Also preserve default shortcuts when loading pre-HUD saves.
                typeof(ForestMission).GetMethod("RestoreQuickSlots", Private).Invoke(mission, new object[] { null });
                Assert.AreEqual("medkit_small", mission.QuickItem(0));
                Assert.AreEqual("stone", mission.QuickItem(1));

                // A real rendered screenshot with deliberately seeded QA inventory; no demo stock ships in gameplay.
                mission.SetPaused(true); mission.SetInventoryOpen(true);
                SetPreviewResolution(1600, 900);
                Set(mission, "selectedStack", 2); Set(mission, "dialogueUntil", 0f); Set(mission, "hp", 75f);
                Directory.CreateDirectory("Logs/HudPreview");
                for (int i = 0; i < 5; i++) yield return null;
                ScreenCapture.CaptureScreenshot("Logs/HudPreview/inventory.png");
                for (int i = 0; i < 10; i++) yield return null;
                stock = (Dictionary<string, int>)typeof(ForestMission).GetField("inventory", Private).GetValue(mission);
                stock["ammo_rifle"] = 2700;
                Assert.Greater(mission.InventoryStacks().Count, 24, "Overflow must remain accessible beyond the visible grid.");
                Set(mission, "hudScroll", 444f);
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
