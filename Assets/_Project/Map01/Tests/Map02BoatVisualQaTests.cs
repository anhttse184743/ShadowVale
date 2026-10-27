using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
public sealed class Map02BoatVisualQaTests:ForestSceneTestBase {
 [UnityTest,Timeout(360000)]public IEnumerator ActualDepartureAndArrivalKeepHungAboveTheHull(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return Audit();yield return new ExitPlayMode();}
 static IEnumerator Audit(){
  var m=Object.FindFirstObjectByType<Map01Mission>();float ready=Time.realtimeSinceStartup+90;while(m==null||!m.IsInitialized){Assert.Less(Time.realtimeSinceStartup,ready);m=Object.FindFirstObjectByType<Map01Mission>();yield return null;}
  m.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ExtractionStage);var ex=m.GetComponentInChildren<Map01Extraction>();ex.autoContinueToMap2=true;
  foreach(var e in m.Enemies)e.GetComponent<Health>().TakeDamage(99999,e.transform.position,null);
  while(!ex.Prepared){Assert.Less(Time.realtimeSinceStartup,ready,"Boat preparation");yield return null;}
  var cc=m.player.GetComponent<CharacterController>();cc.enabled=false;m.player.position=ex.approach;cc.enabled=true;
  string folder="Logs/Map02BoatVisualQa_20261027";Directory.CreateDirectory(folder);var rows=new List<string>{"stage,hips-in-boat,head-in-boat,actor-root,visual-root"};var seen=new HashSet<string>();var errors=new List<string>();float until=Time.realtimeSinceStartup+240;
  var camera=m.gameCamera;
  void Record(string stage,Transform boat,Animator hung){
   var hips=boat.InverseTransformPoint(hung.GetBoneTransform(HumanBodyBones.Hips).position);var head=boat.InverseTransformPoint(hung.GetBoneTransform(HumanBodyBones.Head).position);
   rows.Add(stage+","+hips+","+head+","+boat.InverseTransformPoint(hung.transform.parent.position)+","+boat.InverseTransformPoint(hung.transform.position));
   if(head.y<.4f)errors.Add(stage+" Hùng head below boat: "+head);if(hips.y<-.05f)errors.Add(stage+" Hùng hips below boat floor: "+hips);
   var pos=camera.transform.position;var rot=camera.transform.rotation;var target=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
   try{var focus=boat.position+Vector3.up*.7f;var eye=focus+boat.TransformDirection(new Vector3(4,2.7f,-5));camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(folder+"/"+stage+".png",tex.EncodeToPNG());}
   finally{camera.targetTexture=target;camera.transform.SetPositionAndRotation(pos,rot);RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);}
  }
  try{while(Time.realtimeSinceStartup<until){
   var arrival=Object.FindFirstObjectByType<Map02Arrival>();
   if(arrival!=null){
    if(arrival.CurrentPhase==Map02Arrival.Phase.Gameplay)break;
    if(arrival.CurrentPhase==Map02Arrival.Phase.Approaching||arrival.CurrentPhase==Map02Arrival.Phase.Mooring){string id="Map2-"+arrival.CurrentPhase;if(seen.Add(id)){yield return null;yield return null;Record(id,arrival.transform,arrival.Passengers[2].GetComponentInChildren<Animator>());}}
   }else if(ex!=null){float clock=(float)typeof(Map01Extraction).GetField("clock",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ex);string id="Map1-"+ex.CurrentPhase;
    if(ex.CurrentPhase>=Map01Extraction.Phase.Extraction&&ex.CurrentPhase<=Map01Extraction.Phase.Departing&&clock>.3f&&seen.Add(id)){yield return null;yield return null;Record(id,ex.BoatRoot,ex.HungAnimator);}
   }yield return null;
  }
  Assert.NotNull(Object.FindFirstObjectByType<Map02Arrival>());Assert.AreEqual(Map02Arrival.Phase.Gameplay,Object.FindFirstObjectByType<Map02Arrival>().CurrentPhase,"Natural transfer must hand back gameplay");Assert.True(seen.Contains("Map1-Departing"));Assert.True(seen.Contains("Map2-Approaching"));Assert.IsEmpty(errors,string.Join("\n",errors));
  }finally{File.WriteAllLines(folder+"/positions.csv",rows);File.WriteAllLines(folder+"/problems.txt",errors);}
 } [UnityTest,Timeout(240000)]public IEnumerator CheckpointRestoreCannotStealHungFromTheBoatPose(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return CheckRestore();yield return new ExitPlayMode();}
 static IEnumerator CheckRestore(){
  var m=Object.FindFirstObjectByType<Map01Mission>();float until=Time.realtimeSinceStartup+90;while(m==null||!m.IsInitialized){Assert.Less(Time.realtimeSinceStartup,until);m=Object.FindFirstObjectByType<Map01Mission>();yield return null;}
  m.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ExtractionStage);var ex=m.GetComponentInChildren<Map01Extraction>();ex.autoContinueToMap2=false;while(!ex.Prepared){Assert.Less(Time.realtimeSinceStartup,until);yield return null;}yield return WaitGameSeconds(.3f);
  var visual=m.hung.GetComponentInChildren<Map01HungVisual>();Assert.NotNull(visual);var actor=ex.HungAnimator;var before=ex.BoatRoot.InverseTransformPoint(actor.GetBoneTransform(HumanBodyBones.Hips).position);
  visual.RestoreCheckpointPose();yield return WaitGameSeconds(.3f);
  var after=ex.BoatRoot.InverseTransformPoint(actor.GetBoneTransform(HumanBodyBones.Hips).position);Directory.CreateDirectory("Logs/Map02BoatVisualQa_20261027");File.WriteAllText("Logs/Map02BoatVisualQa_20261027/restore.txt",$"before={before} after={after} controller={actor.runtimeAnimatorController?.name} visualEnabled={visual.enabled}\n");
  ScreenCapture.CaptureScreenshot("Logs/Map02BoatVisualQa_20261027/after-checkpoint-restore.png");yield return null;
  Assert.IsNull(actor.runtimeAnimatorController,"A load must leave Hùng's seated Playable in sole control");Assert.Less(Vector3.Distance(before,after),.05f,"Checkpoint restoration must not ground a seated Hùng into the water");
 }

}
}
