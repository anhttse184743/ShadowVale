using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01.Tests
{
    public abstract class ForestSceneTestBase
    {
        [SetUp]
        public void UseExplicitTestScene()
        {
            // Gameplay tests intentionally open their own scene; normal editor Play starts at Boot.
            EditorSceneManager.playModeStartScene = null;
        }
        [TearDown]
        public void RestoreNormalStartup()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/00_Boot.unity");
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
    }
}
