using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Video;
namespace ShadowVale.Map01.Tests
{
 public sealed class NewGameIntroTests : ForestSceneTestBase
 {
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  [UnityTest,Timeout(180000)]public IEnumerator RealVideoRendersWithAudioPausesAndFinishesOnce()
  {
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
   yield return new EnterPlayMode();yield return PlayFull();yield return new ExitPlayMode();
  }
  static IEnumerator PlayFull()
  {
   var host=new GameObject("Intro playback audit");var intro=host.AddComponent<NewGameIntro>();
   int completed=0;string error=null;intro.Play(()=>completed++,e=>error=e);
   Assert.True(intro.Active);Assert.True(File.Exists(NewGameIntro.FilePath));
   float end=Time.realtimeSinceStartup+25;
   while((!intro.Prepared||intro.PlaybackTime<1.5)&&intro.Active&&Time.realtimeSinceStartup<end)yield return null;
   Assert.IsNull(error);Assert.True(intro.Prepared);Assert.Greater(intro.PlaybackTime,1);
   var player=host.GetComponentInChildren<VideoPlayer>();Assert.NotNull(player);Assert.Greater(player.audioTrackCount,0);
   Assert.NotNull(player.GetTargetAudioSource(0));Assert.AreEqual(1920,player.width);Assert.AreEqual(1080,player.height);
   var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);var previous=RenderTexture.active;RenderTexture.active=player.targetTexture;
   tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();RenderTexture.active=previous;
   var pixels=tex.GetPixels32();int lit=0;for(int i=0;i<pixels.Length;i+=53)if(pixels[i].r+pixels[i].g+pixels[i].b>20)lit++;
   Assert.Greater(lit,100,"Native player must decode actual picture, not only advance its clock.");
   Directory.CreateDirectory("Logs/NewGameIntro");File.WriteAllBytes("Logs/NewGameIntro/actual-frame.png",tex.EncodeToPNG());Object.Destroy(tex);
   intro.TogglePause();double pausedAt=intro.PlaybackTime;yield return new WaitForSecondsRealtime(.4f);
   Assert.True(intro.Paused);Assert.That(intro.PlaybackTime,Is.EqualTo(pausedAt).Within(.08));intro.TogglePause();
   end=Time.realtimeSinceStartup+70;while(intro.Active&&Time.realtimeSinceStartup<end)yield return null;
   Assert.IsNull(error);Assert.False(intro.Active);Assert.AreEqual(1,completed);
   intro.Skip();yield return null;Assert.AreEqual(1,completed);Assert.IsNull(host.GetComponentInChildren<VideoPlayer>());
   Object.Destroy(host);
  }
  [UnityTest,Timeout(180000)]public IEnumerator NewGameTutorialWaitsForIntroAndSkipLoadsOnlyOneTutorial()
  {
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
   yield return new EnterPlayMode();yield return MenuRoute();yield return new ExitPlayMode();
  }
  static IEnumerator MenuRoute()
  {
   var menu=Object.FindFirstObjectByType<ForestMenu>();Assert.NotNull(menu);
   typeof(ForestMenu).GetMethod("MainAction",Private).Invoke(menu,new object[]{1});
   typeof(ForestMenu).GetMethod("MainAction",Private).Invoke(menu,new object[]{0});
   var intro=menu.GetComponent<NewGameIntro>();Assert.NotNull(intro);Assert.True(intro.Active);
   yield return new WaitForSecondsRealtime(1);
   Assert.AreEqual("01_MainMenu",SceneManager.GetActiveScene().name,"Tutorial must not run behind the intro");
   Assert.IsNull(Object.FindFirstObjectByType<TutorialDirector>());Assert.False(ForestMenu.Visible);
   intro.Skip();intro.Skip();float end=Time.realtimeSinceStartup+60;
   while((SceneManager.GetActiveScene().name!=TutorialDirector.SceneName||Object.FindFirstObjectByType<TutorialDirector>()==null)&&Time.realtimeSinceStartup<end)yield return null;
   Assert.AreEqual(TutorialDirector.SceneName,SceneManager.GetActiveScene().name);
   Assert.NotNull(Object.FindFirstObjectByType<TutorialDirector>());Assert.False(intro.Active);
   yield return null;Assert.IsNull(menu.GetComponentInChildren<VideoPlayer>());
   Assert.False(ForestMenu.Visible);Assert.False(Map01OpeningCutscene.Pending);
  }
  [UnityTest,Timeout(90000)]public IEnumerator DecoderErrorCleansUpAndReturnsToTutorialChoice()
  {
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
   yield return new EnterPlayMode();yield return FailRoute();yield return new ExitPlayMode();
  }
  static IEnumerator FailRoute()
  {
   var menu=Object.FindFirstObjectByType<ForestMenu>();
   typeof(ForestMenu).GetMethod("MainAction",Private).Invoke(menu,new object[]{1});
   typeof(ForestMenu).GetMethod("MainAction",Private).Invoke(menu,new object[]{0});
   var intro=menu.GetComponent<NewGameIntro>();
   typeof(NewGameIntro).GetMethod("OnError",Private).Invoke(intro,new object[]{null,"test decoder failure"});
   yield return null;
   Assert.False(intro.Active);Assert.True(ForestMenu.Visible);
   Assert.True((bool)typeof(ForestMenu).GetField("choosingTutorial",Private).GetValue(menu));
   StringAssert.Contains("test decoder failure",(string)typeof(ForestMenu).GetField("message",Private).GetValue(menu));
   Assert.IsNull(menu.GetComponentInChildren<VideoPlayer>());Assert.AreEqual("01_MainMenu",SceneManager.GetActiveScene().name);
  }
 }
}
