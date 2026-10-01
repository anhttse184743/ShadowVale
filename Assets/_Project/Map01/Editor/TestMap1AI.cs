using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ShadowVale.AI.Tactical;
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
        private const string SourceScene = Maps + "/Map 1.unity";
        private const string SourceFolder = Maps + "/Map 1";
        public const string ScenePath = Maps + "/" + Name + ".unity";
        private const string Folder = Maps + "/" + Name;
        private const string Graphs = "Assets/_Project/Resources/" + TacticalGraphAsset.ResourcesFolder;
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
        public static string Create(bool overwrite)
        {
            if (File.Exists(ScenePath) || AssetDatabase.IsValidFolder(Folder))
            {
                if (!overwrite) throw new IOException($"'{Name}' already exists.");
                AssetDatabase.DeleteAsset(ScenePath);
                AssetDatabase.DeleteAsset(Folder);
                AssetDatabase.DeleteAsset($"{Graphs}/{Name}.asset");
            }
            if (!AssetDatabase.CopyAsset(SourceFolder, Folder)) throw new IOException("could not copy " + SourceFolder);
            if (!AssetDatabase.CopyAsset(SourceScene, ScenePath)) throw new IOException("could not copy " + SourceScene);

            // Old → new GUID of every asset of the scene's data folder, matched by relative path.
            var remap = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("", new[] { SourceFolder }))
            {
                string oldPath = AssetDatabase.GUIDToAssetPath(guid);
                string newPath = Folder + oldPath.Substring(SourceFolder.Length);
                string newGuid = AssetDatabase.AssetPathToGUID(newPath);
                if (!string.IsNullOrEmpty(newGuid) && newGuid != guid) remap[guid] = newGuid;
            }
            int files = 0, refs = 0;
            var textFiles = new[] { ScenePath }.Concat(Directory.GetFiles(Folder, "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta")).Select(f => f.Replace('\\', '/')));
            foreach (string path in textFiles)
            {
                int n = RemapGuids(path, remap);
                if (n > 0) { files++; refs += n; }
            }

            // Its own tactical graph, so re-baking it leaves Map 1's alone.
            string graphPath = $"{Graphs}/{Name}.asset";
            if (AssetDatabase.CopyAsset($"{Graphs}/Map 1.asset", graphPath))
            {
                var graph = AssetDatabase.LoadAssetAtPath<TacticalGraphAsset>(graphPath);
                graph.sceneName = Name;
                EditorUtility.SetDirty(graph);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return $"Đã tạo {ScenePath}: {remap.Count} asset riêng của map (terrain, NavMesh, cảnh quan) được copy, " +
                   $"{refs} tham chiếu trong {files} file trỏ sang bản copy; tactical graph riêng. Map chỉ ở máy này (.gitignore).";
        }

        private static readonly Regex GuidRef = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.Compiled);

        /// <summary>Points every reference to an old asset at its copy. Binary-serialised files are left alone.</summary>
        private static int RemapGuids(string path, IReadOnlyDictionary<string, string> remap)
        {
            using (var probe = File.OpenRead(path))
            {
                var head = new byte[5];
                if (probe.Read(head, 0, 5) < 5 || System.Text.Encoding.ASCII.GetString(head) != "%YAML") return 0;
            }
            string text = File.ReadAllText(path);
            int count = 0;
            string updated = GuidRef.Replace(text, m =>
            {
                if (!remap.TryGetValue(m.Groups[1].Value, out var to)) return m.Value;
                count++;
                return "guid: " + to;
            });
            if (count > 0) File.WriteAllText(path, updated);
            return count;
        }
    }
}
