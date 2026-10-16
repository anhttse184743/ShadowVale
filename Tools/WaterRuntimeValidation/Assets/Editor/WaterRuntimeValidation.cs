using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ShadowVale.Map01;
[InitializeOnLoad] public static class WaterRuntimeValidation {
 static RiverWater river;static Rigidbody dropped;static RiverBuoyantBody[] boats;static float started=-1;
 static WaterRuntimeValidation(){EditorApplication.update+=Tick;}
 public static void Run(){SessionState.SetBool("WaterCheck",true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
 static void Tick(){if(!SessionState.GetBool("WaterCheck",false)||!EditorApplication.isPlaying)return;
  try{
   if(started<0){
    var root=new GameObject("River");river=root.AddComponent<RiverWater>();river.leftBank=new[]{new Vector3(-5.6f,.045f,-100),new Vector3(-5.6f,.045f,100)};river.rightBank=new[]{new Vector3(5.6f,.045f,-100),new Vector3(5.6f,.045f,100)};
    var sensor=root.AddComponent<BoxCollider>();sensor.isTrigger=true;sensor.center=new Vector3(0,-.8f,0);sensor.size=new Vector3(12,3,200);root.AddComponent<RiverWaterTrigger>().river=river;
    var probe=new GameObject("Dropped Rigidbody");probe.transform.position=new Vector3(0,1,25);probe.AddComponent<BoxCollider>().size=Vector3.one*.5f;dropped=probe.AddComponent<Rigidbody>();dropped.mass=8;
    boats=new RiverBuoyantBody[2];for(int i=0;i<2;i++){var go=new GameObject("Moored boat "+i);go.transform.position=new Vector3(-1,.21f,i==0?62:-82);var box=go.AddComponent<BoxCollider>();box.center=new Vector3(0,.05f,0);box.size=new Vector3(1.9f,.5f,6.6f);var rb=go.AddComponent<Rigidbody>();rb.mass=100;rb.centerOfMass=new Vector3(0,-.15f,0);rb.angularDamping=.8f;var b=go.AddComponent<RiverBuoyantBody>();b.river=river;b.floatPoints=new[]{new Vector3(-.65f,-.23f,-2.3f),new Vector3(.65f,-.23f,-2.3f),new Vector3(-.65f,-.23f,2.3f),new Vector3(.65f,-.23f,2.3f)};b.immersionDepth=.42f;b.displacedVolume=.2f;b.moored=true;b.mooringPoint=go.transform.position;boats[i]=b;}
    started=Time.time;
   }
   if(Time.time-started<12)return;
   if(dropped.GetComponent<RiverBuoyantBody>()==null)throw new Exception("Automatic buoyancy missing");if(!river.TrySample(dropped.position,Time.time,out var s)||Mathf.Abs(dropped.position.y-s.height)>.8f||dropped.position.z>24)throw new Exception("Dropped-body float/drift failure: "+dropped.position);
   foreach(var b in boats)if(Vector3.Distance(b.transform.position,b.mooringPoint)>2||Mathf.Abs(b.transform.position.y)>.8f)throw new Exception("Boat stability failure: "+b.transform.position);
   if(river.RippleCount<1)throw new Exception("No trigger/wake ripples");
   File.WriteAllLines("../Map02Reports/river-runtime-checks.txt",new[]{"PASS isolated Unity Play Mode, 12 seconds","PASS real trigger callback adds buoyancy to dropped Rigidbody","PASS dropped body floats and drifts: "+dropped.position,"PASS two moored hulls with Map 2 boat mass, float points and colliders remain stable","PASS interaction and wake ripples: "+river.RippleCount});SessionState.SetBool("WaterCheck",false);EditorApplication.Exit(0);
  }catch(Exception e){File.WriteAllText("../Map02Reports/river-runtime-checks.txt","FAIL "+e);SessionState.SetBool("WaterCheck",false);EditorApplication.Exit(1);}
 }
}
