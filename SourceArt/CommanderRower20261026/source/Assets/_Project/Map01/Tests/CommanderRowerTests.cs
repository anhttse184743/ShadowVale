using System;
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
public sealed class CommanderRowerTests:ForestSceneTestBase {
[UnityTest,Timeout(600000)]public IEnumerator CommanderRowsAndHungSupportsArmAcrossBothMaps(){
Type.GetType("ShadowVale.Map01.Editor.CommanderPaddleGripSetup, ShadowVale.Map01.Editor").GetMethod("Prepare").Invoke(null,null);
UnityEditor.SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath("Temp/CommanderRowerSaves/"+Guid.NewGuid().ToString("N")));Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return Run();yield return new ExitPlayMode();
}
// Allocate captured LINQ locals after EnterPlayMode has reloaded the test domain.
static IEnumerator Run(){
yield return null;
float warmDeadline=Time.realtimeSinceStartup+90;
while(Time.realtimeSinceStartup<warmDeadline&&!Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Any(a=>a.name==Map01OpeningCutscene.CommanderVisualName))yield return null;
var m=Object.FindFirstObjectByType<Map01Mission>();Assert.IsTrue(m.IsInitialized);var q=m.GetComponent<Map01Quest>();q.RestoreStage(Map01Quest.ExtractionStage);
var e=m.GetComponentInChildren<Map01Extraction>();e.autoContinueToMap2=false;yield return WaitGameSeconds(.3f);
Assert.IsTrue(e.Prepared);
Directory.CreateDirectory("Logs/CommanderRower");
File.WriteAllText("Logs/CommanderRower/setup.txt","mission="+(m!=null)+" extraction="+(e!=null)+" prepared="+e.Prepared+" phase="+e.CurrentPhase+" commander="+(e.CommanderAnimator!=null)+" hung="+(e.HungAnimator!=null)+" camera="+(m.gameCamera!=null));
var ca=e.CommanderAnimator;Assert.IsTrue(ca!=null,"Commander animator must survive extraction setup");
var ha=e.HungAnimator;Assert.NotNull(ha);
var camera=m.gameCamera;var avatar=ca.avatar;File.AppendAllText("Logs/CommanderRower/setup.txt"," avatar="+(avatar!=null)+" rowClip="+(e.rowLoop!=null));
Assert.IsTrue(avatar.isHuman&&avatar.isValid);Assert.IsTrue(e.rowLoop.humanMotion);
Assert.That(e.CommanderRoot.localPosition.z,Is.EqualTo(-1.6f).Within(.01f));Assert.That(m.hung.localPosition.z,Is.EqualTo(1.6f).Within(.01f));
var lengths=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm}.Select(b=>Vector3.Distance(ca.GetBoneTransform(b).position,ca.GetBoneTransform(b==HumanBodyBones.LeftUpperArm?HumanBodyBones.LeftLowerArm:b==HumanBodyBones.LeftLowerArm?HumanBodyBones.LeftHand:b==HumanBodyBones.RightUpperArm?HumanBodyBones.RightLowerArm:HumanBodyBones.RightHand).position)).ToArray();
string folder="Logs/CommanderRower";Directory.CreateDirectory(folder);var rows=new List<string>{"frame,time,phase,namShots,commanderShots,voice"};int frame=0;float next=0,start=Time.realtimeSinceStartup;
var guards=m.Enemies.Where(x=>x.name=="Extraction guard 0"||x.name=="Extraction guard 1").ToArray();
foreach(var enemy in m.Enemies.Except(guards))enemy.GetComponent<Health>().TakeDamage(99999,enemy.transform.position,null);
for(int i=0;i<guards.Length;i++)if(UnityEngine.AI.NavMesh.SamplePosition(new Vector3(-4,2,90+i*2),out var hit,5,UnityEngine.AI.NavMesh.AllAreas))guards[i].GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(hit.position);
var body=m.player.GetComponent<CharacterController>();body.enabled=false;m.player.position=e.approach;body.enabled=true;
float deadline=Time.realtimeSinceStartup+200;bool closeMap1=false,closeMap2=false;Map02Arrival arrival=null;
try{
while(Time.realtimeSinceStartup<deadline){
 yield return null;
 if(e!=null&&e.HasDeparted&&arrival==null){Assert.Greater(e.NamCoverShots,0);Assert.AreEqual(0,e.CommanderCoverShots);e.ContinueToMap2();}
 arrival=Object.FindFirstObjectByType<Map02Arrival>();
 if(arrival!=null&&arrival.CurrentPhase==Map02Arrival.Phase.Gameplay)break;
 bool map1=arrival==null;bool rowing=map1?e.CurrentPhase==Map01Extraction.Phase.Departing:arrival.CurrentPhase==Map02Arrival.Phase.Approaching;
 if(rowing){
  if(map1)e.SendMessage("LateUpdate");else arrival.SendMessage("LateUpdate");
  Assert.AreSame(avatar,ca.avatar);Assert.IsTrue(ca.GetComponentsInChildren<SkinnedMeshRenderer>().Any(skin=>skin.sharedMesh.blendShapeCount>=2),"Commander grip shapes must be active on a copy");
  foreach(var side in new[]{HumanBodyBones.LeftHand,HumanBodyBones.RightHand}){var hand=ha.GetBoneTransform(side);var lower=ha.GetBoneTransform(side==HumanBodyBones.LeftHand?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);Assert.Less(Vector3.Angle(hand.GetChild(0).position-hand.position,hand.position-lower.position),10,"Hung wrist remains in line with forearm");}
  foreach(var actor in new[]{ha,ca})Assert.IsFalse(actor.GetComponentsInChildren<Weapon>(true).Any(w=>w.IsGun&&w.gameObject.activeInHierarchy));
  var armIds=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm};
  for(int j=0;j<4;j++){var b=armIds[j];var end=j==0?HumanBodyBones.LeftLowerArm:j==1?HumanBodyBones.LeftHand:j==2?HumanBodyBones.RightLowerArm:HumanBodyBones.RightHand;Assert.That(Vector3.Distance(ca.GetBoneTransform(b).position,ca.GetBoneTransform(end).position),Is.EqualTo(lengths[j]).Within(.005f),"Rower limb lengths remain unchanged");}
  var grip=typeof(Map01Extraction).GetMethod("PaddleGrip",BindingFlags.NonPublic|BindingFlags.Instance);
  foreach(var side in new[]{HumanBodyBones.LeftHand,HumanBodyBones.RightHand}){var point=(Vector3)grip.Invoke(e,new object[]{ca.GetBoneTransform(side)});var delta=point-e.Paddle.position;Assert.Less(Vector3.ProjectOnPlane(delta,e.Paddle.forward).magnitude,.015f,"Palm stays on the shaft");}
  var support=Vector3.Lerp(ha.GetBoneTransform(HumanBodyBones.RightLowerArm).position,ha.GetBoneTransform(HumanBodyBones.RightHand).position,.7f);
  Assert.Less(Vector3.Distance(ha.GetBoneTransform(HumanBodyBones.LeftHand).position,support),.12f,"Hung supports his injured right forearm");
  if((map1&&!closeMap1)||(!map1&&!closeMap2)){
   var pos=camera.transform.position;var rot=camera.transform.rotation;var focus=ca.GetBoneTransform(HumanBodyBones.Chest).position;
   camera.transform.SetPositionAndRotation(focus+e.BoatRoot.right*2.2f+e.BoatRoot.up*.35f-e.BoatRoot.forward*.7f,Quaternion.LookRotation(-e.BoatRoot.right*2.2f-e.BoatRoot.up*.35f+e.BoatRoot.forward*.7f));Capture(camera,folder+(map1?"/Map1-rower.png":"/Map2-rower.png"));camera.transform.SetPositionAndRotation(pos,rot);
   focus=ha.GetBoneTransform(HumanBodyBones.Chest).position;var eye=focus+ha.transform.forward*2+ha.transform.right*.8f+Vector3.up*.25f;camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));Capture(camera,folder+(map1?"/Map1-hung.png":"/Map2-hung.png"));camera.transform.SetPositionAndRotation(pos,rot);
   if(map1)closeMap1=true;else closeMap2=true;
  }
 }
 if(camera!=null&&Time.realtimeSinceStartup>=next&&(arrival==null||arrival.CurrentPhase!=Map02Arrival.Phase.Loading)){
  next=Time.realtimeSinceStartup+.2f;Capture(camera,folder+"/"+frame.ToString("D4")+".png");rows.Add(frame+","+(Time.realtimeSinceStartup-start).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+(arrival==null?"Map1-"+e.CurrentPhase:"Map2-"+arrival.CurrentPhase)+","+e.NamCoverShots+","+e.CommanderCoverShots+","+(e.GetComponent<DialogueVoice>()?.CurrentId??""));frame++;
 }
}
Assert.NotNull(arrival);Assert.AreEqual(Map02Arrival.Phase.Gameplay,arrival.CurrentPhase);Assert.IsTrue(closeMap1&&closeMap2);Assert.IsTrue(arrival.Passengers[0].GetComponent<ShadowVale.Gameplay.Player.PlayerController>().enabled);
foreach(var passenger in arrival.Passengers)Assert.Greater(passenger.position.y,.5f);
}finally{File.WriteAllLines(folder+"/frames.csv",rows);}
}
static void Capture(Camera camera,string path){var old=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,540,24);var tex=new Texture2D(960,540,TextureFormat.RGB24,false);try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);}}
}
}
