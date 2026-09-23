using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;

namespace ShadowVale.Map01.Tests
{
    public sealed class ForestMenuIntegrationTests : ForestSceneTestBase
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static void Call(ForestMenu menu, string method, params object[] args) => typeof(ForestMenu).GetMethod(method, Private).Invoke(menu, args);

        [UnityTest]
        public IEnumerator PlayWhileEditingMapStartsAtMenuAndNewGameStillWorks()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            Assert.IsTrue(UnityEditor.EditorApplication.ExecuteMenuItem("ShadowVale/Menu/Luôn bắt đầu từ menu chính"));
            Assert.AreEqual("Assets/_Project/Scenes/00_Boot.unity", UnityEditor.AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            yield return new EnterPlayMode();
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (SceneManager.GetActiveScene().name != "01_MainMenu" && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreEqual("01_MainMenu", SceneManager.GetActiveScene().name);
            Assert.IsTrue(ForestMenu.Visible);
            Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<Map01Mission>(), "No gameplay should run behind the title on startup.");
            var menu = UnityEngine.Object.FindFirstObjectByType<ForestMenu>();
            Call(menu, "MainAction", 1);
            yield return null;
            Assert.IsTrue((bool)typeof(ForestMenu).GetField("playingIntro", Private).GetValue(menu),
                "Chơi mới must play the briefing cutscene before Map 1 loads.");
            Assert.AreEqual("01_MainMenu", SceneManager.GetActiveScene().name, "The cutscene must hold the scene switch, not race it.");
            // "Chơi mới" plays the briefing cutscene first; skip it the same way Esc/Enter would.
            Call(menu, "EndIntro");
            // Map 1 loads synchronously and the screen freezes on this frame for seconds — it must
            // be drawn as a loading screen, not the title (which read as "skipping sent me back").
            Assert.IsTrue((bool)typeof(ForestMenu).GetField("loading", Private).GetValue(menu));
            yield return null;
            Assert.AreEqual("Map 1", SceneManager.GetActiveScene().name);
            Assert.IsFalse(ForestMenu.Visible);
            Assert.IsFalse((bool)typeof(ForestMenu).GetField("loading", Private).GetValue(menu), "Arriving in the map clears the loading screen.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<Map01Mission>());
            yield return new ExitPlayMode();
            Assert.AreEqual("Map 1", SceneManager.GetActiveScene().name, "Stopping Play must restore the scene being edited.");
        }

        [UnityTest]
        public IEnumerator SkippingTheIntroWithEscLandsInTheMapNotAMenu()
        {
            yield return new EnterPlayMode();
            var routing = RouteInputToGame();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                SceneManager.LoadScene("01_MainMenu");
                yield return null; yield return null;
                yield return new WaitForSecondsRealtime(.6f); // Past the title's own key grace.
                var menu = UnityEngine.Object.FindFirstObjectByType<ForestMenu>();
                Call(menu, "MainAction", 1);
                yield return null;
                Assert.IsTrue((bool)typeof(ForestMenu).GetField("playingIntro", Private).GetValue(menu));

                // A real Esc press skips the intro (queued, so ForestMenu.Update reads it next frame).
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (SceneManager.GetActiveScene().name != "Map 1" && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.AreEqual("Map 1", SceneManager.GetActiveScene().name, "Esc during the intro must start Map 1.");

                // An impatient second Esc, the kind pressed during the multi-second load freeze,
                // lands right after arrival — it must not open the pause menu.
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                var mission = UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
                Assert.IsFalse(ForestMenu.Visible, "Skipping the intro must land in the map, not a menu.");
                Assert.IsFalse(mission.Paused);

                // Once settled in, Esc still pauses as normal.
                yield return new WaitForSecondsRealtime(.6f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.IsTrue(ForestMenu.Visible, "Esc must still open the pause menu during play.");
                Assert.IsTrue(mission.Paused);
            }
            finally { InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); Time.timeScale = 1; }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator UnsavedSceneShowsMenuInsteadOfSkybox()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            Assert.IsTrue(ForestMenu.Visible, "An unsaved scene must offer the main menu.");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None).Length);
            ForestMenu.EnsureCreated();
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None).Length);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DirectPlayFromTitleShowsExactlyOneMenu()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            yield return null;
            Assert.IsTrue(ForestMenu.Visible);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None).Length);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DirectPlayFromBootReachesTitle()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/00_Boot.unity");
            yield return new EnterPlayMode();
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual("01_MainMenu", SceneManager.GetActiveScene().name);
            Assert.IsTrue(ForestMenu.Visible);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None).Length);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator MenuStartsPausesSavesAndRestoresMap()
        {
            yield return new EnterPlayMode();
            // Isolate integration saves from the player's actual persistent data.
            string root = Path.Combine(Application.temporaryCachePath, "menu-test-" + Guid.NewGuid().ToString("N"));
            var rootField = typeof(ForestSaveSlots).GetField("storageRoot", BindingFlags.NonPublic | BindingFlags.Static);
            rootField.SetValue(null, root);
            try {
                SceneManager.LoadScene("01_MainMenu");
                yield return null;
                yield return null;
                Assert.IsTrue(ForestMenu.Visible);
                var menu = UnityEngine.Object.FindFirstObjectByType<ForestMenu>();
                Assert.IsNotNull(Resources.Load<Texture2D>("Menu/Background"));
                Assert.IsNotNull(Resources.Load<Texture2D>("Menu/ButtonPlate"));
                Assert.IsNotNull(Resources.Load<Texture2D>("Menu/Wordmark"));
                var stencil = Resources.Load<Font>("Menu/Fonts/BlackOpsOne-Regular");
                Assert.IsNotNull(stencil);
                const string labels = "TIẾP TỤC CHƠI MỚI TẢI GAME THOÁT LƯU XÁC NHẬN HỦY QUAY LẠI";
                stencil.RequestCharactersInTexture(labels, 43);
                foreach (char character in labels) if (!char.IsWhiteSpace(character))
                    Assert.IsTrue(stencil.HasCharacter(character), "Missing Vietnamese stencil glyph: " + character);
                Assert.AreEqual(-1, ForestSaveSlots.Latest());
                Directory.CreateDirectory("Logs/MenuPreview");
                ScreenCapture.CaptureScreenshot("Logs/MenuPreview/main.png");
                for (int i = 0; i < 10; i++) yield return null;
                Call(menu, "MainAction", 1);
                yield return null;
                // "Chơi mới" plays the briefing cutscene first; skip it the same way Esc/Enter would.
                Call(menu, "EndIntro");
                yield return null;
                yield return new WaitForSeconds(1);
                Assert.AreEqual("Map 1", SceneManager.GetActiveScene().name);
                Assert.IsFalse(ForestMenu.Visible);
                var mission = UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
                Assert.IsNotNull(mission);
                var saveSystem = mission.GetComponent<Map01SaveSystem>();
                var position = mission.player.position;
                mission.SetPaused(true);
                Assert.IsTrue(saveSystem.CanSave(), "Initial safe camp must allow saves while paused.");
                Call(menu, "OpenSlots", true);
                Assert.AreEqual(1, typeof(ForestMenu).GetField("selected", Private).GetValue(menu));
                Call(menu, "SlotAction");
                Assert.IsTrue(ForestSaveSlots.Exists(1), "Saving an empty slot through the menu must write a backup.");
                Assert.AreEqual(1, ForestSaveSlots.Latest());
                var entry = ForestSaveSlots.Read(1);
                Assert.AreEqual("Map 1", entry.sceneName);
                Assert.Greater(entry.thumbnail.Length, 100);
                Assert.Greater(entry.playSeconds, 0);
                Call(menu, "SlotAction");
                Assert.IsNotNull(typeof(ForestMenu).GetField("question", Private).GetValue(menu), "Existing backups require confirmation.");
                Call(menu, "Confirm");
                Assert.IsNull(typeof(ForestMenu).GetField("question", Private).GetValue(menu));
                StringAssert.Contains("Đã lưu vào", (string)typeof(ForestMenu).GetField("message", Private).GetValue(menu));
                typeof(ForestMenu).GetField("selected", Private).SetValue(menu, ForestSaveSlots.AutoSlot);
                Call(menu, "SlotAction");
                Assert.IsFalse(ForestSaveSlots.Exists(ForestSaveSlots.AutoSlot));
                StringAssert.Contains("backup", (string)typeof(ForestMenu).GetField("message", Private).GetValue(menu));
                Call(menu, "OpenSlots", true);
                Assert.AreEqual(1, typeof(ForestMenu).GetField("selected", Private).GetValue(menu), "The save tab must select a writable manual slot.");
                typeof(ForestMenu).GetField("visible", Private).SetValue(menu, true);
                ScreenCapture.CaptureScreenshot("Logs/MenuPreview/pause.png");
                for (int i = 0; i < 10; i++) yield return null;
                Call(menu, "OpenSlots", false);
                ScreenCapture.CaptureScreenshot("Logs/MenuPreview/saves.png");
                for (int i = 0; i < 10; i++) yield return null;
                Call(menu, "DeleteSelected");
                ScreenCapture.CaptureScreenshot("Logs/MenuPreview/confirm.png");
                for (int i = 0; i < 10; i++) yield return null;
                Map01SaveSystem.BeginGame(1);
                yield return null;
                // Invoke-based restore depends on actual player frames, not the EditMode runner's wait handling.
                var pending = typeof(Map01SaveSystem).GetField("pendingCheckpoint", BindingFlags.NonPublic | BindingFlags.Static);
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (pending.GetValue(null) != null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsNull(pending.GetValue(null), "Checkpoint restore did not complete.");
                mission = UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
                Assert.Less(Vector3.Distance(position, mission.player.position), .2f);
                Assert.GreaterOrEqual(mission.PlaySeconds, entry.playSeconds);
                Assert.AreEqual(1, Time.timeScale);
                ForestSaveSlots.Delete(1);
                Assert.AreEqual(-1, ForestSaveSlots.Latest());
            } finally {
                rootField.SetValue(null, null);
                Time.timeScale = 1;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            yield return new ExitPlayMode();
        }
    }
}
