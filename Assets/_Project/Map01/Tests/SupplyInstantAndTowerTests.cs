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
public sealed class SupplyInstantAndTowerTests:ForestSceneTestBase {
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 [UnityTest,Timeout(300000)]public IEnumerator LockGrantsCargoImmediatelyAndHandoffHasNoBagAnimation(){Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return Supply();yield return new ExitPlayMode();}
 static IEnumerator Warm(){float until=Time.realtimeSinceStartup+90;while(Time.realtimeSinceStartup<until){var m=Object.FindFirstObjectByType<Map01Mission>();if(m?.IsInitialized==true&&m.GetComponent<Map01Story>()?.Layout!=null&&Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Any(a=>a.name==Map01OpeningCutscene.CommanderVisualName))yield break;yield return null;}Assert.Fail("Map did not initialize");}
 static IEnumerator Supply(){
  yield return Warm();var m=Object.FindFirstObjectByType<Map01Mission>();m.ModernHealth.CinematicInvulnerable=true;foreach(var g in m.Enemies)g.enabled=false;
  var story=m.GetComponent<Map01Story>();var q=m.GetComponent<Map01Quest>();var bag=m.GetComponent<Map01Inventory>();
  q.RestoreStoryStage(3,new Map01Story.Snapshot{rescueAccepted=true,doorUnlocked=true,keyCollected=true});
  foreach(var g in story.Guards(1))if(g.Alive)g.GetComponent<Health>().TakeDamage(99999,g.transform.position,null);
  var cc=m.player.GetComponent<CharacterController>();cc.enabled=false;m.player.position=story.Layout.chestApproach.position;cc.enabled=true;
  Assert.IsTrue(story.TryInteract(),"Chest interaction accepted");var puzzle=m.GetComponent<Map01OperationPuzzleUI>();Assert.IsTrue(puzzle.Active,"Puzzle opens after guards are cleared");
  puzzle.Finish(Map01OperationResult.Cancelled);Assert.IsFalse(story.Progress.supplies);Assert.AreEqual(0,bag.Count(Map01Story.CargoItem));
  Assert.IsTrue(story.TryInteract());Assert.IsTrue(puzzle.Active);
  typeof(Map01OperationPuzzleUI).GetField("instructions",Private).SetValue(puzzle,false);
  puzzle.SubmitLock(Map01SupplyLock.GenerateCode(story.Progress.puzzleSeed));
  Assert.IsFalse(puzzle.Active);Assert.IsTrue(story.Progress.supplies);Assert.AreEqual(1,bag.Count(Map01Story.CargoItem));Assert.AreEqual(4,q.Stage);Assert.IsFalse(m.Cinematic||story.Operating);Assert.IsNull(GameObject.Find("Nam supply interaction"));
  typeof(Map01Story).GetMethod("Collect",Private).Invoke(story,null);Assert.AreEqual(1,bag.Count(Map01Story.CargoItem),"No duplicate on replay");
  story.Progress.supplyReported=true;cc.enabled=false;m.player.position=story.Commander.position+story.Commander.forward*1.8f;cc.enabled=true;var at=m.player.position;
  Assert.IsTrue(story.TryInteract());var dialogue=m.GetComponent<Map01StoryDialogue>();Assert.IsTrue(dialogue.IsPlaying);
  yield return WaitGameSeconds(.8f);dialogue.Close(true);yield return null;yield return null;
  Assert.IsTrue(dialogue.IsPlaying,"The remaining handoff/order dialogue follows immediately");Assert.IsNull(GameObject.Find("Nam supply interaction"));Assert.Less(Vector3.ProjectOnPlane(at-m.player.position,Vector3.up).magnitude,.02f);
  yield return WaitGameSeconds(.8f);dialogue.Close(true);yield return null;
  Assert.IsTrue(story.Progress.suppliesDelivered);Assert.AreEqual(0,bag.Count(Map01Story.CargoItem));Assert.AreEqual(5,q.Stage);Assert.IsTrue(story.Progress.supplyRewardGranted);Assert.IsFalse(story.Operating);Assert.IsNull(GameObject.Find("Nam supply interaction"));
 }
 [UnityTest,Timeout(300000)]public IEnumerator ProbeTowerAimAgainstVisibleBones(){Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return Probe();yield return new ExitPlayMode();}
 static IEnumerator Probe(){
  yield return Warm();var m=Object.FindFirstObjectByType<Map01Mission>();var c3=m.GetComponent<Map01C3Mission>();float until=Time.realtimeSinceStartup+30;while(c3.Data==null&&Time.realtimeSinceStartup<until)yield return null;
  var tower=c3.Guard("tower");Assert.NotNull(tower);var actor=tower.GetComponentInChildren<Animator>();m.ModernHealth.CinematicInvulnerable=true;m.GetComponent<Map01Rescue>().MarkIntroSeen();
  foreach(var g in m.Enemies)g.enabled=false;c3.enabled=false;
  var rows=new List<string>();var capsule=tower.GetComponent<Collider>();rows.Add("root="+tower.transform.position+" collider="+capsule.bounds+" actor="+actor.transform.position);
  foreach(bool alert in new[]{false,true}){
   var nest=c3.At(c3.Data.nestA[0],c3.Data.nestA[1]);var cc=m.player.GetComponent<CharacterController>();cc.enabled=false;m.player.position=nest;cc.enabled=true;
   if(alert){tower.enabled=true;tower.Alert(m.player.position);yield return WaitGameSeconds(.7f);}
   else yield return WaitGameSeconds(.3f);
   foreach(var side in new[]{-1,0,1}){
    var right=Vector3.Cross(Vector3.up,Vector3.ProjectOnPlane(tower.transform.position-nest,Vector3.up).normalized);var eye=nest+Vector3.up*1.6f+right*(side*1.5f);
    foreach(var bone in new[]{HumanBodyBones.Head,HumanBodyBones.Chest,HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm}){
     var point=actor.GetBoneTransform(bone).position;var hits=Physics.RaycastAll(eye,(point-eye).normalized,Vector3.Distance(point,eye)+1,~(1<<m.player.gameObject.layer),QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).ToArray();var first=hits.FirstOrDefault(h=>!Map01Mission.RoundPassesThrough(h.collider));
     if(bone==HumanBodyBones.Head||bone==HumanBodyBones.Chest)Assert.AreSame(tower,first.collider?.GetComponentInParent<Map01EnemyController>(),$"Clear firing area: alert={alert} side={side} bone={bone}, blocked by {first.collider?.name}");
     rows.Add($"alert={alert} side={side} bone={bone} point={point} hit={(first.collider!=null?first.collider.name:"none")} guard={(first.collider!=null?first.collider.GetComponentInParent<Map01EnemyController>()?.name:"")} all="+string.Join(" | ",hits.Select(h=>$"{h.collider.name}@{h.distance:F2}")));
    }
   }
  }
  Directory.CreateDirectory("Logs/TowerAimAudit");File.WriteAllLines("Logs/TowerAimAudit/rays.txt",rows);
 }
}
}
