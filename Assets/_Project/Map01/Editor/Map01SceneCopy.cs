using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ShadowVale.AI.Tactical;
using UnityEditor;

namespace ShadowVale.Map01.Editor
{
    public static class Map01SceneCopy
    {
        private const string Maps = "Assets/_Project/Scenes/Maps";
        private const string Source = Maps + "/Map 1";
        private const string Graphs = "Assets/_Project/Resources/" + TacticalGraphAsset.ResourcesFolder;
        public static string Create(string name, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name == "Map 1")
                throw new ArgumentException("A distinct scene name without path separators is required.", nameof(name));
            string folder = Maps + "/" + name, scene = folder + ".unity";
            string sceneGuid = AssetDatabase.AssetPathToGUID(scene);
            if (File.Exists(scene) || AssetDatabase.IsValidFolder(folder)) {
                if (!overwrite) throw new IOException(name + " already exists.");
                // Copy over the scene's text later to retain its GUID / Build Settings reference.
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                AssetDatabase.DeleteAsset(Graphs + "/" + name + ".asset");
            }
            if (!AssetDatabase.CopyAsset(Source, folder)) throw new IOException("Could not copy " + Source);
            if (string.IsNullOrEmpty(sceneGuid)) {
                if (!AssetDatabase.CopyAsset(Source + ".unity", scene)) throw new IOException("Could not copy source scene.");
            } else File.Copy(Source + ".unity", scene, true);
            var remap = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Source })) {
                string old = AssetDatabase.GUIDToAssetPath(guid), copy = folder + old.Substring(Source.Length);
                string replacement = AssetDatabase.AssetPathToGUID(copy);
                if (!string.IsNullOrEmpty(replacement) && replacement != guid) remap[guid] = replacement;
            }
            var matcher = new Regex(@"guid: ([0-9a-f]{32})");
            foreach (string file in new[] { scene }.Concat(Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")))) {
                using (var probe = File.OpenRead(file)) {
                    var head = new byte[5]; if (probe.Read(head, 0, 5) < 5 || System.Text.Encoding.ASCII.GetString(head) != "%YAML") continue;
                }
                string text = File.ReadAllText(file);
                File.WriteAllText(file, matcher.Replace(text, m => remap.TryGetValue(m.Groups[1].Value, out var to) ? "guid: " + to : m.Value));
            }
            if (AssetDatabase.CopyAsset(Graphs + "/Map 1.asset", Graphs + "/" + name + ".asset")) {
                var graph = AssetDatabase.LoadAssetAtPath<TacticalGraphAsset>(Graphs + "/" + name + ".asset");
                graph.sceneName = name; EditorUtility.SetDirty(graph);
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            return "Đã tạo " + scene + ": " + remap.Count + " asset riêng; tham chiếu đã đổi sang bản copy.";
        }
    }
}
