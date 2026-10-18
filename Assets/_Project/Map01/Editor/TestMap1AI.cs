using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// "Test Map 1 AI": a full copy of Map 1 to try AI changes on — move guards, re-bake the NavMesh
    /// or the tactical graph, sculpt the terrain — without touching Map 1 itself.
    /// <list type="bullet">
    /// <item>The scene's own data (terrain, NavMesh, surroundings in <c>Scenes/Maps/Map 1/</c>) is copied
    /// too and the copy points at the copies, so editing them never changes Map 1.</item>
    /// <item>Its tactical graph is a copy of Map 1's (<c>Resources/TacticalGraphs/Test Map 1 AI</c>); re-bake
    /// it with ShadowVale ▸ AI ▸ Bake Tactical Graph (open scene) after moving things.</item>
    /// <item>In play it saves to its own files (Map01SaveSystem) and reports telemetry as <c>map01_test</c>.</item>
    /// <item>It stays on this machine (.gitignore) and out of Build Settings: the editor reloads it by path.</item>
    /// </list>
    /// Shared assets outside that folder (prefabs, materials, scripts) stay shared: changing a guard
    /// prefab changes both maps — edit the instances in the scene instead.
    /// Headless: <c>-executeMethod ShadowVale.Map01.Editor.TestMap1AI.CreateHeadless</c>.
    /// </summary>
    public static class TestMap1AI
    {
        public const string Name = "Test Map 1 AI";
        private const string Maps = "Assets/_Project/Scenes/Maps";
        public const string ScenePath = Maps + "/" + Name + ".unity";
        private const string Pending = "ShadowVale.TestMap1AI.Play";

        [MenuItem("ShadowVale/Test Map 1 AI/Tạo (hoặc làm lại) từ Map 1", priority = 20)]
        public static void CreateFromMenu()
        {
            bool exists = File.Exists(ScenePath);
            if (exists && !EditorUtility.DisplayDialog(Name,
                    $"'{Name}' đã có. Làm lại sẽ XOÁ mọi thay đổi trên map thử và copy lại từ Map 1.", "Làm lại", "Huỷ"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string report = Create(overwrite: true);
            EditorUtility.DisplayDialog(Name, report, "OK");
        }

        public static void CreateHeadless() => Debug.Log("[TestMap1AI] " + Create(overwrite: true));

        [MenuItem("ShadowVale/Test Map 1 AI/Mở để chỉnh sửa", priority = 21)]
        public static void Open()
        {
            if (!EnsureExists()) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("ShadowVale/Chơi thẳng Test Map 1 AI (bỏ qua menu)", priority = 1)]
        public static void Play()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EnsureExists()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            // After OpenScene: ForestMenuTools points Play back at Boot whenever the scene changes.
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void RestoreMenuStartAfterPlay() =>
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false);
                ForestMenuTools.ConfigureStartup();
            };

        private static bool EnsureExists()
        {
            if (File.Exists(ScenePath)) return true;
            if (!EditorUtility.DisplayDialog(Name, $"Chưa có '{Name}'. Tạo bây giờ từ Map 1?", "Tạo", "Huỷ")) return false;
            Create(overwrite: false);
            return true;
        }

        /// <summary>Copies Map 1 into the test map (replacing an old copy if <paramref name="overwrite"/>).</summary>
        public static string Create(bool overwrite) => Map01SceneCopy.Create(Name, overwrite);

    }
}
