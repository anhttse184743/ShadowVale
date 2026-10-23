using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
 public sealed class Map02DayOneVoiceCoverageTests:ForestSceneTestBase {
  static Map02DialogueCatalog.Entry[] Spoken(){var d=Map02ChapterDefinition.Load();var scenes=d.tasks.Where(t=>t.phase<=1&&!string.IsNullOrEmpty(t.scene)).Select(t=>"m2v3_"+t.scene).Concat(new[]{"m2v3_arrivalbank","m2v3_repairdone","m2v3_duckevent"}).ToHashSet();var boat=new[]{new Map02DialogueCatalog.Entry{id="m2_arrival_sight",speaker="Hùng",text="Bến làng ở phía trước rồi."},new Map02DialogueCatalog.Entry{id="m2_moor_order",speaker="Anh Thành",text="Giữ yên lặng. Đưa thuyền sát bến."},new Map02DialogueCatalog.Entry{id="m2_ashore_order",speaker="Anh Thành",text="Nam, lên trước đi. Chúng ta gặp nhau trên bờ."}};return boat.Concat(d.entries.Where(e=>scenes.Contains(e.conversation)&&!string.IsNullOrWhiteSpace(e.text))).ToArray();}
  [Test]public void AllDayOneAndNightOneSpokenCuesHaveExactDedicatedRecordings(){var lines=Spoken();Assert.AreEqual(50,lines.Length);foreach(var line in lines){Assert.True(Map02DialogueCatalog.HasApprovedRecording(line),line.id+" missing exact recorded words");var recording=DialogueVoice.Find(line.id);StringAssert.StartsWith("Dialogue/Map02DayOneFree/",recording.resource);Assert.Greater(recording.clip.length,.15f);Assert.AreEqual(line.Caption,recording.caption);}Assert.AreEqual("",Map02ChapterDefinition.Load().Lines("shift").Single().text,"The silent ending remains silent.");}
  [UnityTest,Timeout(300000)]public IEnumerator EverySpokenCuePlaysToCompletionWithoutOverlap(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();yield return PlayAll();yield return new ExitPlayMode();}
  static IEnumerator PlayAll(){
   Map02Mission m=null;float limit=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<limit){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
   Assert.NotNull(m?.Chapter);m.Chapter.Cancel();m.Chapter.enabled=false;var voice=DialogueVoice.For(m.gameObject);voice.CanSpeak=()=>true;var heard=new List<string>();voice.LineStarted+=line=>heard.Add(line.id);
   foreach(var line in Spoken()){voice.Play(line.id);Assert.AreEqual(line.id,voice.CurrentId);float end=Time.realtimeSinceStartup+DialogueVoice.Length(line.id)+4;while(voice.HasSpeech&&Time.realtimeSinceStartup<end){Assert.LessOrEqual(Object.FindObjectsByType<DialogueVoice>(FindObjectsSortMode.None).Count(v=>v.HasSpeech),1);yield return null;}Assert.False(voice.HasSpeech,"Audio stalled: "+line.id);}
   CollectionAssert.AreEqual(Spoken().Select(e=>e.id),heard);heard.Clear();var duck=Map02ChapterDefinition.Load().Lines("duckevent");voice.SpeakText(string.Join("\n",duck.Select(e=>e.Caption)));float until=Time.realtimeSinceStartup+12;while(voice.HasSpeech&&Time.realtimeSinceStartup<until)yield return null;CollectionAssert.AreEqual(duck.Select(e=>e.id),heard,"The ambient duck exchange includes both speakers, exactly once.");
   Directory.CreateDirectory("Logs/Map02PlayableAudit_20261022");File.WriteAllLines("Logs/Map02PlayableAudit_20261022/voice-playback.txt",Spoken().Select(e=>e.id+" · "+e.Caption));voice.Stop();m.Chapter.enabled=true;
  }
 }
}
