using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor {
 public static partial class Map02VillageBuilder {
  [InitializeOnLoadMethod] static void RegisterFieldTrees(){EditorApplication.update+=PollFieldTrees;}
  static void PollFieldTrees(){const string request="Tools/Map02FieldTrees.request";if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(request);try{AddFieldTrees();File.WriteAllText(Reports+"/field-trees.status","PASS");}catch(Exception e){File.WriteAllText(Reports+"/field-trees.status","FAIL "+e);Debug.LogException(e);}}
  [MenuItem("ShadowVale/Map 2/Add banyan and flamboyant along field roads")]
  public static void AddFieldTrees(){
   var scene=EditorSceneManager.GetActiveScene();if(scene.path!=ScenePath){if(scene.isDirty)throw new Exception("Save the other scene first.");scene=EditorSceneManager.OpenScene(ScenePath);}
   env=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 Environment")).transform;
   if(env.Find("Banyan and flamboyant along field roads")!=null)throw new Exception("Field trees already added.");
   Directory.CreateDirectory(Reports);EditorSceneManager.SaveScene(scene,Reports+"/Map2_before_field_trees.unity",true);
   var homes=env.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Stilt house")||t.name.StartsWith("Bamboo thatch house")).ToArray();PrepareRoundedLanes(homes);
   palette=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Botanical_Palette.mat");botanical=new Dictionary<string,Mesh>();foreach(var kind in new[]{"Banyan","Flamboyant"})for(int lod=0;lod<2;lod++)botanical[kind+"_LOD"+lod]=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Botanical_"+kind+"_LOD"+lod+".asset");
   var ground=env.Find("Continuous ground with meandering canal").GetComponent<MeshCollider>();
   var occupied=env.GetComponentsInChildren<LODGroup>().Where(g=>!g.name.Contains("Hedge")).Select(g=>g.transform.position).ToList();
   var blockers=env.GetComponentsInChildren<BoxCollider>().Where(c=>c.enabled).Select(c=>c.bounds).ToArray();var candidates=new List<Vector2>();
   rng=new System.Random(261004);
   for(float x=-101;x<103;x+=1.7f)for(float z=-91;z<92;z+=1.7f){var q=new Vector2(x+Rand(-.3f,.3f),z+Rand(-.3f,.3f));float d=RoundedRoad(q);if(d<2.8f||d>5.7f||!RoadsideDry(q))continue;if(q.x<Canal(q.y)&&q.y>-42&&q.y<69)continue;if(Mathf.Abs(q.x-Canal(q.y))<18)continue;candidates.Add(q);}
   for(int i=candidates.Count-1;i>0;i--){int j=rng.Next(i+1);var tmp=candidates[i];candidates[i]=candidates[j];candidates[j]=tmp;}
   var root=Child("Banyan and flamboyant along field roads",env).transform;var added=new List<Vector3>();int banyans=0,flamboyants=0;
   foreach(var q in candidates){if(added.Count>=16)break;var p=Rural(new Vector3(q.x,1,q.y));if(occupied.Any(v=>Vector2.Distance(new Vector2(v.x,v.z),new Vector2(p.x,p.z))<5.8f)||added.Any(v=>Vector3.Distance(v,p)<12)||blockers.Any(b=>b.SqrDistance(p+Vector3.up)<6.25f))continue;if(!ground.Raycast(new Ray(p+Vector3.up*5,Vector3.down),out var hit,10))continue;p.y=hit.point.y;
    bool red=added.Count%2==0;string species=red?"Flamboyant":"Banyan";var go=Child((red?"Roadside royal poinciana ":"Roadside banyan ")+(added.Count+1),root);go.transform.position=p;go.transform.rotation=Quaternion.Euler(0,Rand(0,360),0);go.transform.localScale=Vector3.one*Rand(.66f,.83f);BotanicalTree(go.transform,species);ColliderBox(go.transform,"Tree trunk",new Vector3(0,2,0),new Vector3(red?1.1f:1.5f,4,red?1.1f:1.5f));added.Add(p);if(red)flamboyants++;else banyans++;
   }
   if(added.Count<8)throw new Exception("Not enough safe field-road planting positions.");
   int cleared=0;foreach(var ins in env.GetComponentsInChildren<VillageRiceInstances>()){var keep=new List<Vector4>();var sizes=new List<float>();for(int i=0;i<ins.plants.Length;i++){var v=ins.plants[i];var p=ins.transform.TransformPoint(new Vector3(v.x,v.y,v.z));if(added.Any(t=>Vector2.Distance(new Vector2(t.x,t.z),new Vector2(p.x,p.z))<(ins.name.StartsWith("Connected")?2:1))){cleared++;continue;}keep.Add(v);sizes.Add(ins.sizes[i]);}if(keep.Count!=ins.plants.Length){ins.plants=keep.ToArray();ins.sizes=sizes.ToArray();ins.Rebuild();EditorUtility.SetDirty(ins);}}
   foreach(var old in env.GetComponents<NavMeshSurface>()){old.RemoveData();Object.DestroyImmediate(old);}Physics.SyncTransforms();var nav=env.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.overrideVoxelSize=true;nav.voxelSize=.2f;nav.BuildNavMesh();AssetDatabase.CreateAsset(nav.navMeshData,Root+"/FieldTrees_NavMesh.asset");
   var report=new List<string>{"Added banyans: "+banyans,"Added flamboyants: "+flamboyants,"Cleared crop/grass clumps around trunks: "+cleared};
   foreach(var target in homes.Select(h=>h.position-h.forward*9).Concat(new[]{Rural(new Vector3(35,1,-4)),Rural(new Vector3(65,1,-68))})){var path=new NavMeshPath();bool ok=NavMesh.SamplePosition(Rural(new Vector3(-64,1,11)),out var a,3,NavMesh.AllAreas)&&NavMesh.SamplePosition(target,out var b,3,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;report.Add((ok?"PASS":"FAIL")+" route "+target);}
   AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);File.WriteAllLines(Reports+"/field-trees-checks.txt",report);var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();Capture(camera,new Vector3(0,205,-130),Vector3.zero,112,"map02-field-trees-overview");Capture(camera,new Vector3(105,66,-85),new Vector3(49,1,-21),65,"map02-field-trees");Selection.activeGameObject=root.gameObject;SceneView.RepaintAll();if(report.Any(r=>r.StartsWith("FAIL")))throw new Exception("Navigation route failed.");
  }
 }
}
