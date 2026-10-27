using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;
[InitializeOnLoad] public static class NightStealthBalanceChecks
{
 const string Key="NightStealthBalanceLive";static readonly List<string> report=new();
 static NightStealthBalanceChecks(){EditorApplication.update+=Tick;}
 public static void Begin(){SessionState.SetInt(Key,1);EditorApplication.isPlaying=true;}
 static void Tick(){int s=SessionState.GetInt(Key,0);if(s==0)return;if(s==3&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetInt(Key,0);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));return;}if(s!=1||!EditorApplication.isPlaying)return;SessionState.SetInt(Key,2);try{Run();SessionState.SetInt(Key+"Exit",0);}catch(Exception e){Debug.LogException(e);File.AppendAllText("night-stealth-balance.txt","\nFAIL "+e);SessionState.SetInt(Key+"Exit",1);}SessionState.SetInt(Key,3);EditorApplication.isPlaying=false;}
 static void Check(bool b,string m){if(!b)throw new Exception(m);report.Add("PASS "+m);}
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
 static void Prop(object o,string n,object v)=>o.GetType().GetProperty(n).SetValue(o,v);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,a);
 static Vector3[] Bank(string scene,string name)=>Regex.Matches(Regex.Match(scene,name+@":\r?\n((?:  - \{x:.*\r?\n)+)").Groups[1].Value,@"x: ([^,]+), y: ([^,]+), z: ([^}]+)").Select(m=>new Vector3(float.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture),float.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture),float.Parse(m.Groups[3].Value,CultureInfo.InvariantCulture))).ToArray();
 public static void Run()
 {
  Time.timeScale=1;foreach(var menu in Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None)){Set(menu,"visible",false);Set(menu,"quitAuthorized",true);menu.enabled=false;}
  const string assetsPath="Assets/_Project/Map01/Resources/Characters/Map01ActorAssets.asset";AssetDatabase.DeleteAsset(assetsPath);var art=ScriptableObject.CreateInstance<Map01ActorAssets>();art.enemyController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Characters/Animations/Controllers/AC_Enemy.controller");AssetDatabase.CreateAsset(art,assetsPath);AssetDatabase.SaveAssets();
  var nav=NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Map2NavMesh.asset"));var root=new GameObject("Night 2 actual NPC stealth balance");
  try
  {
   var pawn=new GameObject("Nam");pawn.transform.SetParent(root.transform);var body=pawn.AddComponent<CharacterController>();body.height=1.8f;body.center=Vector3.up*.9f;body.radius=.3f;var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;var hp=pawn.AddComponent<Health>();hp.SetMaxHealth(200);
   var mission=root.AddComponent<Map02Mission>();mission.enabled=false;Set(mission,"player",motor);Set(mission,"health",hp);Prop(mission,"Ready",true);mission.World.story.stage=Map02Stage.QuietPreparation;Set(mission,"dialogue",new Map02ConversationSession(mission.World.story));
   var c=root.AddComponent<Map02ChapterDirector>();c.enabled=false;Prop(c,"Mission",mission);Prop(mission,"Chapter",c);Set(c,"player",motor);Set(c,"root",root);Set(c,"ready",true);var layout=ScriptableObject.CreateInstance<Map02ChapterLayout>();Prop(c,"Layout",layout);
   var sites=(Dictionary<string,Transform>)typeof(Map02ChapterDirector).GetField("sites",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);
   foreach(var item in new[]{("bridge",new Vector3(29,.73f,-4.66f)),("nightPatrol",layout.nightTwoPatrolCenter)}){var t=new GameObject(item.Item1).transform;t.SetParent(root.transform);t.position=item.Item2;sites[item.Item1]=t;}
   var river=root.AddComponent<RiverWater>();string scene=File.ReadAllText("../../Assets/_Project/Scenes/Maps/Map 2.unity");river.leftBank=Bank(scene,"leftBank");river.rightBank=Bank(scene,"rightBank");Call(c,"InitializeNightBankRoute");var light=new GameObject("Scan").AddComponent<Light>();light.transform.SetParent(root.transform);Set(c,"sweepLight",light);c.State.phase=3;Call(c,"RefreshNightPatrol");
   Check(c.Enemies.Count==3&&c.Enemies.All(e=>e.HitPoints.Max==1440&&e.HitPoints.Current==1440),"all three real patrol NPCs spawn with 1440 HP");
   CheckObservation(c,pawn,root);
   c.State.completed.Add("N2_01");c.State.completed.Add("N2_02");
   Check(c.NightBankPositionAllowed(new Vector3(30,1,-30)),"dry field detour is not blocked after observation");Check(c.NightBankPositionAllowed(new Vector3(-40,1,-30)),"village bank is not blocked by corridor bounds");Check(c.NightBankPositionAllowed(layout.nightTwoBankRoute[0]+Vector3.right*8),"player can leave old narrow corridor on dry ground");
   int index=Enumerable.Range(0,river.leftBank.Length).OrderBy(i=>Mathf.Abs(river.leftBank[i].z+30)).First();var water=(river.leftBank[index]+river.rightBank[index])*.5f;Check(!c.NightBankPositionAllowed(water)&&!c.NightBankPositionAllowed(water+Vector3.up*8),"only actual river blocks walking and jumping shortcuts");
   c.State.completed.Remove("N2_02");
   var guard=c.Enemies[0];pawn.transform.position=guard.transform.position+Vector3.left*10;Physics.SyncTransforms();guard.Hear(pawn.transform.position,true);UnityEngine.Random.InitState(4123);
   for(int i=0;i<30&&hp.Current==200;i++){Set(guard,"shot",0f);Call(guard,"Update");}
   Check(hp.Current==100,"first actual unblocked patrol bullet removes 50 percent of maximum player HP");
   for(int i=0;i<30&&!hp.IsDead;i++){Set(guard,"shot",0f);Call(guard,"Update");}
   Check(hp.IsDead,"second actual unblocked patrol bullet kills an unhealed player");hp.RestoreHealth(200);
   var target=c.Enemies[1].HitPoints;
   foreach(float raw in new[]{22f,24f,250f})
   {
    target.RestoreHealth(1440);for(int i=0;i<59;i++)target.TakeBulletDamage(raw,target.transform.position+Vector3.up,null,Vector3.forward);
    Check(!target.IsDead&&target.Current==24,"59 bullets leave enemy alive for raw damage "+raw);target.TakeBulletDamage(raw,target.transform.position+Vector3.up,null,Vector3.forward);Check(target.IsDead,"bullet 60 kills enemy for raw damage "+raw);
   }
   target.RestoreHealth(1440);Call(target,"ResolveBulletDamage",250f,target.transform.position,null,Vector3.forward,WeaponKind.Sniper,true);Check(target.Current==1416,"queued lethal-weapon override cannot bypass sixty-hit patrol durability");
   var ordinary=new GameObject("Ordinary enemy health").AddComponent<Health>();ordinary.transform.SetParent(root.transform);ordinary.SetMaxHealth(100);ordinary.TakeBulletDamage(24,Vector3.zero,null,Vector3.forward);Check(ordinary.Current==76,"ordinary enemies retain standard bullet damage");Call(ordinary,"ResolveBulletDamage",24f,Vector3.zero,null,Vector3.forward,WeaponKind.Rifle,true);Check(ordinary.IsDead,"ordinary enemies retain existing lethal-weapon policy");
   Check(Map02ChapterDirector.NightPatrolSavedHealth(50,0)==720&&Map02ChapterDirector.NightPatrolSavedHealth(0,0)==0&&Map02ChapterDirector.NightPatrolSavedHealth(24,1)==24,"old saves migrate health fraction; dead/current-version soldiers are preserved");
   c.State.completed.Add("N2_02");Prop(mission,"Ready",false);pawn.transform.position=c.NightDoorStand+Vector3.forward*3.01f;c.TickNightStoreArrival();Check(!c.State.night2ReachedStore,"arrival does not trigger outside three metres");pawn.transform.position=c.NightDoorStand+Vector3.forward*2.99f;c.TickNightStoreArrival();Prop(mission,"Ready",true);
   Check(c.State.night2ReachedStore&&!c.State.Done("N2_03"),"reaching hatch ends patrol approach without skipping interior inspection");Check(c.NightPatrolLights.All(l=>!l.enabled),"all three searchlights turn off immediately at hatch");Check(c.Enemies.All(e=>!e.Alerted&&!e.TrackingVisibleTarget),"hatch arrival clears all patrol target locks");
   float clock=c.State.patrolScanSeconds;for(int i=0;i<10;i++){guard.Hear(pawn.transform.position,true);Set(guard,"shot",0f);Call(guard,"Update");Call(c,"TickPatrol",.1f);c.SyncNightPatrolPresentation();}Check(hp.Current==200&&c.State.patrolScanSeconds==clock&&!c.NightGuardIlluminates(guard),"after arrival patrol cannot resume scanning or shooting");
   var saved=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(c.State));Check(saved.night2ReachedStore&&saved.nightPatrolHealthVersion==1,"arrival ceasefire and health version persist in save");Call(c,"RefreshNightPatrol");Check(c.NightPatrolLights.All(l=>!l.enabled),"refresh/load keeps searchlights off after hatch arrival");
   Set(c,"voice",DialogueVoice.For(root));for(int i=0;i<20;i++)Call(c,"TickNightRadio",20f,180f,false,100f);Check(c.State.night2GuidanceStage==7,"radio/chain guidance still finishes after patrol shuts down");
   c.State.ResetPatrolRoute();Check(!c.State.night2ReachedStore&&!c.NightPatrolStopped,"retry resets patrol encounter");
  }
  finally{Object.DestroyImmediate(root);nav.Remove();File.WriteAllLines("night-stealth-balance.txt",report);}
 }
 static void CheckObservation(Map02ChapterDirector c,GameObject pawn,GameObject root)
 {
  c.State.completed.AddRange(Map02ChapterDefinition.Load().tasks.Where(t=>t.phase<3).Select(t=>t.id));
  var eye=new GameObject("Observation camera");eye.transform.SetParent(root.transform);var camera=eye.AddComponent<Camera>();var rig=eye.AddComponent<ThirdPersonCamera>();rig.enabled=false;Set(c,"rig",rig);Set(c,"binocular",true);
  var target=(Vector3)typeof(Map02ChapterDirector).GetProperty("NightObservationPoint",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);
  void Aim(Vector3 p){pawn.transform.position=p;eye.transform.position=p+Vector3.up*1.6f;eye.transform.LookAt(target);Physics.SyncTransforms();}
  bool Look(float dt)=>(bool)Call(c,"TickNightObservation",dt,2f);
  Aim(c.NightPatrolPosition+Vector3.forward*30);Check(!Look(3),"binocular view before mission checkpoint cannot complete observation");
  Check(!c.TryReachNightObservationCheckpoint(c.NightObservationSpawn+Vector3.right*3.1f),"passing outside checkpoint radius does not activate observation");
  Check(c.TryReachNightObservationCheckpoint(c.NightObservationSpawn),"passing mission checkpoint activates observation without binoculars");
  foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
  {
   Aim(c.NightPatrolPosition+direction*35);Set(c,"observed",0f);Check(!Look(1.9f)&&Look(.11f),"visible patrol can be observed for two seconds from direction "+direction);
  }
  Aim(c.NightPatrolPosition+Vector3.forward*40.1f);Check(!Look(3),"outside forty metres resets observation progress");
  Aim(c.NightPatrolPosition+Vector3.forward*40);Check(Look(2.01f),"exact forty metre boundary permits observation");
  var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=Vector3.Lerp(eye.transform.position,target,.5f);wall.transform.rotation=Quaternion.LookRotation(target-eye.transform.position);wall.transform.localScale=new Vector3(20,15,.5f);Physics.SyncTransforms();Check(!Look(3),"obstructed line of sight resets observation even after checkpoint");Object.DestroyImmediate(wall);
  var saved=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(c.State));saved.Validate();Check(saved.night2ObservationCheckpointReached,"checkpoint latch survives validated save round trip");
  Check(!c.State.Available(Map02ChapterDefinition.Load().Find("N2_02"))&&!c.State.Complete("N2_02")&&!c.State.Complete("N2_03"),"radio and warehouse remain locked before observation completes");
  var marker=new GameObject("Locked warehouse marker").AddComponent<Map02ObjectiveObject>();marker.transform.SetParent(root.transform);marker.chapterId="N2_03";marker.radius=3;marker.transform.position=c.NightDoorStand;pawn.transform.position=c.NightDoorStand;Check(!c.Interact(marker),"reaching warehouse marker cannot bypass observation prerequisites");
  Set(c,"binocular",false);Set(c,"observed",0f);
 }
}
