using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Combat;
public static class NightLadderControlChecks
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static readonly List<string> report=new List<string>();
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
 static void Prop(object o,string n,object v)=>o.GetType().GetProperty(n,Flags).SetValue(o,v);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
 static void Check(bool ok,string m){if(!ok)throw new Exception(m);report.Add("PASS "+m);}
 static void Place(CharacterController body,Vector3 at){body.enabled=false;body.transform.position=at;body.enabled=true;Physics.SyncTransforms();}
 static float Depth(Map02UndergroundStore s)=>(float)s.GetType().GetField("depth",Flags).GetValue(s);
 public static void Run()
 {
  int result=0;
  try
  {
   var pawn=new GameObject("Nam");var body=pawn.AddComponent<CharacterController>();body.height=1.8f;body.center=Vector3.up*.9f;
   var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;Call(motor,"Awake");var hp=pawn.AddComponent<Health>();hp.SetMaxHealth(200);
   var root=new GameObject("Mission");var mission=root.AddComponent<Map02Mission>();mission.enabled=false;Set(mission,"player",motor);Set(mission,"health",hp);Prop(mission,"Ready",true);
   var c=root.AddComponent<Map02ChapterDirector>();c.enabled=false;Prop(c,"Mission",mission);Prop(mission,"Chapter",c);Set(c,"player",motor);Set(c,"root",root);Set(c,"health",hp);
   c.State.phase=3;c.State.completed.Add("N2_01");c.State.completed.Add("N2_02");c.State.night2HatchOpen=true;
   var surface=GameObject.CreatePrimitive(PrimitiveType.Cube);surface.transform.position=Vector3.down*.15f;surface.transform.localScale=new Vector3(12,.3f,12);
   var go=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Story/NightStore/UndergroundStore"));
   var store=go.AddComponent<Map02UndergroundStore>();store.enabled=false;Prop(c,"UndergroundStore",store);
   int crouchFallback=0,jumpFallback=0;Func<bool> crouch=()=>{crouchFallback++;return false;};Func<bool> jump=()=>{jumpFallback++;return false;};
   motor.CrouchInteraction=crouch;motor.JumpInteraction=jump;store.Initialize(c);
   Check((bool)Call(store,"AtTopApproach",store.TopStand+Vector3.forward*1.09f),"top interaction accepts the extra 30 cm margin");
   Check(!(bool)Call(store,"AtTopApproach",store.TopStand+Vector3.forward*1.11f),"top interaction rejects positions beyond 1.1 m");
   Check(!(bool)Call(store,"AtTopApproach",store.TopStand+Vector3.up*.5f),"top interaction still requires the correct floor height");
   Check((bool)Call(store,"AtBottomApproach",store.transform.TransformPoint(new Vector3(0,-7,1.14f))),"bottom interaction accepts the extra 30 cm margin");
   Check(!(bool)Call(store,"AtBottomApproach",store.transform.TransformPoint(new Vector3(0,-7,1.16f))),"bottom interaction rejects points outside the added radius");
   Check(!(bool)Call(store,"AtBottomApproach",store.transform.TransformPoint(new Vector3(1.59f,-7,1.14f))),"bottom padding uses a rounded radius rather than expanded square corners");
   Place(body,store.TopStand+Vector3.forward*1.09f);
   Check(!motor.JumpInteraction()&&!store.IsClimbing&&jumpFallback==1,"Space on the surface does not attach for descent and preserves normal jump handling");
   Check(motor.CrouchInteraction()&&store.IsClimbing&&!body.enabled&&crouchFallback==0,"Ctrl at the expanded surface zone consumes crouch and attaches to descend");
   float start=Depth(store);store.StepTraversal(.5f,true,false);Check(Depth(store)>start,"S input descends the ladder");
   start=Depth(store);store.StepTraversal(.5f,false,false);Check(Mathf.Abs(Depth(store)-start)<.001f,"releasing both keys stops ladder movement");
   store.StepTraversal(.5f,true,true);Check(Mathf.Abs(Depth(store)-start)<.001f,"opposing keys do not move the player");
   for(int i=0;i<90&&!(bool)store.GetType().GetField("exiting",Flags).GetValue(store);i++)store.StepTraversal(.1f,true,false);
   Check((bool)store.GetType().GetField("exiting",Flags).GetValue(store)&&Mathf.Abs(Depth(store)-7)<.001f,"S reaches the bottom and begins the room landing");store.CancelTraversal();
   c.State.night2InsideStore=true;Place(body,store.transform.TransformPoint(new Vector3(0,-7,1.14f)));
   Check(!motor.CrouchInteraction()&&!store.IsClimbing&&crouchFallback==1,"Ctrl at the bottom does not attach for ascent");
   Check(motor.JumpInteraction()&&store.IsClimbing&&!body.enabled,"Space at the expanded bottom zone attaches for ascent without jumping");
   start=Depth(store);store.StepTraversal(.5f,false,true);Check(Depth(store)<start,"W input ascends the ladder");
   Prop(mission,"Paused",true);start=Depth(store);Call(store,"Update");Check(Mathf.Abs(Depth(store)-start)<.001f,"pause stops ladder processing");Prop(mission,"Paused",false);
   for(int i=0;i<90&&!(bool)store.GetType().GetField("exiting",Flags).GetValue(store);i++)store.StepTraversal(.1f,false,true);
   Check((bool)store.GetType().GetField("exiting",Flags).GetValue(store)&&Mathf.Abs(Depth(store)-1.1f)<.001f,"W reaches the top and begins the surface landing");store.CancelTraversal();
   Call(store,"OnDestroy");UnityEngine.Object.DestroyImmediate(go);
   Check(motor.JumpInteraction==jump&&motor.CrouchInteraction==crouch,"store cleanup restores previous jump and crouch handlers");
  }
  catch(Exception e){result=1;report.Add("FAIL "+e);Debug.LogException(e);}
  File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../SourceArt/Map02NightBankFlow/ladder-control-checks.txt")),report);EditorApplication.Exit(result);
 }
}
