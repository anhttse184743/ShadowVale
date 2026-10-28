using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map01StoryVoiceTests:ForestSceneTestBase
    {
        [Serializable]sealed class Manifest { public Entry[] entries; }
        [Serializable]sealed class Entry { public string id,caption,resource,cleanResource;public bool radio;public float duration; }
        static Manifest Read()=>new Manifest { entries=JsonUtility.FromJson<Manifest>(File.ReadAllText("SourceArt/Dialogue/CommanderStory/voice-manifest.json")).entries.Where(e=>e.id.StartsWith("m1_story_rescue_order_")).Concat(JsonUtility.FromJson<Manifest>(File.ReadAllText("SourceArt/Dialogue/Map01Revision20261015/voice-manifest.json")).entries).ToArray() };
        [Test]public void AllApprovedLinesHaveExactVoicesAndCleanMasters()
        {
            var rows=Read().entries;Assert.AreEqual(22,rows.Length);Assert.AreEqual(5,rows.Count(e=>e.radio));
            Assert.AreEqual(22,rows.Select(e=>e.id).Distinct().Count());
            foreach(var e in rows){
                var voice=DialogueVoice.Find(e.id);Assert.NotNull(voice,e.id);Assert.NotNull(voice.clip,e.id);
                var revoiced=Map01Revoice.Find(e.id);   // P9: re-voiced in Map 2's presets
                Assert.AreEqual(e.caption,voice.caption);Assert.AreEqual(e.radio,voice.radio);Assert.AreEqual(revoiced?.resource??e.resource,voice.resource);
                Assert.That(voice.clip.length,Is.EqualTo(revoiced?.duration??e.duration).Within(.035f),e.id);
                Assert.AreEqual(1,voice.clip.channels,e.id);
                var clean=Resources.Load<AudioClip>(revoiced?.cleanResource??e.cleanResource);Assert.NotNull(clean,e.id+" clean master");
                if(e.radio)Assert.AreNotSame(clean,voice.clip,"Remote speech uses only the processed clip.");
                else Assert.AreSame(clean,voice.clip,"Nam and nearby characters are heard clean.");
            }
        }
        [UnityTest]public IEnumerator BriefingSpeaksBothCharactersAndAutomaticallyAdvancesThenWaitsForAcceptance()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            // Create captured locals after EnterPlayMode: the domain reload cannot retain
            // a compiler-generated closure allocated before the transition.
            yield return CheckNaturalBriefing();
            yield return new ExitPlayMode();
        }
        static IEnumerator CheckNaturalBriefing()
        {
            var m=Object.FindFirstObjectByType<Map01Mission>();
            Assert.NotNull(m,"The voice playback check must run in Map 1, not the boot menu.");
            float ready=Time.realtimeSinceStartup+20;
            while((!m.IsInitialized||m.ModernPlayer==null||m.ModernCombat==null||m.ModernHealth==null||m.GetComponent<Map01Story>()?.Commander==null)&&Time.realtimeSinceStartup<ready) yield return null;
            Assert.IsTrue(m.IsInitialized,"Mission JSON and scene references must initialize.");
            Assert.NotNull(m.ModernCombat,"Player combat must be initialized before dialogue playback.");
            Assert.NotNull(m.ModernHealth);
            Assert.NotNull(m.GetComponent<Map01Story>()?.Commander);
            var storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.NonPublic|BindingFlags.Static);
            Assert.NotNull(storage,"Expected the test-only checkpoint root override.");
            storage.SetValue(null,Path.GetFullPath("Logs/Map01StoryVoice/saves/"+Guid.NewGuid().ToString("N")));
            var voice=DialogueVoice.For(m.gameObject);
            Assert.NotNull(voice,"The mission must provide a valid DialogueVoice component.");
            var story=m.GetComponent<Map01Story>();var d=m.GetComponent<Map01StoryDialogue>();var q=m.GetComponent<Map01Quest>();
            PlaceNamAtCommander(m);yield return WaitGameSeconds(.3f);
            var at=m.player.position;
            int rounds=m.ModernCombat.RoundsInMagazine;
            float hp=m.ModernHealth.Current;
            Assert.IsTrue(story.TryInteract());d.Close(false);
            Assert.IsFalse(q.RescueOrderAccepted);
            voice.Play("m1_story_rescue_order_02");yield return null;
            Assert.IsTrue(voice.HasSpeech,"Closing without accepting must restore gameplay speech.");
            Assert.AreEqual("m1_story_rescue_order_02",voice.CurrentId);voice.Stop();
            Assert.IsTrue(story.TryInteract());
            Assert.AreEqual("m1_story_rescue_order_01",voice.CurrentId);
            // Begin clears the gameplay caption queue and installs its listener. Observe
            // subsequent lines only after that handoff; the first has already started.
            var heard=new List<string>{voice.CurrentId};
            Action<DialogueVoice.Line> observe=line=>heard.Add(line.id);
            voice.LineStarted+=observe;
            yield return WaitGameSeconds(.85f);
            Assert.That(m.gameCamera.fieldOfView,Is.EqualTo(52).Within(.1f),"The commander uses the shelter's authored board shot.");
            var board=m.alliedCommanderPost.parent.Find("MissionBoard");Assert.NotNull(board);
            foreach(var focus in new[]{story.Commander.position+Vector3.up*1.42f,board.position+Vector3.up*1.7f-board.forward*.08f})
            {
                var screen=m.gameCamera.WorldToViewportPoint(focus);
                Assert.Greater(screen.z,0);Assert.That(screen.x,Is.InRange(0f,1f));Assert.That(screen.y,Is.InRange(0f,1f));
            }
            Directory.CreateDirectory("Logs/Map01StoryVoice");
            ScreenCapture.CaptureScreenshot("Logs/Map01StoryVoice/close-briefing-first-line.png");
            yield return WaitGameSeconds(.2f);
            float deadline=Time.realtimeSinceStartup+120;
            bool capturedNam=false;
            while((d.Index<d.Count-1||voice.HasSpeech)&&Time.realtimeSinceStartup<deadline){
                if(d.Index==1&&!capturedNam) {
                    float shot=(float)typeof(Map01StoryDialogue).GetField("cameraElapsed",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(d);
                    if(shot>=.75f) { ScreenCapture.CaptureScreenshot("Logs/Map01StoryVoice/close-briefing-Nam.png");capturedNam=true; }
                }
                Assert.IsTrue(m.Cinematic);Assert.IsFalse(q.RescueOrderAccepted);
                Assert.Less(Vector3.Distance(at,m.player.position),.02f,"Allow ground-contact settling up to 2 cm, matching the opening interaction test.");
                Assert.LessOrEqual(Object.FindObjectsByType<DialogueVoice>(FindObjectsSortMode.None).Count(v=>v.HasSpeech),1,"Only one character speaks at a time.");
                yield return null;
            }
            Assert.AreEqual(d.Count-1,d.Index);Assert.IsFalse(voice.HasSpeech);
            CollectionAssert.AreEqual(new[]{1,2,3,5}.Select(i=>"m1_story_rescue_order_"+i.ToString("00")).Concat(new[]{"m1_v2_rescue_approach_03"}),heard);
            Assert.IsTrue(d.IsPlaying);Assert.IsFalse(q.RescueOrderAccepted,"Listening must not silently accept the final order.");
            Directory.CreateDirectory("Logs/Map01StoryVoice");
            File.WriteAllLines("Logs/Map01StoryVoice/natural-briefing.txt",heard);
            ScreenCapture.CaptureScreenshot("Logs/Map01StoryVoice/spoken-briefing.png");yield return WaitGameSeconds(1.1f);
            d.Advance();yield return null;Assert.IsTrue(q.RescueOrderAccepted);Assert.IsFalse(m.Cinematic||d.IsPlaying);
            Assert.IsTrue(m.ModernPlayer.enabled&&m.ModernCombat.enabled);Assert.AreEqual(rounds,m.ModernCombat.RoundsInMagazine);Assert.AreEqual(hp,m.ModernHealth.Current);
            voice.LineStarted-=observe;
            voice.Play("m1_story_rescue_order_02");yield return null;
            Assert.AreEqual("m1_story_rescue_order_02",voice.CurrentId);
            Assert.IsTrue(voice.HasSpeech);voice.Stop();
        }
        [TearDown]public void ResetSaves()=>typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);
    }
}

