using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01.Tests
{
    public abstract class ForestSceneTestBase
    {
        [SetUp]
        public void UseExplicitTestScene()
        {
            // Every scene test uses isolated slots, including fixtures that open the real map.
            // A fixture may select a more specific root in its own SetUp.
            SessionState.SetString(ForestSaveSlots.EditorTestRootKey,System.IO.Path.GetFullPath("Temp/Map01SceneTestSaves"));
            // Gameplay tests intentionally open their own scene; normal editor Play starts at Boot.
            EditorSceneManager.playModeStartScene = null;
            // Editor tooling (ForestMenuTools) points Play back at Boot whenever the open scene
            // changes — including the test's own OpenScene, which would then enter Play at the menu
            // instead of the map. Handlers run in subscription order, so this one, added last, wins.
            EditorSceneManager.activeSceneChangedInEditMode -= KeepExplicitStart;
            EditorSceneManager.activeSceneChangedInEditMode += KeepExplicitStart;
            // A frame cap (Application.targetFrameRate) lets the editor ticks that advance a test
            // outrun the game's frames, so a "yield return null" no longer means a frame of Update
            // has run. Every test starts uncapped, and the game's own cap stays off (FramePacing).
            UnityEngine.Application.targetFrameRate = -1;
            SessionState.SetBool(FramePacing.UncappedKey, true);
            // Tests start with the normal loadout whatever Map01Balance.json says about the bench test kit.
            SessionState.SetBool(Map01Inventory.NoTestKitKey, true);
            // Capture fixtures turn async shader compilation off for steady frames; EditorSettings is saved into
            // ProjectSettings, so the editor's own value is kept here (SessionState outlives the play-mode reloads)
            // and put back after every test.
            if (!SessionState.GetBool(AsyncShadersKnownKey, false))
            {
                SessionState.SetBool(AsyncShadersKey, EditorSettings.asyncShaderCompilation);
                SessionState.SetBool(AsyncShadersKnownKey, true);
            }
        }
        private const string AsyncShadersKey = "ShadowVale.Tests.AsyncShaders", AsyncShadersKnownKey = "ShadowVale.Tests.AsyncShadersKnown";
        private static void KeepExplicitStart(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to) =>
            EditorSceneManager.playModeStartScene = null;
        [TearDown]
        public void RestoreNormalStartup()
        {
            SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
            EditorSceneManager.activeSceneChangedInEditMode -= KeepExplicitStart;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/00_Boot.unity");
            SessionState.EraseBool(FramePacing.UncappedKey);
            SessionState.EraseBool(Map01Inventory.NoTestKitKey);
            if (SessionState.GetBool(AsyncShadersKnownKey, false))
            {
                bool editorValue = SessionState.GetBool(AsyncShadersKey, true);
                EditorSettings.asyncShaderCompilation = editorValue; ShaderUtil.allowAsyncCompilation = editorValue;
                SessionState.EraseBool(AsyncShadersKey); SessionState.EraseBool(AsyncShadersKnownKey);
            }
        }

        /// <summary>Fix the Game View to <paramref name="width"/>×<paramref name="height"/>: screenshots
        /// come out at that size, and the HUD scale follows it (1 at 1600×900).</summary>
        protected static void SetPreviewResolution(int width, int height)
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

        /// <summary>Writes a review file, retrying for up to two seconds: a virus scan or a diff viewer can hold a file that was
        /// just written (Win32 1224, "a user-mapped section is open"), and the test then failed on the evidence, not the game.</summary>
        protected static void WriteReviewFile(string path, byte[] bytes)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { System.IO.File.WriteAllBytes(path, bytes); return; }
                catch (System.IO.IOException) when (attempt < 20) { System.Threading.Thread.Sleep(100); }
            }
        }
        protected static void WriteReviewFile(string path, string text) => WriteReviewFile(path, System.Text.Encoding.UTF8.GetBytes(text));

        /// <summary>WaitForSeconds is a YieldInstruction the Edit Mode runner cannot wait on — it
        /// only skips a frame — so game-time waits have to poll Time.time instead.</summary>
        protected static System.Collections.IEnumerator WaitGameSeconds(float seconds)
        {
            float until = UnityEngine.Time.time + seconds;
            while (UnityEngine.Time.time < until) yield return null;
        }

        /// <summary>
        /// Batchmode has no focused Game View, and the Input System's default then treats pointer
        /// and keyboard events as editor input, so synthetic presses never reach Play Mode. Call
        /// after EnterPlayMode (entering it can rebuild the settings) and restore when done.
        /// </summary>
        protected static (InputSettings.EditorInputBehaviorInPlayMode, InputSettings.BackgroundBehavior) RouteInputToGame()
        {
            var settings = InputSystem.settings;
            var previous = (settings.editorInputBehaviorInPlayMode, settings.backgroundBehavior);
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            return previous;
        }
        protected static void RestoreInputRouting((InputSettings.EditorInputBehaviorInPlayMode, InputSettings.BackgroundBehavior) previous)
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = previous.Item1;
            InputSystem.settings.backgroundBehavior = previous.Item2;
        }

        // ---- Map 1's story (Map01Story): the commander's orders --------------------------------------------------------

        protected static System.Collections.IEnumerator WaitForMap01Ready()
        {
            float until=Time.realtimeSinceStartup+40;
            Map01Mission mission;
            do {
                mission=UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
                if(mission!=null&&mission.IsInitialized&&mission.ModernPlayer!=null&&mission.ModernCombat!=null
                    &&mission.ModernHealth!=null&&mission.GetComponent<Map01Story>()?.Commander!=null)yield break;
                yield return null;
            } while(Time.realtimeSinceStartup<until);
            Assert.Fail("Map 1 and its allied commander must initialize before the opening-flow check.");
        }

        protected static System.Collections.IEnumerator WaitForMap01CheckpointRestore()
        {
            yield return WaitForMap01Ready();
            float until = Time.realtimeSinceStartup + 15;
            while (Map01SaveSystem.IsRestoring && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse(Map01SaveSystem.IsRestoring, "The pending checkpoint must finish restoring before its state is inspected.");
            yield return null;
        }

        protected static void AssertWaitingForCommander(Map01Mission mission)
        {
            var quest=mission.GetComponent<Map01Quest>();
            var opening=mission.GetComponentInChildren<Map01OpeningCutscene>();
            Assert.NotNull(opening);Assert.NotNull(opening.CommanderTransform);
            Assert.IsFalse(opening.IsPlaying||Map01OpeningCutscene.Active,"New Game must wait for E beside the commander.");
            Assert.IsFalse(mission.Cinematic||mission.GetComponent<Map01StoryDialogue>().IsPlaying);
            Assert.AreEqual(Map01Quest.RescueStage,quest.Stage);Assert.IsFalse(quest.RescueOrderAccepted);
            StringAssert.Contains("chỉ huy",quest.ObjectiveText);
            var voice=mission.GetComponent<DialogueVoice>();
            Assert.IsTrue(voice==null||!voice.HasSpeech,"No automatic briefing may be queued at map startup.");
            Assert.IsTrue(mission.ModernPlayer.enabled&&mission.ModernCombat.enabled&&mission.CameraInputEnabled);
        }

        /// <summary>Nam set down at <paramref name="at"/>, standing and still.</summary>
        protected static void PlaceNam(Map01Mission mission, Vector3 at)
        {
            var body = mission.player.GetComponent<CharacterController>();
            bool enabled = body.enabled;
            body.enabled = false; mission.player.position = at; body.enabled = enabled;
            mission.ModernPlayer.RestoreMotion(false); mission.Crouched = false;
            Physics.SyncTransforms();
        }

        protected static void PlaceNamAtCommander(Map01Mission mission)
        {
            Assert.IsNotNull(mission.GetComponent<Map01Story>().Commander);
            var post = mission.alliedCommanderPost;
            Assert.IsNotNull(post);
            PlaceNam(mission, post.position + post.forward * 1.8f);
        }

        /// <summary>The running story conversation, confirmed to its end as the player would ("Nhận nhiệm vụ").</summary>
        protected static System.Collections.IEnumerator ConfirmConversation(Map01Mission mission)
        {
            var dialogue = mission.GetComponent<Map01StoryDialogue>();
            Assert.IsTrue(dialogue.IsPlaying, "A story conversation must be running.");
            yield return WaitGameSeconds(1.1f);
            dialogue.Close(true);
            yield return null;
        }

        /// <summary>
        /// The game's first step: [E] beside the commander at the base, his rescue briefing confirmed. Until then Hùng's
        /// guards are not Nam's job (Map01Rescue ignores sightings, the guide points at the commander).
        /// </summary>
        protected static System.Collections.IEnumerator TakeRescueOrder(Map01Mission mission)
        {
            var story = mission.GetComponent<Map01Story>();
            float until = Time.realtimeSinceStartup + 30;
            while (story.Commander == null && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsNotNull(story.Commander, "The commander waits at the base.");
            PlaceNam(mission, story.Commander.position + story.Commander.forward * 1.8f);
            Assert.IsTrue(story.TryInteract(), "[E] beside the commander starts his briefing.");
            yield return ConfirmConversation(mission);
            Assert.IsTrue(mission.GetComponent<Map01Quest>().RescueOrderAccepted, "Confirming the briefing accepts the rescue order.");
        }

        /// <summary>A save's story with the rescue order taken and nothing more.</summary>
        protected static Map01Story.Snapshot RescueOrdered() => new Map01Story.Snapshot { rescueAccepted = true };

        /// <summary>A save's story with points 1 and 2 done: the supplies home, the radio silenced.</summary>
        protected static Map01Story.Snapshot RadioSilenced() => new Map01Story.Snapshot
        {
            rescueAccepted = true, supplies = true, keyCollected = true, doorUnlocked = true, chestUnlocked = true,
            supplyReported = true, suppliesDelivered = true, supplyRewardGranted = true, radioDisabled = true
        };

        /// <summary>
        /// A save's story at point 3 with the officer seen through the binoculars and reported: his death then completes
        /// the objective (Map01Story.CommanderObjectiveComplete) and plays the finale.
        /// </summary>
        protected static Map01Story.Snapshot OfficerReported()
        {
            var progress = RadioSilenced();
            progress.commanderAreaReached = progress.commanderConfirmed = progress.commanderReported = progress.commanderDiscovery = true;
            return progress;
        }
    }
}
