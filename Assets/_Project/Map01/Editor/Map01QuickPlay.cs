using UnityEditor;
using UnityEditor.SceneManagement;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// ShadowVale > Chơi thẳng Map 1: Play straight into Map 1, skipping Boot, the title menu and
    /// the intro. Normal Play still goes through the menu (ForestMenuTools); this only changes
    /// the one session it starts, and puts the menu start back once Play stops.
    /// </summary>
    [InitializeOnLoad]
    public static class Map01QuickPlay
    {
        private const string MapPath = "Assets/_Project/Scenes/Maps/Map 1.unity";
        private const string Pending = "ShadowVale.Map01QuickPlay";

        static Map01QuickPlay() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("ShadowVale/Chơi thẳng Map 1 (bỏ qua menu)", priority = 0)]
        public static void PlayMap1()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MapPath);
            // After OpenScene: ForestMenuTools points Play back at Boot whenever the scene changes.
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(Pending, true); // Survives the domain reload of entering Play.
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            ForestMenuTools.ConfigureStartup(); // The next ordinary Play goes through the menu again.
        }
    }
}
