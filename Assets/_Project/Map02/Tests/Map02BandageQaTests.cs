using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
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
  var rows=new List<string>{"t,right-palm-error-m,left-palm-error-m,bandage-axis-error-m"};var pressRows=new List<string>{"t,lanh-chest-to-hung-arm-m,lanh-forearm-to-hung-shoulder-m"};
  var contactFailures = new List<string>();
  string folder="Logs/Map02Qa_20261027/Bandage";Directory.CreateDirectory(folder);
  bool previousAsync = EditorSettings.asyncShaderCompilation;
  bool previousShaderCompilation = ShaderUtil.allowAsyncCompilation;
  EditorSettings.asyncShaderCompilation = false;
  ShaderUtil.allowAsyncCompilation = false;
  try{foreach(float t in new[]{0f,3f,6f,8f,11f,14f,17f,20f,23f}){
   typeof(Map02ChapterDirector).GetField("sceneClock",Flags).SetValue(c,t);Call("LateUpdate");
   var h=hung.Animator;var l=c.Actor("Chị Lành").Animator;Vector3 a=h.GetBoneTransform(HumanBodyBones.RightUpperArm).position,b=h.GetBoneTransform(HumanBodyBones.RightLowerArm).position;
   var sleeve=(Transform)typeof(Map02ChapterDirector).GetField("bandageSleeve",Flags).GetValue(c);
   float rightPalmError = float.NaN, leftPalmError = float.NaN;
   float sleeveError = sleeve != null && sleeve.gameObject.activeInHierarchy ? Distance(sleeve.position, a, b) : float.NaN;
   if(t>=6&&t<=17){
    object[] frame={Vector3.zero,Vector3.zero,Quaternion.identity,0f};typeof(Map02ChapterDirector).GetMethod("DressingFrame",Flags).Invoke(c,frame);
    var start=(Vector3)frame[0];var finish=(Vector3)frame[1];var rotation=(Quaternion)frame[2];float radius=(float)frame[3],progress=Mathf.Clamp01((t-5)/13.6f),cycle=Mathf.Repeat(progress*3.2f,1);
    var centre=Vector3.Lerp(start,finish,progress);var axis=(finish-start).normalized;
    foreach(bool left in new[]{false,true}){
     float angle=(left?(cycle<.5f?1-cycle:cycle):(cycle<.5f?cycle:1-cycle))*Mathf.PI*2;
     var target=centre+rotation*new Vector3(Mathf.Cos(angle)*(radius+.014f),0,Mathf.Sin(angle)*(radius+.014f))+axis*(left?-.025f:.025f);
     var palm=(Vector3)typeof(Map02ChapterDirector).GetMethod("DressingPalm",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{l,left});
     float distance = Vector3.Distance(palm, target);
     if (left) leftPalmError = distance;
     else rightPalmError = distance;
     if (!(distance < .045f))
      contactFailures.Add($"{t}s: {(left ? "left" : "right")} palm misses the wrap path by {distance * 100:F2} cm.");
    }
    if (!(sleeveError < .004f)) contactFailures.Add($"{t}s: dressing misses the upper arm axis by {sleeveError * 100:F2} cm.");
    // Her body must not press into his arm (Khoa, 26/11/2026): chest and forearms against his right upper arm and shoulder.
    var hisShoulder = h.GetBoneTransform(HumanBodyBones.RightShoulder) != null ? h.GetBoneTransform(HumanBodyBones.RightShoulder).position : a;
    float chest = Mathf.Min(Distance(l.GetBoneTransform(HumanBodyBones.Chest).position, hisShoulder, b), Distance((l.GetBoneTransform(HumanBodyBones.UpperChest) ?? l.GetBoneTransform(HumanBodyBones.Chest)).position, hisShoulder, b));
    float forearm = Mathf.Min(SegmentDistance(l.GetBoneTransform(HumanBodyBones.RightLowerArm).position, l.GetBoneTransform(HumanBodyBones.RightHand).position, hisShoulder, a),
                              SegmentDistance(l.GetBoneTransform(HumanBodyBones.LeftLowerArm).position, l.GetBoneTransform(HumanBodyBones.LeftHand).position, hisShoulder, a));
    pressRows.Add(System.FormattableString.Invariant($"{t},{chest},{forearm}"));
    if (!(chest > .30f)) contactFailures.Add($"{t}s: Lành's chest is {chest * 100:F1} cm from Hùng's arm, pressed into it.");
    if (!(forearm > .07f)) contactFailures.Add($"{t}s: Lành's forearm is {forearm * 100:F1} cm from Hùng's upper arm (bone axes), pressed into it.");
   }
   rows.Add(System.FormattableString.Invariant($"{t},{rightPalmError},{leftPalmError},{sleeveError}"));
   if (t == 6 || t == 11 || t == 17 || t == 23)
   {
    var camera = Object.FindFirstObjectByType<Camera>();
    var focus = Vector3.Lerp(a, b, .5f);
    var forward = hung.transform.forward * 2.3f + Vector3.up * .6f;
    Capture(camera, focus, forward + hung.transform.right * .05f, folder + "/" + t.ToString("00") + ".png");
    Capture(camera, focus, forward + hung.transform.right * 1.3f, folder + "/" + t.ToString("00") + "-side.png");
   }
   yield return null;
  }
   c.Cancel();c.State.Complete("D1_02");var completed=m.Capture();
   var previous=(Transform)typeof(Map02ChapterDirector).GetField("bandageSleeve",Flags).GetValue(c);
   Object.Destroy(previous.gameObject);yield return null;m.Restore(completed);Call("LateUpdate");
   var restored=(Transform)typeof(Map02ChapterDirector).GetField("bandageSleeve",Flags).GetValue(c);
   Assert.NotNull(restored,"A fresh load must rebuild the completed upper-arm dressing");Assert.True(restored.gameObject.activeInHierarchy);
   var roll=(Transform)typeof(Map02ChapterDirector).GetField("bandageRoll",Flags).GetValue(c);Assert.False(roll.gameObject.activeSelf,"The loose roll must not float beside the family after load");
   var body=hung.Animator;Assert.Less(Distance(restored.position,body.GetBoneTransform(HumanBodyBones.RightUpperArm).position,body.GetBoneTransform(HumanBodyBones.RightLowerArm).position),.004f);
   Assert.IsEmpty(contactFailures, string.Join(System.Environment.NewLine, contactFailures));
  }finally{
   EditorSettings.asyncShaderCompilation = previousAsync;
   ShaderUtil.allowAsyncCompilation = previousShaderCompilation;
   File.WriteAllLines(folder+"/contacts.csv",rows);File.WriteAllLines(folder+"/press.csv",pressRows);c.Cancel();c.enabled=true;
  }
 }
 static float SegmentDistance(Vector3 p0,Vector3 p1,Vector3 q0,Vector3 q1){float best=float.MaxValue;for(int i=0;i<=10;i++){var p=Vector3.Lerp(p0,p1,i/10f);best=Mathf.Min(best,Distance(p,q0,q1));}return best;}
static float Distance(Vector3 p,Vector3 a,Vector3 b){var ab=b-a;return Vector3.Distance(p,a+ab*Mathf.Clamp01(Vector3.Dot(p-a,ab)/ab.sqrMagnitude));}
 static void Capture(Camera camera,Vector3 focus,Vector3 offset,string path){
  Assert.NotNull(camera);var pos=camera.transform.position;var rot=camera.transform.rotation;var target=camera.targetTexture;var active=RenderTexture.active;
  var rt=RenderTexture.GetTemporary(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
  try{camera.transform.SetPositionAndRotation(focus+offset,Quaternion.LookRotation(-offset));camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
  finally{camera.targetTexture=target;camera.transform.SetPositionAndRotation(pos,rot);RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);}
 }
}
}
