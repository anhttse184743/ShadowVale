using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
 public sealed class BoatPostDemoHud:MonoBehaviour {
  public string caption;public Vector2 pointer=new(-100,-100);
  void OnGUI(){GUI.depth=-1000;var s=new GUIStyle(GUI.skin.label){fontSize=19,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};GUI.Box(new Rect(10,4,Screen.width-20,31),GUIContent.none);GUI.Label(new Rect(12,4,Screen.width-24,30),caption,s);if(pointer.x>=0)GUI.Label(new Rect(pointer.x,pointer.y,25,30),"➤",new GUIStyle(s){fontSize=25});}
 }
 public sealed class BoatPostDemoTests:ForestSceneTestBase {
  const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
  const string Root="Logs/BoatPostsDemo_20261027";
  static int frame;static string folder;static BoatPostDemoHud hud;
  static void Photo(){ScreenCapture.CaptureScreenshot(folder+"/frame-"+(frame++).ToString("D5")+".png");}
  static IEnumerator Film(float seconds){float stop=Time.time+seconds;while(Time.time<stop){Photo();yield return null;}}
  [UnityTest,Timeout(480000)]public IEnumerator RecordBoatAndVerifyCarry(){
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");SetPreviewResolution(1200,675);yield return new EnterPlayMode();
   int old=Time.captureFramerate;Time.captureFramerate=12;folder=Root+"/Boat";Directory.CreateDirectory(folder);frame=0;hud=new GameObject("Demo caption").AddComponent<BoatPostDemoHud>();Object.DontDestroyOnLoad(hud.gameObject);
   try{
    var m=Object.FindFirstObjectByType<Map01Mission>();float deadline=Time.realtimeSinceStartup+100;while(m==null||!m.IsInitialized){Assert.Less(Time.realtimeSinceStartup,deadline);m=Object.FindFirstObjectByType<Map01Mission>();yield return null;}
    m.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ExtractionStage);var ex=m.GetComponentInChildren<Map01Extraction>();ex.autoContinueToMap2=true;
    foreach(var e in m.Enemies)e.GetComponent<Health>().TakeDamage(99999,e.transform.position,null);
    while(!ex.Prepared){Assert.Less(Time.realtimeSinceStartup,deadline);yield return null;}
    var cc=m.player.GetComponent<CharacterController>();cc.enabled=false;m.player.position=ex.approach;cc.enabled=true;
    float end=Time.realtimeSinceStartup+330;bool boarding=false,arrivalSeen=false;
    while(Time.realtimeSinceStartup<end){
     var a=Object.FindFirstObjectByType<Map02Arrival>();
     if(a!=null){
      if(a.CurrentPhase==Map02Arrival.Phase.Gameplay){hud.caption="ĐẦU MAP 2 · trả điều khiển sau khi lên bờ";yield return Film(2);break;}
      float clock=(float)typeof(Map02Arrival).GetField("clock",F).GetValue(a);
      if(a.CurrentPhase==Map02Arrival.Phase.Approaching||a.CurrentPhase==Map02Arrival.Phase.Mooring){var carry=a.Passengers[0].GetComponentInChildren<Map01BoatCarry>();if(carry!=null){Assert.True(carry.Slung,"Arrival must keep Nam's gun off his hands");Assert.AreNotEqual(carry.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.RightHand),carry.Gun.parent);arrivalSeen=true;}}
      if((a.CurrentPhase==Map02Arrival.Phase.Approaching&&clock<6)||a.CurrentPhase==Map02Arrival.Phase.Mooring||(a.CurrentPhase==Map02Arrival.Phase.Disembarking&&clock<14)){hud.caption="ĐẦU MAP 2 · Nam đeo súng, chỉ huy chèo, Hùng giữ tay bị thương";Photo();}
     }else if(ex!=null){
      float clock=(float)typeof(Map01Extraction).GetField("clock",F).GetValue(ex);
      if(ex.CurrentPhase==Map01Extraction.Phase.Seating){var carry=ex.NamAnimator.GetComponent<Map01BoatCarry>();Assert.NotNull(carry);Assert.True(carry.Slung);boarding=true;}
      if(ex.CurrentPhase>=Map01Extraction.Phase.Approach&&ex.CurrentPhase<=Map01Extraction.Phase.Seating){hud.caption="CUỐI MAP 1 · Nam lên thuyền với súng đeo vai";Photo();}
      if(ex.CurrentPhase==Map01Extraction.Phase.Departing&&clock<6){hud.caption="RỜI BẾN · chỉ rút súng khi có mục tiêu để bắn yểm trợ";Photo();}
     }
     yield return null;
    }
    Assert.True(boarding);Assert.True(arrivalSeen);Assert.AreEqual(Map02Arrival.Phase.Gameplay,Object.FindFirstObjectByType<Map02Arrival>().CurrentPhase);
   }finally{Time.captureFramerate=old;if(hud!=null)Object.Destroy(hud.gameObject);}yield return new ExitPlayMode();
  }
  [UnityTest,Timeout(360000)]public IEnumerator RecordPostsWithActualMouseAndKeyboard(){
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1200,675);yield return new EnterPlayMode();yield return Posts();yield return new ExitPlayMode();
  }
  static IEnumerator Posts(){
   Map02Mission m=null;float until=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}Assert.NotNull(m?.Chapter);
   var c=m.Chapter;c.Cancel();var save=m.Capture();var state=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id=="N1_02")break;state.Complete(t.id);}state.dayOne.arrivalSeen=true;save.chapter=state;m.Restore(save);yield return WaitGameSeconds(1);
   var routing=RouteInputToGame();var kb=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();kb.MakeCurrent();mouse.MakeCurrent();int old=Time.captureFramerate;Time.captureFramerate=12;folder=Root+"/Posts";Directory.CreateDirectory(folder);frame=0;hud=new GameObject("Demo caption").AddComponent<BoatPostDemoHud>();
   object Call(string n,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(n,F).Invoke(c,args);
   void Open(string activity,int index){typeof(Map02ChapterDirector).GetField("postIndex",F).SetValue(c,index);c.State.dayOne.postsReached|=1<<index;Call("OpenDay",Enum.Parse(typeof(Map02ChapterDirector).GetNestedType("DayActivity",BindingFlags.NonPublic),activity),Map02ChapterDefinition.Load().Find(index==0?"N1_02":"N1_03"));}
   IEnumerator Click(float x,float y){float s=Mathf.Min(Screen.width/1600f,Screen.height/900f);var p=new Vector2((Screen.width-1600*s)*.5f+x*s,(Screen.height-900*s)*.5f+y*s);hud.pointer=p;
    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(p.x,Screen.height-p.y)}.WithButton(MouseButton.Left));EditorGUIUtility.QueueGameViewInputEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=p});yield return Film(.25f);
    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(p.x,Screen.height-p.y)});EditorGUIUtility.QueueGameViewInputEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=p});yield return Film(.4f);
   }
   try{
    var p=c.State.dayOne.posts[0];p.lampAngle=0;p.gateClosed=p.latchSeated=p.bundleStowed=true;
    Open("Lamp",0);hud.caption="ĐÊM 1 · chốt bờ tre: xem hướng sáng, xoay chụp, xác nhận";yield return Film(2);yield return Click(1200,640);Assert.True(c.DayActivityActive,"Unsafe light must remain open");yield return Film(2);
    hud.pointer=new Vector2(-100,-100);InputSystem.QueueStateEvent(kb,new KeyboardState(Key.D));yield return Film(2.65f);InputSystem.QueueStateEvent(kb,new KeyboardState());yield return Film(1);Assert.True(p.LampSafe);yield return Click(1200,640);Assert.False(c.DayActivityActive);Assert.True((p.inspected&1)!=0);yield return Film(2);
    c.State.Complete("N1_02");c.Track("N1_03");p=c.State.dayOne.posts[1];p.lampAngle=180;p.gateClosed=p.latchSeated=p.bundleStowed=false;
    Open("Gate",1);hud.caption="ĐÊM 1 · cửa rào: phải khép cửa trước khi cài chốt";yield return Film(2);yield return Click(1200,515);Assert.False(p.latchSeated);yield return Film(2);yield return Click(1200,445);Assert.True(p.gateClosed);yield return Film(1);yield return Click(1200,515);Assert.True(p.latchSeated);yield return Film(1);yield return Click(1200,640);Assert.False(c.DayActivityActive);yield return Film(1);
    Open("Path",1);hud.caption="ĐÊM 1 · chuyển bó củi, chừa lối đi và khoảng mở cửa";yield return Film(2);yield return Click(1200,445);Assert.False(p.bundleStowed);yield return Film(2);yield return Click(1200,515);Assert.True(p.bundleStowed);yield return Film(1);yield return Click(1200,640);Assert.False(c.DayActivityActive);Assert.True(p.Safe);yield return Film(3);
    var loaded=m.Capture();m.Restore(loaded);Assert.AreEqual(6,c.State.dayOne.posts[1].inspected&6,"Inspection progress survives loading");Assert.True(c.State.dayOne.posts[1].Safe);Assert.False(m.InputBlocked);
   }finally{Time.captureFramerate=old;InputSystem.QueueStateEvent(kb,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());c.Cancel();InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(kb);RestoreInputRouting(routing);if(hud!=null)Object.Destroy(hud.gameObject);}
  }
 }
}
