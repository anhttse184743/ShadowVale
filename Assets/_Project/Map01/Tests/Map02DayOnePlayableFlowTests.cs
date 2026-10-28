using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Player;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
 public sealed class Map02DayOnePlayableFlowTests:ForestSceneTestBase {
  const string Folder="Logs/Map02PlayableAudit_20261022";
  [UnityTest,Timeout(1200000)] public IEnumerator LandingToDayTwoThroughPlayerInput(){
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();yield return new Driver().Run();yield return new ExitPlayMode();
  }
  // Plan step 8 (2026-10-25): the same playthrough at the game's own pace: time scale 1 and every line heard out (no E),
  // timed against the doc (Day 1 18–22 min, Night 1 12–14 min). A bot walks straight and solves the puzzles at once,
  // so these are floors; durations-realpace.txt splits them per objective.
  [UnityTest,Timeout(3600000)] public IEnumerator DayOneAndNightOneAtRealPace(){
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();yield return new Driver{realPace=true}.Run();yield return new ExitPlayMode();
  }
  sealed class Driver {
   Map02Mission m;Map02ChapterDirector c;Keyboard kb;Camera camera;int screenshot;public bool realPace;float started,lastMark;readonly List<string> marks=new();
   void Lap(string label){marks.Add($"{label}	{(Time.time-lastMark)/60:F2} min	(total {(Time.time-started)/60:F2} min)");lastMark=Time.time;}
   const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
   T Get<T>(string n)=>(T)typeof(Map02ChapterDirector).GetField(n,Flags).GetValue(c);
   void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(kb,new KeyboardState(keys));
   void Log(string message)=>File.AppendAllText(Folder+"/steps.txt",DateTime.UtcNow.ToString("O")+" "+message+"\n");
   Map02ObjectiveObject Mark(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(x=>x.chapterId==id);
   IEnumerator Tap(Key key){Keys(key);yield return null;Keys();yield return null;}
   IEnumerator Capture(string label){ScreenCapture.CaptureScreenshot(Folder+"/"+(screenshot++).ToString("D2")+"-"+label+".png");yield return null;}
   IEnumerator Conversations(){if(realPace){float until=Time.realtimeSinceStartup+400;while(c.CurrentScene!=null||c.Transitioning){Assert.Less(Time.realtimeSinceStartup,until,"Scene never ended on its own: "+c.CurrentScene?.id);yield return null;}yield return WaitGameSeconds(.2f);yield break;}float deadline=Time.realtimeSinceStartup+150;while(c.CurrentScene!=null||c.Transitioning){Assert.Less(Time.realtimeSinceStartup,deadline,"Scene did not return input: "+c.CurrentScene?.id);if(c.CurrentScene!=null){yield return WaitGameSeconds(.8f);yield return Tap(Key.E);}else yield return null;}yield return WaitGameSeconds(.2f);if(!c.DayActivityActive)Assert.False(m.InputBlocked,"Scene ended but left controls locked.");}
   IEnumerator Walk(Vector3 destination,bool companion=false){
    yield return null;var path=new NavMeshPath();Assert.True(NavMesh.SamplePosition(destination,out var end,2,NavMesh.AllAreas),"No NavMesh destination "+destination);
    Assert.True(NavMesh.CalculatePath(m.Player.position,end.position,NavMesh.AllAreas,path));Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status,"Incomplete route to "+destination);int corner=1;
    var last=m.Player.position;float stuck=Time.realtimeSinceStartup,deadline=Time.realtimeSinceStartup+200;
    while(Vector3.ProjectOnPlane(m.Player.position-destination,Vector3.up).magnitude>1.15f){
     if(c.CurrentScene!=null){Keys();yield break;}
     Assert.Less(Time.realtimeSinceStartup,deadline,"Walking timed out towards "+destination+" from "+m.Player.position);
     if(companion&&c.Companion!=null&&Vector3.Distance(c.Companion.transform.position,m.Player.position)>3.8f){Keys();yield return null;continue;}
     if(Vector3.Distance(last,m.Player.position)>.25f){last=m.Player.position;stuck=Time.realtimeSinceStartup;}Assert.Less(Time.realtimeSinceStartup-stuck,12,"Player stuck at "+m.Player.position+" towards "+destination);
     while(corner<path.corners.Length-1&&Vector3.ProjectOnPlane(path.corners[corner]-m.Player.position,Vector3.up).magnitude<.65f)corner++;
     var delta=Vector3.ProjectOnPlane((corner<path.corners.Length?path.corners[corner]:destination)-m.Player.position,Vector3.up).normalized;
     var forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);float f=Vector3.Dot(delta,forward),r=Vector3.Dot(delta,right);var keys=new List<Key>();if(f>.35f)keys.Add(Key.W);if(f<-.35f)keys.Add(Key.S);if(r>.35f)keys.Add(Key.D);if(r<-.35f)keys.Add(Key.A);Keys(keys.ToArray());yield return null;
    }
    Keys();yield return WaitGameSeconds(.15f);
   }
   IEnumerator Use(string id){c.Track(id);yield return Walk(Mark(id).transform.position);yield return Tap(Key.E);yield return WaitGameSeconds(.15f);Log(id+" E · scene="+c.CurrentScene?.id+" activity="+c.DayActivityName);}
   IEnumerator Escort(string id,Vector3 destination){yield return Use(id);Assert.True(c.Companion!=null||(id=="N1_02"||id=="N1_03")&&(c.State.dayOne.postsReached&(id=="N1_02"?1:2))!=0,id+" did not start escort with E");yield return Walk(destination,true);float end=Time.realtimeSinceStartup+35;while(c.CurrentScene==null&&Get<Map02ChapterActor>("escort")!=null){Keys();Assert.Less(Time.realtimeSinceStartup,end,id+" companion never reached destination: "+c.Companion?.transform.position);yield return null;}if(c.CurrentScene!=null)yield return Conversations();}
   bool Solve(Map02DayOneState state,int i=0){if(i==3)return state.Packed;for(int r=0;r<2;r++)for(int x=2;x<6;x++)for(int y=0;y<4;y++){if(!state.PlaceCrate(i,x,y,r==1,out _))continue;if(Solve(state,i+1))return true;state.crates[i]=new Map02DayOneState.Placement();}return false;}
   static Key Digit(int i)=>new[]{Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5}[i];
   public IEnumerator Run(){
    Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/steps.txt","Fresh landing; all task progress comes from keyboard interactions and normal movement. No teleport, Complete, EndScene or Restore-prefix.\n");
    var input=RouteInputToGame();kb=Keyboard.current??InputSystem.AddDevice<Keyboard>();float previousScale=Time.timeScale;Time.timeScale=realPace?1:2;started=lastMark=Time.time;
    try {
     float deadline=Time.realtimeSinceStartup+180;while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;yield return null;}
     Assert.NotNull(m?.Chapter,"Natural boat arrival did not hand control to mission.");c=m.Chapter;camera=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.enabled&&x.targetTexture==null);Assert.AreEqual(0,c.State.completed.Count);Log("Natural arrival complete");yield return Conversations();yield return Capture("arrival");Lap("Cập bến (thuyền + bàn giao)");
     yield return Use("D1_01");Assert.NotNull(c.CurrentScene,"Contact must react to E");yield return Conversations();Assert.True(c.State.Done("D1_01"));Lap("D1_01 gặp anh Thành");
     yield return Escort("D1_02",c.Sites["dayOneFamily"].position);Assert.True(c.State.Done("D1_02"));yield return Capture("bandage-done");Lap("D1_02 dìu Hùng, thay băng");
     yield return Use("D1_03");yield return Conversations();Assert.True(c.State.Done("D1_03"));Assert.AreEqual(1,c.State.dayOne.medicineMask&1);Lap("D1_03 trao thuốc");
     for(int i=0;i<3;i++){yield return Use("D1_04");Assert.AreEqual("D1_04",c.State.carry);yield return Walk(Mark("D1_04").transform.position);yield return Tap(Key.E);Assert.AreEqual(i+1,c.State.Count("D1_04"));Assert.IsNull(c.State.carry);}
     yield return Use("D1_04");Assert.AreEqual("Packing",c.DayActivityName);yield return Tap(Key.Enter);Assert.False(c.State.Done("D1_04"));
     var plan=new Map02DayOneState();plan.Initialize(c.State.dayOne.seed);Assert.True(Solve(plan));
     for(int i=0;i<3;i++){yield return Tap(Digit(i));if(plan.crates[i].rotated)yield return Tap(Key.R);var target=plan.crates[i];var cursor=Get<Vector2Int>("packingCursor");while(cursor.x!=target.x){yield return Tap(cursor.x<target.x?Key.RightArrow:Key.LeftArrow);cursor=Get<Vector2Int>("packingCursor");}while(cursor.y!=target.y){yield return Tap(cursor.y<target.y?Key.UpArrow:Key.DownArrow);cursor=Get<Vector2Int>("packingCursor");}yield return Tap(Key.E);Assert.AreEqual(target.x,c.State.dayOne.crates[i].x);}
     yield return Capture("packing");yield return Tap(Key.Enter);Assert.True(c.State.Done("D1_04"));yield return WaitGameSeconds(.3f);Lap("D1_04 khiêng 3 thùng, xếp kho");
     yield return Use("D1_05");yield return Conversations();Assert.AreEqual("Dock",c.DayActivityName);
     int hits=c.State.dayOne.stakeHits;yield return Tap(Key.Space);Keys(Key.Space);yield return WaitGameSeconds(2);Keys();yield return null;Assert.LessOrEqual(c.State.dayOne.stakeHits,hits+1,"Held Space must not auto-complete stakes.");
     float dockDeadline=Time.realtimeSinceStartup+50;while(c.State.dayOne.stakeHits<6){Assert.Less(Time.realtimeSinceStartup,dockDeadline,"Dock timing input failed to register");float clock=Get<float>("dayClock"),needle=Mathf.PingPong(clock/Map02DayOneState.SweepSeconds(c.State.dayOne.stakeHits/3),1);if(needle>.46f&&needle<.54f){yield return Tap(Key.Space);}else yield return null;}
     for(int board=0;board<3;board++){int index=Array.IndexOf(c.State.dayOne.boardOrder,board);yield return Tap(Digit(index));while(Get<float>("dockOffset")>.02f){Keys(Key.A);yield return null;}Keys();yield return null;yield return Tap(Key.E);Assert.AreEqual(board+1,c.State.dayOne.BoardCount);}
     yield return Capture("dock");yield return Tap(Key.E);yield return Conversations();Assert.True(c.State.Done("D1_05"));Lap("D1_05 sửa bến");
     yield return Use("D1_06");yield return Conversations();Assert.AreEqual(1,c.State.phase);yield return Capture("night-start");Lap("D1_06 báo cáo, sang đêm");float dayOne=Time.time-started,night=Time.time;
     yield return Use("N1_01");Assert.NotNull(c.CurrentScene);yield return Conversations();Assert.True(c.State.Done("N1_01"));Lap("N1_01 bữa cơm Lời hứa");
     foreach(var id in new[]{"N1_02","N1_03"}){
      yield return Escort(id,c.Sites[id=="N1_02"?"bamboo":"gate"].position);int p=id=="N1_02"?0:1;Assert.AreNotEqual(0,c.State.dayOne.postsReached&(1<<p));
      for(int part=0;part<3;part++){if((c.State.dayOne.posts[p].faults&(1<<part))==0)continue;var props=Get<List<Transform>>(part==0?"postLamps":part==1?"postGates":"postBundles");yield return Walk(props[p].position);yield return Tap(Key.E);Assert.True(c.DayActivityActive,id+" prop "+part+" did not respond to E; "+c.Instruction);if(part==0){while(!c.State.dayOne.posts[p].LampSafe){Keys(Key.D);yield return null;}Keys();yield return null;yield return Tap(Key.E);}else if(part==1){yield return Tap(Key.A);yield return Tap(Key.D);yield return Tap(Key.E);}else{yield return Tap(Key.Digit2);yield return Tap(Key.E);}Assert.False(c.DayActivityActive);yield return WaitGameSeconds(.2f);}
      yield return Use(id);Assert.NotNull(c.CurrentScene,id+" final report E did not respond");yield return Conversations();Assert.True(c.State.Done(id));Log(id+" complete");Lap(id+(id=="N1_02"?" chốt bờ tre (vịt)":" chốt đầu làng"));
     }
     yield return Use("N1_04");yield return Conversations();Assert.True(c.State.Done("N1_04"));Assert.AreEqual(2,c.State.phase);Lap("N1_04 đổi ca");Assert.AreEqual(10,c.State.completed.Count);Assert.False(m.InputBlocked);yield return Capture("day-two-handover");File.WriteAllText(Folder+"/completed.json",JsonUtility.ToJson(m.Capture(),true));Log("PASS: all ten objectives completed through real movement and key inputs");
     if(realPace){float nightOne=Time.time-night;File.WriteAllText(Folder+"/durations-realpace.txt",$"Ngày 1 (cập bến → sang đêm): {dayOne/60:F2} phút · doc 18–22\nĐêm 1 (bữa cơm → đổi ca): {nightOne/60:F2} phút · doc 12–14\nBot đi thẳng và giải câu đố ngay; người chơi thật lâu hơn.\n\n"+string.Join("\n",marks)+"\n");}
    }finally{Keys();Time.timeScale=previousScale;RestoreInputRouting(input);}
   }
  }
 }
}
