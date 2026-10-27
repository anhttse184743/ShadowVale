using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests {
public sealed class Map02BandageQaTests:ForestSceneTestBase {
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 [UnityTest,Timeout(240000)]public IEnumerator CaptureBandageContacts(){
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();yield return Audit();yield return new ExitPlayMode();
 }
 static IEnumerator Audit(){
  Map02Mission m=null;float end=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<end){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
  Assert.NotNull(m?.Chapter);var c=m.Chapter;c.Cancel();c.enabled=false;
  object Call(string n,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(n,Flags).Invoke(c,args);
  var save=m.Capture();var state=new Map02ChapterState();state.Complete("D1_01");state.dayOne.arrivalSeen=true;save.chapter=state;m.Restore(save);
  var hung=c.Actor("Hùng");hung.StopAt(c.Sites["dayOneFamily"].position+Vector3.forward*.6f);
  Call("BeginScene",Map02ChapterDefinition.Load().Find("D1_02"),0,0);
  var rows=new List<string>{"t,left-upper,right-upper,left-fore,right-fore,bandage-upper"};string folder="Logs/Map02Qa_20261027/Bandage";Directory.CreateDirectory(folder);
  try{foreach(float t in new[]{0f,3f,6f,8f,11f,14f,17f,20f,23f}){
   typeof(Map02ChapterDirector).GetField("sceneClock",Flags).SetValue(c,t);Call("LateUpdate");
   var h=hung.Animator;var l=c.Actor("Chị Lành").Animator;Vector3 a=h.GetBoneTransform(HumanBodyBones.RightUpperArm).position,b=h.GetBoneTransform(HumanBodyBones.RightLowerArm).position,w=h.GetBoneTransform(HumanBodyBones.RightHand).position;
   var lh=l.GetBoneTransform(HumanBodyBones.LeftHand).position;var rh=l.GetBoneTransform(HumanBodyBones.RightHand).position;
   var sleeve=(Transform)typeof(Map02ChapterDirector).GetField("bandageSleeve",Flags).GetValue(c);
   if(t>=6&&t<=17){
    object[] frame={Vector3.zero,Vector3.zero,Quaternion.identity,0f};typeof(Map02ChapterDirector).GetMethod("DressingFrame",Flags).Invoke(c,frame);
    var start=(Vector3)frame[0];var finish=(Vector3)frame[1];var rotation=(Quaternion)frame[2];float radius=(float)frame[3],progress=Mathf.Clamp01((t-5)/13.6f),cycle=Mathf.Repeat(progress*3.2f,1);
    var centre=Vector3.Lerp(start,finish,progress);var axis=(finish-start).normalized;
    foreach(bool left in new[]{false,true}){
     float angle=(left?(cycle<.5f?1-cycle:cycle):(cycle<.5f?cycle:1-cycle))*Mathf.PI*2;
     var target=centre+rotation*new Vector3(Mathf.Cos(angle)*(radius+.014f),0,Mathf.Sin(angle)*(radius+.014f))+axis*(left?-.025f:.025f);
     var wrist=l.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var lower=l.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
     var palm=(Vector3)typeof(Map02ChapterDirector).GetMethod("DressingPalm",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{l,left});
     Assert.Less(Vector3.Distance(palm,target),.045f,$"{t}s: {(left?"left":"right")} palm must touch the wrap/roll path");
    }
    Assert.Less(Distance(sleeve.position,a,b),.004f,"Dressing must be on injured upper arm");
   }
   rows.Add($"{t},{Distance(lh,a,b)},{Distance(rh,a,b)},{Distance(lh,b,w)},{Distance(rh,b,w)},{(sleeve!=null?Distance(sleeve.position,a,b):-1)}");
   if(t==6||t==11||t==17||t==23){var camera=m.GetComponent<Map02Mission>()!=null?Object.FindFirstObjectByType<Camera>():null;Capture(camera,Vector3.Lerp(a,b,.5f),hung.transform.forward*2.3f+hung.transform.right*.05f+Vector3.up*.6f,folder+"/"+t.ToString("00")+".png");}
   yield return null;
  }
   c.Cancel();c.State.Complete("D1_02");var completed=m.Capture();
   var previous=(Transform)typeof(Map02ChapterDirector).GetField("bandageSleeve",Flags).GetValue(c);
   Object.Destroy(previous.gameObject);yield return null;m.Restore(completed);Call("LateUpdate");
   var restored=(Transform)typeof(Map02ChapterDirector).GetField("bandageSleeve",Flags).GetValue(c);
   Assert.NotNull(restored,"A fresh load must rebuild the completed upper-arm dressing");Assert.True(restored.gameObject.activeInHierarchy);
   var roll=(Transform)typeof(Map02ChapterDirector).GetField("bandageRoll",Flags).GetValue(c);Assert.False(roll.gameObject.activeSelf,"The loose roll must not float beside the family after load");
   var body=hung.Animator;Assert.Less(Distance(restored.position,body.GetBoneTransform(HumanBodyBones.RightUpperArm).position,body.GetBoneTransform(HumanBodyBones.RightLowerArm).position),.004f);
  }finally{File.WriteAllLines(folder+"/contacts.csv",rows);c.Cancel();c.enabled=true;}
 }
 static float Distance(Vector3 p,Vector3 a,Vector3 b){var ab=b-a;return Vector3.Distance(p,a+ab*Mathf.Clamp01(Vector3.Dot(p-a,ab)/ab.sqrMagnitude));}
 static void Capture(Camera camera,Vector3 focus,Vector3 offset,string path){
  Assert.NotNull(camera);var pos=camera.transform.position;var rot=camera.transform.rotation;var target=camera.targetTexture;var active=RenderTexture.active;
  var rt=RenderTexture.GetTemporary(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
  try{camera.transform.SetPositionAndRotation(focus+offset,Quaternion.LookRotation(-offset));camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
  finally{camera.targetTexture=target;camera.transform.SetPositionAndRotation(pos,rot);RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);}
 }
}
}
