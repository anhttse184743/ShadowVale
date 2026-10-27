using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;
public static class NightWarningChecks
{
 static readonly List<string> report=new List<string>();
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
 static void Prop(object o,string n,object v)=>o.GetType().GetProperty(n,Flags).SetValue(o,v);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);report.Add("PASS "+message);}
 public static void Run()
 {
  int result=0;
  try
  {
   var pawn=new GameObject("Nam");var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;pawn.GetComponent<CharacterController>().height=1.8f;pawn.GetComponent<CharacterController>().center=Vector3.up*.9f;Call(motor,"Awake");
   var hp=pawn.AddComponent<Health>();hp.SetMaxHealth(200);var combat=pawn.AddComponent<PlayerCombat>();combat.enabled=false;
   var root=new GameObject("Mission");var mission=root.AddComponent<Map02Mission>();mission.enabled=false;Set(mission,"player",motor);
   var c=root.AddComponent<Map02ChapterDirector>();c.enabled=false;Prop(c,"Mission",mission);Prop(mission,"Chapter",c);
   Set(c,"player",motor);Set(c,"combat",combat);Set(c,"health",hp);Set(c,"root",root);
   var camera=new GameObject("Camera");camera.AddComponent<Camera>();var rig=camera.AddComponent<ThirdPersonCamera>();rig.enabled=false;Set(c,"rig",rig);
   var light=new GameObject("Beam").AddComponent<Light>();Set(c,"sweepLight",light);
   var layout=ScriptableObject.CreateInstance<Map02ChapterLayout>();layout.nightTwoBankRoute=new[]{Vector3.forward*20,Vector3.forward*30};Prop(c,"Layout",layout);
   var sites=(Dictionary<string,Transform>)c.GetType().GetField("sites",Flags).GetValue(c);
   sites["nightPatrol"]=new GameObject("Patrol post").transform;
   for(int i=0;i<3;i++)
   {
    var guard=new GameObject("Guard "+i);var unit=guard.AddComponent<Map02ChapterEnemy>();unit.enabled=false;unit.id=Map02ChapterDirector.NightPatrolGroup+"_"+i;unit.group=Map02ChapterDirector.NightPatrolGroup;
    var guardHp=guard.AddComponent<Health>();guardHp.SetMaxHealth(1440);Prop(unit,"HitPoints",guardHp);Set(unit,"director",c);c.Enemies.Add(unit);
   }
   c.State.phase=3;c.State.nightPatrolSeed=5;c.State.night2GuidanceStage=7;
   Check(!c.NightPatrolScriptedApproach,"scripted rules wait until report to Hung completes");
   c.State.completed.Add("N2_01");c.State.completed.Add("N2_02");
   Check(c.NightPatrolScriptedApproach,"scripted rules start after the radio report");
   Action<float> tick=dt=>Call(c,"TickPatrol",dt);
   Action placeInBeam=()=>{var sweep=(Vector3)Call(c,"NightPatrolSweepDirection",0);pawn.transform.position=Vector3.ProjectOnPlane(sweep,Vector3.up).normalized*20;Physics.SyncTransforms();};
   placeInBeam();tick(.01f);
   Check(c.State.patrolWarnings==1&&Mathf.Abs(c.State.patrolFocusSeconds-1.5f)<.001f,"first beam contact counts once and starts exactly 1.5 seconds");
   Check(!c.LocksInput&&!c.MaySave,"warning permits player input and prevents saving a partial reaction window");
   Check(Resources.Load<AudioClip>("OperationsAudio/night2_patrol_warning")?.length>0,"English spoken warning imports as nonempty audio");
   Check(Resources.Load<AudioClip>("OperationsAudio/night2_patrol_fire_order")?.length>0,"fire order imports as nonempty spoken audio");
   float scan=c.State.patrolScanSeconds;
   tick(1.49f);Check(!c.NightPatrolFailureActive&&c.State.patrolScanSeconds==scan,"standing does not fail before deadline; sweep clock stays frozen");
   tick(.02f);Check(c.NightPatrolFailureActive&&c.LocksInput&&!c.MaySave,"standing at deadline starts failure cutscene and blocks controls/save");
   Check(!(bool)c.GetType().GetField("nightFireOrderStarted",Flags).GetValue(c),"failure begins without an immediate fire order");
   Call(c,"PresentNightPatrolFailure");
   var cameraStart=(Vector3)rig.GetType().GetField("cinematicPosition",Flags).GetValue(rig);
   Check(Vector3.Distance(cameraStart,camera.transform.position)<.001f,"camera starts at the existing player view without a cut");
   Call(c,"TickNightPatrolFailure",1.5f);Call(c,"PresentNightPatrolFailure");
   var cameraMid=(Vector3)rig.GetType().GetField("cinematicPosition",Flags).GetValue(rig);
   Check(Vector3.Distance(cameraStart,cameraMid)>.01f,"camera travels gradually towards the rear of the squad");
   Call(c,"TickNightPatrolFailure",1.49f);
   Check(!(bool)c.GetType().GetField("nightFireOrderStarted",Flags).GetValue(c)&&c.Enemies.TrueForAll(e=>e.GetComponent<ShadowVale.Gameplay.Enemies.EnemyFeedback>()==null),"no fire order or gunfire feedback before the camera arrives");
   Call(c,"TickNightPatrolFailure",.02f);
   Check((bool)c.GetType().GetField("nightFireOrderStarted",Flags).GetValue(c)&&c.Enemies.TrueForAll(e=>e.GetComponent<ShadowVale.Gameplay.Enemies.EnemyFeedback>()!=null),"camera arrival starts the fire order and squad gunfire");
   Check((string)c.GetType().GetField("nightPatrolCaption",Flags).GetValue(c)==Map02ChapterDirector.NightPatrolFireOrderText,"missed hiding deadline selects the fire order subtitle/audio cue after camera travel");
   Call(c,"TickNightPatrolFailure",5.1f);Check(mission.World.story.failed,"cutscene reaches the mission failed state");
   Call(c,"CancelNightPatrolFailure");Check(!c.NightPatrolFailureActive&&!hp.CinematicInvulnerable,"cancel/load clears cinematic state and temporary invulnerability");
   c.State.patrolWarnings=0;c.State.patrolFocusSeconds=0;placeInBeam();tick(.01f);
   Prop(motor,"IsProne",true);tick(.5f);tick(1.01f);
   Check(!c.NightPatrolFailureActive&&c.State.patrolWarnings==1&&c.State.patrolFocusSeconds==0,"lying down within deadline survives and retains one detection");
   Check((string)c.GetType().GetField("nightPatrolCaption",Flags).GetValue(c)==Map02ChapterDirector.NightPatrolWarningText,"hiding in time keeps the suspicious dialogue without a fire order");
   tick(20f);Check(!c.NightPatrolFailureActive&&c.State.patrolWarnings==1,"remaining prone does not create another detection");
   Prop(motor,"IsProne",false);placeInBeam();tick(.01f);
   Check(c.NightPatrolFailureActive&&c.State.patrolWarnings==2,"second beam contact starts failure immediately without a grace period");
   Check(!(bool)c.GetType().GetField("nightFireOrderStarted",Flags).GetValue(c),"second detection also waits for camera travel before the order");
   Call(c,"TickNightPatrolFailure",Map02ChapterDirector.NightPatrolCameraTravelSeconds+.01f);
   Check((string)c.GetType().GetField("nightPatrolCaption",Flags).GetValue(c)==Map02ChapterDirector.NightPatrolFireOrderText,"second detection selects the same fire order subtitle/audio cue after camera travel");
   Call(c,"CancelNightPatrolFailure");c.State.night2ReachedStore=true;
   Check(!c.NightPatrolScriptedApproach&&c.NightPatrolStopped,"reaching store ends the approach rule");
   c.State.night2ReachedStore=false;c.State.ResetPatrolRoute();
   Check(c.State.patrolWarnings==0&&c.State.patrolFocusSeconds==0&&c.State.Done("N2_01")&&c.State.Done("N2_02"),"retry baseline resets detections and retains observation/report progress");
  }
  catch(Exception e){result=1;report.Add("FAIL "+e);Debug.LogException(e);}
  File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../SourceArt/Map02NightBankFlow/warning-checks.txt")),report);EditorApplication.Exit(result);
 }
}
