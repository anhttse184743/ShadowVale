using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;
[InitializeOnLoad] public static class NightPatrolRestoreChecks
{
 const string LiveKey="NightPatrolCombatLive";
 static NightPatrolRestoreChecks(){EditorApplication.update+=LiveTick;}
 public static void BeginLive(){SessionState.SetInt(LiveKey,1);EditorApplication.isPlaying=true;}
 static void LiveTick()
 {
  int phase=SessionState.GetInt(LiveKey,0);if(phase==0)return;
  if(phase==3&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetInt(LiveKey,0);EditorApplication.Exit(SessionState.GetInt(LiveKey+"Exit",1));return;}
  if(phase!=1||!EditorApplication.isPlaying)return;
  SessionState.SetInt(LiveKey,2);
  try{Run();SessionState.SetInt(LiveKey+"Exit",0);}catch(Exception error){Debug.LogException(error);File.AppendAllText("night-patrol-checks.txt","\nFAIL "+error);SessionState.SetInt(LiveKey+"Exit",1);}
  SessionState.SetInt(LiveKey,3);EditorApplication.isPlaying=false;
 }
 static List<string> report=new();
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);report.Add("PASS "+message);}
 static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
 static void Prop(object obj,string name,object value)=>obj.GetType().GetProperty(name).SetValue(obj,value);
 static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
 public static void Run()
 {
  foreach(var menu in Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None)){Set(menu,"visible",false);Set(menu,"quitAuthorized",true);menu.enabled=false;}
  Time.timeScale=1;
  // Recreate the copied ScriptableObject against the validation DLL, not the source project's MonoScript GUID.
  const string path="Assets/_Project/Map01/Resources/Characters/Map01ActorAssets.asset";
  AssetDatabase.DeleteAsset(path);var assets=ScriptableObject.CreateInstance<Map01ActorAssets>();assets.enemyController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Characters/Animations/Controllers/AC_Enemy.controller");AssetDatabase.CreateAsset(assets,path);AssetDatabase.SaveAssets();
  var nav=NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Map2NavMesh.asset"));
  var root=new GameObject("Night patrol restored on actual Map 2 NavMesh");
  try
  {
   var pawn=new GameObject("Nam");pawn.transform.SetParent(root.transform);var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;Call(motor,"Awake");pawn.GetComponent<CharacterController>().height=1.8f;pawn.GetComponent<CharacterController>().center=Vector3.up*.9f;
   var mission=root.AddComponent<Map02Mission>();mission.enabled=false;Set(mission,"player",motor);Prop(mission,"Ready",true);
   var chapter=root.AddComponent<Map02ChapterDirector>();chapter.enabled=false;Prop(chapter,"Mission",mission);Prop(mission,"Chapter",chapter);Set(chapter,"player",motor);Set(chapter,"root",root);Set(chapter,"ready",true);
   var layout=ScriptableObject.CreateInstance<Map02ChapterLayout>();layout.nightPatrolLightRange=28;layout.patrolSweepSeconds=15;Prop(chapter,"Layout",layout);
   var sites=(Dictionary<string,Transform>)typeof(Map02ChapterDirector).GetField("sites",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter);
   foreach(var pair in new[]{("bridge",new Vector3(29,.725f,-4.66f)),("scout",new Vector3(29,.774f,-16.66f)),("store",new Vector3(-49.15f,1.03f,8.39f)),("nightPatrol",new Vector3(29,.725f,-4.66f)+new Vector3(1,0,-1).normalized*35)})
   {var site=new GameObject(pair.Item1);site.transform.SetParent(root.transform);NavMesh.SamplePosition(pair.Item2,out var hit,12,NavMesh.AllAreas);site.transform.position=hit.position;sites[pair.Item1]=site.transform;}
   var light=new GameObject("Main scan").AddComponent<Light>();light.transform.SetParent(root.transform);Set(chapter,"sweepLight",light);
   chapter.State.phase=3;chapter.State.combatGroups.Add(Map02ChapterDirector.NightPatrolGroup);Call(chapter,"RefreshNightPatrol");
   Check(chapter.Enemies.Count==3,"stale saved group marker repairs all three missing night NPCs");
   foreach(var unit in chapter.Enemies)
   {
    unit.enabled=false;var animator=unit.GetComponentInChildren<Animator>();Check(animator!=null&&animator.isHuman&&unit.GetComponentsInChildren<Renderer>().Any(r=>r.enabled),"actual approved NPC model imported and visible: "+unit.id);
    var torch=unit.GetComponent<Map02PatrolFlashlight>();Check(torch?.Emitter!=null,"flashlight bound to actual humanoid: "+unit.id);Call(torch,"LateUpdate");
    Check(Vector3.ProjectOnPlane(unit.transform.position-sites["bridge"].position,Vector3.up).magnitude>34.5f&&Vector3.ProjectOnPlane(unit.transform.position-sites["bridge"].position,Vector3.up).magnitude<35.5f,"NPC remains 35 m from mission point: "+unit.id);
    Check(unit.GetComponentsInChildren<Map02PatrolFlashlight>().Length==1,"one flashlight per NPC: "+unit.id);
   }
   var positions=chapter.Enemies.Select(u=>u.transform.position).ToArray();
   for(int i=0;i<3;i++)for(int j=i+1;j<3;j++)Check(Vector3.ProjectOnPlane(positions[i]-positions[j],Vector3.up).magnitude>=9.9f,"triangle spacing >=10 m: "+i+"/"+j+" = "+Vector3.Distance(positions[i],positions[j]));
   Check(Vector3.Cross(positions[1]-positions[0],positions[2]-positions[0]).magnitude>1,"three guard posts form a triangle");
   Call(chapter,"RefreshNightPatrol");Check(chapter.Enemies.Count==3,"refresh does not duplicate NPCs");
   Check(chapter.NightPatrolLights.All(l=>l!=null&&Mathf.Abs(l.range-40)<.01f),"all three lights use 40 m range during Night 2");
   var random=typeof(Map02ChapterDirector).GetMethod("PatrolRandom",BindingFlags.Static|BindingFlags.NonPublic);
   var first=new Vector3[3];chapter.State.nightPatrolSeed=123456;
   for(int cycle=0;cycle<3;cycle++)for(int i=0;i<3;i++)
   {
    chapter.State.nightPatrolCycle=cycle;
    var basis=Vector3.ProjectOnPlane(sites["bridge"].position-positions[i],Vector3.up).normalized;
    for(float t=0;t<15;t+=.5f){chapter.State.patrolScanSeconds=t;var direction=(Vector3)Call(chapter,"NightPatrolSweepDirection",i);Check(Vector3.Angle(basis,direction)<=60.1f,"random pass stays within mission-facing 120 degrees: "+cycle+"/"+i+"/"+t);}
    chapter.State.patrolScanSeconds=4;var sample=(Vector3)Call(chapter,"NightPatrolSweepDirection",i);if(cycle==0)first[i]=sample;else Check(Vector3.Angle(first[i],sample)>1,"scan direction changes between cycles: "+i);
   }
   for(float t=11;t<15;t+=.25f)
   {
    chapter.State.patrolScanSeconds=t;Check(chapter.NightPatrolDarkWindow,"guaranteed shared 4 s dark interval: "+t);
    var corridor=sites["bridge"].position+(chapter.NightPatrolPosition-sites["bridge"].position)*.3f;
    for(int i=0;i<3;i++)Check(Vector3.Angle((Vector3)Call(chapter,"NightPatrolSweepDirection",i),corridor-positions[i])>30,"light points to mission sector edge and clears escape corridor during dark window: "+i);
   }
   chapter.State.patrolScanSeconds=4;Call(chapter,"TickPatrol",0f);
   foreach(var unit in chapter.Enemies)
   {
    chapter.TickNightPatrolUnit(unit,unit.GetComponent<NavMeshAgent>());
    var missionDirection=Vector3.ProjectOnPlane(sites["bridge"].position-unit.transform.position,Vector3.up).normalized;
    Check(Vector3.Angle(unit.transform.forward,missionDirection)<=60.1f,"actual guard faces mission sector: "+unit.id);
    var torch=unit.GetComponent<Map02PatrolFlashlight>();Call(torch,"LateUpdate");
    Check(Vector3.Angle(torch.Emitter.forward,missionDirection)<=60.1f,"held flashlight follows mission sector: "+unit.id);
   }
   var unit0=chapter.Enemies[0];var prop=unit0.GetComponent<Map02PatrolFlashlight>();prop.SetDirection(Vector3.forward);Call(prop,"LateUpdate");Call(chapter,"UpdateNightPatrolPresentation",unit0.transform.position+Vector3.up*1.7f,Vector3.forward);
   var eye=prop.Emitter.position;
   pawn.transform.position=eye+Vector3.forward*10-Vector3.up;Physics.SyncTransforms();
   Check(chapter.NightGuardIlluminates(unit0),"exposed player inside flashlight cone is visible");
   pawn.transform.position=eye+Vector3.forward*41-Vector3.up;Physics.SyncTransforms();Check(!chapter.NightGuardIlluminates(unit0),"player beyond 40 m is not illuminated");
   pawn.transform.position=eye+Quaternion.Euler(0,20,0)*Vector3.forward*10-Vector3.up;Physics.SyncTransforms();Check(!chapter.NightGuardIlluminates(unit0),"player outside narrow light cone is not illuminated");
   pawn.transform.position=eye+Vector3.forward*10-Vector3.up;var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=eye+Vector3.forward*5;wall.transform.localScale=new Vector3(4,4,.3f);Physics.SyncTransforms();Check(!chapter.NightGuardIlluminates(unit0),"solid wall blocks flashlight detection");Object.DestroyImmediate(wall);
   var field=new GameObject("Rice hiding spot").AddComponent<VillageRiceInstances>();field.transform.SetParent(root.transform);field.detailedMesh=new Mesh{name="Rice_Validation"};var at=pawn.transform.position;field.plants=new[]{new Vector4(at.x,at.y,at.z,0)};field.Rebuild();Prop(motor,"IsProne",true);Physics.SyncTransforms();
   Check(VillageRiceInstances.Conceals(motor)&&!chapter.NightGuardIlluminates(unit0),"lying in actual planted rice hides Nam from flashlight");
   object[] exposure={unit0.transform.position+Vector3.up*1.7f,Vector3.forward,0f,0f};Check(!(bool)Call(chapter,"NightPatrolExposure",exposure),"rice also blocks mission warning/exposure counter");
   Prop(motor,"IsProne",false);Check(!VillageRiceInstances.Conceals(motor)&&chapter.NightGuardIlluminates(unit0),"standing up in rice removes concealment");
   chapter.State.patrolScanSeconds=2;Call(chapter,"TickPatrol",2f);Check(Mathf.Abs(chapter.State.patrolScanSeconds-3)<.001f,"Night 2 scan clock advances at half speed");
   var hp=pawn.AddComponent<Health>();hp.SetMaxHealth(100);Set(mission,"health",hp);mission.World.story.stage=Map02Stage.QuietPreparation;
   field.enabled=false;Prop(motor,"IsProne",false);
   prop.SetDirection(Vector3.forward);Call(prop,"LateUpdate");Call(chapter,"UpdateNightPatrolPresentation",unit0.transform.position+Vector3.up*1.7f,Vector3.forward);eye=prop.Emitter.position;
   pawn.transform.position=eye+Vector3.forward*35-Vector3.up;Physics.SyncTransforms();Check(chapter.NightGuardIlluminates(unit0),"standing player at 35 m is detected when swept by longer-range flashlight");
   int thresholds=0;for(int n=0;n<1000;n++)if(Map02ChapterEnemy.NightObservationShotHits((n+.5f)/1000))thresholds++;
   Check(thresholds==900&&!Map02ChapterEnemy.NightObservationShotHits(.9f),"hit probability threshold is exactly 90 percent");
   pawn.transform.position=eye+Vector3.forward*20-Vector3.up;Physics.SyncTransforms();unit0.Hear(pawn.transform.position,true);UnityEngine.Random.InitState(12345);
   int hits=0;for(int n=0;n<1000;n++){hp.RestoreHealth(100);Set(unit0,"shot",0f);Call(unit0,"Update");if(hp.Current<100)hits++;}
   Check(hits>850&&hits<950,"actual unblocked enemy fire approaches 90 percent hits: "+hits+"/1000");
   Prop(motor,"IsProne",true);hp.RestoreHealth(100);Set(unit0,"shot",0f);Call(unit0,"Update");
   Check(!unit0.Alerted&&!unit0.Investigating&&hp.Current==100&&!chapter.NightGuardIlluminates(unit0),"lying anywhere during Night 2 clears detection and stops already-alerted enemy fire");
   Prop(motor,"IsProne",false);Set(unit0,"shot",0f);Call(unit0,"Update");Check(unit0.Alerted,"standing back up under beam can be detected again");
   var bulletWall=GameObject.CreatePrimitive(PrimitiveType.Cube);bulletWall.transform.position=(unit0.transform.position+pawn.transform.position)*.5f+Vector3.up;bulletWall.transform.localScale=new Vector3(4,4,.3f);Physics.SyncTransforms();hp.RestoreHealth(100);
   for(int n=0;n<50;n++){Set(unit0,"shot",0f);Call(unit0,"Update");}Check(hp.Current==100,"90 percent accuracy never permits damage through a solid wall");Object.DestroyImmediate(bulletWall);
   chapter.State.completed.Add("N2_01");chapter.State.patrolScanSeconds=2;Call(chapter,"TickPatrol",2f);
   Check(chapter.NightObservationRulesActive&&Mathf.Abs(chapter.State.patrolScanSeconds-3)<.001f&&chapter.NightPatrolLights.All(l=>Mathf.Abs(l.range-40)<.001f),"finishing binocular observation preserves half-speed scan and 40 m detection");
   prop.SetDirection(Vector3.forward);Call(prop,"LateUpdate");Call(chapter,"UpdateNightPatrolPresentation",unit0.transform.position+Vector3.up*1.7f,Vector3.forward);eye=prop.Emitter.position;
   pawn.transform.position=eye+Vector3.forward*35-Vector3.up;Physics.SyncTransforms();Set(unit0,"alerted",false);UnityEngine.Random.InitState(5678);
   int distantHits=0;for(int n=0;n<100;n++){hp.RestoreHealth(100);Set(unit0,"shot",0f);Call(unit0,"Update");if(hp.Current<100)distantHits++;}
   Check(unit0.Alerted&&distantHits>75,"after observation guard detects and shoots at 35 m without requiring a closer approach: "+distantHits+"/100");
   var coverWall=GameObject.CreatePrimitive(PrimitiveType.Cube);coverWall.transform.position=(unit0.transform.position+pawn.transform.position)*.5f+Vector3.up;coverWall.transform.localScale=new Vector3(4,4,.4f);Physics.SyncTransforms();Call(unit0,"Update");
   Check(!unit0.TrackingVisibleTarget,"occluded target releases aiming lock so flashlight can sweep again");
   chapter.HoldBinoculars(true);Check(chapter.Binoculars,"binoculars remain active during sweep test");
   chapter.State.patrolScanSeconds=2;Call(chapter,"TickPatrol",0f);var firstBeam=chapter.NightPatrolLight.transform.forward;float scanMotion=0;
   for(int frame=0;frame<80;frame++)
   {
    Call(unit0,"Update");chapter.TickNightPatrolWorld(.05f);
    foreach(var guard in chapter.Enemies)Call(guard.GetComponent<Map02PatrolFlashlight>(),"LateUpdate");
    scanMotion=Mathf.Max(scanMotion,Vector3.Angle(firstBeam,chapter.NightPatrolLight.transform.forward));
   }
   Check(chapter.Binoculars&&scanMotion>5&&Mathf.Abs(chapter.State.patrolScanSeconds-4)<.01f,"binoculars do not freeze scan clock or beam while target is hidden: motion="+scanMotion);
   chapter.HoldBinoculars(false);
   var aimCombat=pawn.AddComponent<PlayerCombat>();aimCombat.enabled=false;Set(aimCombat,"_aiming",true);
   chapter.State.patrolScanSeconds=2;chapter.TickNightPatrolWorld(0f);firstBeam=chapter.NightPatrolLight.transform.forward;scanMotion=0;
   for(int frame=0;frame<80;frame++)
   {
    Call(unit0,"Update");chapter.TickNightPatrolWorld(.05f);
    foreach(var guard in chapter.Enemies)Call(guard.GetComponent<Map02PatrolFlashlight>(),"LateUpdate");
    scanMotion=Mathf.Max(scanMotion,Vector3.Angle(firstBeam,chapter.NightPatrolLight.transform.forward));
   }
   Check(aimCombat.IsAiming&&scanMotion>5&&Mathf.Abs(chapter.State.patrolScanSeconds-4)<.01f,"gun aiming does not freeze independent world patrol simulation: motion="+scanMotion);
   Set(aimCombat,"_aiming",false);Object.DestroyImmediate(coverWall);
   chapter.State.completed.Add("N2_05");
   Check(!chapter.NightObservationRulesActive&&Mathf.Abs(chapter.NightPatrolScanRate-1)<.001f&&Mathf.Abs(chapter.NightPatrolDetectionRange-28)<.001f,"special patrol rules end after Night 2 patrol encounter, not after observation");
   prop.SetDirection(Vector3.forward);Call(prop,"LateUpdate");Call(chapter,"UpdateNightPatrolPresentation",unit0.transform.position+Vector3.up*1.7f,Vector3.forward);eye=prop.Emitter.position;
   pawn.transform.position=eye+Vector3.forward*10-Vector3.up;Prop(motor,"IsProne",true);Physics.SyncTransforms();
   Check(chapter.NightGuardIlluminates(unit0),"outside Night 2 lying on bare ground does not gain the new full concealment rule");Prop(motor,"IsProne",false);
   int normalHits=0;for(int n=0;n<100;n++){hp.RestoreHealth(100);Set(unit0,"shot",0f);Call(unit0,"Update");if(hp.Current<100)normalHits++;}
   Check(normalHits==100,"outside Night 2 original unobstructed bullet behaviour is unchanged");
   var saved=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(chapter.State));Check(saved.nightPatrolSeed==chapter.State.nightPatrolSeed&&saved.nightPatrolCycle==chapter.State.nightPatrolCycle,"random scan sequence persists in checkpoint");
   Call(chapter,"RefreshNightPatrol");Check(chapter.Enemies.All(u=>!u.gameObject.activeSelf),"night patrol hides after night objective ends");
  }
  finally{Object.DestroyImmediate(root);nav.Remove();File.WriteAllLines("night-patrol-checks.txt",report);}
 }
}
