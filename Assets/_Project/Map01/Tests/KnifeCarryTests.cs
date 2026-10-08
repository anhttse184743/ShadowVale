using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class KnifeCarryTests:ForestSceneTestBase
    {
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static Map01Rescue R=>M.GetComponent<Map01Rescue>();
        static Vector3 Lure(Map01EnemyController guard){
            for(int i=0;i<8;i++){
                var offset=Quaternion.Euler(0,i*45,0)*Vector3.forward*4.5f;
                if(!NavMesh.SamplePosition(guard.transform.position+offset,out var hit,1,NavMesh.AllAreas))continue;
                var route=new NavMeshPath();
                if(NavMesh.CalculatePath(guard.transform.position,hit.position,NavMesh.AllAreas,route)&&route.status==NavMeshPathStatus.PathComplete&&Vector3.Distance(hit.position,guard.transform.position)>3.5f)return hit.position;
            }
            Assert.Fail("No connected dry lure point near the actual guard post");return Vector3.zero;
        }
        [UnityTest]public IEnumerator StoneMakesOverseerLeaveInspectAndReturn()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            var guard=R.Squad[0];foreach(var g in M.Enemies)g.enabled=g==guard;
            var start=guard.transform.position;var facing=guard.transform.rotation;var point=Lure(guard);
            Assert.IsTrue(guard.Hear(point,14,Map01NoiseKind.Stone));Assert.IsTrue(guard.Alerted);
            float max=0;var end=Time.time+23;
            while(Time.time<end){max=Mathf.Max(max,Vector3.Distance(start,guard.transform.position));if(max>2.8f&&!guard.Alerted&&!guard.Capture().returning&&Vector3.Distance(start,guard.transform.position)<.65f)break;yield return null;}
            Assert.Greater(max,2.8f,"Must actually walk away, rather than accept the post as the noise location");
            Assert.Less(Vector3.Distance(start,guard.transform.position),.65f);Assert.Less(Quaternion.Angle(facing,guard.transform.rotation),12);
            Assert.IsFalse(R.Failed);Assert.IsTrue(R.OverseerAlive);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator StoneOpensRearKnifeApproachWithoutCancellingLure()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            var guard=R.Squad[0];foreach(var g in M.Enemies)g.enabled=g==guard;
            guard.Hear(Lure(guard),14,Map01NoiseKind.Stone);yield return WaitGameSeconds(.9f);
            var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;
            M.player.SetPositionAndRotation(guard.transform.position-guard.transform.forward*1.2f,guard.transform.rotation);cc.enabled=true;
            M.ModernPlayer.RestoreMotion(true);M.Crouched=true;M.ModernCombat.Equip(WeaponKind.Knife);Physics.SyncTransforms();
            var destination=guard.Capture().investigate;guard.Hear(M.player.position,3,Map01NoiseKind.Footstep);
            Assert.Less(Vector3.Distance(destination,guard.Capture().investigate),.001f);
            Assert.IsTrue(guard.Capture().stoneLure);Assert.IsTrue(guard.CanSilentTakedown());
            Assert.IsTrue(guard.TrySilentTakedown());Assert.IsFalse(guard.Alive);yield return WaitGameSeconds(5);
            Assert.IsFalse(R.Failed);Assert.IsFalse(M.Cinematic);yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator KnifeWalkSneakAndWeaponSwitchVisualReview()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            yield return Review();yield return new ExitPlayMode();
        }
        static IEnumerator Review()
        {
            foreach(var g in M.Enemies)g.enabled=false;
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var mode=pipeline.GetType().GetProperty("gpuResidentDrawerMode");var previous=mode.GetValue(pipeline);mode.SetValue(pipeline,Enum.ToObject(mode.PropertyType,0));
            var input=RouteInputToGame();var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();
            var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.player.position=R.Layout.safeEntry.position;cc.enabled=true;
            var basis=new GameObject("Knife review input basis").transform;basis.rotation=Quaternion.identity;
            var cameraField=typeof(PlayerController).GetField("cameraTransform",BindingFlags.Instance|BindingFlags.NonPublic);
            var oldBasis=cameraField.GetValue(M.ModernPlayer);cameraField.SetValue(M.ModernPlayer,basis);
            M.ModernPlayer.RestoreMotion(false);M.ModernCombat.Equip(WeaponKind.Knife);
            var actor=M.player.GetComponentInChildren<Animator>();foreach(var mesh in actor.GetComponentsInChildren<SkinnedMeshRenderer>())mesh.forceMatrixRecalculationPerRender=true;
            EditorSettings.asyncShaderCompilation=false;ShaderUtil.allowAsyncCompilation=false;
            Directory.CreateDirectory("Logs/KnifeCarry");
            try{
                int frame=0;
                foreach(var sneak in new[]{false,true}){
                    M.ModernPlayer.RestoreMotion(sneak);InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    for(int i=0;i<8;i++){
                        var speed=i<2?0:sneak?1.2f:2.5f;
                        // Real movement input drives speed and full-body playback; side-on camera follows.
                        InputSystem.QueueStateEvent(keyboard,speed>0?new KeyboardState(Key.W):new KeyboardState());
                        var position=M.player.position;var eye=position+Vector3.right*2.3f+Vector3.up*1.4f+Vector3.forward*2;
                        var focus=position+Vector3.up*(sneak?.8f:1f);
                        M.gameCamera.GetComponent<ThirdPersonCamera>().SetCinematicView(eye,Quaternion.LookRotation(focus-eye),46,1);
                        yield return Seconds(.18f);yield return Draw("Logs/KnifeCarry/"+(frame++).ToString("D3")+".png");
                    }
                    Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(0).IsName(sneak?"Sneak_Knife":"Locomotion_Knife"));
                    Assert.Less(actor.GetLayerWeight(actor.GetLayerIndex(PlayerCombat.UpperBodyLayer)),.03f,"No frozen attack pose over the carry");
                }
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());M.ModernPlayer.RestoreMotion(false);
                M.ModernCombat.Equip(WeaponKind.Rifle);yield return Seconds(.5f);Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(0).IsName("Locomotion_Rifle"));
                M.ModernCombat.Equip(WeaponKind.Knife);yield return Seconds(.5f);Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(0).IsName("Locomotion_Knife"));
                typeof(PlayerCombat).GetMethod("Attack",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(M.ModernCombat,null);
                yield return Seconds(.15f);Assert.Greater(actor.GetLayerWeight(actor.GetLayerIndex(PlayerCombat.UpperBodyLayer)),.5f,"Ordinary stabbing still works");
                yield return Seconds(1.8f);Assert.Less(actor.GetLayerWeight(actor.GetLayerIndex(PlayerCombat.UpperBodyLayer)),.03f);
                File.WriteAllText("Logs/KnifeCarry/review.txt","Real keyboard movement: knife walking, crouched idle/walking, switch rifle/knife and ordinary attack/recovery passed.");
            }finally{InputSystem.QueueStateEvent(keyboard,new KeyboardState());RestoreInputRouting(input);mode.SetValue(pipeline,previous);cameraField.SetValue(M.ModernPlayer,oldBasis);Object.Destroy(basis.gameObject);}
        }
        static IEnumerator Seconds(float seconds){float until=Time.time+seconds;while(Time.time<until)yield return null;}
        static IEnumerator Draw(string path){
            var pump=M.gameObject.AddComponent<ExtractionCapturePump>();bool ready=false;
            pump.draw=()=>{
                var camera=M.gameCamera;var rt=RenderTexture.GetTemporary(960,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;var pixels=new Texture2D(960,720,TextureFormat.RGB24,false);
                try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,960,720),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
                finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(pixels);}ready=true;
            };yield return new WaitUntil(()=>ready);Object.Destroy(pump);
        }
    }
}
