using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
namespace ShadowVale.Map01.Tests {
    public class DialogueVoiceTests:ForestSceneTestBase {
        [Serializable] class Catalog {public Entry[] entries;}
        [Serializable] class Entry {
            public string id,caption,match,resource,cleanResource,language,pendingVoice;
            public bool radio;
            public float duration;
        }
        static Catalog ReadCatalog()=>JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Dialogue/voice-catalog").text);
        static readonly string[] RadioIds={
            "m1_v2_rescue_approach_02","m1_v2_supply_report_02","m1_v2_radio_report_02",
            "m1_v2_commander_confirm_02","m1_v2_extraction_02","m1_ops_supply_report_02",
            "m1_ops_radio_report_02","m1_ops_commander_report_02","m1_ops_retreat_02",
            "m1_c3_gate_checkin_call","m1_c3_gate_checkin_reply","m1_c3_gate_no_answer",
            "m1_c3_gate_alarm","m1_c3_mosin_order"
        };
        string previousSaveRoot;
        [SetUp] public void IsolateSaves() {
            previousSaveRoot=SessionState.GetString(ForestSaveSlots.EditorTestRootKey,"");
            SessionState.SetString(ForestSaveSlots.EditorTestRootKey,
                Path.GetFullPath("Docs/Reviews/VoiceSync_20261018/TestSaves/"+Guid.NewGuid().ToString("N")));
        }
        [TearDown] public void RestoreSaves() {
            Time.timeScale=1;DialogueVoice.StopAll();
            if(string.IsNullOrEmpty(previousSaveRoot))SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
            else SessionState.SetString(ForestSaveSlots.EditorTestRootKey,previousSaveRoot);
        }
        [Test] public void NewRescueRecordingsMatchTheUpdatedCaptions() {
            StringAssert.Contains("cầu gỗ",DialogueVoice.Caption("m1_story_09"));
            StringAssert.Contains("Tay phải còn đau",DialogueVoice.Caption("m1_rescue_freed"));
            StringAssert.DoesNotContain("đã bàn giao",DialogueVoice.Caption("m1_rescue_delivered"));
        }
        [Test] public void LegacyBriefingUsesAlignedVoiceAssets() {
            var opening=Resources.Load<Map01OpeningCutscene>("Cutscenes/Map01Opening");
            Assert.NotNull(opening);
            StringAssert.EndsWith("/m1_open_commander.wav",UnityEditor.AssetDatabase.GetAssetPath(opening.commanderVoice));
            StringAssert.EndsWith("/m1_open_reply.wav",UnityEditor.AssetDatabase.GetAssetPath(opening.soldierVoice));
            Assert.AreEqual(3,opening.commanderSubtitleStarts.Length);
            Assert.Greater(opening.commanderSubtitleStarts[1],0);
            Assert.Greater(opening.commanderSubtitleStarts[2],opening.commanderSubtitleStarts[1]);
            Assert.Less(opening.commanderSubtitleStarts[2],opening.commanderVoice.length);
        }
        [Test] public void EveryStoryLineHasVietnameseAudioAndOnlyRadioIsFiltered() {
            var bank=Resources.Load<TextAsset>("Dialogue/voice-catalog");Assert.NotNull(bank);
            var catalog=ReadCatalog();Assert.IsNotEmpty(catalog.entries);
            var source=JsonUtility.FromJson<Catalog>(File.ReadAllText("SourceArt/Dialogue/voice-catalog.json"));
            CollectionAssert.AreEquivalent(source.entries.Select(e=>e.id),catalog.entries.Select(e=>e.id));
            Assert.AreEqual(catalog.entries.Length,catalog.entries.Select(e=>e.id).Distinct().Count(),"Voice IDs must be unique.");
            Assert.AreEqual(catalog.entries.Length,catalog.entries.Select(e=>e.resource).Distinct().Count(),"Every line needs its own recording.");
            CollectionAssert.AreEquivalent(RadioIds,catalog.entries.Where(e=>e.radio).Select(e=>e.id));
            foreach(var line in catalog.entries) {
                Assert.IsNotEmpty(line.id);Assert.IsNotEmpty(line.caption,line.id);
                var imported=DialogueVoice.Find(line.id);Assert.NotNull(imported,line.id);Assert.NotNull(imported.clip,line.id);
                Assert.AreEqual("vi",line.language,line.id);Assert.AreEqual(48000,imported.clip.frequency);
                Assert.AreEqual(1,imported.clip.channels);Assert.Greater(imported.clip.length,.1f,line.id);
                Assert.That(imported.clip.length,Is.EqualTo(line.duration).Within(.035f),line.id+" duration");
                var master=source.entries.Single(e=>e.id==line.id);
                Assert.AreEqual(master.caption,line.caption,line.id);Assert.AreEqual(master.resource,line.resource,line.id);
                Assert.AreEqual(master.match,line.match,line.id);Assert.AreEqual(master.pendingVoice,line.pendingVoice,line.id);
                Assert.AreEqual(master.radio,line.radio,line.id);
                if(!string.IsNullOrEmpty(line.cleanResource)) {
                    var clean=Resources.Load<AudioClip>(line.cleanResource);Assert.NotNull(clean,line.id+" clean master");
                    if(line.radio) {
                        StringAssert.EndsWith("_radio",line.resource,line.id);
                        Assert.AreNotSame(clean,imported.clip,line.id+" must use the processed radio recording.");
                    } else Assert.AreSame(clean,imported.clip,line.id+" must use the clean recording.");
                }
            }
        }
        [Test] public void CurrentOperationDialogueUsesMatchingCharacterRecordings() {
            var groups=new[]{Map01OperationLines.SupplyOrder,Map01OperationLines.SupplyReport,
                Map01OperationLines.Handoff,Map01OperationLines.RadioReport,Map01OperationLines.CommanderReport,
                Map01OperationLines.EarlyCommanderReport,Map01OperationLines.RetreatReport};
            foreach(var line in groups.SelectMany(group=>group)) {
                var voice=DialogueVoice.Find(line.voice);Assert.NotNull(voice,line.voice);Assert.NotNull(voice.clip,line.voice);
                StringAssert.Contains(line.text,voice.caption,line.voice);
                StringAssert.StartsWith(line.speaker,voice.caption,line.voice);
                Assert.AreEqual(line.radio,voice.radio,line.voice);
            }
        }
        [UnityTest] public IEnumerator PauseAndNewSpeakerDoNotOverlap() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            yield return CheckPauseAndQueue();
            yield return new ExitPlayMode();
        }
        static IEnumerator CheckPauseAndQueue() {
            new GameObject("Test audio listener").AddComponent<AudioListener>();
            var a=DialogueVoice.For(new GameObject("Voice test A"));var b=DialogueVoice.For(new GameObject("Voice test B"));
            a.Play("m1_ops_radio_report_02");yield return WaitGameSeconds(.1f);Assert.AreEqual("m1_ops_radio_report_02",a.CurrentId);
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);Assert.IsTrue(a.HasSpeech);
            float pausedAt=a.GetComponent<AudioSource>().time;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(a.GetComponent<AudioSource>().time,Is.EqualTo(pausedAt).Within(.03f),"Pause must freeze audio playback.");
            Time.timeScale=1;b.Play("m2_moor_order");Assert.IsFalse(a.HasSpeech);Assert.AreEqual("m2_moor_order",b.CurrentId);
            DialogueVoice.StopAll();Assert.IsFalse(b.HasSpeech);
            var first=DialogueVoice.Find("m1_rescue_shelter");var second=DialogueVoice.Find("m1_jetty_observation");
            var heard=new List<string>();a.LineStarted=line=>heard.Add(line.id);
            var queued=a.SpeakText(first.match+"\n"+second.match);
            Assert.Greater(queued,first.clip.length);Assert.AreEqual(first.id,a.CurrentId);
            float deadline=Time.realtimeSinceStartup+queued+5;
            while(a.HasSpeech&&Time.realtimeSinceStartup<deadline) {
                Assert.IsFalse(b.HasSpeech,"The previous speaker cannot overlap a queued conversation.");
                yield return null;
            }
            Assert.IsFalse(a.HasSpeech,"The queue must finish.");
            CollectionAssert.AreEqual(new[]{first.id,second.id},heard);
        }
        [UnityTest] public IEnumerator PendingReplacementLinesCannotAutoPlayTheirOldRecordings() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            yield return CheckPendingReplacementLines();
            yield return new ExitPlayMode();
        }
        static IEnumerator CheckPendingReplacementLines() {
            // Allocate captured state only after EnterPlayMode's domain reload, as in the other playback checks.
            Assert.IsTrue(Application.isPlaying);
            new GameObject("Test audio listener").AddComponent<AudioListener>();
            var voice=DialogueVoice.For(new GameObject("Pending recording regression"));
            int starts=0;voice.LineStarted=_=>starts++;
            var source=voice.GetComponent<AudioSource>();
            Assert.NotNull(source,"The voice's Awake must initialize its AudioSource in Play Mode.");
            yield return null;
            Assert.IsFalse(voice.HasSpeech,"A fresh voice must be idle before the caption is submitted.");
            foreach(var line in ReadCatalog().entries.Where(e=>!string.IsNullOrEmpty(e.pendingVoice))) {
                Assert.IsTrue(string.IsNullOrEmpty(line.match),line.id+" must stay unmatched until its replacement audio is ready.");
                Assert.AreEqual(0,voice.SpeakText(line.caption),line.id);
                yield return null;
                Assert.AreEqual(0,starts,line.id+" must not start a recording.");
                Assert.IsNull(voice.CurrentId,line.id);Assert.IsNull(source.clip,line.id);
                Assert.IsFalse(voice.HasSpeech,line.id+" must not automatically play the old words.");
            }
        }
        [UnityTest] public IEnumerator RadioReplyWaitsForOrderAndSkipStopsSpeech() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
            yield return CheckExtractionRadio(true);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator ExtractionRadioFinishesAllThreeLinesInOrder() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
            yield return CheckExtractionRadio(false);
            yield return new ExitPlayMode();
        }
        static IEnumerator CheckExtractionRadio(bool skip) {
            var mission=UnityEngine.Object.FindFirstObjectByType<Map01Mission>();Assert.NotNull(mission);
            float ready=Time.realtimeSinceStartup+30;
            while((!mission.IsInitialized||mission.ModernCombat==null||mission.GetComponent<Map01Story>()?.Commander==null)
                &&Time.realtimeSinceStartup<ready)yield return null;
            Assert.IsTrue(mission.IsInitialized);Assert.NotNull(mission.ModernCombat);
            foreach(var enemy in mission.Enemies)enemy.enabled=false;
            mission.ModernHealth.CinematicInvulnerable=true;
            var quest=mission.GetComponent<Map01Quest>();var progress=OfficerReported();progress.commanderKilled=true;
            quest.RestoreStoryStage(Map01Quest.ReportBossStage,progress);
            var sequence=Map01Extraction.Get(mission);sequence.autoContinueToMap2=false;
            var voice=DialogueVoice.For(sequence.gameObject);
            var heard=new List<string>();var started=new List<float>();
            voice.LineStarted=line=>{heard.Add(line.id);started.Add(Time.time);};
            mission.GetComponent<Map01Story>().Progress.retreatReported=true;
            quest.CompleteBossDefeat(true);
            Assert.AreEqual(Map01Extraction.Phase.Radio,sequence.CurrentPhase);
            Assert.AreEqual("m1_v2_extraction_01",voice.CurrentId);
            float deadline=Time.realtimeSinceStartup+30;
            while(heard.Count<2&&sequence.CurrentPhase==Map01Extraction.Phase.Radio&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual("m1_v2_extraction_02",voice.CurrentId);
            Assert.GreaterOrEqual(started[1]-started[0],DialogueVoice.Length(heard[0])+.25f,"A reply must wait for the whole previous recording.");
            if(skip) {
                sequence.Skip();Assert.IsFalse(voice.HasSpeech);yield return WaitGameSeconds(.5f);
                Assert.AreEqual(2,heard.Count,"Skipping cannot leave a later line queued.");
            } else {
                while(sequence.CurrentPhase==Map01Extraction.Phase.Radio&&Time.realtimeSinceStartup<deadline)yield return null;
                CollectionAssert.AreEqual(new[]{"m1_v2_extraction_01","m1_v2_extraction_02","m1_v2_extraction_03"},heard);
                Assert.GreaterOrEqual(started[2]-started[1],DialogueVoice.Length(heard[1])+.25f);
                Assert.IsFalse(voice.HasSpeech);
            }
            Assert.AreEqual(Map01Extraction.Phase.Extraction,sequence.CurrentPhase);
            Assert.AreEqual(Map01Quest.ExtractionStage,quest.Stage);Assert.IsFalse(mission.Cinematic);
            Assert.IsTrue(mission.ModernPlayer.enabled&&mission.ModernCombat.enabled);
        }
        [UnityTest] public IEnumerator RadioCueDoesNotReplayOrSelectDryVoice() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            yield return VerifyRadioCueOnce();
            yield return new ExitPlayMode();
        }
        static IEnumerator VerifyRadioCueOnce() {
            new GameObject("Test audio listener").AddComponent<AudioListener>();
            var voice=DialogueVoice.For(new GameObject("Radio cue regression"));int starts=0;
            voice.LineStarted=line=>{starts++;Assert.IsTrue(line.radio);Assert.AreEqual("Dialogue/VI/m1_ops_radio_report_02_radio",line.resource);};
            voice.PlayOnce("m1_ops_radio_report_02");yield return WaitGameSeconds(.1f);
            var source=voice.GetComponent<AudioSource>();float position=source.time;
            voice.PlayOnce("m1_ops_radio_report_02");voice.PlayOnce("m1_ops_radio_report_02");
            Assert.AreEqual(1,starts);Assert.GreaterOrEqual(source.time,position);
            voice.Stop();voice.PlayOnce("m1_ops_radio_report_02");Assert.AreEqual(1,starts);Assert.IsFalse(voice.HasSpeech);
        }
        [UnityTest] public IEnumerator NewGameWaitsForCommanderAndDoesNotRepeatAcceptedBriefing() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            yield return VerifyCommanderHandoff(false);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator ClosingCommanderBriefingDoesNotQueueSpeechOrAcceptOrder() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            yield return VerifyCommanderHandoff(true);
            yield return new ExitPlayMode();
        }
        static IEnumerator VerifyCommanderHandoff(bool cancel) {
            Map01OpeningCutscene.RequestNewGame();Map01SaveSystem.BeginGame(-1);
            yield return WaitForMap01Ready();
            var mission=UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
            AssertWaitingForCommander(mission);yield return WaitGameSeconds(.6f);AssertWaitingForCommander(mission);
            var story=mission.GetComponent<Map01Story>();var dialogue=mission.GetComponent<Map01StoryDialogue>();
            PlaceNam(mission,story.Commander.position+story.Commander.forward*1.8f);
            Assert.IsTrue(story.TryInteract());Assert.IsTrue(dialogue.IsPlaying);
            Assert.AreEqual("m1_story_rescue_order_01",mission.GetComponent<DialogueVoice>().CurrentId);
            yield return WaitGameSeconds(1.1f);dialogue.Close(!cancel);
            Assert.IsFalse(dialogue.IsPlaying||mission.Cinematic);
            yield return WaitGameSeconds(.5f);
            Assert.IsTrue(string.IsNullOrEmpty(mission.Dialogue),"Closing the conversation must clear its captions.");
            Assert.IsFalse(mission.GetComponent<DialogueVoice>().HasSpeech);
            Assert.AreEqual(Map01Quest.RescueStage,mission.Stage);
            Assert.AreEqual(!cancel,mission.GetComponent<Map01Quest>().RescueOrderAccepted);
            if(cancel)AssertWaitingForCommander(mission);
            else StringAssert.Contains("Giải cứu Hùng",mission.GetComponent<Map01Quest>().ObjectiveText);
            Assert.IsTrue(mission.ModernPlayer.enabled);Assert.IsTrue(mission.ModernCombat.enabled);
            var voice=mission.GetComponent<DialogueVoice>();voice.Play("m1_story_rescue_order_02");
            yield return WaitGameSeconds(.1f);
            Assert.IsTrue(voice.HasSpeech,"Closing the conversation must restore the gameplay voice gate.");
            Assert.AreEqual("m1_story_rescue_order_02",voice.CurrentId);voice.Stop();
        }
    }
}
