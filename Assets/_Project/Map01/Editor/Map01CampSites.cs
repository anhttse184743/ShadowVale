using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// ShadowVale > Map 1 > Đặt lại doanh trại theo vị trí hiện tại: drag an "Enemy outpost N" root
    /// somewhere else in Map 1, then run this. For every camp it levels the ground under the camp,
    /// clears the plants standing in it, stations its guards around the compact camp layout
    /// (CompactEnemyCamps), moves its mission anchor, rebakes Map 1's NavMesh and checks that no
    /// camp guard can see the walk to rescue Hùng (the rule RescueTests holds the map to).
    /// </summary>
    public static class Map01CampSites
    {
        private const string MapPath = "Assets/_Project/Scenes/Maps/Map 1.unity";
        private const string TerrainPath = "Assets/_Project/Scenes/Maps/Map 1/Terrain.asset";
        private const string NavMeshPath = "Assets/_Project/Scenes/Maps/Map 1/NavMesh.asset";
        private const string EnvironmentName = "Detailed layout — reference routes and hills";
        private const string GroundName = "Continuous hills — 220 x 200";
        /// <summary>Level ground and no plants within this of a camp's centre; blended out to Blend.</summary>
        private const float Level = 8f, Blend = 13f, Clearing = 10f;
        /// <summary>Compact camp guards: a short loop beside the supply tarp (see CompactEnemyCamps).</summary>
        private static readonly Vector3[] Loop = { new Vector3(1, 0, -2.5f), new Vector3(3.5f, 0, -2.5f), new Vector3(3.5f, 0, -.25f), new Vector3(1, 0, -.25f) };
        private const float CampVision = 14f, CampAttack = 12f;

        [MenuItem("ShadowVale/Map 1/Đặt lại doanh trại theo vị trí hiện tại")]
        public static void ApplyFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try { Apply(); }
            catch (Exception e) { EditorUtility.DisplayDialog("Đặt lại doanh trại", e.Message, "OK"); throw; }
        }

        /// <summary>Batch: the first camp moves off the rescue route, south of the south bridge.</summary>
        public static void MoveFirstCampSouthAndExit()
        {
            try
            {
                EditorSceneManager.OpenScene(MapPath);
                Camp(1).position = new Vector3(40, 0, -80); // Height is set from the ground by Apply.
                Apply();
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static Transform Camp(int number) =>
            GameObject.Find("Enemy outpost " + number)?.transform ?? throw new Exception("Map 1 has no Enemy outpost " + number);

        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene().path == MapPath ? EditorSceneManager.GetActiveScene() : EditorSceneManager.OpenScene(MapPath);
            var env = GameObject.Find(EnvironmentName)?.transform ?? throw new Exception("Map 1 has no " + EnvironmentName);
            var camps = Enumerable.Range(1, 3).Select(Camp).ToArray();
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            // Who belongs where is read from the scene as it stood: each guard's nearest anchor.
            var anchors = Enumerable.Range(1, 3).Select(i => GameObject.Find($"C{i}_MissionAnchor")?.transform).ToArray();
            var guards = mission.GetComponentsInChildren<Map01EnemyController>(true)
                .Where(g => g.name.StartsWith("Outpost guard ") || g.name.StartsWith("rbl_east_")).ToArray();
            var home = guards.ToDictionary(g => g, g => Enumerable.Range(0, 3)
                .OrderBy(i => Flat((anchors[i] != null ? anchors[i] : camps[i]).position, g.transform.position)).First());

            var ground = env.Find(GroundName)?.GetComponent<MeshFilter>() ?? throw new Exception("Map 1 has no " + GroundName);
            foreach (var camp in camps)
            {
                camp.position = new Vector3(camp.position.x, GroundAt(camp.position), camp.position.z);
                camp.rotation = Quaternion.identity; // Guard stations below assume the camp faces north.
            }
            LevelGround(ground, camps.Select(c => c.position).ToArray());
            Physics.SyncTransforms();
            ClearPlants(env, mission.transform, camps);
            for (int i = 0; i < 3; i++) if (anchors[i] != null) anchors[i].position = camps[i].position;

            BakeNavMesh(env.GetComponent<NavMeshSurface>());
            for (int i = 0; i < 3; i++)
            {
                var own = guards.Where(g => home[g] == i).ToArray();
                var pair = own.Where(g => g.name.StartsWith("Outpost guard ")).OrderBy(g => g.name, StringComparer.Ordinal).ToArray();
                for (int k = 0; k < pair.Length; k++)
                {
                    var loop = Enumerable.Range(0, 4).Select(j => OnMesh(camps[i].TransformPoint(Loop[(j + k * 2) % 4]))).ToArray();
                    Station(pair[k], loop[0], loop[1] - loop[0], loop);
                }
                // Any extra rifleman keeps the entrance, looking out.
                foreach (var sentry in own.Where(g => !g.name.StartsWith("Outpost guard ")))
                    Station(sentry, OnMesh(camps[i].TransformPoint(new Vector3(0, 0, -6.3f))), -camps[i].forward);
            }
            Validate(mission, camps, guards);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save Map 1.");
            Debug.Log("[Map1 camps] " + string.Join("  ", camps.Select(c => c.name + " " + c.position.ToString("F1"))));
        }

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private static float GroundAt(Vector3 p)
        {
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, 200, p.z), Vector3.down, 400, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (hit.collider.name == GroundName) return hit.point.y;
            throw new Exception("No ground under " + p);
        }

        /// <summary>
        /// Flattens the ground to each camp's centre height out to <see cref="Level"/> metres,
        /// blending back to the hills by <see cref="Blend"/>. Map 1 keeps its own copy of the hills
        /// mesh for this, so the shared generated asset is never rewritten.
        /// </summary>
        private static void LevelGround(MeshFilter ground, Vector3[] centres)
        {
            var mesh = ground.sharedMesh;
            if (AssetDatabase.GetAssetPath(mesh) != TerrainPath)
            {
                var copy = Object.Instantiate(mesh);
                copy.name = "Map 1 hills (camps levelled)";
                Directory.CreateDirectory(Path.GetDirectoryName(TerrainPath));
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(TerrainPath);
                if (existing != null) { EditorUtility.CopySerialized(copy, existing); Object.DestroyImmediate(copy); mesh = existing; }
                else { AssetDatabase.CreateAsset(copy, TerrainPath); mesh = copy; }
            }
            var vertices = mesh.vertices;
            var world = ground.transform;
            for (int v = 0; v < vertices.Length; v++)
            {
                var p = world.TransformPoint(vertices[v]);
                foreach (var c in centres)
                {
                    float d = Flat(p, c);
                    if (d >= Blend) continue;
                    p.y = Mathf.Lerp(c.y, p.y, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Level, Blend, d)));
                }
                vertices[v] = world.InverseTransformPoint(p);
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            ground.sharedMesh = mesh;
            var collider = ground.GetComponent<MeshCollider>();
            if (collider != null) { collider.sharedMesh = null; collider.sharedMesh = mesh; }
            EditorUtility.SetDirty(ground);
        }

        /// <summary>Plants and rocks standing in a camp go, and with them the bush hiding spots there.</summary>
        private static void ClearPlants(Transform env, Transform mission, Transform[] camps)
        {
            bool InCamp(Vector3 p) => camps.Any(c => Flat(p, c.position) < Clearing);
            var plants = env.GetComponentsInChildren<LODGroup>(true)
                .Where(g => !camps.Any(c => g.transform.IsChildOf(c)) && InCamp(g.transform.position))
                .Select(g => PrefabUtility.GetOutermostPrefabInstanceRoot(g.gameObject) ?? g.gameObject).Distinct().ToArray();
            foreach (var plant in plants) Object.DestroyImmediate(plant);
            var spots = mission.GetComponentsInChildren<ForestPoint>(true).Where(p => p.kind == ForestPointKind.Hide && InCamp(p.transform.position)).ToArray();
            foreach (var spot in spots) Object.DestroyImmediate(spot.gameObject);
            Debug.Log($"[Map1 camps] cleared {plants.Length} plants and {spots.Length} hiding spots");
        }

        private static void BakeNavMesh(NavMeshSurface surface)
        {
            if (surface == null) throw new Exception("Map 1's environment has no NavMeshSurface.");
            Physics.SyncTransforms();
            surface.RemoveData();
            surface.BuildNavMesh();
            Directory.CreateDirectory(Path.GetDirectoryName(NavMeshPath));
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
            if (existing != null) { EditorUtility.CopySerialized(surface.navMeshData, existing); surface.navMeshData = existing; EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            AssetDatabase.SaveAssets();
            surface.RemoveData(); surface.AddData();
            EditorUtility.SetDirty(surface);
        }

        private static Vector3 OnMesh(Vector3 p) =>
            NavMesh.SamplePosition(p, out var hit, 2.5f, NavMesh.AllAreas) ? hit.position : throw new Exception("No NavMesh near " + p);

        private static void Station(Map01EnemyController guard, Vector3 at, Vector3 facing, params Vector3[] patrol)
        {
            guard.transform.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.ProjectOnPlane(facing, Vector3.up).normalized));
            if (PrefabUtility.IsPartOfPrefabInstance(guard.transform)) PrefabUtility.RecordPrefabInstancePropertyModifications(guard.transform);
            var so = new SerializedObject(guard);
            var list = so.FindProperty("patrolPoints");
            list.arraySize = patrol.Length;
            for (int i = 0; i < patrol.Length; i++) list.GetArrayElementAtIndex(i).vector3Value = patrol[i];
            so.FindProperty("visionRange").floatValue = CampVision;
            so.FindProperty("attackRange").floatValue = CampAttack;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static float DistanceToRoute(Vector3 p, Vector3[] route)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < route.Length; i++)
            {
                Vector2 a = new Vector2(route[i].x, route[i].z), ab = new Vector2(route[i + 1].x, route[i + 1].z) - a, q = new Vector2(p.x, p.z);
                float t = ab.sqrMagnitude < 1e-4f ? 0 : Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
            }
            return best;
        }

        private static Vector3[] Route(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(OnMesh(from), OnMesh(to), NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new Exception($"No walking route {from} -> {to}");
            return path.corners;
        }

        private static void Validate(Map01Mission mission, Transform[] camps, Map01EnemyController[] guards)
        {
            var player = mission.GetComponentInChildren<CharacterController>(true).transform;
            var herb = mission.GetComponentsInChildren<ForestPoint>(true).First(p => p.id == "tutorial_loot").transform.position;
            var rescue = Route(player.position, herb).Concat(Route(herb, mission.hung.position)).ToArray();
            foreach (var camp in camps) Route(player.position, camp.position);
            foreach (var guard in guards)
                foreach (var spot in guard.PatrolPoints.Prepend(guard.transform.position))
                {
                    if (DistanceToRoute(spot, rescue) <= guard.VisionRange + 2)
                        throw new Exception($"{guard.name} would see the rescue route from {spot}");
                    Route(guard.transform.position, spot);
                }
        }
    }
}
