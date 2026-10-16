using System.Collections.Generic;
using System.IO;
using ShadowVale.AI.Coordination;
using ShadowVale.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Creates the scenes, the default solver profile and the build settings. Idempotent: existing
    /// assets are left alone. Menu <b>ShadowVale ▸ Scaffold Project</b>, or headless:
    /// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod ShadowVale.Editor.ProjectScaffold.Run</c>
    /// </summary>
    public static class ProjectScaffold
    {
        private const string ScenesRoot = "Assets/_Project/Scenes";
        private const string ProfilePath = "Assets/_Project/ScriptableObjects/SolverProfiles/SolverProfile_Default.asset";

        private static readonly string[] BuildScenes = { "00_Boot", "01_MainMenu" };

        [MenuItem("ShadowVale/Scaffold Project")]
        public static void Run()
        {
            var profile = EnsureSolverProfile();

            var buildPaths = new List<string>();
            foreach (var name in BuildScenes)
                buildPaths.Add(EnsureScene($"{ScenesRoot}/{name}.unity", name == "00_Boot" ? profile : null));

            var list = new List<EditorBuildSettingsScene>();
            foreach (var p in buildPaths) list.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = list.ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Scaffold] done: {buildPaths.Count} build scenes, profile at {ProfilePath}");
        }

        private static SolverProfileConfig EnsureSolverProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SolverProfileConfig>(ProfilePath);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
            var so = ScriptableObject.CreateInstance<SolverProfileConfig>();
            AssetDatabase.CreateAsset(so, ProfilePath);
            Debug.Log($"[Scaffold] created {ProfilePath}");
            return so;
        }

        private static string EnsureScene(string path, SolverProfileConfig bootProfile)
        {
            if (File.Exists(path)) return path;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ConfigureIsometricCamera();

            if (bootProfile != null)
            {
                var go = new GameObject("GameBootstrap");
                var boot = go.AddComponent<GameBootstrap>();
                var so = new SerializedObject(boot);
                so.FindProperty("solverProfile").objectReferenceValue = bootProfile;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[Scaffold] created scene {path}");
            return path;
        }

        /// <summary>The whole "2.5D": orthographic camera locked at (30, 45, 0). Same in every scene.</summary>
        private static void ConfigureIsometricCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = 10f;
            cam.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            cam.transform.position = new Vector3(-12f, 14f, -12f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
        }
    }
}
