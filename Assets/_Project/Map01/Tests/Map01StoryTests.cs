using System;
using System.Collections;
using System.IO;
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
    public sealed class Map01StoryTests:ForestSceneTestBase
    {
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static readonly FieldInfo Storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
        static void Isolate()=>Storage.SetValue(null,Path.GetFullPath("Logs/Map01Story/saves/"+Guid.NewGuid().ToString("N")));
        [TearDown]public void ResetStorage()=>Storage.SetValue(null,null);
        static void At(Vector3 at)
        {
            var cc=M.player.GetComponent<CharacterController>();bool on=cc.enabled;cc.enabled=false;M.player.position=at;cc.enabled=on;
            M.ModernPlayer.RestoreMotion(false);Physics.SyncTransforms();
        }
        static IEnumerator Confirm(Map01StoryDialogue d)
        {
            float until=Time.realtimeSinceStartup+90;
            while(d.IsPlaying&&Time.realtimeSinceStartup<until){float start=Time.time;while(Time.time-start<1.05f)yield return null;d.Advance();yield return null;}
            Assert.IsFalse(d.IsPlaying,"Every line and final confirmation must release controls.");
        }
        [UnityTest]public IEnumerator NewGameWaitsForCommanderAndSeparateFinalAcceptance()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");Map01OpeningCutscene.RequestNewGame();
            yield return new EnterPlayMode();yield return null;yield return null;Isolate();
            var q=M.GetComponent<Map01Quest>();var s=M.GetComponent<Map01Story>();var d=M.GetComponent<Map01StoryDialogue>();
            Assert.IsFalse(M.Cinematic,"New Game must not launch the removed opening.");
            Assert.IsFalse(q.RescueOrderAccepted);Assert.NotNull(s.Commander);Assert.IsTrue(q.ObjectiveText.Contains("chỉ huy"));
            Assert.AreEqual(1,Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Count(a=>a.name=="Commander (shared player mesh)"));
            var position=M.player.position;int rounds=M.ModernCombat.RoundsInMagazine;
            Assert.IsTrue(s.TryInteract());Assert.IsTrue(d.IsPlaying);Assert.IsFalse(q.RescueOrderAccepted);
            yield return WaitGameSeconds(.8f);Assert.Less(Vector3.Distance(position,M.player.position),.001f);
            Capture("commander-conversation");
            ScreenCapture.CaptureScreenshot("Logs/Map01Story/dialogue-full-gameview.png");yield return WaitGameSeconds(.3f);
            d.Close(false);Assert.IsFalse(q.RescueOrderAccepted,"Closing a briefing cannot silently accept the order.");
            Assert.IsTrue(s.TryInteract());yield return Confirm(d);
            Assert.IsTrue(q.RescueOrderAccepted);Assert.AreEqual(Map01Quest.RescueStage,q.Stage);
            Assert.IsTrue(M.ModernPlayer.enabled&&M.ModernCombat.enabled);Assert.AreEqual(rounds,M.ModernCombat.RoundsInMagazine);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator LegacySupplyAndRadioSavesKeepProgressWithoutDuplicateRewards()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;Isolate();
            foreach(var enemy in M.Enemies)enemy.enabled=false;
            var q=M.GetComponent<Map01Quest>();var story=M.GetComponent<Map01Story>();var bag=M.GetComponent<Map01Inventory>();
            int ammo=bag.Count("ammo_rifle");
            q.RestoreStoryStage(4,new Map01Story.Snapshot{revision=2,rescueAccepted=true,supplies=true});story.AfterInventoryRestore();
            Assert.IsTrue(story.Progress.supplies);Assert.IsFalse(story.Progress.suppliesDelivered);Assert.IsTrue(story.Progress.supplyRewardGranted);
            Assert.AreEqual(1,bag.Count(Map01Story.CargoItem));Assert.AreEqual(ammo,bag.Count("ammo_rifle"));
            q.RestoreStoryStage(5,new Map01Story.Snapshot{revision=2,rescueAccepted=true,supplies=true});
            Assert.IsTrue(story.Progress.suppliesDelivered&&story.Progress.supplyReported);Assert.AreEqual(ammo,bag.Count("ammo_rifle"));
            q.RestoreStoryStage(6,new Map01Story.Snapshot{revision=2,rescueAccepted=true,supplies=true,radioDisabled=true});
            Assert.IsTrue(story.Progress.radioDisabled);Assert.AreEqual(6,q.Stage);Assert.IsNull(M.GetComponentInChildren<Map01Extraction>());
            var captured=story.Capture();story.Progress.radioResponseCalled=true;Assert.IsFalse(captured.radioResponseCalled,"Snapshots must not alias live progress.");
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator RescueApproachAndExecutionNeverCutAwayFromNam()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;Isolate();
            var q=M.GetComponent<Map01Quest>();q.RestoreStoryStage(Map01Quest.RescueStage,new Map01Story.Snapshot{rescueAccepted=true});
            var rescue=M.GetComponent<Map01Rescue>();foreach(var enemy in M.Enemies)enemy.enabled=false;
            At(rescue.RetryPoint);yield return WaitGameSeconds(.7f);
            var director=M.gameObject.AddComponent<Map01RescueCinematic>();
            var position=M.player.position;var camera=M.gameCamera.transform.position;var rotation=M.gameCamera.transform.rotation;
            Assert.IsTrue(director.ShowIntro(rescue,rescue.Squad[0]));yield return WaitGameSeconds(2);
            Assert.Less(Vector3.Distance(position,M.player.position),.001f);Assert.Less(Vector3.Distance(camera,M.gameCamera.transform.position),.001f);
            Assert.Less(Quaternion.Angle(rotation,M.gameCamera.transform.rotation),.01f);
            Assert.IsFalse(M.GetComponentsInChildren<DialogueVoice>().Any(v=>v.CurrentId=="m1_rescue_interrogate"));
            Capture("rescue-radio-Nam-view");director.Skip();yield return null;
            camera=M.gameCamera.transform.position;rotation=M.gameCamera.transform.rotation;
            rescue.ReportDetection(new Map01Detection(rescue.Squad[0],Map01DetectionCause.Sight,M.player.position));
            yield return WaitGameSeconds(2);
            Assert.AreEqual(Map01RescueCinematic.Mode.Execution,director.CurrentMode);
            Assert.Less(Vector3.Distance(camera,M.gameCamera.transform.position),.001f);Assert.Less(Quaternion.Angle(rotation,M.gameCamera.transform.rotation),.01f);
            Capture("alarm-Nam-view");director.Skip();yield return null;Assert.IsTrue(rescue.Failed);
            Assert.IsFalse(M.Cinematic);yield return new ExitPlayMode();
        }
        static IEnumerator ConfirmCommanderAtThirdPost()
        {
            var story=M.GetComponent<Map01Story>();var officer=M.GetComponent<Map01Quest>().CommanderEnemy;
            var target=officer.transform.position+Vector3.up*1.25f;bool found=false;
            for(int i=0;i<24;i++){
                var candidate=officer.transform.position+Quaternion.Euler(0,i*15,0)*Vector3.forward*6;
                if(!UnityEngine.AI.NavMesh.SamplePosition(candidate,out var sample,2,UnityEngine.AI.NavMesh.AllAreas))continue;
                var eye=sample.position+Vector3.up*1.6f;
                if(Physics.Linecast(eye,target,out var hit,M.ObstructionMask,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(officer.transform))continue;
                At(sample.position);M.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>().SetCinematicView(eye,Quaternion.LookRotation(target-eye),55,1);found=true;break;
            }
            Assert.IsTrue(found,"Point three must offer an unobstructed observation position.");
            var dialogue=M.GetComponent<Map01StoryDialogue>();float until=Time.realtimeSinceStartup+8;
            while(!dialogue.IsPlaying&&Time.realtimeSinceStartup<until)yield return null;
            Assert.IsTrue(dialogue.IsPlaying,"Approaching the actual third post identifies the living officer.");
            yield return Confirm(dialogue);
            Assert.IsTrue(story.Progress.commanderAreaReached&&story.Progress.commanderConfirmed);
        }
        static void Capture(string name,Vector3? eye=null,Vector3? target=null)
        {
            Directory.CreateDirectory("Logs/Map01Story");var cam=M.gameCamera;
            var p=cam.transform.position;var q=cam.transform.rotation;
            if(eye.HasValue){cam.transform.position=eye.Value;cam.transform.LookAt(target.Value);}
            var rt=RenderTexture.GetTemporary(1280,720,24);var old=cam.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/Map01Story/"+name+".png",image.EncodeToPNG());}
            finally{cam.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);cam.transform.SetPositionAndRotation(p,q);}
        }
    }
}
