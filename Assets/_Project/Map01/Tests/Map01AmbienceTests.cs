using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests
{
 public sealed class Map01AmbienceTests : ForestSceneTestBase
 {
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  [UnityTest]public IEnumerator ImportedEnvironmentalClipsLoadCompletely()
  {
   foreach(var name in new[]{"wind_leaves","birds_1","birds_2","birds_3"})
   {
    var clip=Resources.Load<AudioClip>("Map01Ambience/"+name);Assert.NotNull(clip,name);
    Assert.AreEqual(48000,clip.frequency);Assert.That(clip.length,Is.InRange(4.9f,45.1f));
    clip.LoadAudioData();float end=Time.realtimeSinceStartup+10;
    while(clip.loadState==AudioDataLoadState.Loading&&Time.realtimeSinceStartup<end)yield return null;
    Assert.AreEqual(AudioDataLoadState.Loaded,clip.loadState,name);
    Assert.Greater(clip.samples,48000*4);
   }
  }
  [UnityTest,Timeout(90000)]public IEnumerator PauseDuckResumeAndStopWithTheMap()
  {
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   yield return new EnterPlayMode();yield return RunLifecycle();yield return new ExitPlayMode();
  }
  static IEnumerator RunLifecycle()
  {
   // The menu bootstrap opens its title panel in an empty test scene.
   foreach(var menu in Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None))
    typeof(ForestMenu).GetField("visible",Private).SetValue(menu,false);
   Time.timeScale=1;
   var host=new GameObject("Ambience lifecycle");host.AddComponent<AudioListener>();
   var ambience=host.AddComponent<Map01Ambience>();
   float until=Time.realtimeSinceStartup+15;while(!ambience.Ready&&Time.realtimeSinceStartup<until)yield return null;
   Assert.True(ambience.Ready);yield return new WaitForSecondsRealtime(3.2f);
   var wind=(AudioSource)typeof(Map01Ambience).GetField("wind",Private).GetValue(ambience);
   var birds=(AudioSource)typeof(Map01Ambience).GetField("birds",Private).GetValue(ambience);
   Assert.True(wind.isPlaying,$"wind stopped: menu={ForestMenu.Visible}, scale={Time.timeScale}, data={wind.clip.loadState}");Assert.That(ambience.WindVolume,Is.EqualTo(Map01Ambience.WindLevel).Within(.01f));
   var voice=DialogueVoice.For(host);voice.CanSpeak=()=>true;voice.Play("m1_story_09");Assert.True(voice.HasSpeech);
   yield return new WaitForSecondsRealtime(.6f);Assert.Less(ambience.WindVolume,.10f);voice.Stop();
   typeof(Map01Ambience).GetField("nextBird",Private).SetValue(ambience,0f);
   yield return new WaitForSecondsRealtime(.1f);Assert.True(birds.isPlaying);
   Time.timeScale=0;yield return null;yield return null;
   float bedAt=wind.time,callAt=birds.time;
   yield return new WaitForSecondsRealtime(.3f);
   Assert.That(wind.time,Is.EqualTo(bedAt).Within(.03f));Assert.That(birds.time,Is.EqualTo(callAt).Within(.03f));
   Time.timeScale=1;yield return new WaitForSecondsRealtime(.25f);
   Assert.True(wind.isPlaying);Assert.Greater(wind.time,bedAt+.1f);
   Assert.True(birds.isPlaying);Assert.Greater(birds.time,callAt+.1f);
   // Transition keeps the carried player, but no Map 1 sound may continue with it.
   ambience.StopForTransition();yield return null;Assert.False(wind.isPlaying);Assert.False(birds.isPlaying);
   Assert.False(ambience.Ready);Object.Destroy(host);yield return null;
  }
  [UnityTest,Timeout(180000)]public IEnumerator RealMapInstallsOneAmbientBed()
  {
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
   yield return new EnterPlayMode();yield return CheckMap();yield return new ExitPlayMode();
  }
  static IEnumerator CheckMap()
  {
   Map01Mission mission=null;float until=Time.realtimeSinceStartup+60;
   while(Time.realtimeSinceStartup<until)
   {
    mission=Object.FindFirstObjectByType<Map01Mission>();
    if(mission!=null&&mission.IsInitialized&&mission.GetComponent<Map01Ambience>()?.Ready==true)break;
    yield return null;
   }
   Assert.NotNull(mission);Assert.True(mission.IsInitialized);
   var sound=mission.GetComponent<Map01Ambience>();Assert.NotNull(sound);Assert.True(sound.Ready);
   Assert.AreSame(sound,Map01Ambience.Attach(mission));Assert.AreEqual(1,Object.FindObjectsByType<Map01Ambience>(FindObjectsSortMode.None).Length);
   Assert.AreEqual(2,sound.GetComponentsInChildren<AudioSource>().Count(s=>s.name.StartsWith("Map 1 —")));
   // Exercise the actual authored room and roof, including loading/starting indoors.
   var floor=GameObject.Find("ShelterShell").GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="Floor");
   var roof=GameObject.Find("ShelterShell").GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="Earth covered roof");
   var original=mission.player.position;
   var controller=mission.player.GetComponent<CharacterController>();bool wasEnabled=controller!=null&&controller.enabled;
   bool movementEnabled=mission.ModernPlayer.enabled;mission.ModernPlayer.enabled=false;
   if(controller!=null)controller.enabled=false;
   foreach(var menu in Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None))typeof(ForestMenu).GetField("visible",Private).SetValue(menu,false);
   mission.SetPaused(false);
   var room=floor.transform.TransformPoint(floor.center+Vector3.up*(floor.size.y*.5f+.08f));
   var outside=floor.transform.TransformPoint(floor.center+new Vector3(floor.size.x*.5f+3,0,0));
   var bed=(AudioSource)typeof(Map01Ambience).GetField("wind",Private).GetValue(sound);
   var calls=(AudioSource)typeof(Map01Ambience).GetField("birds",Private).GetValue(sound);
   try
   {
    mission.player.position=room;yield return new WaitForSecondsRealtime(.8f);
    Assert.True(sound.InsideShelter,"Room floor must silence outdoor ambience");Assert.AreEqual(0,sound.WindVolume,.0001f);Assert.False(bed.isPlaying);Assert.False(calls.isPlaying);
    // The ground on top of the roof is outside, despite sharing the room's X/Z.
    mission.player.position=new Vector3(room.x,roof.bounds.max.y+.3f,room.z);
    yield return new WaitForSecondsRealtime(1.6f);
    Assert.False(sound.InsideShelter);Assert.True(bed.isPlaying);Assert.Greater(sound.WindVolume,.01f);
    mission.player.position=room;yield return new WaitForSecondsRealtime(.8f);
    Assert.True(sound.InsideShelter);Assert.False(bed.isPlaying);Assert.AreEqual(0,sound.WindVolume,.0001f);
    mission.player.position=outside;yield return new WaitForSecondsRealtime(1.6f);
    Assert.False(sound.InsideShelter);Assert.True(bed.isPlaying);Assert.Greater(sound.WindVolume,.01f);
   }
   finally{mission.player.position=original;if(controller!=null)controller.enabled=wasEnabled;mission.ModernPlayer.enabled=movementEnabled;}
  }
 }
}
