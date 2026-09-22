using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
 [InitializeOnLoad] public static class Map01LatestRevision
 {
  const string Root="Assets/_Project/Art/Environment/Map01_LatestRevision";
  const string Reports="Tools/Map01OptimizedReports";
  const string Request="Tools/Map01Latest.request";
  static Material palette;static PhysicsMaterial friction;
  static readonly Vector4[] OldHuts={new Vector4(-70,-49,7,5),new Vector4(-55,-57,6,4),new Vector4(-70,-62,6,5),new Vector4(46,39,10,7),new Vector4(34,33,6,5)};
  static readonly List<Bounds> houseBounds=new List<Bounds>();
  static Map01LatestRevision(){EditorApplication.update+=Poll;File.WriteAllText(Reports+"/latest.ready","2");}
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;string action=File.ReadAllText(Request).Trim();File.Delete(Request);try{if(action=="check")Check();else Apply();File.WriteAllText(Reports+"/latest.status","PASS "+DateTime.UtcNow.ToString("O"));}catch(Exception e){File.WriteAllText(Reports+"/latest.status","FAIL "+e);Debug.LogException(e);}}
  static OptimizedMapBuilder.SourceData Read(string path){using(var f=File.OpenRead(path))using(var z=new GZipStream(f,CompressionMode.Decompress))using(var r=new StreamReader(z))return JsonUtility.FromJson<OptimizedMapBuilder.SourceData>(r.ReadToEnd());}
  static float H(float x,float z){float river=TropicalRealismBuilder.RiverX(z);float hills=14*Mathf.Exp(-((x+57)*(x+57)/440+(z-48)*(z-48)/520))+7*Mathf.Exp(-((x-68)*(x-68)/650+(z-57)*(z-57)/700));float raw=2.7f+.7f*Mathf.Sin(x*.11f)*Mathf.Cos(z*.09f)+.4f*Mathf.Sin(z*.22f+x*.1f)+hills;return -.8f+(raw+.8f)*Mathf.Clamp01((Mathf.Abs(x-river)-3)/8);}
  static GameObject Child(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
  static Mesh Save(Mesh m,string name){string path=Root+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);m.name=name;if(old==null){AssetDatabase.CreateAsset(m,path);return m;}EditorUtility.CopySerialized(m,old);EditorUtility.SetDirty(old);Object.DestroyImmediate(m);return old;}
  static Mesh Import(OptimizedMapBuilder.MeshData d){var m=new Mesh{indexFormat=IndexFormat.UInt32};int n=d.vertices.Length/3;var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];for(int i=0;i<n;i++){v[i]=new Vector3(d.vertices[i*3],d.vertices[i*3+1],d.vertices[i*3+2]);normals[i]=new Vector3(d.normals[i*3],d.normals[i*3+1],d.normals[i*3+2]);colors[i]=new Color(d.colors[i*4],d.colors[i*4+1],d.colors[i*4+2],1);}m.vertices=v;m.normals=normals;m.colors=colors;m.triangles=d.triangles;m.RecalculateBounds();return Save(m,d.id);}
  static BoxCollider Box(Transform root,string name,Vector3 center,Vector3 size){var go=Child(name,root);go.layer=LayerMask.NameToLayer("Obstacle");var c=go.AddComponent<BoxCollider>();c.center=center;c.size=size;c.sharedMaterial=friction;return c;}
  static bool HutGeometry(Vector3 p){return OldHuts.Any(h=>Mathf.Abs(p.x-h.x)<h.z/2+1&&Mathf.Abs(p.z-h.y)<h.w/2+1.3f&&p.y>H(p.x,p.z)+.18f);}
  static bool RaisedGround(Vector3 p){float d=p.y-H(p.x,p.z);return d>.022f&&d<.17f;}
  static bool GroundPoint(Vector3 p)=>Mathf.Abs(p.y-H(p.x,p.z))<.018f;
  static bool ShelterWall(Vector3 p)=>p.x>47.35f&&p.x<54.65f&&Mathf.Abs(p.z-28.3f)<.17f&&p.y>H(51,26)+.18f;
  static GameObject HousePrefab(Mesh[] meshes)
  {
   var go=new GameObject("Thatched veranda house");var lods=new LOD[2];
   for(int i=0;i<2;i++){var child=Child("LOD"+i,go.transform);child.AddComponent<MeshFilter>().sharedMesh=meshes[i];var r=child.AddComponent<MeshRenderer>();r.sharedMaterial=palette;lods[i]=new LOD(i==0?.16f:.008f,new Renderer[]{r});}
   var lod=go.AddComponent<LODGroup>();lod.SetLODs(lods);lod.RecalculateBounds();go.AddComponent<ForestInstanceTint>();
   Box(go.transform,"Continuous floor",new Vector3(0,.105f,-.8f),new Vector3(6.4f,.21f,6.8f));
   Box(go.transform,"Back wall",new Vector3(0,1.6f,2.38f),new Vector3(6,2.8f,.18f));
   foreach(float side in new[]{-1f,1f}){Box(go.transform,"Side wall",new Vector3(side*2.92f,1.6f,.18f),new Vector3(.18f,2.8f,4.3f));Box(go.transform,"Door jamb",new Vector3(side*1.95f,1.6f,-2.05f),new Vector3(1.9f,2.8f,.18f));foreach(float z in new[]{-4f,-2f,2.35f})Box(go.transform,"Veranda post",new Vector3(side*2.9f,1.35f,z),new Vector3(.22f,2.7f,.22f));}
   // Three shallow approach steps; broad enough for the CharacterController.
   for(int i=0;i<3;i++)Box(go.transform,"Entry step",new Vector3(0,-.02f-i*.15f,-4.3f-i*.36f),new Vector3(1.9f,.16f,.40f));
   var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/ThatchHouse.prefab");Object.DestroyImmediate(go);return prefab;
  }
  static void PlaceHouse(GameObject prefab,Transform parent,string name,Vector3 position,Quaternion rotation,Vector3 scale)
  {
   var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.name=name;go.transform.SetPositionAndRotation(position,rotation);go.transform.localScale=scale;PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   houseBounds.Add(new Bounds(position+new Vector3(0,2,-.9f*scale.z),new Vector3(8.4f*scale.x,14,9.7f*scale.z)));
  }
  [MenuItem("ShadowVale/Map 1/Apply Latest Paths Houses and Collision")]
  public static void Apply()
  {
   var scene=EditorSceneManager.GetActiveScene();if(scene.path!=OptimizedMapBuilder.ScenePath)throw new Exception("Open Map 1 before applying this revision.");
   // Preserve both disk and live editor work before making an incremental revision.
   File.Copy(scene.path,Reports+"/Map1.before-latest.unity",true);if(scene.isDirty)EditorSceneManager.SaveScene(scene);
   Directory.CreateDirectory(Root);AssetDatabase.Refresh();houseBounds.Clear();var env=GameObject.Find("01 Environment • Blender optimized");if(env==null)throw new Exception("Missing environment");
   var source=Read("SourceArt/Map01_Optimized/Map01.meshdata.json.gz");palette=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Optimized/Materials/MapPalette.mat");
   friction=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Root+"/Slide.physicsMaterial");if(friction==null){friction=new PhysicsMaterial("Slide");AssetDatabase.CreateAsset(friction,Root+"/Slide.physicsMaterial");}friction.staticFriction=0;friction.dynamicFriction=0;friction.frictionCombine=PhysicsMaterialCombine.Minimum;EditorUtility.SetDirty(friction);
   var previous=env.transform.Find("Latest revision");if(previous!=null)Object.DestroyImmediate(previous.gameObject);var root=Child("Latest revision",env.transform);
   var samples=new List<Vector2>();foreach(var route in source.routes)for(int i=3;i<route.positions.Length;i+=3){var a=new Vector2(route.positions[i-3],route.positions[i-1]);var b=new Vector2(route.positions[i],route.positions[i+2]);int steps=Mathf.CeilToInt(Vector2.Distance(a,b)*2);for(int j=0;j<=steps;j++)samples.Add(Vector2.Lerp(a,b,j/(float)Mathf.Max(1,steps)));}
   int removed=0,painted=0;
   foreach(var filter in env.transform.Find("Ground & structures • 24m chunks").GetComponentsInChildren<MeshFilter>())
   {
    if(!filter.sharedMesh.name.Contains("opaque"))continue;
    var m=Object.Instantiate(filter.sharedMesh);var v=m.vertices;var colors=m.colors;var indices=m.triangles;var kept=new List<int>();
    for(int i=0;i<indices.Length;i+=3){var a=v[indices[i]];var b=v[indices[i+1]];var c=v[indices[i+2]];var p=(a+b+c)/3;bool terrain=GroundPoint(a)&&GroundPoint(b)&&GroundPoint(c);if(!terrain&&(HutGeometry(p)||(RaisedGround(a)&&RaisedGround(b)&&RaisedGround(c)))){removed++;continue;}kept.AddRange(new[]{indices[i],indices[i+1],indices[i+2]});}
    for(int i=0;i<v.Length;i++){if(!GroundPoint(v[i]))continue;var p=new Vector2(v[i].x,v[i].z);float d=samples.Min(s=>(s-p).sqrMagnitude);float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1.75f,Mathf.Sqrt(d)));if(blend<=0)continue;var color=new Color(.29f,.245f,.16f)*( .94f+.09f*Mathf.PerlinNoise(p.x,p.y));color.a=1;colors[i]=Color.Lerp(colors[i],color,blend);painted++;}
    m.colors=colors;m.triangles=kept.ToArray();m.RecalculateBounds();filter.sharedMesh=Save(m,filter.name+"_flush");
   }
   // Remove old hut walk surfaces and the problematic shelter-wall triangle shell.
   foreach(var col in env.transform.Find("Collision • simplified").GetComponentsInChildren<MeshCollider>().Where(c=>c.sharedMesh!=null))
   {
    var m=Object.Instantiate(col.sharedMesh);var v=m.vertices;var tris=m.triangles;var kept=new List<int>();for(int i=0;i<tris.Length;i+=3){var a=v[tris[i]];var b=v[tris[i+1]];var c=v[tris[i+2]];var p=(a+b+c)/3;if(!(GroundPoint(a)&&GroundPoint(b)&&GroundPoint(c))&&(HutGeometry(p)||ShelterWall(p)))continue;kept.AddRange(new[]{tris[i],tris[i+1],tris[i+2]});}m.triangles=kept.ToArray();m.RecalculateBounds();col.sharedMaterial=friction;col.sharedMesh=null;Map01CollisionMesh.Assign(col,Save(m,col.name+"_collision"));
   }
   foreach(var col in env.GetComponentsInChildren<BoxCollider>())
   {if(col.name.Contains("Map table")||col.name.Contains("Back wall of intelligence")||(HutGeometry(col.bounds.center)&&col.name.Contains("wall")))col.enabled=false;else col.sharedMaterial=friction;}
   float floor=H(51,26);Box(root.transform,"Map table solid",new Vector3(51,floor+.89f,26),new Vector3(3.34f,1.0f,1.94f));Box(root.transform,"Shelter wall smooth",new Vector3(51,floor+1.6f,28.3f),new Vector3(7.02f,3.02f,.20f));
   var houseData=Read("SourceArt/Map01_Optimized/House.meshdata.json.gz");var meshes=houseData.meshes.OrderBy(m=>m.id).Select(Import).ToArray();var prefab=HousePrefab(meshes);
   foreach(var house in env.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("House ")).ToArray())
   {var parent=house.parent;var pos=house.position;var rot=house.rotation;var scale=house.localScale;string name=house.name;Object.DestroyImmediate(house.gameObject);PlaceHouse(prefab,parent,name,pos,rot,scale);}
   foreach(var h in OldHuts)PlaceHouse(prefab,root.transform,"Village thatch "+h.x+" "+h.y,new Vector3(h.x,H(h.x,h.y)+.39f,h.y),Quaternion.identity,new Vector3(h.z/6f,1,h.w/4.8f));
   int grass=0,cleared=0;var grassRoot=env.transform.Find("Groundcover • shared prefabs");if(grassRoot==null)throw new Exception("Current groundcover root missing");
   // Update existing connected grass prefabs: short blades fill gaps between tall tufts.
   for(int variant=0;variant<4;variant++)
   {
    var original=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Environment/Map01_Optimized/Meshes/Prop_Grass"+variant+".asset");if(original==null)throw new Exception("Grass mesh missing");var lodMeshes=new Mesh[3];
    for(int level=0;level<3;level++)
    {
     var combos=new List<CombineInstance>();int count=level==0?3:level==1?2:1;
     for(int j=0;j<count;j++)combos.Add(new CombineInstance{mesh=original,transform=Matrix4x4.TRS(new Vector3(j==0?0:.15f*j,0,j==0?0:-.14f*j),Quaternion.Euler(0,j*137,0),new Vector3(j==0?1:1.06f,j==0?1.25f:j==1?.44f:.70f,j==0?1:1.06f))});
     var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(combos.ToArray(),true,true);m.RecalculateBounds();lodMeshes[level]=Save(m,"MixedGrass"+variant+"_LOD"+level);
    }
    string path="Assets/_Project/Art/Environment/Map01_Optimized/Prefabs/Prop_Grass"+variant+".prefab";var go=PrefabUtility.LoadPrefabContents(path);
    try{foreach(var child in go.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);var lods=new LOD[3];for(int level=0;level<3;level++){var child=Child("LOD"+level,go.transform);child.AddComponent<MeshFilter>().sharedMesh=lodMeshes[level];var r=child.AddComponent<MeshRenderer>();r.sharedMaterial=palette;r.shadowCastingMode=ShadowCastingMode.Off;lods[level]=new LOD(new[]{.085f,.04f,.018f}[level],new Renderer[]{r});}go.GetComponent<LODGroup>().SetLODs(lods);go.GetComponent<LODGroup>().RecalculateBounds();PrefabUtility.SaveAsPrefabAsset(go,path);}finally{PrefabUtility.UnloadPrefabContents(go);}
   }
   Physics.SyncTransforms();
   foreach(var patch in grassRoot.Cast<Transform>().ToArray())
   {
    var bounds=new Bounds(patch.position,new Vector3(3.9f*patch.localScale.x,1,3.9f*patch.localScale.z));if(houseBounds.Any(h=>h.Intersects(bounds))){Object.DestroyImmediate(patch.gameObject);cleared++;continue;}
    var hits=Physics.RaycastAll(patch.position+Vector3.up*30,Vector3.down,60,LayerMask.GetMask("Obstacle")).Where(h=>h.collider.name.StartsWith("Collision_Walk")||h.collider.name.StartsWith("Hill ")).OrderByDescending(h=>h.point.y).ToArray();if(hits.Length>0){patch.position=hits[0].point+Vector3.up*.005f;patch.rotation=Quaternion.FromToRotation(Vector3.up,hits[0].normal)*Quaternion.Euler(0,patch.eulerAngles.y,0);PrefabUtility.RecordPrefabInstancePropertyModifications(patch);}grass++;
   }
   // Other foliage systems also need to respect the larger veranda footprint.
   foreach(var plant in env.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Tall grass patch ")||t.name.StartsWith("Banana ")||t.name.StartsWith("Tree ")).ToArray())
   {
    var footprint=new Bounds(plant.position,new Vector3(plant.name.StartsWith("Tree ")?2:5,1,plant.name.StartsWith("Tree ")?2:5));
    if(!houseBounds.Any(h=>h.Intersects(footprint)))continue;
    if(plant.name.StartsWith("Tall grass patch ")){string index=plant.name.Substring("Tall grass patch ".Length);var hide=plant.parent.Find("Grass concealment "+index);if(hide!=null)Object.DestroyImmediate(hide.gameObject);}
    plant.gameObject.SetActive(false);if(PrefabUtility.IsPartOfPrefabInstance(plant))PrefabUtility.RecordPrefabInstancePropertyModifications(plant.gameObject);cleared++;
   }
   var controller=Object.FindFirstObjectByType<ForestMission>().player.GetComponent<CharacterController>();controller.skinWidth=.06f;controller.minMoveDistance=0;controller.enableOverlapRecovery=true;controller.sharedMaterial=friction;
   Physics.SyncTransforms();var surface=env.GetComponent<NavMeshSurface>();surface.BuildNavMesh();var nav=AssetDatabase.LoadAssetAtPath<NavMeshData>(Root+"/LatestNavMesh.asset");if(nav==null)AssetDatabase.CreateAsset(surface.navMeshData,Root+"/LatestNavMesh.asset");else{EditorUtility.CopySerialized(surface.navMeshData,nav);surface.RemoveData();surface.navMeshData=nav;surface.AddData();EditorUtility.SetDirty(nav);}
   OptimizedMapBuilder.Validate();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);Check();TropicalRealismBuilder.Capture();
   File.WriteAllText(Reports+"/latest-summary.txt",$"Flush terrain: {painted} painted vertices; {removed} raised/obsolete triangles removed.\nThatch houses: {houseBounds.Count}, 2 LODs, open veranda and layered roof.\nMixed grass: {grass} existing instances with 3 near tufts / 2 mid / 1 far; {cleared} obstructing patches removed.\nTable and shelter wall: smooth box collision; navigation and controller checks PASS.\n");
  }
  public static void Check()
  {
   var results=new List<string>();var go=new GameObject("Temporary controller verification");var cc=go.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.35f;cc.skinWidth=.06f;cc.stepOffset=.22f;
   Action<Vector3> place=p=>{cc.enabled=false;go.transform.position=p;cc.enabled=true;Physics.SyncTransforms();};float y=H(51,26)+.35f;
   try
   {
    place(new Vector3(51,y,23.3f));for(int i=0;i<90;i++)cc.Move(new Vector3(0,-.04f,.05f));if(go.transform.position.z>24.85f)throw new Exception("Controller crossed the table");results.Add("PASS table blocks forward walk");
    place(new Vector3(52,y,27.5f));for(int i=0;i<30;i++)cc.Move(new Vector3(0,-.04f,.05f));if(go.transform.position.z>28)throw new Exception("Controller crossed shelter wall");float x=go.transform.position.x;for(int i=0;i<45;i++)cc.Move(new Vector3(.045f,-.02f,.02f));if(go.transform.position.x-x<1.4f)throw new Exception("Controller stuck sliding along wall");float z=go.transform.position.z;for(int i=0;i<25;i++)cc.Move(new Vector3(0,0,-.04f));if(z-go.transform.position.z<.6f)throw new Exception("Controller stuck leaving wall");results.Add("PASS wall blocks, slides and releases controller");
    OptimizedMapBuilder.Validate();results.Add("PASS original routes");
   }
   finally{Object.DestroyImmediate(go);File.WriteAllLines(Reports+"/latest-checks.txt",results);}
  }
 }
}


