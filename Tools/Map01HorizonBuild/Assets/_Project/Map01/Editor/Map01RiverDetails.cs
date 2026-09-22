using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Object = UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static partial class Map01RiverDetails
    {
        const string Root = "Assets/_Project/Art/Environment/Map01_RiverDetails";
        const string Reports = "Tools/Map01OptimizedReports";
        static Map01RiverDetails() { EditorApplication.update += Poll; }
        static void Poll()
        {
            const string request = "Tools/Map01RiverDetails.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Apply(); File.WriteAllText(Reports + "/river-details.status", "PASS " + DateTime.UtcNow.ToString("O")); }
            catch (Exception e) { File.WriteAllText(Reports + "/river-details.status", "FAIL " + e); Debug.LogException(e); }
        }
        [MenuItem("ShadowVale/Map 1/Apply river and bunker details")]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != OptimizedMapBuilder.ScenePath) throw new InvalidOperationException("Open Map 1 before applying details.");
            if (scene.isDirty) { EditorSceneManager.SaveScene(scene, Reports + "/Map1_unsaved_before_river_details.unity", true); EditorSceneManager.SaveScene(scene); }
            Directory.CreateDirectory(Root);
            if (!File.Exists(Reports + "/Map1_before_river_details.unity")) File.Copy(scene.path, Reports + "/Map1_before_river_details.unity");
            OptimizedMapBuilder.SourceData data;
            using (var f = File.OpenRead("SourceArt/Map01_RiverDetails/RiverDetails.meshdata.json.gz"))
            using (var z = new GZipStream(f, CompressionMode.Decompress))
            using (var r = new StreamReader(z)) data = JsonUtility.FromJson<OptimizedMapBuilder.SourceData>(r.ReadToEnd());
            var meshes = new Dictionary<string, Mesh>();
            foreach (var s in data.meshes)
            {
                string path = Root + "/" + s.id + ".asset";
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(path); bool fresh = m == null;
                if (fresh) m = new Mesh(); else m.Clear();
                m.name = s.id; m.indexFormat = IndexFormat.UInt32;
                int n = s.vertices.Length / 3; var v = new Vector3[n]; var normals = new Vector3[n]; var c = new Color[n];
                for (int i = 0; i < n; i++) { v[i] = new Vector3(s.vertices[i*3],s.vertices[i*3+1],s.vertices[i*3+2]); normals[i] = new Vector3(s.normals[i*3],s.normals[i*3+1],s.normals[i*3+2]); c[i] = new Color(s.colors[i*4],s.colors[i*4+1],s.colors[i*4+2],1); }
                m.vertices = v; m.normals = normals; m.colors = c; m.triangles = s.triangles; m.RecalculateBounds();
                if (fresh) AssetDatabase.CreateAsset(m,path); else EditorUtility.SetDirty(m);
                meshes.Add(s.id,m);
            }
            var env = GameObject.Find("01 Environment • Blender optimized");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Optimized/Materials/MapPalette.mat");
            var old = env.transform.Find("05 River details"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("05 River details"); root.transform.SetParent(env.transform,false);
            var boat = new GameObject("Boat • separate floating hull"); boat.transform.SetParent(root.transform,false);
            boat.AddComponent<MeshFilter>().sharedMesh = meshes["Boat_Floating"];
            boat.AddComponent<MeshRenderer>().sharedMaterial = material;
            boat.AddComponent<ForestInstanceTint>();
            // Surface vertices, rather than the riverbed collider, determine the waterline.
            float zBoat = -10; float xBoat = TropicalRealismBuilder.RiverX(zBoat);
            var waters = env.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null && f.sharedMesh.name.Contains("water")).ToArray();
            var waterVertices = waters.SelectMany(f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v))).ToArray();
            if (waterVertices.Length == 0) throw new InvalidOperationException("No river surface found.");
            float waterY = waterVertices.OrderBy(v => (new Vector2(v.x-xBoat,v.z-zBoat)).sqrMagnitude).First().y;
            boat.transform.position = new Vector3(xBoat, waterY + .12f, zBoat);
            boat.transform.rotation = Quaternion.Euler(0,30,0);
            // Simple solid hull prevents walking through it without adding navigation onto a floating prop.
            var hull = boat.AddComponent<BoxCollider>(); hull.center = new Vector3(0,.1f,0); hull.size = new Vector3(1.9f,.65f,6.6f);
            boat.layer = LayerMask.NameToLayer("Obstacle");
            int rocks = 0;
            foreach (var f in env.GetComponentsInChildren<MeshFilter>())
            {
                if (f.sharedMesh == null || !(f.sharedMesh.name == "Prop_Rock" || f.sharedMesh.name.StartsWith("Rock_Moss_"))) continue;
                var before = f.sharedMesh.bounds; var replacement = meshes["Rock_Moss_" + rocks%3];
                // Preserve old silhouette extents and world-space centre even for scaled prefab instances.
                var center = f.transform.TransformPoint(before.center); var a = before.size; var b = replacement.bounds.size;
                f.transform.localScale = Vector3.Scale(f.transform.localScale,new Vector3(a.x/b.x,a.y/b.y,a.z/b.z));
                f.sharedMesh = replacement; f.transform.position += center - f.transform.TransformPoint(replacement.bounds.center);
                PrefabUtility.RecordPrefabInstancePropertyModifications(f); PrefabUtility.RecordPrefabInstancePropertyModifications(f.transform); rocks++;
            }
            var bunker = GameObject.Find("Bunker_Entrance");
            if (bunker == null) throw new InvalidOperationException("Existing bunker not found.");
            // Clear complete foliage patches from the room and trench, including their concealment triggers.
            foreach (var plant in env.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Tall grass patch ") || t.name.StartsWith("Grass concealment ") || t.name.StartsWith("Banana ") || t.name.StartsWith("Prop_Grass")).ToArray())
            {
                var local = bunker.transform.InverseTransformPoint(plant.position);
                if (Mathf.Abs(local.x) < 4.5f && local.z > -6 && local.z < 10) { plant.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(plant.gameObject); }
            }
            var filter = bunker.GetComponent<MeshFilter>(); filter.sharedMesh = meshes["Bunker_Buried"]; PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            var previous = bunker.transform.Find("Earth mound collision"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var earth = new GameObject("Earth mound collision"); earth.transform.SetParent(bunker.transform,false); earth.layer = LayerMask.NameToLayer("Obstacle");
            earth.AddComponent<MeshCollider>().sharedMesh = meshes["Bunker_Buried"];
            // Thick solid backing overlaps the rear wall and extends below ground.
            // This also blocks entry if a controller ever reaches the inside of the earth shell.
            var backing = bunker.transform.Find("Rear solid collision");
            if (backing == null) { var go = new GameObject("Rear solid collision"); go.transform.SetParent(bunker.transform,false); backing = go.transform; }
            backing.gameObject.layer = LayerMask.NameToLayer("Obstacle");
            var rear = backing.GetComponent<BoxCollider>(); if (rear == null) rear = backing.gameObject.AddComponent<BoxCollider>();
            rear.center = new Vector3(0,.6f,-4.3f); rear.size = new Vector3(3.5f,5.2f,.65f); rear.isTrigger = false;
            Physics.SyncTransforms();
            FitMoundAndDress(bunker.transform, meshes["Bunker_Mound"], material);
            RepairNorthernJetty(env, root.transform, meshes, material, waterVertices);
            CheckRearCollision(bunker.transform);
            var surface = env.GetComponent<NavMeshSurface>(); surface.BuildNavMesh();
            var nav = AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/_Project/Art/Environment/Map01_Optimized/Map01_NavMesh.asset");
            EditorUtility.CopySerialized(surface.navMeshData,nav); surface.RemoveData(); surface.navMeshData=nav; surface.AddData(); EditorUtility.SetDirty(nav);
            CheckNorthernNavigation();
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            var mission = Object.FindFirstObjectByType<ForestMission>(); var pathCheck = new NavMeshPath();
            bool start = NavMesh.SamplePosition(mission.player.position,out var p,1,NavMesh.AllAreas);
            bool end = NavMesh.SamplePosition(bunker.transform.position+new Vector3(0,.2f,8),out var q,2,NavMesh.AllAreas);
            bool route = start && end && NavMesh.CalculatePath(p.position,q.position,NavMesh.AllAreas,pathCheck) && pathCheck.status == NavMeshPathStatus.PathComplete;
            File.WriteAllText(Reports+"/river-details-checks.txt", $"Boat waterline: {waterY}; origin: {boat.transform.position}; keel below surface, gunwale above.\nDetailed rock instances: {rocks}\nBunker exit navigation: {route}\n");
            Capture(boat.transform.position+new Vector3(9,7,-10),boat.transform.position,"boat-details");
            Capture(bunker.transform.position+new Vector3(0,5,10),bunker.transform.position+new Vector3(0,1,2),"bunker-details");
            Capture(bunker.transform.position+new Vector3(0,4,-11),bunker.transform.position+new Vector3(0,1,-3),"bunker-rear-details");
            var rock = env.GetComponentsInChildren<MeshFilter>().First(f=> f.sharedMesh!=null && f.sharedMesh.name.StartsWith("Rock_Moss_"));
            Capture(rock.transform.position+new Vector3(3,2,-3),rock.transform.position,"rock-details");
            if (!route || rocks == 0) throw new InvalidOperationException("Details validation failed; inspect river-details-checks.txt.");
        }
        static void CheckRearCollision(Transform bunker)
        {
            var source = Object.FindFirstObjectByType<ForestMission>().player.GetComponent<CharacterController>();
            var probe = new GameObject("Temporary bunker collision probe");
            probe.layer = source.gameObject.layer;
            var controller = probe.AddComponent<CharacterController>();
            controller.radius=source.radius; controller.height=source.height; controller.center=source.center;
            controller.skinWidth=source.skinWidth; controller.stepOffset=source.stepOffset; controller.slopeLimit=source.slopeLimit;
            var results = new List<string>();
            try
            {
                foreach (float x in new[]{-1f,0f,1f})
                {
                    controller.enabled=false;
                    probe.transform.position=bunker.TransformPoint(new Vector3(x,.2f,-5.4f));
                    controller.enabled=true; Physics.SyncTransforms();
                    bool blocked=false;
                    for(int i=0;i<100;i++) blocked |= (controller.Move(bunker.forward*.035f)&CollisionFlags.Sides)!=0;
                    var local=bunker.InverseTransformPoint(probe.transform.position);
                    if(!blocked || local.z > -4.5f) throw new InvalidOperationException("Rear bunker collision failed at " + local);
                    results.Add("PASS CharacterController blocked from rear at x="+x+", final z="+local.z);
                }
                if (Physics.GetIgnoreLayerCollision(source.gameObject.layer,LayerMask.NameToLayer("Obstacle"))) throw new InvalidOperationException("Player/Obstacle collision is disabled.");
                results.Add("PASS Player/Obstacle layers collide; rear wall is solid, not a trigger");
                File.WriteAllLines(Reports+"/bunker-rear-collision.txt",results);
            }
            finally { Object.DestroyImmediate(probe); }
        }
        static void Capture(Vector3 position, Vector3 target, string name)
        {
            var go = new GameObject("Details review camera"); var camera = go.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.enabled=false; camera.orthographic=false; camera.fieldOfView=55;
            var rt = new RenderTexture(1280,900,24); var image = new Texture2D(1280,900,TextureFormat.RGB24,false); var previous=RenderTexture.active;
            try { camera.transform.position=position; camera.transform.LookAt(target); camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,1280,900),0,0); image.Apply(); File.WriteAllBytes(Reports+"/"+name+".png",image.EncodeToPNG()); }
            finally { RenderTexture.active=previous; camera.targetTexture=null; Object.DestroyImmediate(image); Object.DestroyImmediate(rt); Object.DestroyImmediate(go); }
        }
    }
}



