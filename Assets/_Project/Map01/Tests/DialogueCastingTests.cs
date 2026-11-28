using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class DialogueCastingTests : ForestSceneTestBase
    {
        [Serializable] sealed class Row { public string id,speaker,voice,provider,resource,sha256; public float duration; public Derived derivedFrom; }
        [Serializable] sealed class Derived { public string id,sha256; }
        [Serializable] sealed class Bank { public Row[] entries; }
        [Serializable] sealed class Script { public Map02DialogueCatalog.Entry[] entries; }
        static Row[] Read() => JsonUtility.FromJson<Bank>(Resources.Load<TextAsset>("Dialogue/voice-catalog").text).entries;
        static string Hash(string path)
        {
            using var hash=SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        }

        [Test] public void EveryRecordingInBothMapsHasTheSharedCastAndVerifiedBytes()
        {
            var rows=Read();Assert.GreaterOrEqual(rows.Length,273);
            var manifest=CurrentVoiceRevision.All;
            CollectionAssert.AreEquivalent(rows.Select(r=>r.id),manifest.Select(r=>r.id),"Active manifest must include every batch from both maps.");
            foreach(var row in rows)
            {
                Assert.IsTrue(DialogueCasting.IsAllowed(row.speaker,row.voice,row.provider),row.id+" has missing or incorrect cast metadata.");
                Assert.AreEqual(row.voice,manifest.Single(r=>r.id==row.id).voice,row.id+" manifest cast");
                Assert.AreEqual(64,row.sha256?.Length,row.id+" must have a verifiable audio hash.");
                Assert.AreEqual(row.sha256,Hash("Assets/Resources/"+row.resource+".wav"),row.id);
                Assert.NotNull(DialogueVoice.Find(row.id)?.clip,row.id);
            }
            CollectionAssert.AreEqual(File.ReadAllBytes("Assets/Resources/Dialogue/voice-catalog.json"),File.ReadAllBytes("SourceArt/Dialogue/voice-catalog.json"));
        }

        [Test] public void BothMapTwoScriptsResolveTheSameCharacterThroughRecordingAliases()
        {
            foreach(var path in new[]{"Story/Map02Chapter","Dialogue/Map02Story/script"})
            {
                var json=Resources.Load<TextAsset>(path);Assert.NotNull(json,path);
                foreach(var line in JsonUtility.FromJson<Script>(json.text).entries.Where(e=>!string.IsNullOrWhiteSpace(e.text)))
                {
                    var recorded=DialogueVoice.Find(line.RecordingId);Assert.NotNull(recorded,line.id);
                    Assert.IsTrue(DialogueCasting.SameCharacter(line.speaker,recorded.speaker),line.id);
                    Assert.IsTrue(Map02DialogueCatalog.HasApprovedRecording(line),line.id);
                }
            }
        }

        [Test] public void HungEchoUsesHisCurrentExactCaptionMasterAndFitsTheFinaleWindow()
        {
            var rows=Read();var echo=rows.Single(r=>r.id=="m2v3_echo_promise");var source=rows.Single(r=>r.id=="m2v3_sacrifice_02");
            Assert.AreEqual("Hùng",echo.speaker);Assert.AreEqual("Anh Khôi",echo.voice);Assert.AreEqual(source.voice,echo.voice);
            Assert.NotNull(echo.derivedFrom);Assert.AreEqual(source.id,echo.derivedFrom.id);Assert.AreEqual(source.sha256,echo.derivedFrom.sha256);
            Assert.AreNotEqual(source.sha256,echo.sha256,"The remembered line retains its processed echo.");
            Assert.That(DialogueVoice.Length(echo.id),Is.EqualTo(4.65f).Within(.001f));
            Assert.Less(echo.duration,Map02ChapterDirector.FireOrderAt-Map02ChapterDirector.EchoAt);
        }

        [Test] public void SharedCastAcceptsRoleAliasesAndRejectsEnemyOrNamVoicesForHung()
        {
            Assert.IsTrue(DialogueCasting.SameCharacter("Chỉ huy","Anh Thành"));
            Assert.IsTrue(DialogueCasting.SameCharacter("Người trinh sát","Tâm"));
            Assert.IsTrue(DialogueCasting.IsAllowed("Sĩ quan địch","Martin","Runway"));
            Assert.IsFalse(DialogueCasting.IsAllowed("Hùng","Martin","Runway"));
            Assert.IsFalse(DialogueCasting.IsAllowed("Hùng","Minh Quân","VieNeu offline"));
            Assert.IsFalse(DialogueCasting.IsAllowed(null,"Anh Khôi","VieNeu offline"));
            Assert.IsFalse(DialogueCasting.SameCharacter("unknown","unknown"));
        }

        [Test] public void PlaybackLookupRefusesAMismatchedVoiceInsteadOfSilentlyPlayingIt()
        {
            typeof(DialogueVoice).GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            var line=DialogueVoice.Find("m2v3_echo_promise");Assert.NotNull(line);var original=line.voice;
            try
            {
                line.voice="Minh Quân";
                LogAssert.Expect(LogType.Error,"Dialogue voice cast mismatch: m2v3_echo_promise · Hùng · Minh Quân · VieNeu offline");
                Assert.IsNull(DialogueVoice.Find(line.id));Assert.IsNull(DialogueVoice.Find(line.id));
            }
            finally { line.voice=original; }
            Assert.NotNull(DialogueVoice.Find(line.id));
        }

        [UnityTest,Timeout(60000)] public IEnumerator HungVoiceStaysTheSameAcrossMapOneMapTwoAndTheEcho()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            var host=new GameObject("Cross-map Hùng voice check");host.AddComponent<AudioListener>();var voice=DialogueVoice.For(host);voice.CanSpeak=()=>true;
            foreach(var id in new[]{"m1_rescue_freed","m2v3_sacrifice_02","m2v3_echo_promise"})
            {
                var row=DialogueVoice.Find(id);Assert.NotNull(row);Assert.AreEqual("Anh Khôi",row.voice,id);Assert.AreEqual("Hùng",row.speaker,id);
                voice.Play(id);Assert.AreEqual(id,voice.CurrentId);Assert.AreSame(row.clip,host.GetComponent<AudioSource>().clip);
                float deadline=Time.realtimeSinceStartup+row.clip.length+3;
                while(voice.HasSpeech&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsFalse(voice.HasSpeech,"Playback stalled: "+id);
            }
            voice.Stop();UnityEngine.Object.Destroy(host);yield return new ExitPlayMode();
        }
    }
}
