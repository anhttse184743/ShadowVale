using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShadowVale.AI.Tactical;
using ShadowVale.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Bakes the open scene's tactical graph — the QUBO's input (M3) — for any map:
    /// <list type="number">
    /// <item>sample the baked NavMesh on a grid (plus <see cref="TacticalNodeMarker"/>s);</item>
    /// <item>pull each sample against a nearby solid obstacle, if any, so cover is where walls are;</item>
    /// <item>connect neighbours whose NavMesh path is complete and not much longer than the straight line;</item>
    /// <item>bridge gaps: close points the grid leaves far apart (a river) but the NavMesh joins by a
    ///       short path (a bridge) get a chain of nodes along that path — its ends are chokepoints;</item>
    /// <item>keep the largest connected piece; flag cover (facing = side it protects from);</item>
    /// <item>chokepoints = bridge ends, Escape markers, objects named "exit", top betweenness.</item>
    /// </list>
    /// Writes <c>Assets/_Project/Resources/TacticalGraphs/&lt;scene&gt;.asset</c> and the same graph in the
    /// content bundle schema (<c>&lt;scene&gt;.nav_graph.json</c>) for the web platform.
    /// Menu <b>ShadowVale ▸ AI ▸ Bake Tactical Graph</b>, or headless:
    /// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod ShadowVale.Editor.TacticalGraphBaker.BakeMap1</c>
    /// </summary>
    public static class TacticalGraphBaker
    {
        public const string OutputFolder = "Assets/_Project/Resources/" + TacticalGraphAsset.ResourcesFolder;

        public sealed class Settings
        {
            public float Spacing = 9f;
            public int MaxNodes = 500;
            public float EdgeReach = 1.5f;          // × spacing
            public float MaxDetour = 1.4f;          // path length / straight distance
            public float GapReach = 3f;             // × spacing: how far apart a gap's two sides may be
            public int GapMinHops = 4;              // graph hops above which a short NavMesh path is a gap
            public float CoverProbeDistance = 1.6f;
            public float CoverProbeHeight = .7f;    // crouched cover: sandbags count
            public float CoverSnapRadius = .4f;     // × spacing
            public float MinCoverHeight = .8f, MinCoverWidth = .8f;
            public float CoverSideSampleMinLength = 1.5f; // obstacles this long get a node on each side
            public float ChokepointShare = .05f;    // top share of nodes by betweenness
            public float ExitSnapDistance = 15f;
        }

        private static readonly Vector3[] ProbeDirections = Enumerable.Range(0, 8)
            .Select(i => Quaternion.Euler(0, i * 45f, 0) * Vector3.forward).ToArray();

        [MenuItem("ShadowVale/AI/Bake Tactical Graph (open scene)")]
        public static void BakeOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var asset = Bake(scene.name, new Settings(), out string report);
            if (asset == null) { EditorUtility.DisplayDialog("Tactical graph", report, "OK"); return; }
            string path = Save(asset);
            Debug.Log($"[TacticalGraph] {path}\n{report}");
            EditorUtility.DisplayDialog("Tactical graph", report + "\n\n" + path, "OK");
        }

        /// <summary>Headless entry point for Map 1.</summary>
        public static void BakeMap1()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity", OpenSceneMode.Single);
            var asset = Bake(scene.name, new Settings(), out string report);
            if (asset == null) throw new System.InvalidOperationException(report);
            Debug.Log($"[TacticalGraph] {Save(asset)}\n{report}");
        }

        public static TacticalGraphAsset Bake(string sceneName, Settings s, out string report)
        {
            CoverCache.Clear();
            var triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices.Length == 0) { report = "Scene has no baked NavMesh."; return null; }
            var bounds = new Bounds(triangulation.vertices[0], Vector3.zero);
            foreach (var v in triangulation.vertices) bounds.Encapsulate(v);
            int mask = LayerMask.GetMask("Default", "Obstacle", "Cover", "VisionBlocker");

            List<Vector3> points; List<List<int>> adjacency; HashSet<int> portals; float spacing = s.Spacing;
            while (true)
            {
                points = SamplePoints(bounds, spacing);
                SnapToCover(points, spacing, s, mask);
                AddCoverSamples(points, spacing, s, mask);
                adjacency = Connect(points, spacing * s.EdgeReach, s.MaxDetour);
                portals = BridgeGaps(points, adjacency, spacing, s);
                var keep = GraphAlgorithms.LargestComponent(Freeze(adjacency));
                Reindex(ref points, ref adjacency, ref portals, keep);
                if (points.Count <= s.MaxNodes) break;
                spacing *= 1.15f;
            }

            int n = points.Count;
            var frozen = Freeze(adjacency);
            var nodes = new TacticalGraphAsset.Node[n];
            for (int i = 0; i < n; i++)
            {
                bool cover = TryCover(points[i], s, mask, out float facing);
                nodes[i] = new TacticalGraphAsset.Node { position = points[i], cover = cover, coverFacing = facing, neighbors = frozen[i] };
            }
            foreach (int c in Chokepoints(points, frozen, portals, s)) nodes[c].chokepoint = true;

            var asset = ScriptableObject.CreateInstance<TacticalGraphAsset>();
            asset.sceneName = sceneName; asset.spacing = spacing; asset.nodes = nodes;
            int edges = frozen.Sum(a => a.Length) / 2;
            report = $"Scene '{sceneName}': {n} nodes, {edges} edges, spacing {spacing:0.#} m, " +
                     $"{nodes.Count(x => x.cover)} cover, {nodes.Count(x => x.chokepoint)} chokepoints ({portals.Count} gap nodes).";
            return asset;
        }

        // ---- sampling ----------------------------------------------------------------------------

        private static List<Vector3> SamplePoints(Bounds bounds, float spacing)
        {
            var points = new List<Vector3>();
            foreach (var marker in Object.FindObjectsByType<TacticalNodeMarker>(FindObjectsSortMode.None).OrderBy(m => m.name))
                if (NavMesh.SamplePosition(marker.transform.position, out var hit, 3f, NavMesh.AllAreas)) AddUnique(points, hit.position, spacing * .5f);
            for (float x = bounds.min.x + spacing * .5f; x <= bounds.max.x; x += spacing)
            for (float z = bounds.min.z + spacing * .5f; z <= bounds.max.z; z += spacing)
            {
                var probe = new Vector3(x, bounds.center.y, z);
                if (!NavMesh.SamplePosition(probe, out var hit, bounds.extents.y + spacing * .5f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.x - x) > spacing * .5f || Mathf.Abs(hit.position.z - z) > spacing * .5f) continue;
                AddUnique(points, hit.position, spacing * .5f);
            }
            return points;
        }

        private static int AddUnique(List<Vector3> points, Vector3 p, float minDistance)
        {
            for (int i = 0; i < points.Count; i++) if ((points[i] - p).sqrMagnitude < minDistance * minDistance) return i;
            points.Add(p);
            return points.Count - 1;
        }

        private static readonly Dictionary<Collider, bool> CoverCache = new Dictionary<Collider, bool>();

        /// <summary>
        /// A visible, solid, person-sized obstacle — not an invisible boundary wall, not the ground.
        /// Props often keep their collision on an invisible child ("Cover collider" under a sandbag wall),
        /// so "visible" means a renderer in the same group overlapping the collider.
        /// </summary>
        private static bool IsCoverCollider(Collider c, Settings s)
        {
            if (c == null || c.isTrigger || c is TerrainCollider) return false;
            if (CoverCache.TryGetValue(c, out bool cached)) return cached;
            var b = c.bounds;
            bool result = false;
            if (b.size.y >= s.MinCoverHeight && Mathf.Max(b.size.x, b.size.z) >= s.MinCoverWidth && b.size.x < 60f && b.size.z < 60f)
            {
                if (c.TryGetComponent<Renderer>(out _)) result = true;
                else
                {
                    var area = b; area.Expand(.5f);
                    var group = c.transform.parent != null ? c.transform.parent : c.transform;
                    foreach (var r in group.GetComponentsInChildren<Renderer>())
                        if (r.bounds.Intersects(area)) { result = true; break; }
                }
            }
            CoverCache[c] = result;
            return result;
        }

        /// <summary>A node on each long side of every sizeable cover obstacle (sandbag walls, tent walls, rocks).</summary>
        private static void AddCoverSamples(List<Vector3> points, float spacing, Settings s, int mask)
        {
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.InstanceID))
            {
                if ((mask & (1 << c.gameObject.layer)) == 0 || !IsCoverCollider(c, s)) continue;
                var b = c.bounds;
                if (Mathf.Max(b.size.x, b.size.z) < s.CoverSideSampleMinLength) continue;
                Vector3 normal = b.size.x >= b.size.z ? Vector3.forward : Vector3.right;
                float half = b.size.x >= b.size.z ? b.extents.z : b.extents.x;
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector3 p = b.center + normal * side * (half + .8f);
                    p.y = b.min.y;
                    if (!NavMesh.SamplePosition(p, out var hit, 1.2f, NavMesh.AllAreas)) continue;
                    if (TryCover(hit.position, s, mask, out _)) AddUnique(points, hit.position, spacing * .35f);
                }
            }
        }

        /// <summary>Moves each sample next to the nearest cover obstacle within reach, when there is one.</summary>
        private static void SnapToCover(List<Vector3> points, float spacing, Settings s, int mask)
        {
            float radius = spacing * s.CoverSnapRadius;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 chest = points[i] + Vector3.up * s.CoverProbeHeight;
                Collider best = null; Vector3 bestPoint = default; float bestD = float.PositiveInfinity;
                foreach (var c in Physics.OverlapSphere(chest, radius, mask, QueryTriggerInteraction.Ignore))
                {
                    if (!IsCoverCollider(c, s)) continue;
                    Vector3 p = c is MeshCollider { convex: false } ? c.bounds.ClosestPoint(chest) : c.ClosestPoint(chest);
                    float d = (p - chest).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = c; bestPoint = p; }
                }
                if (best == null || bestD < .01f) continue;
                Vector3 away = Vector3.ProjectOnPlane(chest - bestPoint, Vector3.up).normalized;
                if (!NavMesh.SamplePosition(bestPoint + away * .8f - Vector3.up * s.CoverProbeHeight, out var hit, 1f, NavMesh.AllAreas)) continue;
                if (TryCover(hit.position, s, mask, out _)) points[i] = hit.position;
            }
        }

        // ---- edges -------------------------------------------------------------------------------

        private static bool PathLength(Vector3 a, Vector3 b, NavMeshPath path, out float length)
        {
            length = 0f;
            if (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            for (int k = 1; k < path.corners.Length; k++) length += Vector3.Distance(path.corners[k - 1], path.corners[k]);
            return true;
        }

        private static List<List<int>> Connect(List<Vector3> points, float reach, float maxDetour)
        {
            int n = points.Count;
            var adjacency = Enumerable.Range(0, n).Select(_ => new List<int>()).ToList();
            var path = new NavMeshPath();
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                float straight = Vector3.Distance(points[i], points[j]);
                if (straight > reach) continue;
                if (!PathLength(points[i], points[j], path, out float length) || length > straight * maxDetour + .5f) continue;
                adjacency[i].Add(j); adjacency[j].Add(i);
            }
            return adjacency;
        }

        /// <summary>
        /// Points close in space but far apart in the graph, joined by a short NavMesh path, are the two
        /// sides of a gap the grid missed (a narrow bridge). A chain of nodes along that path joins them.
        /// Returns the chain nodes (and the two sides) as chokepoint candidates.
        /// </summary>
        private static HashSet<int> BridgeGaps(List<Vector3> points, List<List<int>> adjacency, float spacing, Settings s)
        {
            var portals = new HashSet<int>();
            var path = new NavMeshPath();
            float reach = spacing * s.GapReach;
            int original = points.Count;
            int[] hops = GraphAlgorithms.AllPairsHops(Freeze(adjacency));
            int n = points.Count;
            for (int i = 0; i < original; i++)
            for (int j = i + 1; j < original; j++)
            {
                float straight = Vector3.Distance(points[i], points[j]);
                if (straight > reach) continue;
                int h = hops[i * n + j];
                if (h != GraphAlgorithms.Unreachable && h <= s.GapMinHops) continue;
                if (!PathLength(points[i], points[j], path, out float length) || length > straight * s.MaxDetour + .5f) continue;

                // Nodes along the path every ~spacing; the path's own ends are i and j.
                var chain = new List<int> { i };
                var corners = path.corners;
                float carried = 0f;
                for (int k = 1; k < corners.Length; k++)
                {
                    Vector3 a = corners[k - 1], b = corners[k];
                    float seg = Vector3.Distance(a, b);
                    for (float t = spacing - carried; t < seg; t += spacing)
                    {
                        Vector3 p = Vector3.Lerp(a, b, t / seg);
                        if ((p - points[j]).sqrMagnitude < spacing * spacing * .25f) continue;
                        int idx = AddUnique(points, p, spacing * .3f);
                        if (idx == points.Count - 1 && adjacency.Count < points.Count) adjacency.Add(new List<int>());
                        if (chain[chain.Count - 1] != idx) chain.Add(idx);
                    }
                    carried = (carried + seg) % spacing;
                }
                chain.Add(j);
                for (int k = 1; k < chain.Count; k++) Link(adjacency, chain[k - 1], chain[k]);
                foreach (int c in chain) portals.Add(c);

                hops = GraphAlgorithms.AllPairsHops(Freeze(adjacency));
                n = points.Count;
            }
            return portals;
        }

        private static void Link(List<List<int>> adjacency, int a, int b)
        {
            if (a == b || adjacency[a].Contains(b)) return;
            adjacency[a].Add(b); adjacency[b].Add(a);
        }

        private static int[][] Freeze(List<List<int>> adjacency) => adjacency.Select(a => a.OrderBy(x => x).ToArray()).ToArray();

        private static void Reindex(ref List<Vector3> points, ref List<List<int>> adjacency, ref HashSet<int> portals, List<int> keep)
        {
            var map = new Dictionary<int, int>();
            for (int i = 0; i < keep.Count; i++) map[keep[i]] = i;
            var oldPoints = points; var oldAdjacency = adjacency;
            points = keep.Select(i => oldPoints[i]).ToList();
            adjacency = keep.Select(i => oldAdjacency[i].Where(map.ContainsKey).Select(j => map[j]).ToList()).ToList();
            portals = new HashSet<int>(portals.Where(map.ContainsKey).Select(i => map[i]));
        }

        // ---- flags -------------------------------------------------------------------------------

        /// <summary>A cover obstacle within a step at chest height; facing points at it.</summary>
        private static bool TryCover(Vector3 at, Settings s, int mask, out float facing)
        {
            facing = 0f;
            float nearest = float.PositiveInfinity;
            Vector3 origin = at + Vector3.up * s.CoverProbeHeight;
            foreach (var dir in ProbeDirections)
            {
                if (!Physics.Raycast(origin, dir, out var hit, s.CoverProbeDistance, mask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y > .5f || !IsCoverCollider(hit.collider, s)) continue;
                if (hit.distance < nearest) { nearest = hit.distance; facing = Quaternion.LookRotation(dir).eulerAngles.y; }
            }
            return nearest < float.PositiveInfinity;
        }

        private static IEnumerable<int> Chokepoints(List<Vector3> points, int[][] adjacency, HashSet<int> portals, Settings s)
        {
            var result = new HashSet<int>(portals);
            var betweenness = GraphAlgorithms.Betweenness(adjacency);
            int top = Mathf.Max(3, Mathf.RoundToInt(points.Count * s.ChokepointShare));
            foreach (int i in Enumerable.Range(0, points.Count).OrderByDescending(i => betweenness[i]).ThenBy(i => i).Take(top)) result.Add(i);

            var anchors = Object.FindObjectsByType<TacticalNodeMarker>(FindObjectsSortMode.None)
                .Where(m => m.kind == TacticalNodeMarker.Kind.Escape).Select(m => m.transform.position)
                .Concat(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Where(t => t.name.IndexOf("exit", System.StringComparison.OrdinalIgnoreCase) >= 0).Select(t => t.position));
            foreach (var anchor in anchors)
            {
                int best = -1; float bestD = s.ExitSnapDistance * s.ExitSnapDistance;
                for (int i = 0; i < points.Count; i++)
                {
                    float d = (points[i] - anchor).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best >= 0) result.Add(best);
            }
            return result.OrderBy(i => i);
        }

        // ---- output ------------------------------------------------------------------------------

        private static string Save(TacticalGraphAsset asset)
        {
            Directory.CreateDirectory(OutputFolder);
            string path = $"{OutputFolder}/{asset.sceneName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TacticalGraphAsset>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(asset);
                asset = existing;
            }
            else AssetDatabase.CreateAsset(asset, path);
            string mapId = asset.sceneName.Replace(" ", "").ToLowerInvariant();
            File.WriteAllText($"{OutputFolder}/{asset.sceneName}.nav_graph.json",
                ContentJson.Serialize(asset.ToMapDefinition(mapId, asset.sceneName), indented: true), new UTF8Encoding(false));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return path;
        }
    }
}
