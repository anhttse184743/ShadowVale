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
using UnityEditor;

namespace ShadowVale.Map01.Tests
{
    public sealed class ForestMenuIntegrationTests : ForestSceneTestBase
    {
        string previousSaveRoot;
        static Keyboard escapeKeyboard;
        static bool escapeHeld;
        [SetUp] public void IsolateSaves() {
            previousSaveRoot=SessionState.GetString(ForestSaveSlots.EditorTestRootKey, "");
            SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath("Docs/Reviews/CommanderOpening_20261018/TestSaves/"+Guid.NewGuid().ToString("N")));
        }
        [TearDown] public void RestoreSaves() {
            Time.timeScale=1;
            if(string.IsNullOrEmpty(previousSaveRoot))SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
            else SessionState.SetString(ForestSaveSlots.EditorTestRootKey,previousSaveRoot);
        }
        static void PumpEscape() {
            escapeKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(escapeKeyboard,escapeHeld?new KeyboardState(Key.Escape):new KeyboardState());
        }

        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static void Call(ForestMenu menu, string method, params object[] args) => typeof(ForestMenu).GetMethod(method, Private).Invoke(menu, args);

        [UnityTest]
        public IEnumerator PlayWhileEditingMapStartsAtMenuAndNewGameStillWorks()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            Assert.IsTrue(UnityEditor.EditorApplication.ExecuteMenuItem("ShadowVale/Menu/Luôn bắt đầu từ menu chính"));
            Assert.AreEqual("Assets/_Project/Scenes/00_Boot.unity", UnityEditor.AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            yield return new EnterPlayMode();
            yield return CheckMenuNewGame();
            yield return new ExitPlayMode();
            Assert.AreEqual("Map 1", SceneManager.GetActiveScene().name, "Stopping Play must restore the scene being edited.");
        }
        static IEnumerator CheckMenuNewGame() {
            double deadline=Time.realtimeSinceStartupAsDouble+20;
            while(SceneManager.GetActiveScene().name!="01_MainMenu"&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.AreEqual("01_MainMenu",SceneManager.GetActiveScene().name);Assert.IsTrue(ForestMenu.Visible);
            Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<Map01Mission>(),"No gameplay runs behind the title.");
            var menu=UnityEngine.Object.FindFirstObjectByType<ForestMenu>();
            Call(menu,"MainAction",1);Call(menu,"MainAction",1); // New Game, then skip the optional tutorial.
            yield return WaitForMap01Ready();
            Assert.AreEqual("Map 1",SceneManager.GetActiveScene().name);
            AssertWaitingForCommander(UnityEngine.Object.FindFirstObjectByType<Map01Mission>());
            Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<UnityEngine.Video.VideoPlayer>());
            Assert.IsFalse(ForestMenu.Visible);
            Assert.IsFalse((bool)typeof(ForestMenu).GetField("loading",Private).GetValue(menu));
        }
        [UnityTest]
        public IEnumerator EscClosesCommanderConversationWithoutAcceptingAndStillPausesGameplay() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            yield return CheckMenuNewGame();
            yield return CheckConversationEscape();
            yield return new ExitPlayMode();
        }
        static IEnumerator CheckConversationEscape() {
            var mission=UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
            var story=mission.GetComponent<Map01Story>();var dialogue=mission.GetComponent<Map01StoryDialogue>();
            var routing=RouteInputToGame();escapeKeyboard=InputSystem.AddDevice<Keyboard>();escapeHeld=false;
            InputSystem.onBeforeUpdate+=PumpEscape;
            try {
                yield return WaitGameSeconds(1.2f);
                PlaceNam(mission,story.Commander.position+story.Commander.forward*1.8f);
                Assert.IsTrue(story.TryInteract());Assert.IsTrue(dialogue.IsPlaying);
                escapeHeld=true;yield return WaitGameSeconds(1.2f);
                Assert.IsFalse(dialogue.IsPlaying);Assert.IsFalse(mission.GetComponent<Map01Quest>().RescueOrderAccepted);
                escapeHeld=false;yield return WaitGameSeconds(1.3f);
                AssertWaitingForCommander(mission);Assert.IsFalse(ForestMenu.Visible||mission.Paused,"Closing the conversation returns to gameplay.");
                escapeHeld=true;yield return new WaitForSecondsRealtime(.3f);
                Assert.IsTrue(ForestMenu.Visible&&mission.Paused,"A later Esc press still opens the pause menu.");
                escapeHeld=false;yield return null;
            } finally {
                InputSystem.onBeforeUpdate-=PumpEscape;InputSystem.RemoveDevice(escapeKeyboard);escapeKeyboard=null;
                RestoreInputRouting(routing);Time.timeScale=1;
            }
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
            float deadline=Time.realtimeSinceStartup+15;
            while(SceneManager.GetActiveScene().name!="01_MainMenu"&&Time.realtimeSinceStartup<deadline)yield return null;
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
            string root = Path.GetFullPath("Docs/Reviews/CommanderOpening_20261018/TestSaves/Menu/" + Guid.NewGuid().ToString("N"));
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
                Call(menu, "MainAction", 1); // New game opens the tutorial choice.
                Call(menu, "MainAction", 1); // Skip tutorial to enter Map 1 and wait for the commander.
                yield return null;
                yield return WaitForMap01Ready();
                AssertWaitingForCommander(UnityEngine.Object.FindFirstObjectByType<Map01Mission>());
                yield return WaitGameSeconds(.3f);
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
                AssertWaitingForCommander(mission);
                Assert.Less(Vector3.Distance(position, mission.player.position), .2f);
                Assert.GreaterOrEqual(mission.PlaySeconds, entry.playSeconds);
                Assert.AreEqual(1, Time.timeScale);
                ForestSaveSlots.Delete(1);
                Assert.AreEqual(-1, ForestSaveSlots.Latest());
            } finally {
                rootField.SetValue(null, null);
                Time.timeScale = 1;
                // Keep isolated test saves as evidence; never touch the player's persistent slots.
            }
            yield return new ExitPlayMode();
        }
    }
}
