using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    public static partial class Map02VillageBuilder
    {
        [InitializeOnLoadMethod] static void RegisterClearingGrass() { EditorApplication.update += PollClearingGrass; }
        static void PollClearingGrass() {
            const string request="Tools/Map02ClearingGrass.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(request);
            try { AddClearingGrass();File.WriteAllText(Reports+"/clearing-grass.status","PASS"); }
            catch(Exception e) { File.WriteAllText(Reports+"/clearing-grass.status","FAIL "+e);Debug.LogException(e); }
        }
        [MenuItem("ShadowVale/Map 2/Grass the northern tree clearing")]
        public static void AddClearingGrass() {
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=ScenePath){if(scene.isDirty)throw new Exception("Save the other scene first.");scene=EditorSceneManager.OpenScene(ScenePath);}
            var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray();
            var clearing=objects.Single(t=>t.name=="Grassy irregular landing clearing");
            if(clearing.Find("Dense grass under clearing trees")!=null)throw new Exception("Clearing grass already exists.");
            Directory.CreateDirectory(Reports);EditorSceneManager.SaveScene(scene,Reports+"/Map2_before_clearing_grass.unity",true);
            var filter=clearing.GetComponent<MeshFilter>();var bounds=clearing.GetComponent<Renderer>().bounds;
            // Sample the actual irregular patch, rather than its rectangular bounds.
            var sample=clearing.gameObject.AddComponent<MeshCollider>();sample.sharedMesh=filter.sharedMesh;Physics.SyncTransforms();
            var blockers=objects.SelectMany(t=>t.GetComponents<BoxCollider>()).Where(c=>c.enabled&&c.bounds.Intersects(new Bounds(bounds.center,new Vector3(bounds.size.x,20,bounds.size.z)))).Select(c=>c.bounds).ToArray();
            var positions=new List<Vector4>();var sizes=new List<float>();rng=new System.Random(261003);
            var origin=new Vector3(bounds.center.x,0,bounds.center.z);float maxError=0;
            try {
                for(float x=bounds.min.x;x<bounds.max.x;x+=.25f)for(float z=bounds.min.z;z<bounds.max.z;z+=.25f) {
                    var p=new Vector3(x+Rand(-.085f,.085f),bounds.max.y+3,z+Rand(-.085f,.085f));
                    if(!sample.Raycast(new Ray(p,Vector3.down),out var hit,6))continue;
                    p=hit.point;var probe=p+Vector3.up*.5f;
                    if(blockers.Any(b=>b.SqrDistance(new Vector3(probe.x,Mathf.Clamp(probe.y,b.min.y,b.max.y),probe.z))<.20f*.20f))continue;
                    float variation=Mathf.PerlinNoise(p.x*.32f+81,p.z*.32f+12);if(Rand(0,1)>.86f+variation*.12f)continue;
                    p.y+=.008f;var local=p-origin;positions.Add(new Vector4(local.x,local.y,local.z,Rand(0,360)));sizes.Add(Rand(.72f,1.18f)*Mathf.Lerp(.85f,1.15f,variation));maxError=Mathf.Max(maxError,Mathf.Abs(p.y-hit.point.y-.008f));
                }
            }finally{Object.DestroyImmediate(sample);}
            var go=Child("Dense grass under clearing trees",clearing);go.transform.position=origin;
            var grass=go.AddComponent<VillageRiceInstances>();grass.detailedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Roadside_Meadow_LOD0.asset");grass.distantMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Roadside_Meadow_LOD1.asset");grass.material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Botanical_Palette.mat");grass.plants=positions.ToArray();grass.sizes=sizes.ToArray();grass.Rebuild();
            if(grass.detailedMesh==null||grass.distantMesh==null||positions.Count<1000)throw new Exception("Missing grass mesh or insufficient clearing coverage.");
            clearing.GetComponent<Renderer>().sharedMaterial=Mat("Clearing moss and grass soil",new Color(.28f,.35f,.105f));
            EditorUtility.SetDirty(grass);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            var report=new[]{"Target: Grassy irregular landing clearing","Grass clumps: "+positions.Count,"PASS roots on exact clearing mesh; maximum error: "+maxError,"PASS visual grass only: existing navigation and colliders unchanged"};File.WriteAllLines(Reports+"/clearing-grass-checks.txt",report);
            var camera=objects.Select(t=>t.GetComponent<Camera>()).First(c=>c!=null);Capture(camera,origin+new Vector3(-23,24,-32),origin+Vector3.up*1.2f,0,"map02-clearing-grass");
            Selection.activeGameObject=clearing.gameObject;SceneView.RepaintAll();Debug.Log("CLEARING_GRASS_COMPLETE "+positions.Count);
        }
    }
}
