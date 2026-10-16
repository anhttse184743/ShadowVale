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
    public sealed class Map01StoryVoiceSupplementTests:ForestSceneTestBase
    {
        [Serializable]sealed class Manifest { public Entry[] entries; }
        [Serializable]sealed class Entry { public string id,caption,resource,match,voice;public bool radio;public float duration; }
        static Manifest Read()=>JsonUtility.FromJson<Manifest>(File.ReadAllText("SourceArt/Dialogue/Map01Remaining/voice-manifest.json"));
        static readonly string[] BoatOrder={"m1_board_invite_current","m1_depart_order","m1_depart_destination_question","m1_depart_destination_order","m1_depart_bank_reply"};
        [Test]public void EightSupplementLinesHaveMatchingCleanAudioAndExistingCharacterVoices()
        {
            var rows=Read().entries;Assert.AreEqual(8,rows.Length);
            foreach(var e in rows) {
                var v=DialogueVoice.Find(e.id);Assert.NotNull(v,e.id);Assert.NotNull(v.clip,e.id);
                Assert.AreEqual(e.caption,v.caption);Assert.AreEqual(e.resource,v.resource);Assert.IsFalse(v.radio);
                Assert.AreEqual(48000,v.clip.frequency);Assert.AreEqual(1,v.clip.channels);
                Assert.That(v.clip.length,Is.EqualTo(e.duration).Within(.035f));
                Assert.Contains(e.voice,new[]{"Brodie","Clint","Tom"});
            }
        }
        [UnityTest]public IEnumerator RemainingGameplayAndBoatDialogueSpeakInOrderBeforeCompletion()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            yield return CheckPlayback(false);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator SkippingBoardingStopsSupplementDialogueAndCompletesOnce()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            yield return CheckPlayback(true);
            yield return new ExitPlayMode();
        }
        static IEnumerator CheckPlayback(bool skip)
        {
            var m=Object.FindFirstObjectByType<Map01Mission>();Assert.NotNull(m);
            float ready=Time.realtimeSinceStartup+20;
            while(m.ModernCombat==null && Time.realtimeSinceStartup<ready)yield return null;
            Assert.NotNull(m.ModernCombat);
            typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.NonPublic|BindingFlags.Static)
                .SetValue(null,Path.GetFullPath("Logs/Map01RemainingVoice/saves/"+Guid.NewGuid().ToString("N")));
            if(!skip) {
                var gameplay=DialogueVoice.For(m.gameObject);
                foreach(var row in Read().entries.Where(e=>!string.IsNullOrEmpty(e.match))) {
                    m.Say(row.caption,1);yield return null;
                    Assert.AreEqual(row.id,gameplay.CurrentId,row.caption);
                    Assert.IsTrue(gameplay.HasSpeech);gameplay.Stop();
                }
                m.Say(null,0);
            }
            var q=m.GetComponent<Map01Quest>();q.RestoreStage(Map01Quest.ExtractionStage);
            var e=m.GetComponentInChildren<Map01Extraction>();Assert.NotNull(e);Assert.IsTrue(e.Prepared);
            e.autoContinueToMap2=false;
            var voice=DialogueVoice.For(e.gameObject);var heard=new List<string>();
            Action<DialogueVoice.Line> observe=line=>heard.Add(line.id);voice.LineStarted+=observe;
            var cc=m.player.GetComponent<CharacterController>();bool was=cc.enabled;cc.enabled=false;
            m.player.position=e.approach;cc.enabled=was;Physics.SyncTransforms();
            float begin=Time.realtimeSinceStartup+8;
            while(e.CurrentPhase==Map01Extraction.Phase.Extraction&&Time.realtimeSinceStartup<begin)yield return null;
            Assert.AreEqual(Map01Extraction.Phase.Approach,e.CurrentPhase);
            Assert.AreEqual(BoatOrder[0],voice.CurrentId);Assert.IsTrue(voice.HasSpeech);
            if(skip) {
                e.Skip();e.Skip();yield return null;
                Assert.IsTrue(e.HasDeparted);Assert.IsFalse(voice.HasSpeech);
                Assert.AreEqual(Map01Quest.CompleteStage,q.Stage);
                Assert.AreEqual("Map 1",UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            } else {
                float deadline=Time.realtimeSinceStartup+60;
                while(!e.HasDeparted && Time.realtimeSinceStartup<deadline) {
                    if(voice.HasSpeech)Assert.AreNotEqual(Map01Quest.CompleteStage,q.Stage,"Completion must wait for the final spoken line.");
                    Assert.LessOrEqual(Object.FindObjectsByType<DialogueVoice>(FindObjectsSortMode.None).Count(v=>v.HasSpeech),1);
                    yield return null;
                }
                CollectionAssert.AreEqual(BoatOrder,heard);
                Assert.IsTrue(e.HasDeparted);Assert.IsFalse(voice.HasSpeech);Assert.AreEqual(Map01Quest.CompleteStage,q.Stage);
                Assert.IsNotNull(ForestSaveSlots.Read(ForestSaveSlots.AutoSlot));
                Assert.AreEqual("Map 1",UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                Directory.CreateDirectory("Logs/Map01RemainingVoice");
                File.WriteAllLines("Logs/Map01RemainingVoice/boat-lines.txt",heard);
            }
            voice.LineStarted-=observe;
        }
        [TearDown]public void RestoreSaveRoot()=>typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);
    }
}
