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
 [InitializeOnLoad] public static class Map01Sept22
 {
  const string Root="Assets/_Project/Art/Environment/Map01_Sept22", Reports="Tools/Map01OptimizedReports";
  static Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();static Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>();static Material mat;static PhysicsMaterial physics;
  static Map01Sept22(){EditorApplication.update+=Poll;File.WriteAllText(Reports+"/sept22.ready","1");}
  static void Poll(){const string request="Tools/Map01Sept22.request";if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;File.Delete(request);try{Apply();File.WriteAllText(Reports+"/sept22.status","PASS "+DateTime.UtcNow.ToString("O"));}catch(Exception e){File.WriteAllText(Reports+"/sept22.status","FAIL "+e);Debug.LogException(e);}}
  static GameObject Child(string name,Transform parent){var g=new GameObject(name);g.transform.SetParent(parent,false);return g;}
  static Vector3 V(float[] a,int i)=>new Vector3(a[i],a[i+1],a[i+2]);
  static float Height(float x,float z){float rx=8+12*Mathf.Sin(z*.041f)+4*Mathf.Sin(z*.105f);float hills=14*Mathf.Exp(-((x+57)*(x+57)/440+(z-48)*(z-48)/520))+7*Mathf.Exp(-((x-68)*(x-68)/650+(z-57)*(z-57)/700));float raw=2.7f+.7f*Mathf.Sin(x*.11f)*Mathf.Cos(z*.09f)+.4f*Mathf.Sin(z*.22f+x*.1f)+hills;return -.8f+(raw+.8f)*Mathf.Clamp01((Mathf.Abs(x-rx)-3)/8);}
  static float Ground(float x,float z){var hits=Physics.RaycastAll(new Vector3(x,50,z),Vector3.down,100).Where(h=>h.collider.name.StartsWith("Collision_Walk")||h.collider.name.StartsWith("Hill ")).OrderByDescending(h=>h.point.y).ToArray();return hits.Length>0?hits[0].point.y:Height(x,z);}
  static BoxCollider Box(Transform parent,string name,Vector3 center,Vector3 size){var g=Child(name,parent);g.layer=LayerMask.NameToLayer("Obstacle");var c=g.AddComponent<BoxCollider>();c.center=center;c.size=size;c.sharedMaterial=physics;return c;}
  static void Import()
  {
   Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/Prefabs");
   OptimizedMapBuilder.SourceData data;using(var f=File.OpenRead("SourceArt/Map01_Sept22/Sept22.meshdata.json.gz"))using(var zip=new GZipStream(f,CompressionMode.Decompress))using(var r=new StreamReader(zip))data=JsonUtility.FromJson<OptimizedMapBuilder.SourceData>(r.ReadToEnd());
   meshes.Clear();foreach(var src in data.meshes){string path=Root+"/Meshes/"+src.id+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=mesh==null;if(create)mesh=new Mesh{name=src.id};else mesh.Clear();mesh.indexFormat=IndexFormat.UInt32;int n=src.vertices.Length/3;var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];for(int i=0;i<n;i++){v[i]=V(src.vertices,i*3);normals[i]=V(src.normals,i*3);colors[i]=new Color(src.colors[i*4],src.colors[i*4+1],src.colors[i*4+2],1);}mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.triangles=src.triangles;mesh.RecalculateBounds();if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);meshes[src.id]=mesh;}
   mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Optimized/Materials/MapPalette.mat");physics=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/_Project/Art/Environment/Map01_LatestRevision/Slide.physicsMaterial");AssetDatabase.SaveAssets();
  }
  static GameObject Prefab(string id){if(prefabs.ContainsKey(id))return prefabs[id];var g=new GameObject(id);g.AddComponent<MeshFilter>().sharedMesh=meshes[id];g.AddComponent<MeshRenderer>().sharedMaterial=mat;g.AddComponent<ForestInstanceTint>();var p=PrefabUtility.SaveAsPrefabAsset(g,Root+"/Prefabs/"+id+".prefab");Object.DestroyImmediate(g);prefabs[id]=p;return p;}
  static GameObject Place(string id,Transform parent,Vector3 position,Vector3 scale){var g=(GameObject)PrefabUtility.InstantiatePrefab(Prefab(id),parent);g.transform.localPosition=position;g.transform.localScale=scale;PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);return g;}
  static void StripBridge(Mesh mesh)
  {
   var v=mesh.vertices;var indices=mesh.triangles;var keep=new List<int>(indices.Length);for(int i=0;i<indices.Length;i+=3){bool bridge=true;for(int j=0;j<3;j++){var p=v[indices[i+j]];bridge&=p.x>-4.5f&&p.x<19.7f&&Mathf.Abs(p.z)<2.35f&&Mathf.Abs(p.y-Height(p.x,p.z))>.11f;}if(!bridge){keep.Add(indices[i]);keep.Add(indices[i+1]);keep.Add(indices[i+2]);}}if(keep.Count!=indices.Length){mesh.triangles=keep.ToArray();EditorUtility.SetDirty(mesh);}
  }
  static void Ramp(Transform parent,Vector3 a,Vector3 b,float width,string name)
  {
   var middle=(a+b)*.5f;var direction=b-a;var g=Place("Ramp_Plank",parent,middle,new Vector3(width/1.85f,1,direction.magnitude));g.name=name;g.transform.localRotation=Quaternion.LookRotation(direction.normalized,Vector3.up);PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);var c=g.AddComponent<BoxCollider>();c.size=new Vector3(1.85f,.12f,1);g.layer=LayerMask.NameToLayer("Obstacle");c.sharedMaterial=physics;
  }
  static void Stairs(Transform root,Vector3 start,float rise,float width)
  {
   int count=Mathf.Max(2,Mathf.CeilToInt(rise/.18f));float step=rise/count;for(int j=0;j<count;j++){var p=start+new Vector3(0,-rise+(j+.5f)*step,-(count-j)*.32f);Place("Stair_Step",root,p,new Vector3(width/1.85f,step/.18f,1));}
   Ramp(root,start+new Vector3(0,-rise,-count*.32f-.2f),start+new Vector3(0,0,.1f),width,"Smooth stair collision");
  }
  static void Apply()
  {
   var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.isDirty||scene.path!=OptimizedMapBuilder.ScenePath)throw new Exception("Save Map 1 and leave Play Mode first.");
   File.Copy(scene.path,Reports+"/Map1_before_sept22.unity",true);
   // Keep the pulled LatestRevision houses, paths, grass and wall fixes intact.
   Import();prefabs.Clear();
   var env=GameObject.Find("01 Environment • Blender optimized");var surface=env.GetComponent<NavMeshSurface>();var mission=Object.FindFirstObjectByType<ForestMission>();
   int trees=0;foreach(var f in env.GetComponentsInChildren<MeshFilter>(true)){if(f.sharedMesh==null)continue;string n=f.sharedMesh.name;string id=n.StartsWith("Tree_")?"Forest_"+n.Substring(5):n;if(!meshes.ContainsKey(id)||!id.StartsWith("Forest_"))continue;f.sharedMesh=meshes[id];PrefabUtility.RecordPrefabInstancePropertyModifications(f);if(id.EndsWith("LOD0"))trees++;}
   // Update prefab definitions so new instances also use the fuller Blender canopy.
   for(int t=0;t<5;t++){string path="Assets/_Project/Art/Environment/Map01_Optimized/Prefabs/Tree_"+t+".prefab";var g=PrefabUtility.LoadPrefabContents(path);try{foreach(var f in g.GetComponentsInChildren<MeshFilter>()){int l=int.Parse(f.name.Replace("LOD",""));f.sharedMesh=meshes["Forest_"+t+"_LOD"+l];}var group=g.GetComponent<LODGroup>();var lod=group.GetLODs();for(int l=0;l<3;l++)lod[l].screenRelativeTransitionHeight=new[]{.25f,.095f,.014f}[l];group.SetLODs(lod);group.RecalculateBounds();PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}}
   var additions=env.transform.Find("04 Blender landmarks");if(additions!=null)Object.DestroyImmediate(additions.gameObject);var root=Child("04 Blender landmarks",env.transform).transform;
   // Remove the old bridge triangles, preserving the terrain and riverbed.
   foreach(var mesh in env.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null&&f.sharedMesh.name.StartsWith("Chunk_")&&f.sharedMesh.name.Contains("opaque")).Select(f=>f.sharedMesh).Distinct())StripBridge(mesh);
   foreach(var c in env.GetComponentsInChildren<MeshCollider>().Where(c=>c.sharedMesh!=null&&c.sharedMesh.name.StartsWith("Collision_"))){StripBridge(c.sharedMesh);var m=c.sharedMesh;c.sharedMesh=null;c.sharedMesh=m;}
   foreach(var c in env.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Solid Bridge handrail")).ToArray())Object.DestroyImmediate(c.gameObject);
   var bridge=Place("Bridge_New",root,new Vector3(7.65f,3.08f,0),Vector3.one);
   Box(bridge.transform,"Bridge deck",Vector3.zero,new Vector3(24,.18f,3.7f));
   foreach(float side in new[]{-1f,1f}){Box(bridge.transform,"Bridge railing solid",new Vector3(0,.67f,side*1.73f),new Vector3(24,1.3f,.18f));foreach(float x in new[]{-11.7f,-3.9f,3.9f,11.7f})Box(bridge.transform,"Bridge pile",new Vector3(x,-1.8f,side*1.73f),new Vector3(.32f,3.8f,.32f));}
   Ramp(root,new Vector3(-6,Ground(-6,0)+.08f,0),new Vector3(-4.2f,3.08f,0),3.4f,"West bridge approach");Ramp(root,new Vector3(19.5f,3.08f,0),new Vector3(22,Ground(22,0)+.08f,0),3.4f,"East bridge approach");
   int houses=0;foreach(var house in env.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("House ")||t.name.StartsWith("Village thatch ")).ToArray())
   {
    var old=house.Find("Stilts and stairs");if(old!=null)Object.DestroyImmediate(old.gameObject);
    float highest=-100;foreach(float x in new[]{-3f,0,3f})foreach(float z in new[]{-4f,0,2.4f}){var p=house.TransformPoint(new Vector3(x,0,z));highest=Mathf.Max(highest,Ground(p.x,p.z));}
    var hp=house.position;hp.y=highest+.35f;house.position=hp;PrefabUtility.RecordPrefabInstancePropertyModifications(house);
    var support=Child("Stilts and stairs",house).transform;
    foreach(float x in new[]{-2.9f,0,2.9f})foreach(float z in new[]{-4,0,2.35f}){var p=house.TransformPoint(new Vector3(x,0,z));float bottom=Ground(p.x,p.z)-.2f;float length=p.y-bottom;Place("House_Stilt",support,new Vector3(x,-length,z),new Vector3(1,length,1));Box(support,"Stilt collider",new Vector3(x,-length*.5f,z),new Vector3(.25f,length,.25f));}
    var front=house.TransformPoint(new Vector3(0,0,-4.2f));float lower=Ground(front.x,front.z-1.5f);Stairs(support,new Vector3(0,.2f,-4.1f),Mathf.Max(.4f,house.position.y+.2f-lower),1.8f);houses++;
   }
   Physics.SyncTransforms();float bx=-83,bz=-78;float baseY=Ground(bx,bz)+.10f;
   var bunker=Place("Bunker_Entrance",root,new Vector3(bx,baseY,bz),Vector3.one);
   Box(bunker.transform,"Bunker floor",new Vector3(0,.05f,0),new Vector3(3.1f,.18f,8));Box(bunker.transform,"Bunker ceiling",new Vector3(0,2.68f,0),new Vector3(3.4f,.28f,8.3f));Box(bunker.transform,"Bunker back",new Vector3(0,1.3f,-4),new Vector3(3.4f,2.6f,.25f));
   foreach(float side in new[]{-1f,1f})Box(bunker.transform,"Bunker wall",new Vector3(side*1.55f,1.3f,0),new Vector3(.22f,2.6f,8));
   Ramp(root,new Vector3(bx,baseY+.15f,bz+3.9f),new Vector3(bx,Ground(bx,bz+7)+.08f,bz+7),2.65f,"Bunker entrance ramp");
   foreach(var t in env.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Tree ")||t.name.StartsWith("Prop_Grass")).ToArray())if(Vector2.Distance(new Vector2(t.position.x,t.position.z),new Vector2(bx,bz))<8){t.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);}
   var spawn=Child("Spawn • underground base",root);spawn.transform.position=new Vector3(bx,baseY+.25f,bz-2.3f);mission.player.position=spawn.transform.position;mission.player.rotation=Quaternion.identity;mission.hung.position=new Vector3(bx+.65f,baseY+.25f,bz+.5f);
   foreach(var p in Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None))if(p.id=="supplies"){p.transform.position=new Vector3(bx+.85f,baseY+.45f,bz+1);var crate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/Map01_Optimized/Prefabs/Prop_Crate.prefab");var g=(GameObject)PrefabUtility.InstantiatePrefab(crate,root);g.transform.position=p.transform.position;g.transform.localScale=Vector3.one*.65f;PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);}
   var light=Child("Bunker warm lamp",bunker.transform).AddComponent<Light>();light.type=LightType.Point;light.range=6;light.intensity=1.7f;light.color=new Color(1,.69f,.36f);light.transform.localPosition=new Vector3(0,2.3f,-1);
   Physics.SyncTransforms();surface.BuildNavMesh();var nav=AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/_Project/Art/Environment/Map01_Optimized/Map01_NavMesh.asset");EditorUtility.CopySerialized(surface.navMeshData,nav);nav.name="Map01_NavMesh";surface.RemoveData();surface.navMeshData=nav;surface.AddData();EditorUtility.SetDirty(nav);
   AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);OptimizedMapBuilder.Validate();
   var lines=new List<string>();
   foreach(var pair in new[]{new[]{spawn.transform.position,new Vector3(-63,Ground(-63,-55)+.2f,-55)},new[]{new Vector3(-4,3.3f,0),new Vector3(19,3.3f,0)}}){var path=new NavMeshPath();bool a=NavMesh.SamplePosition(pair[0],out var p,.8f,NavMesh.AllAreas);bool b=NavMesh.SamplePosition(pair[1],out var q,.8f,NavMesh.AllAreas);bool ok=a&&b&&NavMesh.CalculatePath(p.position,q.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;lines.Add((ok?"PASS":"FAIL")+" landmark navigation "+pair[0]+" -> "+pair[1]);}
   var rails=bridge.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Bridge railing solid").ToArray();lines.Add(rails.Length==2?"PASS two continuous bridge rail colliders":"FAIL bridge rail colliders");
   File.WriteAllLines(Reports+"/sept22-checks.txt",lines);File.WriteAllText(Reports+"/sept22-summary.txt",trees+" dense tree instances; "+houses+" houses supported with Blender stilts/stairs; new timber bridge; bunker spawn.");
   TropicalRealismBuilder.Capture();if(lines.Any(l=>l.StartsWith("FAIL")))throw new Exception("Landmark checks failed; inspect sept22-checks.txt");
  }
 }
}


