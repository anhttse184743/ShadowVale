using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
 public sealed class Map02VoiceCompletionTests:ForestSceneTestBase {
  [Test]public void EverySpokenChapterCueHasAnExactLoadableRecording(){
   var spoken=Map02ChapterDefinition.Load().entries.Where(e=>!string.IsNullOrWhiteSpace(e.text)).ToArray();Assert.GreaterOrEqual(spoken.Length,151);
   foreach(var e in spoken){Assert.True(Map02DialogueCatalog.HasApprovedRecording(e),e.id);Assert.Greater(DialogueVoice.Length(e.RecordingId),.15f,e.id);}
  }
  [Test]public void NightPatrolReusesApprovedEnemyAudioWithoutNewSynthesis(){
   foreach(var pair in new[]{("night2_patrol_warning","m1_ops_enemy_spotted",Map02ChapterDirector.NightPatrolWarningText),("night2_patrol_fire_order","m1_ops_enemy_order",Map02ChapterDirector.NightPatrolFireOrderText)}){
    var source=DialogueVoice.Find(pair.Item2);Assert.NotNull(source.clip);CollectionAssert.AreEqual(File.ReadAllBytes("Assets/Resources/"+source.resource+".wav"),File.ReadAllBytes("Assets/Resources/OperationsAudio/"+pair.Item1+".wav"));StringAssert.EndsWith(pair.Item3,source.caption);
   }
  }
  [UnityTest,Timeout(300000)]public IEnumerator DayThreeReportSpeaksAllThreeLinesBeforeHandingBackControl(){
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();yield return Report();yield return new ExitPlayMode();
  }
  static IEnumerator Report(){
   Map02Mission m=null;float until=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}Assert.NotNull(m?.Chapter);
   var c=m.Chapter;c.Cancel();var save=m.Capture();var state=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id=="D3_06")break;state.Complete(t.id);}state.dayOne.arrivalSeen=true;save.chapter=state;m.Restore(save);yield return WaitGameSeconds(1);c.Track("D3_06");
   var heard=new List<string>();var voice=DialogueVoice.For(c.gameObject);voice.LineStarted+=line=>heard.Add(line.id);
   var marker=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(x=>x.chapterId=="D3_06");var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=(marker.standPoint??marker.transform).position;cc.enabled=true;
   var route=RouteInputToGame();var kb=InputSystem.AddDevice<Keyboard>();kb.MakeCurrent();
   try{InputSystem.QueueStateEvent(kb,new KeyboardState());yield return null;yield return null;InputSystem.QueueStateEvent(kb,new KeyboardState(Key.E));yield return null;InputSystem.QueueStateEvent(kb,new KeyboardState());yield return null;Assert.AreEqual("D3_06",c.CurrentScene?.id);
    float end=Time.realtimeSinceStartup+55;while(!c.State.Done("D3_06")||c.Transitioning){Assert.Less(Time.realtimeSinceStartup,end,"Report did not finish naturally");yield return null;}
    CollectionAssert.AreEqual(new[]{"m2v3_day3report_01","m2v3_day3report_02","m2v3_day1report_03"},heard);Assert.AreEqual(5,c.State.phase);Assert.False(m.InputBlocked);
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    typeof(Map02ChapterDirector).GetMethod("PlayNightPatrolLine",flags).Invoke(c,new object[]{"night2_patrol_warning",Map02ChapterDirector.NightPatrolWarningText});
    var enemy=(AudioSource)typeof(Map02ChapterDirector).GetField("nightWarningAudio",flags).GetValue(c);
    yield return new WaitForSecondsRealtime(.12f);Assert.True(enemy.isPlaying);
    m.SetPaused(true);yield return null;float at=enemy.time;yield return new WaitForSecondsRealtime(.2f);Assert.That(enemy.time,Is.EqualTo(at).Within(.03f),"Direct patrol voice must pause with gameplay");
    m.SetPaused(false);yield return new WaitForSecondsRealtime(.15f);Assert.Greater(enemy.time,at+.03f,"Direct patrol voice must resume");enemy.Stop();

    Directory.CreateDirectory("Logs/VoiceCompletion_20261028");File.WriteAllLines("Logs/VoiceCompletion_20261028/day3-report-playback.txt",heard);
   }finally{InputSystem.QueueStateEvent(kb,new KeyboardState());InputSystem.RemoveDevice(kb);RestoreInputRouting(route);c.Cancel();}
  }
 }
}
