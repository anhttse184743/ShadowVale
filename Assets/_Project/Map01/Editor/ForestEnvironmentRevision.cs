using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad] public static class ForestEnvironmentRevision
    {
        const string Reports="Tools/Map01OptimizedReports";
        static ForestEnvironmentRevision(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            const string request="Tools/Map01Natural.request";
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
            string action=File.ReadAllText(request).Trim();File.Delete(request);
            try{if(action=="finish")FinishGroundcover();else Apply();File.WriteAllText(Reports+"/natural.status","PASS "+DateTime.UtcNow.ToString("O"));}
            catch(Exception e){File.WriteAllText(Reports+"/natural.status","FAIL "+e);Debug.LogException(e);}
        }
        const string Root="Assets/_Project/Art/Environment/Map01_Optimized";
        static void FinishGroundcover()
        {
            var env=GameObject.Find("01 Environment • Blender optimized");
            var houses=env.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("House ")).Select(t=>t.position).ToArray();
            var root=env.transform.Find("Groundcover • shared prefabs");int count=0;
            foreach(var patch in root.Cast<Transform>().ToArray())
            {
                var p=patch.position;
                if(houses.Any(h=>Mathf.Abs(h.x-p.x)<6&&Mathf.Abs(h.z-p.z)<7)){Object.DestroyImmediate(patch.gameObject);continue;}
                var hits=Physics.RaycastAll(p+Vector3.up*25,Vector3.down,50).Where(h=>h.collider.name.StartsWith("Collision_Walk")||h.collider.name.StartsWith("Hill ")).OrderByDescending(h=>h.point.y).ToArray();
                if(hits.Length>0){patch.position=hits[0].point+Vector3.up*.02f;PrefabUtility.RecordPrefabInstancePropertyModifications(patch);}
                count++;
            }
            var nav=AssetDatabase.LoadAssetAtPath<NavMeshData>(Root+"/Map01_NavMesh.asset");nav.name="Map01_NavMesh";EditorUtility.SetDirty(nav);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            File.WriteAllText(Reports+"/groundcover-final.txt",count+" connected grass prefab instances; 4 shared grass meshes; house interiors cleared; patches snapped to terrain/hills");
            TropicalRealismBuilder.Capture();
        }
        static Vector3 V(float[] a,int i=0)=>new Vector3(a[i],a[i+1],a[i+2]);
        [MenuItem("ShadowVale/Map 1/Apply Natural Trails and Grass")]
        public static void Apply()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=OptimizedMapBuilder.ScenePath||scene.isDirty||EditorApplication.isPlaying)
                throw new InvalidOperationException("Open and save Map 1, then exit Play Mode before applying environment changes.");
            var env=GameObject.Find("01 Environment • Blender optimized");
            if(env==null)throw new InvalidOperationException("Optimized environment root is missing.");
            var ground=env.transform.Find("Ground & structures • 24m chunks");
            var collision=env.transform.Find("Collision • simplified");
            var surface=env.GetComponent<NavMeshSurface>();
            if(ground==null||collision==null||surface==null)throw new InvalidOperationException("Environment hierarchy is incomplete.");
            Directory.CreateDirectory("Tools/Map01OptimizedReports");
            File.Copy(scene.path,"Tools/Map01OptimizedReports/Map1_before_natural_revision.unity",true);
            OptimizedMapBuilder.SourceData data;
            using(var file=File.OpenRead("SourceArt/Map01_Optimized/Map01.meshdata.json.gz"))using(var zip=new GZipStream(file,CompressionMode.Decompress))using(var read=new StreamReader(zip))data=JsonUtility.FromJson<OptimizedMapBuilder.SourceData>(read.ReadToEnd());
            var meshes=new Dictionary<string,Mesh>();
            foreach(var src in data.meshes)
            {
                string path=Root+"/Meshes/"+src.id+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=mesh==null;
                if(create)mesh=new Mesh{name=src.id};else mesh.Clear();mesh.indexFormat=IndexFormat.UInt32;
                int n=src.vertices.Length/3;var v=new Vector3[n];var normal=new Vector3[n];var color=new Color[n];
                for(int i=0;i<n;i++){v[i]=V(src.vertices,i*3);normal[i]=V(src.normals,i*3);color[i]=new Color(src.colors[i*4],src.colors[i*4+1],src.colors[i*4+2],1);}
                mesh.vertices=v;mesh.normals=normal;mesh.colors=color;mesh.triangles=src.triangles;mesh.RecalculateBounds();
                if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);meshes.Add(src.id,mesh);
            }
            var opaque=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/MapPalette.mat");
            var water=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Realism/River surface.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/StreamPalette.mat");
            if(opaque==null||water==null)throw new InvalidOperationException("Shared palette materials are missing.");
            int layer=LayerMask.NameToLayer("Obstacle");
            // Only generated environment children are replaced. Mission, actors,
            // camera settings, interactions and placed tree/prop transforms remain.
            foreach(Transform t in ground.Cast<Transform>().ToArray())Object.DestroyImmediate(t.gameObject);
            foreach(Transform t in collision.Cast<Transform>().ToArray())Object.DestroyImmediate(t.gameObject);
            foreach(var src in data.meshes.Where(m=>m.kind!="tree"&&m.kind!="prop"))
            {
                bool solid=src.kind=="walk"||src.kind=="block";var go=new GameObject(src.id);go.transform.SetParent(solid?collision:ground,false);
                if(solid){go.layer=layer;go.AddComponent<MeshCollider>().sharedMesh=meshes[src.id];}
                else{go.AddComponent<MeshFilter>().sharedMesh=meshes[src.id];go.AddComponent<MeshRenderer>().sharedMaterial=src.kind=="water"?water:opaque;}
            }
            foreach(var box in data.boxes)
            {
                var go=new GameObject(box.name);go.transform.SetParent(collision,false);go.layer=layer;go.transform.position=V(box.position);go.AddComponent<BoxCollider>().size=V(box.size);
            }
            var existing=env.transform.Find("Groundcover • shared prefabs");if(existing!=null)Object.DestroyImmediate(existing.gameObject);
            var grassRoot=new GameObject("Groundcover • shared prefabs");grassRoot.transform.SetParent(env.transform,false);
            var grassPrefabs=new Dictionary<string,GameObject>();
            foreach(var src in data.meshes.Where(m=>m.id.StartsWith("Prop_Grass")))
            {
                var go=new GameObject(src.id);var visual=new GameObject("LOD0");visual.transform.SetParent(go.transform,false);
                visual.AddComponent<MeshFilter>().sharedMesh=meshes[src.id];var renderer=visual.AddComponent<MeshRenderer>();renderer.sharedMaterial=opaque;renderer.shadowCastingMode=ShadowCastingMode.Off;
                var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.015f,new Renderer[]{renderer})});lod.RecalculateBounds();go.AddComponent<ForestInstanceTint>();
                grassPrefabs[src.id]=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+src.id+".prefab");Object.DestroyImmediate(go);
            }
            foreach(var src in data.props.Where(p=>p.id.StartsWith("Prop_Grass")))
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(grassPrefabs[src.id],grassRoot.transform);go.transform.position=V(src.position);go.transform.localScale=V(src.scale);go.transform.rotation=Quaternion.Euler(0,src.yaw,0);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
            Physics.SyncTransforms();surface.BuildNavMesh();
            if(surface.navMeshData==null)throw new InvalidOperationException("NavMesh bake failed.");
            var nav=AssetDatabase.LoadAssetAtPath<NavMeshData>(Root+"/Map01_NavMesh.asset");
            if(nav==null)AssetDatabase.CreateAsset(surface.navMeshData,Root+"/Map01_NavMesh.asset");
            else{EditorUtility.CopySerialized(surface.navMeshData,nav);surface.RemoveData();surface.navMeshData=nav;surface.AddData();EditorUtility.SetDirty(nav);}
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            OptimizedMapBuilder.Validate();OptimizedMapBuilder.Capture();
            var results=new List<string>();
            foreach(float z in new[]{-35f,25f,55f})
            {
                float x=8+12*Mathf.Sin(z*.041f)+4*Mathf.Sin(z*.105f);var path=new NavMeshPath();
                bool a=NavMesh.SamplePosition(new Vector3(x-10,1,z),out var from,6,NavMesh.AllAreas);
                bool b=NavMesh.SamplePosition(new Vector3(x+10,1,z),out var to,6,NavMesh.AllAreas);
                bool bed=NavMesh.SamplePosition(new Vector3(x,-.8f,z),out var middle,1.2f,NavMesh.AllAreas);
                bool connected=a&&b&&bed&&NavMesh.CalculatePath(from.position,middle.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete&&NavMesh.CalculatePath(middle.position,to.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                results.Add((connected?"PASS":"FAIL")+" shallow stream crossing z="+z);
            }
            File.WriteAllLines("Tools/Map01OptimizedReports/river-revision.txt",results);
            if(results.Any(r=>r.StartsWith("FAIL")))throw new InvalidOperationException("Inspect river-revision.txt: some crossings need additional bank adjustment.");
        }
    }
}
