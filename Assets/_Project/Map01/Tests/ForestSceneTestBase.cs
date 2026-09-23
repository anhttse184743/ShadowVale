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
