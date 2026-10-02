using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor {
 public static partial class Map02VillageBuilder {
  [InitializeOnLoadMethod] static void RegisterPhysicalRiver(){EditorApplication.update+=PollPhysicalRiver;}
  static void PollPhysicalRiver(){const string req="Tools/Map02PhysicalRiverV2.request";if(!File.Exists(req)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(req);try{ApplyPhysicalRiver();File.WriteAllText(Reports+"/physical-river.status","PASS");}catch(Exception e){File.WriteAllText(Reports+"/physical-river.status","FAIL "+e);Debug.LogException(e);}}
  [MenuItem("ShadowVale/Map 2/Apply physical river")]
  public static void ApplyPhysicalRiver(){
   var scene=EditorSceneManager.GetActiveScene();if(scene.path!=ScenePath){if(scene.isDirty)throw new Exception("Save the active scene first.");scene=EditorSceneManager.OpenScene(ScenePath);}
   env=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 Environment")).transform;
   var water=env.Find("Winding canal water");if(water.GetComponent<RiverWater>()!=null){if(env.GetComponentsInChildren<RiverBuoyantBody>().Length>0)throw new Exception("Physical river already applied.");Object.DestroyImmediate(water.GetComponent<RiverWater>());var partial=env.Find("Physical river interaction volumes");if(partial!=null)Object.DestroyImmediate(partial.gameObject);}
   var shader=Shader.Find("ShadowVale/Map 2 Physical River");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Water shader has errors.");
   var report=CheckRiverPhysics();Directory.CreateDirectory(Reports);if(!File.Exists(Reports+"/Map2_before_physical_river.unity"))EditorSceneManager.SaveScene(scene,Reports+"/Map2_before_physical_river.unity",true);
   var river=water.gameObject.AddComponent<RiverWater>();river.leftBank=new Vector3[201];river.rightBank=new Vector3[201];
   for(int i=0;i<=200;i++){float z=i-100;river.leftBank[i]=Rural(new Vector3(Canal(z)-5.6f,.045f,z));river.rightBank[i]=Rural(new Vector3(Canal(z)+5.6f,.045f,z));}
   // Visual overlap is hidden by the opaque bank; retain the existing physics channel.
   var mesh=RiverShorelineMesh.Build(river.leftBank,river.rightBank,water.worldToLocalMatrix);
   water.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,"PhysicalRiver_Surface");
   var material=new Material(shader);AssetDatabase.CreateAsset(material,Root+"/PhysicalRiver.mat");river.waterRenderer=water.GetComponent<Renderer>();river.waterRenderer.sharedMaterial=material;river.waterRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;river.UpdateAppearance();
   var sensors=Child("Physical river interaction volumes",env).transform;
   for(int i=0;i<200;i+=4){var a=(river.leftBank[i]+river.rightBank[i])*.5f;var b=(river.leftBank[i+4]+river.rightBank[i+4])*.5f;var go=Child("Water sensor "+i,sensors);go.layer=4;go.transform.position=(a+b)*.5f-Vector3.up*.9f;go.transform.rotation=Quaternion.LookRotation(b-a);var trigger=go.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(14,3.2f,Vector3.Distance(a,b)+1);go.AddComponent<RiverWaterTrigger>().river=river;}
   int boats=0;foreach(var boat in env.GetComponentsInChildren<Transform>().Where(t=>t.name=="Canal boat").ToArray()){
    var body=boat.gameObject.GetComponent<Rigidbody>();if(body==null)body=boat.gameObject.AddComponent<Rigidbody>();body.mass=100;body.linearDamping=.12f;body.angularDamping=.8f;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
    var collider=boat.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,.05f,0);collider.size=new Vector3(1.9f,.5f,6.6f);
    var buoy=boat.gameObject.AddComponent<RiverBuoyantBody>();buoy.river=river;buoy.floatPoints=new[]{new Vector3(-.65f,-.23f,-2.3f),new Vector3(.65f,-.23f,-2.3f),new Vector3(-.65f,-.23f,2.3f),new Vector3(.65f,-.23f,2.3f)};buoy.immersionDepth=.42f;buoy.displacedVolume=.20f;buoy.moored=true;buoy.mooringPoint=boat.position;body.centerOfMass=new Vector3(0,-.15f,0);boats++;
   }
   foreach(var camera in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>())){var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;}
   EditorUtility.SetDirty(river);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);report.Add("PASS saved physical river, "+boats+" moored boats, 50 interaction volumes");
   File.WriteAllLines(Reports+"/physical-river-checks.txt",report);
   CapturePhysicalRiver(scene,river);
   if(ShaderUtil.ShaderHasError(shader))throw new Exception("Shader failed after rendering.");Selection.activeGameObject=water.gameObject;
  }
  static void CapturePhysicalRiver(Scene scene,RiverWater river){
   var source=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();var go=new GameObject("Temporary water review camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().requiresColorTexture=true;
   var target=Rural(new Vector3(Canal(25),.045f,25));camera.transform.position=target+new Vector3(18,15,-27);camera.transform.LookAt(target);camera.orthographic=false;camera.fieldOfView=55;var texture=new RenderTexture(1600,1000,24);var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);var previous=RenderTexture.active;
   try{river.UpdateAppearance();camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(Reports+"/map02-physical-river.png",image.EncodeToPNG());}finally{RenderTexture.active=previous;Object.DestroyImmediate(go);Object.DestroyImmediate(texture);Object.DestroyImmediate(image);}
  }
  static List<string> CheckRiverPhysics(){
   var report=new List<string>();var testScene=EditorSceneManager.NewPreviewScene();
   try{
    var go=new GameObject("Test water");SceneManager.MoveGameObjectToScene(go,testScene);var river=go.AddComponent<RiverWater>();river.leftBank=new[]{new Vector3(-5,0,-50),new Vector3(-5,0,0),new Vector3(-5,0,50)};river.rightBank=new[]{new Vector3(5,0,-50),new Vector3(5,0,0),new Vector3(5,0,50)};river.waveAmplitude=0;
    if(!river.TrySample(Vector3.zero,0,out _)||river.TrySample(new Vector3(6,0,0),0,out _)||river.TrySample(new Vector3(0,0,51),0,out _))throw new Exception("River bounds check failed.");report.Add("PASS wet channel / dry bank / end cap sampling");
    var box=new GameObject("Floating test body");SceneManager.MoveGameObjectToScene(box,testScene);box.transform.position=new Vector3(0,.3f,0);box.AddComponent<BoxCollider>();var body=box.AddComponent<Rigidbody>();body.mass=100;var buoy=box.AddComponent<RiverBuoyantBody>();buoy.river=river;buoy.displacedVolume=.2f;buoy.immersionDepth=.5f;buoy.floatPoints=new[]{new Vector3(-.5f,-.25f,-.5f),new Vector3(.5f,-.25f,-.5f),new Vector3(-.5f,-.25f,.5f),new Vector3(.5f,-.25f,.5f)};var physics=testScene.GetPhysicsScene();if(physics==Physics.defaultPhysicsScene)throw new Exception("Validation must use isolated physics.");
    for(int i=0;i<600;i++){buoy.Step(.02f,i*.02f);physics.Simulate(.02f);}
    if(Mathf.Abs(body.position.y)>.15f||body.position.z>-.5f||float.IsNaN(body.position.y))throw new Exception("Buoyancy/current test failed: "+body.position);report.Add("PASS 12-second force simulation: stable buoyancy and downstream drift "+body.position);
    buoy.moored=true;buoy.mooringPoint=body.position;var anchor=body.position;river.waveAmplitude=.055f;
    for(int i=0;i<600;i++){buoy.Step(.02f,12+i*.02f);physics.Simulate(.02f);}
    if(Vector3.Distance(body.position,anchor)>1.5f||Mathf.Abs(body.position.y)>.4f)throw new Exception("Mooring test failed.");report.Add("PASS 12-second wave and soft-mooring stability");
    for(int i=0;i<40;i++)river.EmitRipple(Vector3.zero);if(river.RippleCount!=16||river.EmitRipple(new Vector3(100,0,0)))throw new Exception("Ripple bounds/capacity test failed.");report.Add("PASS bounded ripple buffer and dry-bank rejection");
   }finally{EditorSceneManager.ClosePreviewScene(testScene);}return report;
  }
 }
}
