using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
public sealed class Map02WorkViewQaTests:ForestSceneTestBase {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 [UnityTest,Timeout(300000)]public IEnumerator WorkRemainsVisibleAndBandageUsesTheActualSceneCamera(){
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1600,900);yield return new EnterPlayMode();yield return Check();yield return new ExitPlayMode();
 }
 static IEnumerator Check(){
  Map02Mission m=null;float until=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
  Assert.NotNull(m?.Chapter);var c=m.Chapter;c.Cancel();c.enabled=false;
  object Call(string name,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(name,F).Invoke(c,args);
  void Set(string name,object value)=>typeof(Map02ChapterDirector).GetField(name,F).SetValue(c,value);
  var lens=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.enabled&&x.targetTexture==null);
  void Step(){Call("LateUpdate");lens.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>().SendMessage("LateUpdate");}
  string folder="Logs/Map02Qa_20261027/Playback";Directory.CreateDirectory(folder);
  var rt=RenderTexture.GetTemporary(720,405,24);var tex=new Texture2D(720,405,TextureFormat.RGB24,false);
  void Capture(string path){var previous=lens.targetTexture;var active=RenderTexture.active;try{lens.targetTexture=rt;lens.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,720,405),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}finally{lens.targetTexture=previous;RenderTexture.active=active;}}
  try{
   var save=m.Capture();var state=new Map02ChapterState();state.Complete("D1_01");state.dayOne.arrivalSeen=true;save.chapter=state;m.Restore(save);
   c.Actor("Hùng").StopAt(c.Sites["dayOneFamily"].position+Vector3.forward*.6f);Call("BeginScene",Map02ChapterDefinition.Load().Find("D1_02"),0,0);
   for(int frame=0;frame<288;frame++){Set("sceneClock",frame/12f);Step();Capture(folder+"/bandage-"+frame.ToString("D4")+".png");yield return null;}
   c.Cancel();c.State.Complete("D1_02");c.State.introduced.Add("D1_05");var dock=c.Sites["dock"].position;
   var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.SetPositionAndRotation(dock+new Vector3(-.525f,0,.3f),Quaternion.identity);cc.enabled=true;
   var activity=Enum.Parse(typeof(Map02ChapterDirector).GetNestedType("DayActivity",BindingFlags.NonPublic),"Dock");Call("OpenDay",activity,Map02ChapterDefinition.Load().Find("D1_05"));Set("dayInputReady",true);c.State.dayOne.stakeHits=6;Call("SyncDayProps");Set("selectedBoard",0);Set("dockOffset",.24f);
   for(int i=0;i<30;i++){Step();yield return null;}Step();
   var centre=lens.WorldToViewportPoint(dock+new Vector3(.24f,.36f,1.05f));Assert.Greater(centre.z,0);Assert.That(centre.x,Is.InRange(.15f,.85f));Assert.That(centre.y,Is.InRange(.30f,.85f),"The work must be above the HUD strip");
   foreach(float x in new[]{-.65f,.65f})foreach(float z in new[]{.95f,1.95f}){var at=lens.WorldToViewportPoint(dock+new Vector3(x,.27f,z));Assert.That(at.x,Is.InRange(.08f,.92f));Assert.That(at.y,Is.InRange(.27f,.92f),"Both bearings and all steps remain visible");}
   float screenRight=lens.WorldToViewportPoint(dock+new Vector3(.24f,.36f,1.05f)).x;float screenLeft=lens.WorldToViewportPoint(dock+new Vector3(-.24f,.36f,1.05f)).x;Assert.Greater(screenRight-screenLeft,.05f,"D/right must move the world preview to screen-right");
   Capture(folder+"/dock-overhead.png");ScreenCapture.CaptureScreenshot(folder+"/dock-overhead-ui.png");yield return null;
  }finally{c.Cancel();c.enabled=true;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);}
 }
}
}
