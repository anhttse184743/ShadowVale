using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
public sealed class Map02ArmSceneQaTests:ForestSceneTestBase {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 [UnityTest,Timeout(300000)]public IEnumerator CaptureMealAndSleepArms(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();yield return Capture();yield return new ExitPlayMode();}
 static IEnumerator Capture(){
  Map02Mission m=null;float end=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<end){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
  Assert.NotNull(m?.Chapter);var c=m.Chapter;c.Cancel();c.enabled=false;string folder="Logs/Map02Arms_20261027";Directory.CreateDirectory(folder);File.WriteAllLines(folder+"/human-finger-muscles.txt",HumanTrait.MuscleName.Where(n=>n.Contains("Thumb")||n.Contains("Index")));var rows=new List<string>{"scene,time,actor,left-wrist-angle,right-wrist-angle"};
  object Call(string n,params object[] a)=>typeof(Map02ChapterDirector).GetMethod(n,F).Invoke(c,a);
  void Before(string id){var s=m.Capture();var state=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id==id)break;state.Complete(t.id);}state.dayOne.arrivalSeen=true;s.chapter=state;m.Restore(s);Call("BeginScene",Map02ChapterDefinition.Load().Find(id),0,0);}
  var lens=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.enabled&&x.targetTexture==null);var light=new GameObject("Arm diagnostic key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.65f;light.transform.rotation=Quaternion.Euler(45,35,0);
  var rt=RenderTexture.GetTemporary(1024,576,24);var tex=new Texture2D(1024,576,TextureFormat.RGB24,false);
  try{foreach(string id in new[]{"N1_01","N1_04"}){
   Before(id);float approach=(float)typeof(Map02ChapterDirector).GetField("dayApproachDuration",F).GetValue(c);
   foreach(float t in id=="N1_01"?new[]{0f,4f,8f,14f,33.5f,36f,44.5f,60f,67f}:new[]{0f,3f,8f,11f}){
    for(int settle=0;settle<3;settle++){typeof(Map02ChapterDirector).GetField("sceneClock",F).SetValue(c,approach+t);Call("LateUpdate");foreach(var twist in Object.FindObjectsByType<ForearmTwist>(FindObjectsSortMode.None))twist.Apply();yield return null;}
    typeof(Map02ChapterDirector).GetField("sceneClock",F).SetValue(c,approach+t);Call("LateUpdate");foreach(var twist in Object.FindObjectsByType<ForearmTwist>(FindObjectsSortMode.None))twist.Apply();
    foreach(var pair in new[]{("Nam",m.Player.GetComponentInChildren<Animator>()),("Hung",c.Actor("Hùng").Animator)}){
     var a=pair.Item2;float Angle(bool left){var lower=a.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var hand=a.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var finger=(Transform)typeof(Map02ChapterDirector).GetMethod("SceneMiddle",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{a,left});return Vector3.Angle(hand.position-lower.position,finger.position-hand.position);}
     rows.Add($"{id},{t},{pair.Item1},{Angle(true)},{Angle(false)}");Assert.LessOrEqual(Angle(true),36,$"{id} {t}s {pair.Item1} left wrist");Assert.LessOrEqual(Angle(false),36,$"{id} {t}s {pair.Item1} right wrist");
     var chest=a.GetBoneTransform(HumanBodyBones.Chest);var focus=(chest.position+a.GetBoneTransform(HumanBodyBones.LeftHand).position+a.GetBoneTransform(HumanBodyBones.RightHand).position)/3;var forward=Map02ChapterDirector.BodyForward(a,a.transform);var right=Vector3.Cross(Vector3.up,forward);var offset=(id=="N1_04"&&pair.Item1=="Hung"?Vector3.up*1.5f+forward*.5f:right*.8f+forward*1.65f+Vector3.up*.35f);
     var pos=lens.transform.position;var rot=lens.transform.rotation;var target=lens.targetTexture;var active=RenderTexture.active;
     try{lens.transform.SetPositionAndRotation(focus+offset,Quaternion.LookRotation(-offset));lens.targetTexture=rt;lens.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1024,576),0,0);tex.Apply();File.WriteAllBytes(folder+"/"+id+"-"+t.ToString("00.0")+"-"+pair.Item1+".png",tex.EncodeToPNG());}
     finally{lens.targetTexture=target;lens.transform.SetPositionAndRotation(pos,rot);RenderTexture.active=active;}
    }yield return null;
   }c.Cancel();
  }}finally{File.WriteAllLines(folder+"/wrists.csv",rows);c.Cancel();c.enabled=true;Object.Destroy(light.gameObject);RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);}
 }
}
}
