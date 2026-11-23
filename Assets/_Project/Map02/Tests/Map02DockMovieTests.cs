using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
 public sealed class DockMovieCapture:MonoBehaviour {
  public string Folder="Logs/DockMovie_20261121";
  public bool recording;public int frames,channels=2;public string input="";public Vector2 pointer=new(-100,-100);
  BinaryWriter audio;bool audioActive;
  public void Begin(){Directory.CreateDirectory(Folder);audio=new BinaryWriter(File.Create(Folder+"/audio.f32"));channels=AudioSettings.speakerMode==AudioSpeakerMode.Mono?1:2;audioActive=AudioRenderer.Start();recording=true;StartCoroutine(Capture());}
  IEnumerator Capture(){while(recording){
   ScreenCapture.CaptureScreenshot(Folder+"/frame-"+(frames++).ToString("D5")+".png");
   if(audioActive){int count=AudioRenderer.GetSampleCountForCaptureFrame();if(count>0){using(var buffer=new NativeArray<float>(count*channels,Allocator.Temp)){if(AudioRenderer.Render(buffer)){var data=buffer.ToArray();byte[] bytes=new byte[data.Length*4];Buffer.BlockCopy(data,0,bytes,0,bytes.Length);audio.Write(bytes);}}}}
   yield return null;
  }}
  public void End(){recording=false;if(audioActive){AudioRenderer.Stop();audioActive=false;}audio?.Dispose();audio=null;File.WriteAllText(Folder+"/capture.txt",$"frames={frames}\nfps=24\nrate={AudioSettings.outputSampleRate}\nchannels={channels}\n");}
  void OnDestroy(){if(audio!=null)End();}
  void OnGUI(){if(!recording)return;GUI.depth=-1000;
   if(input!=""){GUI.color=new Color(0,0,0,.85f);GUI.DrawTexture(new Rect(20,72,310,38),Texture2D.whiteTexture);GUI.color=Color.white;GUI.Label(new Rect(30,76,290,33),input,new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold,normal={textColor=new Color(1,.85f,.4f)}});}
   if(pointer.x>=0){GUI.color=new Color(1,.8f,.25f);GUI.DrawTexture(new Rect(pointer.x-4,pointer.y-4,10,10),Texture2D.whiteTexture);GUI.color=Color.white;}
  }
 }
 public sealed class Map02DockMovieTests:ForestSceneTestBase {
  const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
  Map02ChapterDirector chapter;Keyboard keyboard;DockMovieCapture recorder;
  object Call(string name,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(name,F).Invoke(chapter,args);
  T Get<T>(string name)=>(T)typeof(Map02ChapterDirector).GetField(name,F).GetValue(chapter);
  IEnumerator Film(float seconds){float stop=Time.time+seconds;while(Time.time<stop)yield return null;}
  void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
  IEnumerator Tap(Key key){recorder.input="PHÍM "+key.ToString().ToUpperInvariant();Keys(key);yield return Film(.09f);Keys();yield return Film(.22f);recorder.input="";}
  IEnumerator Click(float x,float y){var p=new Vector2(x*Screen.width/1600f,y*Screen.height/900f);recorder.pointer=p;recorder.input="BẤM CHUỘT TRÁI";
   EditorGUIUtility.QueueGameViewInputEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=p});yield return Film(.13f);
   EditorGUIUtility.QueueGameViewInputEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=p});yield return Film(.35f);recorder.input="";
  }
  IEnumerator Tutorial(){yield return Film(.3f);Assert.True(Get<bool>("dayHelpVisible"));for(int page=0;page<3;page++){yield return Film(2.4f);yield return Click(1175,729);}Assert.False(Get<bool>("dayHelpVisible"));recorder.pointer=new Vector2(-100,-100);yield return Film(.5f);}
  [UnityTest,Timeout(600000),Explicit("Record a real-input minigame demo, excluded from routine CI.")]
  public IEnumerator RecordDockTutorialAndCompleteRepair(){
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1280,720);yield return new EnterPlayMode();
   Map02Mission mission=null;float end=Time.realtimeSinceStartup+100;
   while(Time.realtimeSinceStartup<end){mission=Object.FindFirstObjectByType<Map02Mission>();Object.FindFirstObjectByType<Map02Arrival>()?.Skip();if(mission?.Ready==true&&mission.Chapter!=null)break;yield return null;}
   Assert.NotNull(mission?.Chapter);chapter=mission.Chapter;chapter.Cancel();var save=mission.Capture();save.chapter=new Map02ChapterState();foreach(var task in Map02ChapterDefinition.Load().tasks){if(task.id=="D1_05")break;save.chapter.Complete(task.id);}save.chapter.dayOne.arrivalSeen=true;mission.Restore(save);
   var routing=RouteInputToGame();keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();int previousRate=Time.captureFramerate;float previousScale=Time.timeScale;Time.timeScale=1;Time.captureFramerate=24;
   recorder=new GameObject("Dock video capture").AddComponent<DockMovieCapture>();
   try{
    var kind=typeof(Map02ChapterDirector).GetNestedType("DayActivity",BindingFlags.NonPublic);Call("OpenDay",Enum.Parse(kind,"Dock"),Map02ChapterDefinition.Load().Find("D1_05"));yield return null;yield return null;
    recorder.Begin();yield return Tutorial();yield return Film(1);
    float deadline=Time.time+90;
    while(chapter.State.dayOne.stakeHits<6){Assert.Less(Time.time,deadline,"Six hits must be achievable through Space");float needle=Mathf.PingPong(Get<float>("dayClock")/Map02DayOneState.SweepSeconds(chapter.State.dayOne.stakeHits/3),1);if(!Get<bool>("dayHelpVisible")&&needle>.47f&&needle<.53f)yield return Tap(Key.Space);else yield return null;}
    yield return Film(1);yield return Tutorial();
    for(int board=0;board<3;board++){
     int card=Array.IndexOf(chapter.State.dayOne.boardOrder,board);yield return Click(290,290+card*90);Assert.AreEqual(board,Get<int>("selectedBoard"));yield return Film(1.1f);
     recorder.pointer=new Vector2(-100,-100);recorder.input="GIỮ A · CĂN VÁN SANG TRÁI";Keys(Key.A);while(Get<float>("dockOffset")>.02f)yield return null;Keys();recorder.input="";yield return Film(1.0f);
     yield return Click(1340,627);Assert.AreEqual(board+1,chapter.State.dayOne.BoardCount);yield return Film(1.2f);
    }
    yield return Film(2);yield return Click(1340,627);Assert.False(chapter.DayActivityActive);recorder.pointer=new Vector2(-100,-100);
    deadline=Time.time+70;while(chapter.CurrentScene!=null||chapter.Transitioning){Assert.Less(Time.time,deadline,"Repair scene must return control");yield return Film(1.4f);yield return Tap(Key.E);}
    Assert.True(chapter.State.Done("D1_05"));yield return Film(2.5f);recorder.End();yield return null;yield return null;
   }finally{if(recorder!=null){recorder.End();Object.Destroy(recorder.gameObject);}Keys();chapter.Cancel();InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing);Time.captureFramerate=previousRate;Time.timeScale=previousScale;}
   yield return new ExitPlayMode();
  }
  IEnumerator DragLamp(){
   recorder.input="GIỮ CHUỘT TRÁI · KÉO CHỤP ĐÈN 3D";
   Vector2 Point(float x)=>new Vector2(x*Screen.width/1600f,500*Screen.height/900f);
   var start=Point(220);recorder.pointer=start;
   EditorGUIUtility.QueueGameViewInputEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=start});yield return Film(.2f);
   for(int step=1;step<=60;step++){
    var p=Point(220+500f*step/60);recorder.pointer=p;
    EditorGUIUtility.QueueGameViewInputEvent(new Event{type=EventType.MouseDrag,button=0,mousePosition=p,delta=new Vector2(500f/60*Screen.width/1600f,0)});yield return Film(.042f);
   }
   EditorGUIUtility.QueueGameViewInputEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=Point(720)});yield return Film(1);recorder.input="";
  }
  IEnumerator WalkTo(Vector3 destination){
   var path=new UnityEngine.AI.NavMeshPath();Assert.True(UnityEngine.AI.NavMesh.SamplePosition(destination,out var goal,2,UnityEngine.AI.NavMesh.AllAreas));
   Assert.True(UnityEngine.AI.NavMesh.CalculatePath(Object.FindFirstObjectByType<Map02Mission>().Player.position,goal.position,UnityEngine.AI.NavMesh.AllAreas,path));
   int corner=1;float until=Time.time+40;var player=Object.FindFirstObjectByType<Map02Mission>().Player;var camera=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c=>c.enabled&&c.targetTexture==null);
   while(Vector3.ProjectOnPlane(player.position-destination,Vector3.up).magnitude>1.1f){
    Assert.Less(Time.time,until,"Walk to post prop timed out");while(corner<path.corners.Length-1&&Vector3.ProjectOnPlane(path.corners[corner]-player.position,Vector3.up).magnitude<.6f)corner++;
    var direction=Vector3.ProjectOnPlane(path.corners[Mathf.Min(corner,path.corners.Length-1)]-player.position,Vector3.up).normalized;
    var forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
    var keys=new System.Collections.Generic.List<Key>();float x=Vector3.Dot(direction,right),y=Vector3.Dot(direction,forward);
    if(x>.25f)keys.Add(Key.D);if(x<-.25f)keys.Add(Key.A);if(y>.25f)keys.Add(Key.W);if(y<-.25f)keys.Add(Key.S);Keys(keys.ToArray());yield return null;
   }Keys();yield return Film(.3f);
  }
  [UnityTest,Timeout(600000),Explicit("Record night-watch minigames with actual input, excluded from routine CI.")]
  public IEnumerator RecordNightWatchTutorialAndAllThreeChecks(){
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1280,720);yield return new EnterPlayMode();
   Map02Mission mission=null;float end=Time.realtimeSinceStartup+100;
   while(Time.realtimeSinceStartup<end){mission=Object.FindFirstObjectByType<Map02Mission>();Object.FindFirstObjectByType<Map02Arrival>()?.Skip();if(mission?.Ready==true&&mission.Chapter!=null)break;yield return null;}
   Assert.NotNull(mission?.Chapter);chapter=mission.Chapter;chapter.Cancel();var save=mission.Capture();save.chapter=new Map02ChapterState();foreach(var task in Map02ChapterDefinition.Load().tasks){if(task.id=="N1_02")break;save.chapter.Complete(task.id);}save.chapter.dayOne.arrivalSeen=true;mission.Restore(save);chapter.Track("N1_02");Call("StartPost",Map02ChapterDefinition.Load().Find("N1_02"));
   var post=chapter.State.dayOne.posts[0];post.faults=7;post.lampAngle=0;post.gateClosed=post.latchSeated=post.bundleStowed=false;Call("SyncDayProps");
   var lamps=Get<System.Collections.Generic.List<Transform>>("postLamps");var gates=Get<System.Collections.Generic.List<Transform>>("postGates");var wood=Get<System.Collections.Generic.List<Transform>>("postBundles");
   var cc=mission.Player.GetComponent<CharacterController>();cc.enabled=false;mission.Player.position=lamps[0].position-Vector3.up*.9f-lamps[0].forward*.85f;cc.enabled=true;
   var routing=RouteInputToGame();keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();int previousRate=Time.captureFramerate;float previousScale=Time.timeScale;Time.timeScale=1;Time.captureFramerate=24;
   recorder=new GameObject("Night watch video capture").AddComponent<DockMovieCapture>();recorder.Folder="Logs/NightWatchPerimeterMovie_20261123";
   try{
        yield return Film(.5f);recorder.Begin();
    var movieRig=typeof(Map02ChapterDirector).GetField("rig",F).GetValue(chapter);var worldGate=gates[0];
    var movieEye=worldGate.position+worldGate.rotation*new Vector3(3.3f,2.3f,-5.6f);var movieFocus=worldGate.position+Vector3.up*.8f;
    movieRig.GetType().GetMethod("SetCinematicView").Invoke(movieRig,new object[]{movieEye,Quaternion.LookRotation(movieFocus-movieEye),52f,1f});
    yield return Film(3.5f);movieRig.GetType().GetMethod("ClearCinematicView").Invoke(movieRig,null);yield return Film(1.2f);yield return Tap(Key.E);Assert.AreEqual("Lamp",chapter.DayActivityName);yield return Tutorial();
    yield return Click(1220,641);Assert.True(chapter.DayActivityActive,"Unsafe light must remain open");yield return Film(1.2f);
    yield return DragLamp();Assert.True(post.LampSafe);yield return Film(1.5f);yield return Click(1220,641);Assert.False(chapter.DayActivityActive);recorder.pointer=new Vector2(-100,-100);
    yield return WalkTo(gates[0].GetComponent<Map02NightWatchVisual>().InteractionPosition);yield return Tap(Key.E);Assert.AreEqual("Gate",chapter.DayActivityName);yield return Tutorial();
    yield return Click(1220,516);Assert.False(post.latchSeated);yield return Film(1.3f);yield return Click(1220,445);yield return Film(1);yield return Click(1220,516);Assert.True(post.gateClosed&&post.latchSeated);yield return Film(1);yield return Click(1220,641);Assert.False(chapter.DayActivityActive);recorder.pointer=new Vector2(-100,-100);
    yield return WalkTo(wood[0].position);yield return Tap(Key.E);Assert.AreEqual("Path",chapter.DayActivityName);yield return Tutorial();
    yield return Click(1220,445);Assert.False(post.bundleStowed);yield return Film(1.3f);yield return Click(1220,516);Assert.True(post.bundleStowed);yield return Film(1.3f);yield return Click(1220,641);Assert.True(post.Safe);Assert.False(chapter.State.Done("N1_02"));recorder.pointer=new Vector2(-100,-100);
    var keeper=(Map02ChapterActor)Call("PostKeeper","N1_02");yield return WalkTo(keeper.transform.position);yield return Tap(Key.E);Assert.NotNull(chapter.CurrentScene,"Report must start after all three checks");
    float deadline=Time.time+70;while(chapter.CurrentScene!=null||chapter.Transitioning){Assert.Less(Time.time,deadline);yield return Film(1.8f);yield return Tap(Key.E);}Assert.True(chapter.State.Done("N1_02"));yield return Film(2.5f);recorder.End();yield return null;yield return null;
   }finally{if(recorder!=null){recorder.End();Object.Destroy(recorder.gameObject);}Keys();chapter.Cancel();InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing);Time.captureFramerate=previousRate;Time.timeScale=previousScale;}
   yield return new ExitPlayMode();
  } }
}
