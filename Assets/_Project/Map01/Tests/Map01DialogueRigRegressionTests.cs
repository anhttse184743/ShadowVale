using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map01DialogueRigRegressionTests:ForestSceneTestBase
    {
        [Test]public void PointThreeMainOrderContainsMosinBeforeExactlyOneAcknowledgement()
        {
            var lines=Map01OperationLines.RadioReport;
            CollectionAssert.AreEqual(new[]{"m1_ops_radio_report_01","m1_ops_radio_report_02","m1_c3_mosin_order","m1_v2_rescue_approach_03"},lines.Select(x=>x.voice));
            Assert.AreEqual(1,lines.Count(x=>x.speaker=="Nam"&&x.text=="Rõ."));
            Assert.AreEqual(Map01C3Dialogue.MosinInstruction,lines[2].text);
            Assert.True(lines[1].radio&&lines[2].radio);Assert.False(lines.Last().radio);
            foreach(var line in lines){var recording=DialogueVoice.Find(line.voice);Assert.NotNull(recording?.clip,line.voice);Assert.AreEqual(line.speaker+(line.radio?" (bộ đàm): ":": ")+line.text,recording.caption,line.voice);}
        }
        [UnityTest,Timeout(240000)]public IEnumerator LegacyRigCanStartAndCheckpointReattachesHungController()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            // Simulate an older Nam rig without the authored helper names, while
            // retaining its original mesh and bone hierarchy for normal skinning.
            var rig=Object.FindFirstObjectByType<ShadowVale.Gameplay.Player.PlayerController>();
            Assert.NotNull(rig);
            foreach(var t in rig.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("ForeArmTwist")))t.name="Legacy_"+t.name;
            yield return new EnterPlayMode();yield return WaitForMap01Ready();
            var m=Object.FindFirstObjectByType<Map01Mission>();
            foreach(var enemy in m.Enemies)enemy.enabled=false;
            m.GetComponent<Map01Rescue>().MarkIntroSeen();
            var pistol=m.player.GetComponent<Map01Pistol>();var mosin=m.player.GetComponent<Map01Mosin>();
            Assert.NotNull(pistol?.Weapon,"Missing helper tracks must not abort mission startup or pistol registration.");
            Assert.NotNull(mosin?.Weapon,"Mosin shares the same compatibility path.");
            m.ModernCombat.Equip(WeaponKind.Pistol);yield return WaitGameSeconds(.4f);
            Assert.Greater(pistol.PoseWeight,.9f);
            var twist=m.player.GetComponentInChildren<ShadowVale.Gameplay.Player.ForearmTwist>();
            if(twist!=null)Assert.AreEqual(0,(int)twist.GetType().GetField("authoredPoseOwners",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(twist),"A legacy rig must retain its dynamic twist correction.");
            Assert.True(pistol.Weapon.gameObject.activeInHierarchy);
            Assert.True(m.player.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(s=>s.bones.All(b=>b!=null)),"Never install a partially bound replacement hand mesh.");
            m.ModernCombat.Equip(WeaponKind.Sniper);yield return WaitGameSeconds(.4f);Assert.Greater(mosin.PoseWeight,.9f);
            var hung=m.hung.GetComponentInChildren<Map01HungVisual>();var controller=hung.actor.runtimeAnimatorController;
            Assert.NotNull(controller);
            hung.actor.runtimeAnimatorController=null;
            hung.RestoreCheckpointPose();
            Assert.AreSame(controller,hung.actor.runtimeAnimatorController);
            Assert.True(hung.actor.GetCurrentAnimatorStateInfo(0).IsName(Map01HungVisual.CaptiveState));
            Assert.True(hung.RopeVisible);
            // A held bound-death pose also relinquishes its Playable on load.
            hung.EndCaptivePresentation(true);Assert.IsNull(hung.actor.runtimeAnimatorController);
            hung.RestoreCheckpointPose();Assert.AreSame(controller,hung.actor.runtimeAnimatorController);
            Assert.True(hung.actor.GetCurrentAnimatorStateInfo(0).IsName(Map01HungVisual.CaptiveState));
            Assert.True(hung.RopeVisible);
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }
        [UnityTest,Timeout(240000)]public IEnumerator MainRadioReportDeliversMosinAndAcknowledgesOnlyOnce()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return CheckMainRadioReport();
            yield return new ExitPlayMode();
        }
        // Captured quest callbacks must be allocated after EnterPlayMode's domain
        // reload; the runner cannot retain compiler-generated closure objects.
        static IEnumerator CheckMainRadioReport()
        {
            yield return WaitForMap01Ready();
            var m=Object.FindFirstObjectByType<Map01Mission>();Assert.IsTrue(m!=null);
            foreach(var enemy in m.Enemies)enemy.enabled=false;
            m.GetComponent<Map01Rescue>().MarkIntroSeen();
            var quest=m.GetComponent<Map01Quest>();Assert.NotNull(quest,"The main map must retain its quest component.");
            Assert.NotNull(m.GetComponent<Map01Story>(),"Story still exists before selecting the report checkpoint.");
            quest.RestoreStage(Map01Quest.ReportCampsStage);
            var story=m.GetComponent<Map01Story>();story.Progress.radioDisabled=story.Progress.powerDisabled=true;
            var talk=m.GetComponent<Map01StoryDialogue>();Assert.NotNull(talk);
            Assert.True(talk.Begin(Map01OperationLines.RadioReport,()=>quest.BeginCommanderOrder(),"Tới điểm 3",true));
            var voice=DialogueVoice.For(m.gameObject);var heard=new List<string>{voice.CurrentId};
            Assert.AreEqual("m1_ops_radio_report_01",voice.CurrentId,$"Initial voice gate: menu={ForestMenu.Visible}, paused={m.Paused}, scale={Time.timeScale}, enabled={talk.enabled}, canSpeak={voice.CanSpeak?.Invoke()}");
            var previous=voice.LineStarted;voice.LineStarted=line=>{previous?.Invoke(line);heard.Add(line.id);};
            m.Say("Thông báo gameplay đến chậm",1);m.Say(null,0);
            Assert.True(voice.CanSpeak(),"Gameplay notices cannot replace the cinematic voice gate.");
            Assert.AreEqual("m1_ops_radio_report_01",voice.CurrentId);
            float until=Time.realtimeSinceStartup+45;
            while((talk.Index!=3||voice.HasSpeech)&&Time.realtimeSinceStartup<until)yield return null;
            Assert.AreEqual(3,talk.Index,$"Dialogue stalled: menu={ForestMenu.Visible}, paused={m.Paused}, scale={Time.timeScale}, enabled={talk.enabled}, voice={voice.CurrentId}, speech={voice.HasSpeech}, elapsed={typeof(Map01StoryDialogue).GetField("elapsed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(talk)}, time={Time.time}");Assert.False(voice.HasSpeech);yield return WaitGameSeconds(1.1f);
            talk.Advance();yield return WaitGameSeconds(1);
            Assert.AreEqual(Map01Quest.BossStage,quest.Stage);Assert.False(voice.HasSpeech,"Confirming the main dialogue must not enqueue another Mosin order.");
            CollectionAssert.AreEqual(new[]{"m1_ops_radio_report_01","m1_ops_radio_report_02","m1_c3_mosin_order","m1_v2_rescue_approach_03"},heard,"Stage transition must not replay the Mosin order or Nam's reply.");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
