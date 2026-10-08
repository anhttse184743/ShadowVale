using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class QuickRescueTests:ForestSceneTestBase
    {
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static Map01Rescue R=>M.GetComponent<Map01Rescue>();
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Isolate(){typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Path.GetFullPath("Logs/QuickRescue/saves/"+Guid.NewGuid().ToString("N")));}
        static void Place(Vector3 p,Quaternion q){var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.player.SetPositionAndRotation(p,q);cc.enabled=true;M.ModernPlayer.RestoreMotion(true);M.Crouched=true;Physics.SyncTransforms();}
        static Vector3 FarStone(Map01EnemyController g){
            for(int i=0;i<16;i++){
                var direction=Quaternion.Euler(0,i*22.5f,0)*g.transform.forward;
                if(!NavMesh.SamplePosition(g.transform.position+direction*11.5f,out var h,.8f,NavMesh.AllAreas)||Vector3.Distance(h.position,g.transform.position)<10)continue;
                var path=new NavMeshPath();if(NavMesh.CalculatePath(g.transform.position,h.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)return h.position;
            }Assert.Fail("No dry connected far stone destination");return Vector3.zero;
        }
        [UnityTest]public IEnumerator FarStoneAndAwayDetectionRallyEnemiesWithoutKillingHungAndSaveKeepsAlarm()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            Isolate();foreach(var g in M.Enemies)g.enabled=g==R.Squad[0];
            var overseer=R.Squad[0];var post=overseer.transform.position;float hungHP=R.HungHealth;
            Assert.IsTrue(R.CanExecuteCaptive);var rock=FarStone(overseer);Assert.IsTrue(overseer.Hear(rock,14,Map01NoiseKind.Stone));
            float departure=Time.time+4;while(Vector3.Distance(overseer.transform.position,post)<1.5f&&Time.time<departure)yield return null;
            Assert.IsFalse(R.CanExecuteCaptive,"Leaving the post must remove execution before reaching the stone.");
            float until=Time.time+10;while(Vector3.Distance(overseer.transform.position,post)<10&&Time.time<until)yield return null;
            Assert.Greater(Vector3.Distance(overseer.transform.position,post),9.5f);Assert.IsFalse(R.CanExecuteCaptive);
            R.ReportDetection(new Map01Detection(R.Squad[1],Map01DetectionCause.Sight,M.player.position));
            Assert.IsTrue(R.CombatAlarm);Assert.IsFalse(M.Cinematic);Assert.AreEqual(Map01Rescue.Phase.Captive,R.CurrentPhase);
            foreach(var g in R.Squad){Assert.IsTrue(g.Engaged);Assert.Less(Vector3.Distance(g.Capture().investigate,M.player.position),.05f);}
            Assert.AreEqual(hungHP,R.HungHealth);Assert.IsFalse(R.Failed);
            var save=M.GetComponent<Map01SaveSystem>();Assert.IsTrue(save.SaveSlot(4,out var error,true),error);
            Assert.IsTrue(R.Capture().combatAlarm);
            R.ResetSquad();Map01SaveSystem.BeginGame(4);yield return WaitGameSeconds(.9f);
            overseer=R.Squad[0];
            Assert.IsTrue(R.CombatAlarm);Assert.IsFalse(R.CanExecuteCaptive);
            Assert.AreEqual(4,R.Squad.Select(g=>g.SaveId).Distinct().Count());
            R.ResetSquad();yield return WaitGameSeconds(2.1f);
            Assert.IsTrue(overseer.AtHostagePost);Assert.IsFalse(R.CanExecuteCaptive,"Combat alarm cannot revert to remote hostage execution.");
            R.ReportDetection(new Map01Detection(overseer,Map01DetectionCause.Gunshot,M.player.position));
            yield return WaitGameSeconds(2.2f);Assert.AreEqual(hungHP,R.HungHealth);Assert.IsFalse(R.Failed);Assert.IsFalse(M.Cinematic);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator HintMatchesRearDistanceWeaponAndCooldownThenSingleNeckKillRestoresGameplay()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            Isolate();yield return Review();yield return new ExitPlayMode();
        }
        static IEnumerator Review()
        {
            SetPreviewResolution(1280,720);
            foreach(var g in M.Enemies)g.enabled=false;
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;var mode=pipeline.GetType().GetProperty("gpuResidentDrawerMode");var previous=mode.GetValue(pipeline);mode.SetValue(pipeline,Enum.ToObject(mode.PropertyType,0));
            ShaderUtil.allowAsyncCompilation=false;EditorSettings.asyncShaderCompilation=false;
            Directory.CreateDirectory("Logs/QuickRescue/frames");var guard=R.Squad[0];
            try{
                M.ModernCombat.Equip(WeaponKind.Knife);Place(guard.transform.position-guard.transform.forward*1.9f,guard.transform.rotation);
                yield return WaitGameSeconds(.2f);Assert.IsTrue(R.GetTakedownHint(out var target,out var ready));Assert.AreSame(guard,target);Assert.IsTrue(ready,"Overseer rear reach is forgiving at 1.9 m.");
                var focus=guard.transform.position+Vector3.up*1.05f;var eye=focus+guard.transform.right*3.8f-guard.transform.forward*1.1f+Vector3.up*.7f;
                M.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>().SetCinematicView(eye,Quaternion.LookRotation(focus-eye),52,1);
                yield return Capture("Logs/QuickRescue/hint-ready-body.png");
                var screenshot=ScreenCapture.CaptureScreenshotAsTexture();if(screenshot!=null){File.WriteAllBytes("Logs/QuickRescue/hint-ready-hud.png",screenshot.EncodeToPNG());Object.Destroy(screenshot);}
                M.ModernCombat.RestoreAttackCooldown(.7f);Assert.IsTrue(R.GetTakedownHint(out _,out ready));Assert.IsFalse(ready);M.ModernCombat.RestoreAttackCooldown(0);
                Place(guard.transform.position+guard.transform.forward*1.4f,guard.transform.rotation);Assert.IsTrue(R.GetTakedownHint(out _,out ready));Assert.IsFalse(ready);
                M.ModernCombat.Equip(WeaponKind.Rifle);Assert.IsFalse(R.GetTakedownHint(out _,out _));
                Place(guard.transform.position-guard.transform.forward*1.4f,guard.transform.rotation);M.ModernCombat.Equip(WeaponKind.Knife);
                float start=Time.time;typeof(PlayerCombat).GetMethod("Attack",Private).Invoke(M.ModernCombat,null);
                Assert.IsFalse(guard.Alive);Assert.IsTrue(M.Cinematic);Assert.IsFalse(R.GetTakedownHint(out _,out _));
                var rows=new List<string>{"frame,time,handToNeck"};float minimum=float.PositiveInfinity;int index=0;Time.captureDeltaTime=1/60f;
                while(Time.time-start<2.6f){
                    var nam=M.player.GetComponentInChildren<Animator>();var victim=guard.GetComponentInChildren<Animator>();
                    float d=Vector3.Distance(nam.GetBoneTransform(HumanBodyBones.RightHand).position,victim.GetBoneTransform(HumanBodyBones.Neck).position);
                    if(Time.time-start>.45f&&Time.time-start<.95f)minimum=Mathf.Min(minimum,d);
                    rows.Add($"{index},{Time.time-start:F4},{d:F4}");yield return Capture($"Logs/QuickRescue/frames/{index++:D4}.png");
                }
                Time.captureDeltaTime=0;File.WriteAllLines("Logs/QuickRescue/contact.csv",rows);
                Assert.Less(minimum,.4f,"The single strike must contact the neck.");
                Assert.IsFalse(Map01NamActions.For(M).Busy);Assert.IsFalse(M.Cinematic);Assert.IsTrue(M.ModernPlayer.enabled&&M.ModernCombat.enabled);
                Assert.IsFalse(guard.TrySilentTakedown());Assert.IsFalse(R.OverseerAlive);Assert.IsFalse(R.Failed);
                File.WriteAllText("Logs/QuickRescue/review.txt",$"Single neck pair duration {Map01NamActions.TakedownSeconds:F2}s; closest wrist to neck {minimum:F3}m; camera and controls restored; icon readiness matches rear/range/weapon/cooldown.");
            }finally{Time.captureDeltaTime=0;mode.SetValue(pipeline,previous);}
        }
        static IEnumerator Capture(string path){
            var pump=M.gameObject.AddComponent<ExtractionCapturePump>();bool ready=false;
            pump.draw=()=>{var camera=M.gameCamera;var rt=RenderTexture.GetTemporary(1280,720,24);var old=camera.targetTexture;var active=RenderTexture.active;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
                try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
                finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(pixels);}ready=true;};
            yield return new WaitUntil(()=>ready);Object.Destroy(pump);
        }
    }
}
