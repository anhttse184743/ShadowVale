using System;
using System.Linq;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests
{
    public sealed class FullGameAudioTests:ForestSceneTestBase
    {
        [Serializable] sealed class Bank {public Item[] entries;}
        [Serializable] sealed class Item {public string id,resource,caption,speaker,voice,provider;public float duration;public bool radio;}
        static Item[] Read()=>JsonUtility.FromJson<Bank>(Resources.Load<TextAsset>("Dialogue/voice-catalog").text).entries;
        [Test] public void EveryCatalogCueHasAnImportedAudibleClipWithCorrectDuration()
        {
            var bank=Read();Assert.GreaterOrEqual(bank.Length,260);Assert.AreEqual(bank.Length,bank.Select(e=>e.id).Distinct().Count());
            foreach(var entry in bank){var clip=Resources.Load<AudioClip>(entry.resource);Assert.NotNull(clip,entry.id);Assert.Greater(clip.length,.1f,entry.id);Assert.That(clip.length,Is.EqualTo(entry.duration).Within(.04f),entry.id);Assert.AreEqual(48000,clip.frequency,entry.id);}
        }
        [Test] public void EnemyVoicesUseOnlyOriginalRunwayRecordings()
        {
            var roles=new[]{"Lính địch","Lính","Giám sát","Lính phản ứng","Sĩ quan địch","Lính gác cổng","Lính tuần tra","Chỉ huy địch","Địch"};
            var enemies=Read().Where(e=>roles.Contains(e.speaker)).ToArray();Assert.AreEqual(15,enemies.Length);
            foreach(var e in enemies){Assert.AreEqual("Runway",e.provider,e.id);Assert.AreEqual("Martin",e.voice,e.id);}
        }
        [Test] public void EntireCurrentMapTwoScriptHasExactVoicesIncludingFinale()
        {
            foreach(var line in Map02ChapterDefinition.Load().entries.Where(e=>!string.IsNullOrWhiteSpace(e.text)))Assert.True(Map02DialogueCatalog.HasApprovedRecording(line),line.id+" · "+line.Caption);
        }
        [Test] public void CastingDistinguishesYoungNamMiddleAgedHungWomanLanhAndElderSau()
        {
            var bank=Read();foreach(var e in bank.Where(e=>e.speaker=="Nam"))Assert.AreEqual("Minh Quân",e.voice,e.id);
            foreach(var e in bank.Where(e=>e.speaker=="Chỉ huy"||e.speaker=="Anh Thành"))Assert.AreEqual("Minh Đức",e.voice,e.id);
            foreach(var e in bank.Where(e=>e.speaker=="Hùng"))Assert.AreEqual("Anh Khôi",e.voice,e.id);
            foreach(var e in bank.Where(e=>e.speaker=="Chị Lành"))Assert.AreEqual("Thục Đoan",e.voice,e.id);
            foreach(var e in bank.Where(e=>e.speaker=="Ông Sáu"))Assert.AreEqual("Thiền Tâm Đức",e.voice,e.id);
        }
        [Test] public void DayOneEnvironmentAndFlashbackAssetsExist()
        {
            foreach(var id in new[]{"crickets","rooster","oars","fire","bamboo","duck","plane","cough","crate","children","hung_theme","hung_invitation","hung_laugh","night_river","tank_tracks"})Assert.NotNull(Resources.Load<AudioClip>("Map02DayOne/"+id),id);
        }
        [Test] public void AircraftProgressSurvivesSaveRoundTripAndDefaultsOffForOldSaves()
        {
            var old=JsonUtility.FromJson<Map02ChapterState>("{\"version\":3,\"phase\":0}");Assert.False(old.villagePlanePlayed);Assert.AreEqual(0,old.villageDaySeconds);
            old.villagePlanePlayed=true;old.villageDaySeconds=481.5f;var restored=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(old));Assert.True(restored.villagePlanePlayed);Assert.AreEqual(481.5f,restored.villageDaySeconds);
        }
        [UnityTest,Timeout(240000)] public IEnumerator ApprovedCommanderRecordingsCompletePlayback()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            var host=new GameObject("Approved commander playback audit");host.AddComponent<AudioListener>();var voice=DialogueVoice.For(host);voice.CanSpeak=()=>true;var source=host.GetComponent<AudioSource>();source.pitch=3;
            var lines=Read().Where(e=>e.speaker=="Chỉ huy"||e.speaker=="Anh Thành").ToArray();Assert.AreEqual(49,lines.Length);
            foreach(var line in lines)
            {
                voice.Play(line.id);Assert.AreEqual(line.id,voice.CurrentId,line.id);float end=Time.realtimeSinceStartup+source.clip.length/3+3;
                while(voice.HasSpeech&&Time.realtimeSinceStartup<end)yield return null;
                Assert.False(voice.HasSpeech,"Stalled commander cue: "+line.id);
            }
            voice.Stop();UnityEngine.Object.Destroy(host);yield return new ExitPlayMode();
        }
        [UnityTest,Timeout(600000)] public IEnumerator EveryCatalogCueCompletesPlaybackAndPauseStopsTheClock()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            var host=new GameObject("Isolated voice playback audit");host.AddComponent<AudioListener>();var voice=DialogueVoice.For(host);voice.CanSpeak=()=>true;var source=host.GetComponent<AudioSource>();source.pitch=3;
            foreach(var line in Read())
            {
                voice.Play(line.id);Assert.AreEqual(line.id,voice.CurrentId,line.id);float until=Time.realtimeSinceStartup+source.clip.length/3+3;
                while(voice.HasSpeech&&Time.realtimeSinceStartup<until)yield return null;
                Assert.False(voice.HasSpeech,"Stalled voice "+line.id);
            }
            var sample=Read().First(e=>e.duration>3);voice.Play(sample.id);yield return new WaitForSecondsRealtime(.1f);Time.timeScale=0;yield return null;float at=source.time;yield return new WaitForSecondsRealtime(.2f);Assert.That(source.time,Is.EqualTo(at).Within(.03f));Time.timeScale=1;voice.Stop();UnityEngine.Object.Destroy(host);yield return new ExitPlayMode();
        }
    }
}
