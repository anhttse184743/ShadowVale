using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
public sealed class Map02CursorOwnershipTests:ForestSceneTestBase {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 [Test] public void DisabledCameraCannotRecaptureThePointerOnClick(){
  var go=new GameObject("Hidden village camera cursor regression");var target=new GameObject("Target");var mouse=InputSystem.AddDevice<Mouse>();var oldLock=Cursor.lockState;bool oldVisible=Cursor.visible;
  try{var lens=go.AddComponent<Camera>();lens.enabled=false;var rig=go.AddComponent<ThirdPersonCamera>();typeof(ThirdPersonCamera).GetMethod("Awake",F).Invoke(rig,null);rig.SetTarget(target.transform);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;mouse.MakeCurrent();
   typeof(ThirdPersonCamera).GetField("_cursorWanted",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
   typeof(ThirdPersonCamera).GetMethod("Start",F).Invoke(rig,null);
   Assert.False((bool)typeof(ThirdPersonCamera).GetField("_cursorWanted",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null),"A hidden camera must not capture the cursor on Start");
   InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(300,400)}.WithButton(MouseButton.Left));InputSystem.Update();
   typeof(ThirdPersonCamera).GetMethod("LateUpdate",F).Invoke(rig,null);
   Assert.False((bool)typeof(ThirdPersonCamera).GetField("_cursorWanted",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null),"A disabled rendering camera must never lock the global cursor while another UI owns it");
  }finally{InputSystem.RemoveDevice(mouse);Object.DestroyImmediate(go);Object.DestroyImmediate(target);Cursor.lockState=oldLock;Cursor.visible=oldVisible;}
 }
 [UnityTest,Timeout(300000)]public IEnumerator ActualMouseCanSaveSlotAndOperateDockAtThreeScales(){
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1600,900);yield return new EnterPlayMode();yield return LiveAudit();yield return new ExitPlayMode();
 }
 static IEnumerator LiveAudit(){
  Map02Mission m=null;float until=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
  Assert.NotNull(m?.Chapter);var c=m.Chapter;c.Cancel();var save=m.Capture();var state=new Map02ChapterState();state.Complete("D1_01");state.Complete("D1_02");state.dayOne.arrivalSeen=true;save.chapter=state;m.Restore(save);yield return WaitGameSeconds(1.5f);
  object Call(string name,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(name,F).Invoke(c,args);
  void Set(string name,object value)=>typeof(Map02ChapterDirector).GetField(name,F).SetValue(c,value);
  var menu=Object.FindFirstObjectByType<ForestMenu>();var routing=RouteInputToGame();var kb=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();kb.MakeCurrent();mouse.MakeCurrent();
  var camera=Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None).First(x=>x.GetComponent<Camera>().enabled);string folder="Logs/Map02CursorUi_20261027";Directory.CreateDirectory(folder);
  int physicalFrames=0;bool stolenAfterPhysicalInput=false;
  void ObserveInput(){if(InputState.currentUpdateType!=InputUpdateType.Dynamic||!mouse.leftButton.isPressed||!m.InputBlocked)return;physicalFrames++;camera.SendMessage("LateUpdate");if(Wanted())stolenAfterPhysicalInput=true;}
  InputSystem.onAfterUpdate+=ObserveInput;
  bool Wanted()=>(bool)typeof(ThirdPersonCamera).GetField("_cursorWanted",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  IEnumerator Escape(){typeof(ForestMenu).GetField("ignoreKeysUntil",F).SetValue(menu,0f);InputSystem.QueueStateEvent(kb,new KeyboardState(Key.Escape));yield return null;InputSystem.QueueStateEvent(kb,new KeyboardState());for(int f=0;f<3;f++)yield return null;}
  IEnumerator Pointer(Vector2 point,EventType type){
   float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);var at=new Vector2((Screen.width-1600*scale)/2+point.x*scale,(Screen.height-900*scale)/2+point.y*scale);
   var input=new MouseState{position=new Vector2(at.x,Screen.height-at.y),delta=new Vector2(12,7)};if(type!=EventType.MouseUp)input=input.WithButton(MouseButton.Left);mouse.MakeCurrent();InputSystem.QueueStateEvent(mouse,input);
   UnityEditor.EditorGUIUtility.QueueGameViewInputEvent(new Event{type=type,button=0,mousePosition=at});yield return new WaitForSecondsRealtime(.15f);for(int f=0;f<4;f++)yield return null;
   File.AppendAllText(folder+"/cursor.txt",$"{Screen.width}x{Screen.height} {type} {point} wanted={Wanted()} actual={Cursor.lockState} menu={ForestMenu.Visible} blocked={m.InputBlocked} inputReady={typeof(Map02ChapterDirector).GetField("dayInputReady",F).GetValue(c)} selected={typeof(Map02ChapterDirector).GetField("selectedBoard",F).GetValue(c)} held={mouse.leftButton.isPressed}\n");
   if(ForestMenu.Visible||c.DayActivityActive||m.InputBlocked)Assert.False(Wanted(),"No camera may recapture a pointer owned by the menu/minigame");
  }
  IEnumerator Click(Vector2 p){yield return Pointer(p,EventType.MouseDown);yield return Pointer(p,EventType.MouseUp);}
  try{
   File.WriteAllText(folder+"/cursor.txt",string.Join("\n",Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None).Select(x=>$"camera={x.name} driver={x.enabled} renders={x.GetComponent<Camera>().enabled} gate={(x.InputAllowed!=null?x.InputAllowed().ToString():"none")}"))+"\n");
   yield return Escape();Assert.True(ForestMenu.Visible);Assert.True(m.Paused);yield return Click(new Vector2(380,377));
   Assert.True((bool)typeof(ForestMenu).GetField("browser",F).GetValue(menu),"The real Lưu game click opens the slot browser");yield return Click(new Vector2(1040,360));yield return Click(new Vector2(750,780));
   if(typeof(ForestMenu).GetField("question",F).GetValue(menu)!=null)yield return Click(new Vector2(620,507));
   Assert.True(ForestSaveSlots.Exists(1));var entry=ForestSaveSlots.Read(1);Assert.AreEqual("Map 2",entry.sceneName);Assert.True(Map02Mission.Parse(entry.checkpoint).chapter.Done("D1_02"));StringAssert.Contains("Đã lưu",(string)typeof(ForestMenu).GetField("message",F).GetValue(menu));
   ScreenCapture.CaptureScreenshot(folder+"/saved-slot.png");yield return null;
   yield return Escape();yield return Escape();Assert.False(ForestMenu.Visible);Assert.False(m.Paused);yield return WaitGameSeconds(.2f);
   Call("OpenDay",Enum.Parse(typeof(Map02ChapterDirector).GetNestedType("DayActivity",BindingFlags.NonPublic),"Dock"),Map02ChapterDefinition.Load().Find("D1_05"));c.State.dayOne.stakeHits=6;Call("SyncDayProps");
   for(int i=0;i<3;i++){
    int width=new[]{1200,1600,1920}[i],height=new[]{675,900,1080}[i];SetPreviewResolution(width,height);float sizeUntil=Time.realtimeSinceStartup+10;int stable=0;while(stable<3){Assert.Less(Time.realtimeSinceStartup,sizeUntil,"GameView resize did not settle");yield return new WaitForSecondsRealtime(.2f);if(Screen.width==width&&Screen.height==height)stable++;else{stable=0;SetPreviewResolution(width,height);}}
    float readyUntil=Time.realtimeSinceStartup+10;while(!(bool)typeof(Map02ChapterDirector).GetField("dayInputReady",F).GetValue(c)){Assert.Less(Time.realtimeSinceStartup,readyUntil,"Opening input was not released");yield return null;}for(int f=0;f<4;f++)yield return null;
    Assert.AreEqual(width,Screen.width);Assert.AreEqual(height,Screen.height);float yaw=(float)typeof(ThirdPersonCamera).GetField("_yaw",F).GetValue(camera);
    yield return Click(new Vector2(290,290));Assert.AreEqual(c.State.dayOne.boardOrder[0],(int)typeof(Map02ChapterDirector).GetField("selectedBoard",F).GetValue(c));Assert.AreEqual(yaw,(float)typeof(ThirdPersonCamera).GetField("_yaw",F).GetValue(camera),.01f,"Mouse UI input must not rotate Nam's camera");
    yield return Click(new Vector2(825,660));Assert.Less(Mathf.Abs((float)typeof(Map02ChapterDirector).GetField("dockOffset",F).GetValue(c)),.07f);
    ScreenCapture.CaptureScreenshot(folder+"/dock-"+width+".png");yield return null;
   }
   yield return Click(new Vector2(1400,123));Assert.False(c.DayActivityActive);Assert.False(m.Player.GetComponent<ShadowVale.Gameplay.Combat.PlayerCombat>().HandsOccupied);Assert.Greater(physicalFrames,0,"The camera test must observe a held physical mouse button in Dynamic input");Assert.False(stolenAfterPhysicalInput);File.AppendAllText(folder+"/cursor.txt",$"physicalDynamicFrames={physicalFrames} stolen={stolenAfterPhysicalInput}\n");
  }finally{InputSystem.onAfterUpdate-=ObserveInput;InputSystem.QueueStateEvent(kb,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());c.Cancel();m.SetPaused(false);InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(kb);RestoreInputRouting(routing);}
 }

}
}
