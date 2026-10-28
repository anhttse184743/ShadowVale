using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class C3DialogueVoiceTests : ForestSceneTestBase
    {
        [Serializable] sealed class Manifest { public Entry[] entries; }
        [Serializable] sealed class Entry { public string id,caption,resource,voice; public bool radio,userApproved; public float duration; }
        [Test] public void EveryApprovedRetakeHasExactCaptionChannelAndImportedAudio()
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("SourceArt/Dialogue/C3_20261018/voice-manifest.json"));
            Assert.AreEqual(16,manifest.entries.Length);
            foreach(var entry in manifest.entries)
            {
                var line=DialogueVoice.Find(entry.id);Assert.NotNull(line,entry.id);Assert.NotNull(line.clip,entry.id);
                Assert.IsTrue(entry.userApproved,entry.id);Assert.AreEqual(entry.caption,line.caption,entry.id);
                Assert.AreEqual(entry.caption,line.match,entry.id);Assert.AreEqual(entry.radio,line.radio,entry.id);
                Assert.AreEqual(entry.resource,line.resource,entry.id);
                StringAssert.EndsWith(entry.radio?"_radio":"_clean",line.resource);
                Assert.AreEqual(48000,line.clip.frequency);Assert.AreEqual(1,line.clip.channels);
                Assert.Greater(line.clip.length,.3f);Assert.That(line.clip.length,Is.EqualTo(CurrentVoiceRevision.For(entry.id).duration).Within(.035f));
            }
            Assert.AreEqual(Map01C3Dialogue.PhoneCut,DialogueVoice.Caption("m1_c3_phone_cut"));
            Assert.AreEqual(Map01C3Dialogue.MalletRemoved,DialogueVoice.Caption("m1_c3_mallet_removed"));
            Assert.AreEqual(Map01C3Dialogue.EnemyCall,DialogueVoice.Caption("m1_c3_enemy_call"));
        }
        [Test] public void SourceAndRuntimeCatalogStayIdenticalAfterPrepare()
        {
            Assert.AreEqual(File.ReadAllText("SourceArt/Dialogue/voice-catalog.json"),File.ReadAllText("Assets/Resources/Dialogue/voice-catalog.json"));
            var order=Map01OperationLines.SupplyOrder;
            Assert.AreEqual("Nam",order[0].speaker);Assert.AreEqual("Chỉ huy",order[1].speaker);
            Assert.AreEqual("Hùng",order[2].speaker);Assert.AreEqual("Chỉ huy",order[3].speaker);
            Assert.AreEqual("Rõ.",order[4].text);Assert.IsFalse(order[4].radio);
        }
        [UnityTest] public IEnumerator GateCheckInPlaysBothTransmittedVoicesOnce()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
            yield return CheckSequence(Map01C3Dialogue.CheckIn,new[]{"m1_c3_gate_checkin_call","m1_c3_gate_checkin_reply"},true);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator MosinOrderFinishesBeforeLocalNamAcknowledgement()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
            yield return CheckSequence(Map01C3Dialogue.CommanderOrder,new[]{"m1_c3_mosin_order","m1_v2_rescue_approach_03"},false);
            yield return new ExitPlayMode();
        }
        static IEnumerator CheckSequence(string text,string[] expected,bool secondRadio)
        {
            var host=new GameObject("C3 approved dialogue sequence test");var voice=DialogueVoice.For(host);
            var heard=new List<string>();var channels=new List<bool>();var times=new List<float>();
            voice.LineStarted=line=>{heard.Add(line.id);channels.Add(line.radio);times.Add(Time.time);};
            voice.SpeakText(text);Assert.AreEqual(expected[0],voice.CurrentId);
            float deadline=Time.time+25;
            while(voice.HasSpeech&&Time.time<deadline)yield return null;
            CollectionAssert.AreEqual(expected,heard);Assert.IsTrue(channels[0]);Assert.AreEqual(secondRadio,channels[1]);
            Assert.GreaterOrEqual(times[1]-times[0],DialogueVoice.Length(expected[0])-.05f);
            UnityEngine.Object.Destroy(host);
        }
    }
}
