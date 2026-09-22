using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static class ForestMenuTools
    {
        static ForestMenuTools()
        {
            EditorApplication.delayCall += ConfigureStartup;
            EditorApplication.delayCall += RecoverAndReport;
            EditorApplication.playModeStateChanged += _ => EditorApplication.delayCall += RecoverAndReport;
        }
        [MenuItem("ShadowVale/Menu/Luôn bắt đầu từ menu chính")]
        public static void ConfigureStartup()
        {
            // Play must use the same entry scene as the build, even while a map is open for editing.
            // This changes the next Play session, not the user's currently open/unsaved scene.
            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/00_Boot.unity");
            if (boot != null) EditorSceneManager.playModeStartScene = boot;
        }
        private static void RecoverAndReport()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isCompiling)
                ForestMenu.EnsureCreated();
            WriteStatus();
        }
        [MenuItem("ShadowVale/Menu/Mở menu chính")]
        public static void OpenTitle()
        {
            const string path = "Assets/_Project/Scenes/01_MainMenu.unity";
            if (EditorApplication.isPlaying) {
                Time.timeScale = 1;
                SceneManager.LoadScene("01_MainMenu");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);
        }
        [MenuItem("ShadowVale/Menu/Ghi trạng thái chẩn đoán")]
        public static void WriteStatus()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) {
                EditorApplication.delayCall += WriteStatus; return;
            }
            var scene = SceneManager.GetActiveScene();
            var menu = Object.FindFirstObjectByType<ForestMenu>();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/Menu-EditorStatus.txt",
                $"Scene: {scene.name}\nPath: {scene.path}\nPlaying: {EditorApplication.isPlaying}\n" +
                $"Paused: {EditorApplication.isPaused}\nScene dirty: {scene.isDirty}\n" +
                $"Menu object: {(menu != null ? menu.name : "none")}\nVisible: {ForestMenu.Visible}\n" +
                $"Play start scene: {AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)}\n");
        }
    }
}
