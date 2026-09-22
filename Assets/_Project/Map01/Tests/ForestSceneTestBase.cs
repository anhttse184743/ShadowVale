using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

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
    }
}
