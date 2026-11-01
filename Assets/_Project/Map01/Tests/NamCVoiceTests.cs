using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests {
public sealed class NamCVoiceTests:ForestSceneTestBase {
 [Serializable]sealed class Item{public string id,speaker,voice,voiceRevision,resource;public float duration;}
 [Serializable]sealed class Bank{public Item[] entries;}
 static Item[] Nam()=>JsonUtility.FromJson<Bank>(Resources.Load<TextAsset>("Dialogue/voice-catalog").text).entries.Where(e=>e.speaker=="Nam").ToArray();
 [Test]public void BothMapsUseNamCAndOpeningReplyMatchesTheApprovedVoice(){
  var all=Nam();Assert.GreaterOrEqual(all.Length,89);Assert.AreEqual(all.Length,all.Select(e=>e.id).Distinct().Count());Assert.IsTrue(all.Any(e=>e.id.StartsWith("m1_"))&&all.Any(e=>e.id.StartsWith("m2")));
  foreach(var e in all){Assert.AreEqual("Minh Quân",e.voice,e.id);Assert.AreEqual("Nam_C_20261026",e.voiceRevision,e.id);var clip=Resources.Load<AudioClip>(e.resource);Assert.NotNull(clip,e.id);Assert.That(clip.length,Is.EqualTo(e.duration).Within(.04f),e.id);}
  var opening=Resources.Load<Map01OpeningCutscene>("Cutscenes/Map01Opening");Assert.NotNull(opening.soldierVoice);Assert.That(opening.soldierVoice.length,Is.EqualTo(all.Single(e=>e.id=="free_literal_7be0a5941d33").duration).Within(.04f));
 }
 [Test]public void NamLinesFitTheAuthoredSupperGestureWindows(){
  var cues=(Dictionary<string,float>)typeof(Map02ChapterDirector).GetField("MealCueStarts",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);var sorted=cues.OrderBy(x=>x.Value).ToArray();
  foreach(var e in Nam().Where(e=>cues.ContainsKey(e.id))){int i=Array.FindIndex(sorted,x=>x.Key==e.id);float window=(i+1<sorted.Length?sorted[i+1].Value:70)-sorted[i].Value;Assert.Less(e.duration,window-.08f,"The voice must finish before the next speaker's gesture: "+e.id);}
 }
 [UnityTest,Timeout(300000)]public IEnumerator EveryNamCueFinishesWithoutOverlapAndStopsOnPause(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();yield return PlayAll();yield return new ExitPlayMode();}
 static IEnumerator PlayAll(){
  var host=new GameObject("Nam C playback audit");host.AddComponent<AudioListener>();var voice=DialogueVoice.For(host);voice.CanSpeak=()=>true;var source=host.GetComponent<AudioSource>();source.pitch=2;var heard=new List<string>();voice.LineStarted+=line=>heard.Add(line.id);
  foreach(var line in Nam()){
   voice.Play(line.id);Assert.AreEqual(line.id,voice.CurrentId);float end=Time.realtimeSinceStartup+line.duration/2+3;
   while(voice.HasSpeech&&Time.realtimeSinceStartup<end){Assert.LessOrEqual(UnityEngine.Object.FindObjectsByType<DialogueVoice>(FindObjectsSortMode.None).Count(v=>v.HasSpeech),1);yield return null;}
   Assert.False(voice.HasSpeech,"Playback stalled: "+line.id);
  }
  CollectionAssert.AreEqual(Nam().Select(e=>e.id),heard);
  var pauseLine=Nam().First(e=>e.duration>2);voice.Play(pauseLine.id);yield return new WaitForSecondsRealtime(.1f);Time.timeScale=0;yield return null;float at=source.time;yield return new WaitForSecondsRealtime(.2f);Assert.That(source.time,Is.EqualTo(at).Within(.03f));Time.timeScale=1;voice.Stop();UnityEngine.Object.Destroy(host);
 }
}
}
