using System.Linq;
using System.Text;
using ShadowVale.AI.Tactical;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Baker = ShadowVale.Editor.TacticalGraphBaker;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// Bakes Map 1's tactical graph (the squads' and the commander's map) on the current Map 1, with the C3 command
    /// post's cover positions (Map01C3Layout.json "cover") forced in as nodes: the commander relocates only between
    /// cover nodes inside the C3 fence, and the plain grid alone leaves the post with few. The markers are temporary;
    /// Map 1.unity is closed without saving. Menu ShadowVale ▸ AI ▸ Bake Map 1 Tactical Graph (with C3 cover), or
    /// headless: <c>-executeMethod ShadowVale.Map01.Editor.Map01TacticalGraphBake.BakeHeadless</c>.
    /// </summary>
    public static class Map01TacticalGraphBake
    {
        public const string ScenePath = "Assets/_Project/Scenes/Maps/Map 1.unity";
        const string LayoutPath = "Assets/_Project/Map01/Resources/" + Map01C3Mission.ResourcePath + ".json";

        [MenuItem("ShadowVale/AI/Bake Map 1 Tactical Graph (with C3 cover)")]
        public static void BakeFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string report = Bake();
            Debug.Log(report);
            EditorUtility.DisplayDialog("Map 1 tactical graph", report, "OK");
        }

        public static void BakeHeadless()
        {
            string report = Bake();
            Debug.Log(report);
            if (report.StartsWith("FAILED")) EditorApplication.Exit(1);
        }

        public static string Bake()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (text == null) return "FAILED: no " + LayoutPath;
            var layout = JsonUtility.FromJson<Map01C3Mission.Layout>(text.text);
            var root = new GameObject("C3 cover markers (bake only)");
            int placed = 0, shielded = 0;
            for (int i = 0; i + 1 < layout.cover.Length; i += 2)
            {
                if (!Ground(layout.cover[i], layout.cover[i + 1], out var at)) continue;
                var marker = new GameObject($"C3 cover {i / 2:00}").AddComponent<TacticalNodeMarker>();
                marker.transform.SetParent(root.transform, false);
                marker.transform.position = at;
                // Cover only where something chest-high stands beside the spot; it faces that way (the side it shields).
                if (NearestObstacle(at, out float yaw)) { marker.kind = TacticalNodeMarker.Kind.Cover; marker.transform.rotation = Quaternion.Euler(0, yaw, 0); shielded++; }
                placed++;
            }
            var asset = Baker.Bake(scene.name, new Baker.Settings(), out string report);
            Object.DestroyImmediate(root);
            if (asset == null) return "FAILED: " + report;

            var sb = new StringBuilder();
            int inside = 0, insideCover = 0;
            foreach (var node in asset.nodes)
            {
                if (!InFence(layout, node.position)) continue;
                inside++;
                if (node.cover) insideCover++;
            }
            // The camps' own cover (AiIntegrationTests asks for some within 12 m of each outpost marker).
            var camps = new StringBuilder();
            foreach (var camp in new[] { "Enemy outpost 1", "Enemy outpost 2", "Enemy outpost 3" })
            {
                var marker = GameObject.Find(camp);
                if (marker == null) continue;
                int near = asset.nodes.Count(n => n.cover && (n.position - marker.transform.position).sqrMagnitude < 12f * 12f);
                camps.Append($"\n{camp}: {near} cover nodes within 12 m.");
            }
            string path = Baker.Save(asset);
            // Re-open so the scene in memory is the saved one: the markers were never written to Map 1.unity.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            sb.Append("[TacticalGraph] ").Append(path).Append('\n').Append(report).Append('\n')
              .Append($"C3: {placed}/{layout.cover.Length / 2} cover positions on the ground, {shielded} beside an obstacle; {inside} nodes inside the fence, {insideCover} of them cover.")
              .Append(camps);
            return sb.ToString();
        }

        const float ObstacleReach = 2.5f, ChestHeight = .7f, MinObstacleHeight = .8f;

        /// <summary>The direction (yaw) of the nearest solid, chest-high obstacle within reach, the ground excluded.</summary>
        static bool NearestObstacle(Vector3 at, out float yaw)
        {
            yaw = 0; float nearest = float.PositiveInfinity;
            var origin = at + Vector3.up * ChestHeight;
            for (int d = 0; d < 8; d++)
            {
                var dir = Quaternion.Euler(0, d * 45f, 0) * Vector3.forward;
                if (!Physics.Raycast(origin, dir, out var hit, ObstacleReach, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y > .5f || hit.collider is TerrainCollider || hit.collider.bounds.size.y < MinObstacleHeight) continue;
                if (hit.distance < nearest) { nearest = hit.distance; yaw = d * 45f; }
            }
            return nearest < float.PositiveInfinity;
        }

        static bool InFence(Map01C3Mission.Layout layout, Vector3 p)
            => p.x >= layout.fenceMin[0] && p.x <= layout.fenceMax[0] && p.z >= layout.fenceMin[1] && p.z <= layout.fenceMax[1];

        /// <summary>The walkable ground at (x, z): the highest hit straight down, snapped onto the NavMesh.</summary>
        static bool Ground(float x, float z, out Vector3 at)
        {
            at = new Vector3(x, 0, z);
            var hits = Physics.RaycastAll(new Vector3(x, 400f, z), Vector3.down, 800f, ~0, QueryTriggerInteraction.Ignore)
                .OrderBy(h => h.distance);
            foreach (var hit in hits)
                if (NavMesh.SamplePosition(hit.point, out var nav, 1.5f, NavMesh.AllAreas)) { at = nav.position; return true; }
            return false;
        }
    }
}
