using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;

namespace ShadowVale.Map01.Editor
{
    // Chunk vertices are already baked in Blender world coordinates.
    // Moving an individual chunk separates its renderer from the shared ground collider.
    [InitializeOnLoad]
    public static class Map01ChunkAlignment
    {
        const string Request = "Tools/Map01ChunkAlignment.request";
        const string Report = "Tools/Map01OptimizedReports/chunk-alignment.txt";
        static readonly string[] Names = { "Chunk_-1_-2_opaque", "Chunk_-1_-3_opaque" };
        static Map01ChunkAlignment() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { Repair(); } catch (Exception e) { File.WriteAllText(Report, "FAIL " + e); Debug.LogException(e); }
        }
        [MenuItem("ShadowVale/Map 1/Repair two displaced ground chunks")]
        public static void Repair()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != OptimizedMapBuilder.ScenePath || scene.isDirty || EditorApplication.isPlaying)
                throw new InvalidOperationException("Save Map 1 and leave Play Mode before repairing chunk alignment.");
            var chunks = Names.Select(n => GameObject.Find(n)?.GetComponent<MeshFilter>()).ToArray();
            if (chunks.Any(m => m == null || m.sharedMesh == null)) throw new InvalidOperationException("Both ground chunks must exist.");
            var lines = new List<string>();
            foreach (var chunk in chunks)
            {
                if (Quaternion.Angle(chunk.transform.rotation, Quaternion.identity) > .001f || (chunk.transform.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                    throw new InvalidOperationException("Unexpected chunk rotation/scale; refusing to change the terrain layout.");
                lines.Add(chunk.name + " before: " + chunk.transform.position.ToString("F4"));
            }
            File.Copy(scene.path, "Tools/Map01OptimizedReports/Map1_before_chunk_alignment.unity", true);
            Undo.RecordObjects(chunks.Select(c => c.transform).ToArray(), "Align Blender ground chunks");
            foreach (var chunk in chunks) chunk.transform.position = Vector3.zero;
            Physics.SyncTransforms();
            var edges = chunks.Select(c => c.sharedMesh.vertices
                .Where(v => Mathf.Abs(v.z + 48) < .0001f && v.x >= -24 && v.x <= 0)
                .Select(v => c.transform.TransformPoint(v)).Distinct().ToArray()).ToArray();
            float maxGap = 0;
            // All 25 terrain grid vertices along the complete 24 m shared edge must match.
            for (int x = -24; x <= 0; x++)
            {
                var a = edges[0].Where(v => Mathf.Abs(v.x - x) < .0001f).ToArray();
                var b = edges[1].Where(v => Mathf.Abs(v.x - x) < .0001f).ToArray();
                if (a.Length == 0 || b.Length == 0) throw new Exception("Missing shared edge vertex at x=" + x);
                maxGap = Mathf.Max(maxGap, a.Min(p => b.Min(q => Vector3.Distance(p, q))));
            }
            if (maxGap > .0001f) throw new Exception("Shared terrain edge still has a gap: " + maxGap);
            var ground = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                .Where(c => c.enabled && c.name.StartsWith("Collision_Walk") && c.sharedMesh != null && c.sharedMesh.triangles.Length > 0).ToArray();
            float maxColliderError = 0;
            for (int x = -24; x <= 0; x++)
            {
                var ray = new Ray(new Vector3(x, 40, -48), Vector3.down);
                var hits = new List<RaycastHit>();
                foreach (var collider in ground) if (collider.Raycast(ray, out var hit, 80)) hits.Add(hit);
                if (hits.Count == 0) throw new Exception("Ground collider absent at shared edge x=" + x);
                float error = edges[0].Where(v => Mathf.Abs(v.x-x)<.0001f).Min(v => hits.Min(hit => Mathf.Abs(hit.point.y-v.y)));
                maxColliderError = Mathf.Max(maxColliderError, error);
            }
            if (maxColliderError > .002f) throw new Exception("Ground collider differs from terrain: " + maxColliderError);
            var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null || surface.navMeshData == null) throw new Exception("Map NavMesh is missing.");
            // This map bakes navigation from physics, which was already at the correct origin.
            if (surface.useGeometry != UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders)
                throw new Exception("Unexpected NavMesh source; navigation needs an explicit rebake.");
            lines.Add("PASS: 25/25 shared edge vertices; maximum gap = " + maxGap.ToString("F6") + " m");
            lines.Add("PASS: 25/25 ground collider samples; maximum error = " + maxColliderError.ToString("F6") + " m");
            lines.Add("PASS: NavMesh uses unchanged PhysicsColliders; existing bake remains aligned.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
            lines.Add("PASS: saved " + scene.path + " at " + DateTime.UtcNow.ToString("O"));
            File.WriteAllLines(Report, lines);
            SceneView.RepaintAll();
        }
    }
}
