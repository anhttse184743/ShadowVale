using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
 public sealed class Map02InteractionQaTests:ForestSceneTestBase {
  const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
  [UnityTest,Timeout(240000)]public IEnumerator PackingMouseAtThreeScalesDoesNotLiftOtherCratesAndResetKeepsTemplate(){
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();yield return Packing();yield return new ExitPlayMode();
  }
  [UnityTest,Timeout(240000)]public IEnumerator HammerCommitsAtContactRetainsLastStakeAndHonoursPauseCloseAndRelease(){
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();yield return Hammer();yield return new ExitPlayMode();
  }
  [UnityTest,Timeout(240000)]public IEnumerator DockMouseCanInspectRotateAlignAndCompleteAllThreeSteps(){
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();yield return DockMouse();yield return new ExitPlayMode();
  }
  static object Call(Map02ChapterDirector c,string n,params object[] a)=>typeof(Map02ChapterDirector).GetMethod(n,F).Invoke(c,a);
  static T Get<T>(Map02ChapterDirector c,string n)=>(T)typeof(Map02ChapterDirector).GetField(n,F).GetValue(c);
  static void Set(Map02ChapterDirector c,string n,object value)=>typeof(Map02ChapterDirector).GetField(n,F).SetValue(c,value);
  static IEnumerator Ready(Action<Map02Mission> use){Map02Mission m=null;float end=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<end){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}Assert.NotNull(m?.Chapter);use(m);}
  static void Prefix(Map02Mission m){var save=m.Capture();var state=new Map02ChapterState();state.Complete("D1_01");state.Complete("D1_02");state.dayOne.arrivalSeen=true;save.chapter=state;m.Restore(save);}
  static void Open(Map02ChapterDirector c,string activity,string task){Call(c,"OpenDay",Enum.Parse(typeof(Map02ChapterDirector).GetNestedType("DayActivity",BindingFlags.NonPublic),activity),Map02ChapterDefinition.Load().Find(task));}
  static IEnumerator Gui(Map02ChapterDirector c,EventType type,Vector2 point,string method="DrawPacking"){
   // Queue through the editor's native game-input bridge. Replacing Event.current
   // inside Repaint changes its type but does not refresh IMGUI's cached mouse position.
   var at=new Vector2(point.x*Screen.width/1600f,point.y*Screen.height/900f);
   UnityEditor.EditorGUIUtility.QueueGameViewInputEvent(new Event{type=type,button=0,mousePosition=at});
   for(int i=0;i<4;i++)yield return null;
   System.IO.Directory.CreateDirectory("Logs/Map02Qa_20261027");System.IO.File.AppendAllText("Logs/Map02Qa_20261027/native-ui.txt",$"{method} {type} requested={point} screen={Screen.width}x{Screen.height} crate={Get<int>(c,"selectedCrate")} board={Get<int>(c,"selectedBoard")} feedback={Get<string>(c,"dayFeedback")}\n");
  }
  static IEnumerator Click(Map02ChapterDirector c,Vector2 point,string method="DrawPacking"){yield return Gui(c,EventType.MouseDown,point,method);yield return Gui(c,EventType.MouseUp,point,method);}
  static IEnumerator Packing(){
   Map02Mission m=null;yield return Ready(x=>m=x);var c=m.Chapter;c.Cancel();c.enabled=false;Prefix(m);
   for(int i=0;i<3;i++)c.State.Increment("D1_04");Open(c,"Packing","D1_04");Set(c,"dayInputReady",true);
   c.State.dayOne.wetPattern=0;
   try{foreach(float scale in new[]{.75f,1f,1.2f}){
    SetPreviewResolution(Mathf.RoundToInt(1600*scale),Mathf.RoundToInt(900*scale));for(int frame=0;frame<4;frame++)yield return null;
    Call(c,"ResetPacking");Set(c,"dayInputReady",true);Assert.AreEqual(Mathf.RoundToInt(1600*scale),Screen.width);Assert.AreEqual(Mathf.RoundToInt(900*scale),Screen.height);
    yield return Click(c,new Vector2(382,454));
    Assert.AreEqual(2,c.State.dayOne.crates[0].x,"Top-left pointer x at scale "+scale);Assert.AreEqual(0,c.State.dayOne.crates[0].y,"Top-left pointer y at scale "+scale);
    Assert.AreEqual(1,Get<int>(c,"selectedCrate"));
    yield return Click(c,new Vector2(382,454));
    Assert.AreEqual(1,Get<int>(c,"selectedCrate"),"An invalid drop must not steal the placed crate");Assert.AreEqual(2,c.State.dayOne.crates[0].x);StringAssert.Contains("chồng",Get<string>(c,"dayFeedback"));
    int pattern=c.State.dayOne.wetPattern;yield return Click(c,new Vector2(1295,637));
    Assert.True(c.State.dayOne.crates.All(p=>p.x<0),"Reset button at scale "+scale);Assert.AreEqual(pattern,c.State.dayOne.wetPattern);Assert.False(c.State.dayOne.stockCredited);
   }}finally{c.Cancel();c.enabled=true;}
  }
  static IEnumerator DockMouse(){
   Map02Mission m=null;yield return Ready(x=>m=x);var c=m.Chapter;c.Cancel();c.enabled=false;Prefix(m);
   Open(c,"Dock","D1_05");Set(c,"dayInputReady",true);c.State.dayOne.stakeHits=6;
   
   IEnumerator Card(int board)=>Click(c,new Vector2(157+Array.IndexOf(c.State.dayOne.boardOrder,board)*112,776),"DrawDock");
   IEnumerator Confirm()=>Click(c,new Vector2(1357,771),"DrawDock");
   IEnumerator Align(){yield return Gui(c,EventType.MouseDown,new Vector2(1030,826),"DrawDock");yield return Gui(c,EventType.MouseDrag,new Vector2(935,826),"DrawDock");yield return Gui(c,EventType.MouseUp,new Vector2(935,826),"DrawDock");}
   try{
    yield return Card(3);yield return Confirm();Assert.AreEqual(0,c.State.dayOne.BoardCount);StringAssert.Contains("nứt",Get<string>(c,"dayFeedback"));
    yield return Card(4);yield return Confirm();Assert.AreEqual(0,c.State.dayOne.BoardCount);StringAssert.Contains("dài",Get<string>(c,"dayFeedback"));
    yield return Card(0);yield return Align();Assert.LessOrEqual(Mathf.Abs(Get<float>(c,"dockOffset")),.07f);
    yield return Click(c,new Vector2(1357,819),"DrawDock");yield return Confirm();Assert.AreEqual(0,c.State.dayOne.BoardCount);StringAssert.Contains("đầu",Get<string>(c,"dayFeedback"));
    yield return Click(c,new Vector2(1357,819),"DrawDock");yield return Confirm();Assert.AreEqual(1,c.State.dayOne.BoardCount);
    yield return Click(c,new Vector2(768,738),"DrawDock");Assert.AreEqual(1,Get<int>(c,"dockSlot"),"A finished step must not steal selection from an empty step");
    for(int board=1;board<3;board++){yield return Card(board);yield return Align();yield return Confirm();Assert.AreEqual(board+1,c.State.dayOne.BoardCount);}
    yield return Click(c,new Vector2(405,808),"DrawDock");Assert.False(c.DayActivityActive);Assert.AreEqual("D1_05",c.CurrentScene?.id);Call(c,"EndScene");Assert.True(c.State.Done("D1_05"));
   }finally{c.Cancel();c.enabled=true;}
  }
  static IEnumerator Hammer(){
   Map02Mission m=null;yield return Ready(x=>m=x);var c=m.Chapter;c.Cancel();Prefix(m);var routing=RouteInputToGame();var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
   void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
   try{
    c.enabled=false;m.enabled=false;Keys(Key.E);yield return null;Open(c,"Dock","D1_05");c.State.dayOne.stakeHits=2;Call(c,"SyncDayProps");m.enabled=true;c.enabled=true;
    yield return null;Assert.False(Get<bool>(c,"dayInputReady"));Keys();yield return null;yield return null;Assert.True(Get<bool>(c,"dayInputReady"));
    Set(c,"dayClock",Map02DayOneState.SweepSeconds(0)*.5f);Keys(Key.Space);yield return null;
    Assert.AreEqual(2,c.State.dayOne.stakeHits,"Press begins the blow; progress belongs to the later contact");
    yield return WaitGameSeconds(.22f);Assert.AreEqual(3,c.State.dayOne.stakeHits);Assert.AreEqual(0,(int)typeof(Map02ChapterDirector).GetProperty("VisibleStakeIndex",F).GetValue(c));
    yield return WaitGameSeconds(.7f);Assert.AreEqual(3,c.State.dayOne.stakeHits,"Holding Space must not repeat the blow");Assert.AreEqual(1,(int)typeof(Map02ChapterDirector).GetProperty("VisibleStakeIndex",F).GetValue(c));
    Keys();yield return null;yield return null;Set(c,"dayClock",Map02DayOneState.SweepSeconds(1)*.5f);Keys(Key.Space);yield return null;
    m.SetPaused(true);float clock=Get<float>(c,"strikeClock");yield return new WaitForSecondsRealtime(.3f);Assert.AreEqual(clock,Get<float>(c,"strikeClock"),.001f);Assert.AreEqual(3,c.State.dayOne.stakeHits);
    m.SetPaused(false);Keys();yield return WaitGameSeconds(.22f);Assert.AreEqual(4,c.State.dayOne.stakeHits);
    Keys(Key.Escape);yield return null;Assert.True(c.DayActivityActive,"Do not save/exit halfway through a blow");Keys();yield return WaitGameSeconds(1);Assert.False(c.DayActivityActive);Assert.False(m.Player.GetComponent<ShadowVale.Gameplay.Combat.PlayerCombat>().HandsOccupied);
   }finally{Keys();m.SetPaused(false);m.enabled=true;c.enabled=true;c.Cancel();InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing);}
  }
  [Test]public void PlankCannotReplaceACompletedStepOrSpendUsedBoard(){
   var s=new Map02DayOneState();s.Initialize(73);s.stakeHits=6;Assert.True(s.LayBoard(0,0,false,out _));Assert.False(s.LayBoard(1,0,false,out var reason));StringAssert.Contains("Bậc",reason);Assert.AreEqual(0,s.boards[0].x);Assert.False(s.LayBoard(0,1,false,out _));Assert.AreEqual(1,s.BoardCount);
   var restored=JsonUtility.FromJson<Map02DayOneState>(JsonUtility.ToJson(s));restored.Validate();Assert.AreEqual(0,restored.boards[0].x);
  }
 }
}
