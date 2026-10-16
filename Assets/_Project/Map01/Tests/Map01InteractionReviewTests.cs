using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map01InteractionReviewTests:ForestSceneTestBase
    {
        [TestCase(18,30,true,true,true)][TestCase(100,25,true,true,true)][TestCase(36,36,true,true,false)]
        [TestCase(10,0,true,true,false)][TestCase(36,0,false,true,false)][TestCase(36,0,true,false,false)]
        public void CampObservationDoesNotRequirePinpointingAnOfficer(float distance,float angle,bool binoculars,bool still,bool expected)
        {Assert.AreEqual(expected,Map01Story.CampObservationQualifies(distance,angle,binoculars,still));}
        [TestCase("Lính địch: Có kẻ đột nhập!",false)][TestCase("Sĩ quan địch: Chặn hắn!",false)]
        [TestCase("Hùng (thì thào): Nam?",false)][TestCase("Chỉ huy (bộ đàm): Nam, báo cáo.",true)]
        [TestCase("[E] Mở khóa hòm",true)]
        public void AmbientSpeechHasNoCaptionButCommanderAndHintsRemain(string text,bool shown)
        {Assert.AreEqual(shown,Map01Mission.GameplayCaption(text)!=null);}
        [UnityTest]public IEnumerator WarmDialogueStartsWithoutBlockingAndKeepsAnklesPlanted()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return Review();yield return new ExitPlayMode();
        }
        static IEnumerator Review()
        {
            var m=Object.FindFirstObjectByType<Map01Mission>();var warm=Map01InteractionWarmup.For(m);float deadline=Time.realtimeSinceStartup+45;
            while(!warm.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(warm.Ready);
            foreach(var e in m.Enemies)e.enabled=false;
            SetPreviewResolution(3840,2160);
            var actor=m.player.GetComponentInChildren<Animator>();var left=actor.GetBoneTransform(HumanBodyBones.LeftFoot);var right=actor.GetBoneTransform(HumanBodyBones.RightFoot);
            float support=Mathf.Min(left.position.y,right.position.y);
            Assert.IsTrue(m.GetComponent<Map01Story>().TryInteract());Assert.IsTrue(m.GetComponent<DialogueVoice>().HasSpeech);
            Assert.Less(warm.DialogueBeginMilliseconds,300,"Prepared dialogue must not stall for seconds.");
            yield return WaitGameSeconds(.8f);Assert.Less(Mathf.Abs(Mathf.Min(left.position.y,right.position.y)-support),.025f);
            Directory.CreateDirectory("Logs/InteractionReview");File.WriteAllText("Logs/InteractionReview/dialogue-latency.txt",warm.DialogueBeginMilliseconds+" ms; feet displacement "+Mathf.Abs(Mathf.Min(left.position.y,right.position.y)-support));
            ScreenCapture.CaptureScreenshot("Logs/InteractionReview/dialogue-4k.png");yield return WaitGameSeconds(.2f);
            SetPreviewResolution(1280,720);yield return WaitGameSeconds(.2f);ScreenCapture.CaptureScreenshot("Logs/InteractionReview/dialogue-720p.png");yield return WaitGameSeconds(.2f);
            m.GetComponent<Map01StoryDialogue>().Close(false);
        }
        [UnityTest]public IEnumerator PreparedBushStartsQuicklyAndBothOutcomesRestoreGameplay()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return BushReview();yield return new ExitPlayMode();
        }
        static IEnumerator BushReview()
        {
            var m=Object.FindFirstObjectByType<Map01Mission>();var warm=Map01InteractionWarmup.For(m);float ready=Time.realtimeSinceStartup+45;
            while(!warm.Ready&&Time.realtimeSinceStartup<ready)yield return null;Assert.IsTrue(warm.Ready);
            foreach(var enemy in m.Enemies)enemy.enabled=false;m.ModernHealth.CinematicInvulnerable=true;
            var rescue=m.GetComponent<Map01Rescue>();rescue.MarkIntroSeen();m.GetComponent<Map01Quest>().RestoreStoryStage(0,new Map01Story.Snapshot{rescueAccepted=true});
            var overseer=rescue.Squad[0];var away=rescue.Squad[3].transform.position;overseer.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(away);
            Assert.Greater(Vector3.Distance(overseer.transform.position,m.hung.position),9);
            var bush=m.GetComponent<Map01BushTakedown>();Directory.CreateDirectory("Logs/InteractionReview");
            foreach(bool won in new[]{true,false}){
                var victim=rescue.Squad[won?1:2];Assert.IsTrue(RescueBushTests.PlaceBush(victim));
                Assert.IsTrue(bush.TryBegin(victim));Assert.Less(warm.BushBeginMilliseconds,300,"Prepared pull must not block for seconds.");
                File.AppendAllText("Logs/InteractionReview/bush-latency.txt",(won?"success":"escape")+": "+warm.BushBeginMilliseconds+" ms\n");
                yield return WaitGameSeconds(.5f);ScreenCapture.CaptureScreenshot("Logs/InteractionReview/bush-pull.png");
                yield return WaitGameSeconds(Map01BushTakedown.PullSeconds-.3f);Assert.AreEqual(Map01BushTakedown.Phase.Struggle,bush.CurrentPhase);
                ScreenCapture.CaptureScreenshot("Logs/InteractionReview/bush-timing.png");
                if(won){for(int i=0;i<3;i++){bush.Timing.Tick(bush.Timing.Gap,false);bush.Timing.Tick(Mathf.Max(0,bush.Timing.Center*2-bush.Timing.Elapsed),true);}Assert.IsTrue(bush.Timing.Won);}
                else{bush.Timing.Tick(2.1f,false);bush.Timing.Tick(.35f,false);bush.Timing.Tick(2.1f,false);}
                yield return WaitGameSeconds(3);Assert.IsFalse(bush.Active);Assert.AreEqual(!won,victim.Alive);
                Assert.IsTrue(m.ModernCombat.enabled&&m.ModernPlayer.enabled);Assert.IsFalse(m.Cinematic);
            }
        }
        [UnityTest]public IEnumerator ExecutionUsesOneSpokenCueAimsAtCaptiveAndKeepsRopes()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return ExecutionReview();yield return new ExitPlayMode();
        }
        static IEnumerator ExecutionReview()
        {
            var m=Object.FindFirstObjectByType<Map01Mission>();var warm=Map01InteractionWarmup.For(m);float ready=Time.realtimeSinceStartup+45;
            while(!warm.Ready&&Time.realtimeSinceStartup<ready)yield return null;Assert.IsTrue(warm.Ready);
            foreach(var enemy in m.Enemies)enemy.enabled=false;
            var rescue=m.GetComponent<Map01Rescue>();rescue.MarkIntroSeen();m.GetComponent<Map01Quest>().RestoreStoryStage(0,new Map01Story.Snapshot{rescueAccepted=true});
            var director=m.GetComponent<Map01RescueCinematic>()??m.gameObject.AddComponent<Map01RescueCinematic>();
            rescue.ReportDetection(new Map01Detection(rescue.Squad[1],Map01DetectionCause.Sight,m.player.position));Assert.IsTrue(director.IsPlaying);
            var voice=m.GetComponentsInChildren<DialogueVoice>().First(v=>v.gameObject.name=="Rescue cinematic dialogue");
            var heard=new System.Collections.Generic.HashSet<string>();bool aimed=false;float error=180,end=Time.realtimeSinceStartup+40;
            var captive=m.hung.GetComponentInChildren<Map01HungVisual>();
            while(director.IsPlaying&&Time.realtimeSinceStartup<end){
                if(!string.IsNullOrEmpty(voice.CurrentId))heard.Add(voice.CurrentId);Assert.IsTrue(captive.RopeVisible);
                int beat=(int)typeof(Map01RescueCinematic).GetField("storyBeat",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(director);
                if(beat==2){var muzzle=rescue.Squad[0].GetComponentInChildren<Map01Rifle>().weapon.Muzzle;
                    error=Mathf.Min(error,Vector3.Angle(muzzle.forward,captive.actor.GetBoneTransform(HumanBodyBones.Chest).position-muzzle.position));
                    if(Vector3.Angle(muzzle.forward,captive.actor.GetBoneTransform(HumanBodyBones.Chest).position-muzzle.position)<5){
                        if(!aimed){Directory.CreateDirectory("Logs/InteractionReview");ScreenCapture.CaptureScreenshot("Logs/InteractionReview/execution-no-subtitle.png");}aimed=true;
                    }}
                yield return null;
            }
            Directory.CreateDirectory("Logs/InteractionReview");File.WriteAllText("Logs/InteractionReview/execution-check.txt","Aim error "+error+" degrees; cues "+heard.Count+"; failed "+rescue.Failed+"; rope "+captive.RopeVisible);
            Assert.IsFalse(director.IsPlaying);Assert.AreEqual(1,heard.Count);Assert.IsTrue(aimed,"Muzzle never aimed at captive: "+error);Assert.IsTrue(rescue.Failed,"Execution must complete failure");Assert.IsTrue(captive.RopeVisible,"Rope must remain visible");
            Assert.Less(captive.actor.GetBoneTransform(HumanBodyBones.Head).position.y-m.hung.position.y,.8f);
        }
    }
}
