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
 [InitializeOnLoad] public static class Map01Polish
 {
  const string Root="Assets/_Project/Art/Environment/Map01_Optimized";
  const string Reports="Tools/Map01OptimizedReports";
  static Map01Polish(){EditorApplication.update+=Poll;File.WriteAllText(Reports+"/polish.ready","1");}
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Tools/Map01Polish.request"))return;File.Delete("Tools/Map01Polish.request");try{Apply();File.WriteAllText(Reports+"/polish.status","PASS "+DateTime.UtcNow.ToString("O"));}catch(Exception e){File.WriteAllText(Reports+"/polish.status","FAIL "+e);Debug.LogException(e);}}
  static OptimizedMapBuilder.SourceData Read(string file){using(var f=File.OpenRead(file))using(var g=new GZipStream(f,CompressionMode.Decompress))using(var r=new StreamReader(g))return JsonUtility.FromJson<OptimizedMapBuilder.SourceData>(r.ReadToEnd());}
  static Vector3 V(float[] a,int i=0)=>new Vector3(a[i],a[i+1],a[i+2]);
  static Mesh SaveMesh(Mesh m,string name){string path=Root+"/Meshes/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){m.name=name;AssetDatabase.CreateAsset(m,path);return m;}EditorUtility.CopySerialized(m,old);old.name=name;EditorUtility.SetDirty(old);Object.DestroyImmediate(m);return old;}
  static float Height(float x,float y){float rx=8+12*Mathf.Sin(y*.041f)+4*Mathf.Sin(y*.105f);float hill=14*Mathf.Exp(-((x+57)*(x+57)/440+(y-48)*(y-48)/520))+7*Mathf.Exp(-((x-68)*(x-68)/650+(y-57)*(y-57)/700));float raw=2.7f+.7f*Mathf.Sin(x*.11f)*Mathf.Cos(y*.09f)+.4f*Mathf.Sin(y*.22f+x*.1f)+hill;return -.8f+(raw+.8f)*Mathf.Clamp01((Mathf.Abs(x-rx)-3)/8);}
  static void Box(Transform parent,string name,Vector3 center,Vector3 size,PhysicsMaterial physics){var go=new GameObject(name);go.transform.SetParent(parent,false);go.layer=LayerMask.NameToLayer("Obstacle");var c=go.AddComponent<BoxCollider>();c.center=center;c.size=size;c.sharedMaterial=physics;}
  public static void Apply()
  {
   ForestEnvironmentRevision.Apply();
   var env=GameObject.Find("01 Environment • Blender optimized");var data=Read("SourceArt/Map01_Optimized/Map01.meshdata.json.gz");
   // Paint paths into terrain vertices: no raised strip or separate path collision.
   var paths=new List<Vector2>();
   foreach(var route in data.routes.Where(r=>r.id!="base_flank"))
   {
    var points=Enumerable.Range(0,route.positions.Length/3).Select(i=>new Vector2(route.positions[i*3],route.positions[i*3+2])).ToArray();
    for(int i=0;i<points.Length-1;i++){var a=points[Mathf.Max(0,i-1)];var b=points[i];var c=points[i+1];var d=points[Mathf.Min(points.Length-1,i+2)];int steps=Mathf.Max(2,Mathf.CeilToInt(Vector2.Distance(b,c)*2));for(int j=0;j<steps;j++){float t=j/(float)steps;paths.Add(.5f*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));}}
   }
   foreach(var filter in env.transform.Find("Ground & structures • 24m chunks").GetComponentsInChildren<MeshFilter>())
   {
    var mesh=filter.sharedMesh;if(!mesh.name.EndsWith("opaque"))continue;var v=mesh.vertices;var colors=mesh.colors;
    for(int i=0;i<v.Length;i++)
    {
     var p=v[i];if(Mathf.Abs(p.y-Height(p.x,p.z))>.008f)continue;float distance=1000;
     foreach(var sample in paths)distance=Mathf.Min(distance,(sample-new Vector2(p.x,p.z)).sqrMagnitude);
     float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,1.8f,Mathf.Sqrt(distance)));
     foreach(var center in new[]{new Vector2(-62,-53),new Vector2(-27,-9),new Vector2(45,34),new Vector2(22,85)})blend=Mathf.Max(blend,(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,9,Vector2.Distance(new Vector2(p.x,p.z),center))))*.75f);
     var soil=new Color(.27f,.235f,.155f)*( .94f+.10f*Mathf.PerlinNoise(p.x*.8f,p.z*.8f));soil.a=1;colors[i]=Color.Lerp(colors[i],soil,blend);
    }
    mesh.colors=colors;EditorUtility.SetDirty(mesh);
   }
   string physicsPath=Root+"/Materials/MapSolid.physicsMaterial";var physics=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(physicsPath);
   if(physics==null){physics=new PhysicsMaterial("MapSolid");AssetDatabase.CreateAsset(physics,physicsPath);}physics.dynamicFriction=0;physics.staticFriction=0;physics.frictionCombine=PhysicsMaterialCombine.Minimum;EditorUtility.SetDirty(physics);
   foreach(var c in env.GetComponentsInChildren<Collider>())c.sharedMaterial=physics;
   // Authored wall boxes replace triangle seams; colliders follow open doorway.
   var houseData=Read("SourceArt/Map01_Optimized/House.meshdata.json.gz");var house=new GameObject("PalmHouse");var palette=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/MapPalette.mat");var lods=new LOD[2];
   for(int level=0;level<2;level++)
   {
    var src=houseData.meshes.Single(m=>m.id=="House_LOD"+level);int n=src.vertices.Length/3;var verts=new Vector3[n];var normals=new Vector3[n];var col=new Color[n];for(int i=0;i<n;i++){verts[i]=V(src.vertices,i*3);normals[i]=V(src.normals,i*3);col[i]=new Color(src.colors[i*4],src.colors[i*4+1],src.colors[i*4+2],1);}
    var mesh=new Mesh{indexFormat=IndexFormat.UInt32,vertices=verts,normals=normals,colors=col,triangles=src.triangles};mesh.RecalculateBounds();mesh=SaveMesh(mesh,"PalmHouse_LOD"+level);var visual=new GameObject("LOD"+level);visual.transform.SetParent(house.transform,false);visual.AddComponent<MeshFilter>().sharedMesh=mesh;var r=visual.AddComponent<MeshRenderer>();r.sharedMaterial=palette;lods[level]=new LOD(level==0?.14f:.006f,new Renderer[]{r});
   }
   var group=house.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();house.AddComponent<ForestInstanceTint>();
   Box(house.transform,"Floor",new Vector3(0,.1f,-.8f),new Vector3(6.4f,.2f,6.8f),physics);
   Box(house.transform,"Back wall",new Vector3(0,1.6f,2.38f),new Vector3(6,.18f+2.6f,.18f),physics);
   foreach(float side in new[]{-1f,1f}){Box(house.transform,"Side wall",new Vector3(side*2.92f,1.6f,.18f),new Vector3(.18f,2.8f,4.3f),physics);Box(house.transform,"Door side",new Vector3(side*1.95f,1.6f,-2.05f),new Vector3(1.9f,2.8f,.18f),physics);foreach(float z in new[]{-4,2.35f})Box(house.transform,"Post",new Vector3(side*2.9f,1.35f,z),new Vector3(.22f,2.7f,.22f),physics);}
   var prefab=PrefabUtility.SaveAsPrefabAsset(house,Root+"/Prefabs/PalmHouse.prefab");Object.DestroyImmediate(house);int houses=0;
   foreach(var old in env.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("House ")).ToArray())
   {var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,old.parent);go.name=old.name;go.transform.localPosition=old.localPosition;go.transform.localRotation=old.localRotation;go.transform.localScale=old.localScale;PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);Object.DestroyImmediate(old.gameObject);houses++;}
   // Dense near grass, shared half-density middle and sparse far meshes.
   for(int variant=0;variant<4;variant++)
   {
    var source=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/Prop_Grass"+variant+".asset");var meshes=new Mesh[3];
    for(int level=0;level<3;level++)
    {
     var m=new Mesh{indexFormat=IndexFormat.UInt32};if(level==0)m.CombineMeshes(new[]{new CombineInstance{mesh=source,transform=Matrix4x4.identity},new CombineInstance{mesh=source,transform=Matrix4x4.TRS(new Vector3(.12f,0,-.08f),Quaternion.Euler(0,137,0),new Vector3(.93f,1.05f,.93f))}},true,true);
     else {m.vertices=source.vertices;m.normals=source.normals;m.colors=source.colors;m.triangles=level==1?source.triangles:source.triangles.Take((source.triangles.Length/12)*3).ToArray();}m.RecalculateBounds();meshes[level]=SaveMesh(m,"DenseGrass_"+variant+"_LOD"+level);
    }
    string path=Root+"/Prefabs/Prop_Grass"+variant+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
    try{foreach(var old in root.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(old.gameObject);var glods=new LOD[3];for(int l=0;l<3;l++){var child=new GameObject("LOD"+l);child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=meshes[l];var r=child.AddComponent<MeshRenderer>();r.sharedMaterial=palette;r.shadowCastingMode=ShadowCastingMode.Off;glods[l]=new LOD(new[]{.09f,.04f,.018f}[l],new Renderer[]{r});}root.GetComponent<LODGroup>().SetLODs(glods);root.GetComponent<LODGroup>().RecalculateBounds();PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
   }
   var controller=Object.FindFirstObjectByType<ForestMission>().player.GetComponent<CharacterController>();controller.skinWidth=.06f;controller.stepOffset=.22f;controller.minMoveDistance=0;controller.enableOverlapRecovery=true;controller.sharedMaterial=physics;
   var surface=env.GetComponent<NavMeshSurface>();Physics.SyncTransforms();surface.BuildNavMesh();var nav=AssetDatabase.LoadAssetAtPath<NavMeshData>(Root+"/Map01_NavMesh.asset");EditorUtility.CopySerialized(surface.navMeshData,nav);nav.name="Map01_NavMesh";surface.RemoveData();surface.navMeshData=nav;surface.AddData();EditorUtility.SetDirty(nav);
   AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());OptimizedMapBuilder.Validate();TropicalRealismBuilder.Capture();
   File.WriteAllText(Reports+"/polish-summary.txt",houses+" palm houses as connected prefabs; ground-painted trails; dense grass: 4 prefabs x 3 LODs; solid fences, handrails and tables; zero-friction walls; wading speed 68%.");
   File.WriteAllText("Tools/Map01Natural.request","finish");
  }
 }
}

