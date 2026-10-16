using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
namespace ShadowVale.Map01.Tests {
    public class DialogueVoiceTests:ForestSceneTestBase {
        [Serializable] class Catalog {public DialogueVoice.Line[] entries;}
        [Test] public void BriefingUsesAlignedVoicesAndHungCarriesSupplies() {
            var opening=Resources.Load<Map01OpeningCutscene>("Cutscenes/Map01Opening");
            StringAssert.EndsWith("/m1_open_commander.wav",UnityEditor.AssetDatabase.GetAssetPath(opening.commanderVoice));
            StringAssert.EndsWith("/m1_open_reply.wav",UnityEditor.AssetDatabase.GetAssetPath(opening.soldierVoice));
            Assert.AreEqual(3,opening.commanderSubtitleStarts.Length);
            Assert.Greater(opening.commanderSubtitleStarts[1],0);
            Assert.Greater(opening.commanderSubtitleStarts[2],opening.commanderSubtitleStarts[1]);
            Assert.Less(opening.commanderSubtitleStarts[2],opening.commanderVoice.length);
            StringAssert.Contains("tiếp tế",DialogueVoice.Caption("m1_story_02"));
            StringAssert.DoesNotContain("đưa thư",DialogueVoice.Caption("m1_story_02"));
            StringAssert.Contains("Ta nghi",DialogueVoice.Caption("m1_story_09"));
            StringAssert.DoesNotContain("mật lệnh",DialogueVoice.Caption("m1_story_09"));
            StringAssert.Contains("đưa hàng tiếp tế",DialogueVoice.Caption("m1_rescue_thanks"));
            StringAssert.Contains("Chúng tưởng",DialogueVoice.Caption("m1_rescue_thanks"));
        }
        [Test] public void EveryStoryLineHasVietnameseAudioAndOnlyRadioIsFiltered() {
            var bank=Resources.Load<TextAsset>("Dialogue/voice-catalog");Assert.NotNull(bank);
            var catalog=JsonUtility.FromJson<Catalog>(bank.text);Assert.AreEqual(26,catalog.entries.Length);
            int radio=0;
            foreach(var line in catalog.entries) {
                var imported=DialogueVoice.Find(line.id);Assert.NotNull(imported.clip,line.id);
                Assert.AreEqual("vi",line.language,line.id);Assert.AreEqual(48000,imported.clip.frequency);
                Assert.AreEqual(1,imported.clip.channels);Assert.Greater(imported.clip.length,1,line.id);
                Assert.AreEqual(line.id=="m1_radio_order"||line.id=="m1_radio_reply",line.radio);if(line.radio)radio++;
            }
            Assert.AreEqual(2,radio);
        }
        [UnityTest] public IEnumerator PauseAndNewSpeakerDoNotOverlap() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
            var a=DialogueVoice.For(new GameObject("Voice test A"));var b=DialogueVoice.For(new GameObject("Voice test B"));
            a.Play("m1_radio_reply");yield return WaitGameSeconds(.1f);Assert.AreEqual("m1_radio_reply",a.CurrentId);
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);Assert.IsTrue(a.HasSpeech);
            Time.timeScale=1;b.Play("m2_moor_order");Assert.IsFalse(a.HasSpeech);Assert.AreEqual("m2_moor_order",b.CurrentId);
            DialogueVoice.StopAll();Assert.IsFalse(b.HasSpeech);
            var queued=a.SpeakText(DialogueVoice.Caption("m1_story_06")+"\n"+DialogueVoice.Caption("m1_rescue_thanks"));
            Assert.Greater(queued,DialogueVoice.Length("m1_story_06"));Assert.AreEqual("m1_story_06",a.CurrentId);
            a.Stop();yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator RadioReplyWaitsForOrderAndSkipStopsSpeech() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
            var mission=UnityEngine.Object.FindFirstObjectByType<Map01Mission>();var sequence=Map01Extraction.Get(mission);
            sequence.autoContinueToMap2=false;sequence.Begin(true);
            var voice=sequence.GetComponent<DialogueVoice>();Assert.AreEqual("m1_radio_order",voice.CurrentId);
            float replyAt=(float)typeof(Map01Extraction).GetField("radioReplyAt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(sequence);
            Assert.GreaterOrEqual(replyAt,DialogueVoice.Length("m1_radio_order")+.25f,"The reply must be scheduled after the complete order.");
            float deadline=Time.realtimeSinceStartup+20;
            while(voice.CurrentId!="m1_radio_reply" && sequence.CurrentPhase==Map01Extraction.Phase.Radio && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual("m1_radio_reply",voice.CurrentId);
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
            voice.LineStarted=line=>{starts++;Assert.IsTrue(line.radio);Assert.AreEqual("Dialogue/VI/m1_radio_order",line.resource);};
            voice.PlayOnce("m1_radio_order");yield return WaitGameSeconds(.1f);
            var source=voice.GetComponent<AudioSource>();float position=source.time;
            voice.PlayOnce("m1_radio_order");voice.PlayOnce("m1_radio_order");
            Assert.AreEqual(1,starts);Assert.GreaterOrEqual(source.time,position);
            voice.Stop();voice.PlayOnce("m1_radio_order");Assert.AreEqual(1,starts);Assert.IsFalse(voice.HasSpeech);
        }
        [UnityTest] public IEnumerator OpeningFinishesWithoutRepeatingBriefing() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            yield return VerifyOpeningHandoff(false);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator OpeningSkipDoesNotQueueAnotherBriefing() {
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
            Assert.IsTrue(opening.IsPlaying);
            var voice=mission.GetComponent<DialogueVoice>();
            Assert.IsTrue(voice==null || !voice.HasSpeech,"No second briefing may wait behind the opening.");
            if(skip)opening.Skip();
            double deadline=Time.realtimeSinceStartupAsDouble+90;
            while(opening.IsPlaying && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.IsFalse(opening.IsPlaying);Assert.IsFalse(mission.Cinematic);
            yield return WaitGameSeconds(.5f);
            Assert.IsTrue(string.IsNullOrEmpty(mission.Dialogue),"Opening subtitles must end with the cutscene.");
            Assert.IsFalse(mission.GetComponent<DialogueVoice>().HasSpeech);
            Assert.AreEqual(Map01Quest.RescueStage,mission.Stage);
            StringAssert.Contains("Giải cứu Hùng",mission.GetComponent<Map01Quest>().ObjectiveText);
            Assert.IsTrue(mission.ModernPlayer.enabled);Assert.IsTrue(mission.ModernCombat.enabled);
        }
    }
}
