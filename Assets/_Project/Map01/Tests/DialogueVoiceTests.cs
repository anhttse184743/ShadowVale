using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
namespace ShadowVale.Map01.Tests {
    public class DialogueVoiceTests:ForestSceneTestBase {
        [Serializable] class Catalog {public DialogueVoice.Line[] entries;}
        [Test] public void BriefingAssetsRemainAlignedAndHungReportsTutorialCapture() {
            var opening=Resources.Load<Map01OpeningCutscene>("Cutscenes/Map01Opening");
            StringAssert.EndsWith("/m1_open_commander.wav",UnityEditor.AssetDatabase.GetAssetPath(opening.commanderVoice));
            StringAssert.EndsWith("/m1_open_reply.wav",UnityEditor.AssetDatabase.GetAssetPath(opening.soldierVoice));
            Assert.AreEqual(3,opening.commanderSubtitleStarts.Length);
            Assert.Greater(opening.commanderSubtitleStarts[1],0);
            Assert.Greater(opening.commanderSubtitleStarts[2],opening.commanderSubtitleStarts[1]);
            Assert.Less(opening.commanderSubtitleStarts[2],opening.commanderVoice.length);
            StringAssert.Contains("cầu gỗ",DialogueVoice.Caption("m1_story_09"));
            StringAssert.DoesNotContain("mật lệnh",DialogueVoice.Caption("m1_story_09"));
            StringAssert.Contains("Tay phải còn đau",DialogueVoice.Caption("m1_rescue_freed"));
            StringAssert.DoesNotContain("đưa hàng tiếp tế",DialogueVoice.Caption("m1_rescue_freed"));
            StringAssert.DoesNotContain("đã bàn giao",DialogueVoice.Caption("m1_rescue_delivered"));
        }
        [Test] public void EveryStoryLineHasVietnameseAudioAndOnlyRadioIsFiltered() {
            var bank=Resources.Load<TextAsset>("Dialogue/voice-catalog");Assert.NotNull(bank);
            var catalog=JsonUtility.FromJson<Catalog>(bank.text);Assert.AreEqual(76,catalog.entries.Length);
            Assert.AreEqual(catalog.entries.Length,catalog.entries.Select(e=>e.id).Distinct().Count());
            var radioIds=new[]{"m1_v2_rescue_approach_02","m1_v2_supply_report_02","m1_v2_radio_report_02","m1_v2_commander_confirm_02","m1_v2_extraction_02","m1_ops_supply_report_02","m1_ops_radio_report_02","m1_ops_commander_report_02","m1_ops_retreat_02","m1_c3_gate_checkin_call","m1_c3_gate_checkin_reply","m1_c3_gate_no_answer","m1_c3_gate_alarm","m1_c3_mosin_order"};
            int radio=0;
            foreach(var line in catalog.entries) {
                var imported=DialogueVoice.Find(line.id);Assert.NotNull(imported.clip,line.id);
                Assert.AreEqual("vi",line.language,line.id);Assert.AreEqual(48000,imported.clip.frequency);
                Assert.AreEqual(1,imported.clip.channels);Assert.Greater(imported.clip.length,.3f,line.id);
                Assert.AreEqual(radioIds.Contains(line.id),line.radio,line.id);if(line.radio)radio++;
            }
            Assert.AreEqual(radioIds.Length,radio);
        }
        [UnityTest] public IEnumerator PauseAndNewSpeakerDoNotOverlap() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
            var a=DialogueVoice.For(new GameObject("Voice test A"));var b=DialogueVoice.For(new GameObject("Voice test B"));
            a.Play("m1_v2_rescue_approach_03");yield return WaitGameSeconds(.1f);Assert.AreEqual("m1_v2_rescue_approach_03",a.CurrentId);
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);Assert.IsTrue(a.HasSpeech);
            Time.timeScale=1;b.Play("m2_moor_order");Assert.IsFalse(a.HasSpeech);Assert.AreEqual("m2_moor_order",b.CurrentId);
            DialogueVoice.StopAll();Assert.IsFalse(b.HasSpeech);
            var queued=a.SpeakText(DialogueVoice.Caption("m1_story_09")+"\n"+DialogueVoice.Caption("m1_rescue_freed"));
            Assert.Greater(queued,DialogueVoice.Length("m1_story_09"));Assert.AreEqual("m1_story_09",a.CurrentId);
            a.Stop();yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator RadioReplyWaitsForOrderAndSkipStopsSpeech() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
            var mission=UnityEngine.Object.FindFirstObjectByType<Map01Mission>();var sequence=Map01Extraction.Get(mission);
            // RestoreStage resumes extraction without replaying radio. This test exercises the live radio entry.
            typeof(Map01Quest).GetField("stage",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .SetValue(mission.GetComponent<Map01Quest>(),Map01Quest.ExtractionStage);
            sequence.autoContinueToMap2=false;sequence.Begin(true);
            var voice=sequence.GetComponent<DialogueVoice>();Assert.AreEqual("m1_v2_extraction_01",voice.CurrentId);
            float replyAt=(float)typeof(Map01Extraction).GetField("nextRadioAt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(sequence);
            Assert.GreaterOrEqual(replyAt,DialogueVoice.Length("m1_v2_extraction_01")+.25f,"The reply must be scheduled after the complete report.");
            float deadline=Time.realtimeSinceStartup+20;
            while(voice.CurrentId!="m1_v2_extraction_02" && sequence.CurrentPhase==Map01Extraction.Phase.Radio && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual("m1_v2_extraction_02",voice.CurrentId);
            sequence.Skip();Assert.IsFalse(voice.HasSpeech);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator RadioCueDoesNotReplayOrSelectDryVoice() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
            yield return VerifyRadioCueOnce();
            yield return new ExitPlayMode();
        }
        static IEnumerator VerifyRadioCueOnce() {
            var voice=DialogueVoice.For(new GameObject("Radio cue regression"));int starts=0;
            voice.LineStarted=line=>{starts++;Assert.IsTrue(line.radio);Assert.AreEqual("Dialogue/VI/m1_v2_extraction_02_radio",line.resource);};
            voice.PlayOnce("m1_v2_extraction_02");yield return WaitGameSeconds(.1f);
            var source=voice.GetComponent<AudioSource>();float position=source.time;
            voice.PlayOnce("m1_v2_extraction_02");voice.PlayOnce("m1_v2_extraction_02");
            Assert.AreEqual(1,starts);Assert.GreaterOrEqual(source.time,position);
            voice.Stop();voice.PlayOnce("m1_v2_extraction_02");Assert.AreEqual(1,starts);Assert.IsFalse(voice.HasSpeech);
        }
        [UnityTest] public IEnumerator NewGameWaitsForCommanderConversationWithoutLegacyOpening() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            yield return VerifyOpeningHandoff(false);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator SkippingDormantOpeningCannotAcceptRescueOrder() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            yield return VerifyOpeningHandoff(true);
            yield return new ExitPlayMode();
        }
        static IEnumerator VerifyOpeningHandoff(bool skip) {
            Map01OpeningCutscene.RequestNewGame();Map01SaveSystem.BeginGame(-1);
            yield return null;yield return null;
            var mission=UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
            var opening=UnityEngine.Object.FindFirstObjectByType<Map01OpeningCutscene>();
            Assert.NotNull(opening);Assert.IsFalse(opening.IsPlaying,"Orders must wait for the E conversation.");
            var voice=mission.GetComponent<DialogueVoice>();
            Assert.IsTrue(voice==null || !voice.HasSpeech,"No second briefing may wait behind the opening.");
            if(skip)opening.Skip();
            Assert.IsFalse(opening.IsPlaying);Assert.IsFalse(mission.Cinematic);
            yield return WaitGameSeconds(.5f);
            Assert.IsTrue(string.IsNullOrEmpty(mission.Dialogue),"Opening subtitles must end with the cutscene.");
            var remainingVoice=mission.GetComponent<DialogueVoice>();
            Assert.IsTrue(remainingVoice==null || !remainingVoice.HasSpeech);
            Assert.AreEqual(Map01Quest.RescueStage,mission.Stage);
            Assert.IsFalse(mission.GetComponent<Map01Quest>().RescueOrderAccepted,"Skipping a dormant old opening must not grant the rescue order.");
            StringAssert.Contains("nhận nhiệm vụ cứu Hùng",mission.GetComponent<Map01Quest>().ObjectiveText);
            Assert.IsTrue(mission.ModernPlayer.enabled);Assert.IsTrue(mission.ModernCombat.enabled);
        }
    }
}
