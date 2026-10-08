using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class KnifeWristLureTests:ForestSceneTestBase
    {
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static Map01Rescue R=>M.GetComponent<Map01Rescue>();
        [UnityTest]public IEnumerator NeutralWristsInKnifeSneakAndStanding()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            yield return WristReview();yield return new ExitPlayMode();
        }
        static IEnumerator WristReview()
        {
            foreach(var g in M.Enemies){g.enabled=false;foreach(var mesh in g.GetComponentsInChildren<Renderer>())mesh.enabled=false;}
            foreach(var mesh in M.hung.GetComponentsInChildren<Renderer>())mesh.enabled=false;
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;var mode=pipeline.GetType().GetProperty("gpuResidentDrawerMode");
            var drawer=mode.GetValue(pipeline);mode.SetValue(pipeline,Enum.ToObject(mode.PropertyType,0));
            Directory.CreateDirectory("Logs/KnifeWristLure");
            EditorSettings.asyncShaderCompilation=false;ShaderUtil.allowAsyncCompilation=false;
            var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.player.SetPositionAndRotation(R.Layout.guardPosts[0].position,Quaternion.identity);cc.enabled=true;
            M.ModernPlayer.RestoreMotion(false);M.ModernCombat.Equip(WeaponKind.Knife);
            yield return Seconds(.6f);var playerEnabled=M.ModernPlayer.enabled;M.ModernPlayer.enabled=false;
            var a=M.player.GetComponentInChildren<Animator>();foreach(var mesh in a.GetComponentsInChildren<SkinnedMeshRenderer>())mesh.forceMatrixRecalculationPerRender=true;
            var lines=new List<string>{"crouched,speed,phase,hand,wristDeviationDegrees"};float max=0;
            try{
                foreach(var stance in new[]{(false,0f),(false,2.5f),(false,6f),(true,0f),(true,1.2f)}){
                    bool crouch=stance.Item1;float speed=stance.Item2;
                    for(int i=0;i<16;i++){
                        a.SetFloat("Speed",speed);a.SetBool("Sneaking",crouch);a.Play(crouch?"Sneak_Knife":"Locomotion_Knife",0,i/16f);a.Update(0);
                        foreach(var right in new[]{false,true}){
                            var lower=a.GetBoneTransform(right?HumanBodyBones.RightLowerArm:HumanBodyBones.LeftLowerArm);
                            var hand=a.GetBoneTransform(right?HumanBodyBones.RightHand:HumanBodyBones.LeftHand);
                            var end=hand.Cast<Transform>().First(t=>t.name.IndexOf("end",StringComparison.OrdinalIgnoreCase)>=0);
                            var deviation=Vector3.Angle(hand.position-lower.position,end.position-hand.position);max=Mathf.Max(max,deviation);
                            lines.Add($"{crouch},{speed},{i},{(right?"right":"left")},{deviation:F3}");
                        }
                        if(i==4)for(int side=0;side<2;side++){
                            var focus=M.player.position+Vector3.up*.95f;var eye=focus+Vector3.forward*2.7f+Vector3.right*(side==0?1.4f:2.8f)+Vector3.up*.25f;
                            M.gameCamera.GetComponent<ThirdPersonCamera>().SetCinematicView(eye,Quaternion.LookRotation(focus-eye),44,1);
                            yield return Draw($"Logs/KnifeWristLure/knife-{(crouch?"sneak-":"")}{speed}-{side}.png");
                        }
                    }
                }
                File.WriteAllLines("Logs/KnifeWristLure/wrist-metrics.csv",lines);
                Assert.Less(max,20f,"The hand's finger axis must stay close to the forearm, not kink sideways.");
            }finally{M.ModernPlayer.enabled=playerEnabled;mode.SetValue(pipeline,drawer);}
        }
        [UnityTest]public IEnumerator RealStoneFlightAndCrouchedApproachCanFinishOverseer()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            yield return Approach();yield return new ExitPlayMode();
        }
        static IEnumerator Approach()
        {
            var guard=R.Squad[0];foreach(var g in M.Enemies)g.enabled=g==guard;
            var input=RouteInputToGame();var previousKeyboard=Keyboard.current;
            // Isolate batchmode movement from the editor/hardware keyboard; another
            // editor's focus events must not steal the synthetic WASD state.
            var kb=InputSystem.AddDevice<Keyboard>("Rescue approach keyboard");kb.MakeCurrent();
            var basis=new GameObject("Crouched approach input basis").transform;
            var field=typeof(PlayerController).GetField("cameraTransform",BindingFlags.NonPublic|BindingFlags.Instance);var previousBasis=field.GetValue(M.ModernPlayer);field.SetValue(M.ModernPlayer,basis);
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;var mode=pipeline.GetType().GetProperty("gpuResidentDrawerMode");var drawer=mode.GetValue(pipeline);mode.SetValue(pipeline,Enum.ToObject(mode.PropertyType,0));
            Directory.CreateDirectory("Logs/KnifeWristLure");
            var stone=M.GetComponent<Map01StoneThrow>();var post=guard.transform.position;var point=Vector3.zero;var start=Vector3.zero;
            var footsteps=M.player.GetComponent<PlayerFootsteps>();if(footsteps!=null)footsteps.enabled=false;
            try{
                bool found=false;
                for(int angle=0;angle<16;angle++){
                    var direction=Quaternion.Euler(0,angle*22.5f,0)*guard.transform.forward;
                    if(Vector3.Dot(direction,guard.transform.forward)<-.15f)continue;
                    if(!NavMesh.SamplePosition(post+direction*7.5f,out var hit,1.5f,NavMesh.AllAreas))continue;
                    var rear=(-direction-guard.transform.forward).normalized;
                    if(!NavMesh.SamplePosition(post+rear*2.2f,out var behind,1,NavMesh.AllAreas))continue;
                    var initial=Vector3.ProjectOnPlane(behind.position-post,Vector3.up);
                    if(Vector3.Angle(initial,guard.transform.forward)<90||Vector3.Angle(initial,direction)<90)continue;
                    var path=new NavMeshPath();if(!NavMesh.CalculatePath(behind.position,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                    var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.player.SetPositionAndRotation(behind.position,Quaternion.LookRotation(direction));cc.enabled=true;
                    M.ModernPlayer.RestoreMotion(true);M.Crouched=true;M.ModernCombat.Equip(WeaponKind.Knife);InputSystem.QueueStateEvent(kb,new KeyboardState());
                    typeof(Map01StoneThrow).GetMethod("Plan",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(stone,new object[]{hit.position});
                    if(Vector3.Distance(stone.Landing,post)<5f||Vector3.Distance(stone.Landing,post)>stone.HearingRadius)continue;
                    if(!NavMesh.SamplePosition(stone.Landing,out var dry,.8f,NavMesh.AllAreas)||Vector3.Distance(dry.position,stone.Landing)>.8f)continue;
                    point=hit.position;start=M.player.position;found=true;break;
                }
                Assert.IsTrue(found,"Need an actual clear ballistic arc and dry connected lure point.");
                yield return Seconds(.3f);Assert.IsTrue(stone.ThrowAt(point));
                float until=Time.time+8;while(guard.Capture().searchRemaining<=0&&Time.time<until&&!R.Failed)yield return null;
                Assert.IsFalse(R.Failed);Assert.Greater(guard.Capture().searchRemaining,0);Assert.Greater(Vector3.Distance(post,guard.transform.position),4f);
                if(footsteps!=null)footsteps.enabled=true;
                Assert.IsTrue(M.ModernPlayer.enabled,"The preceding pose review must restore the locomotion component.");
                Assert.IsTrue(M.ModernPlayer.InputAllowed?.Invoke()??true,$"Approach input blocked: stopped {M.Stopped}, cinematic {M.Cinematic}, paused {M.Paused}, health {M.ModernHealth.Current}, menu {ForestMenu.Visible}");
                Assert.IsTrue(kb.enabled,"Synthetic approach keyboard is disabled.");
                var away=Vector3.ProjectOnPlane(point-post,Vector3.up).normalized;
                var goal=guard.transform.position-away*1.2f;
                Assert.IsTrue(NavMesh.SamplePosition(goal,out var goalHit,1,NavMesh.AllAreas));
                var route=new NavMeshPath();Assert.IsTrue(NavMesh.CalculatePath(M.player.position,goalHit.position,NavMesh.AllAreas,route));
                int corner=1;until=Time.time+8.5f;float maxYaw=0;
                while(!guard.CanSilentTakedown()&&!R.Failed&&Time.time<until){
                    var target=route.corners[Mathf.Min(corner,route.corners.Length-1)];var delta=Vector3.ProjectOnPlane(target-M.player.position,Vector3.up);
                    if(delta.magnitude<.3f&&corner<route.corners.Length-1){corner++;continue;}
                    // NavMesh sampling can leave the last waypoint slightly outside knife reach.
                    // Finish the final half metre with real character movement toward the guard.
                    if(corner>=route.corners.Length-1&&delta.magnitude<.5f)
                        delta=Vector3.ProjectOnPlane(guard.transform.position-M.player.position,Vector3.up);
                    if(delta.sqrMagnitude>.001f)basis.rotation=Quaternion.LookRotation(delta);
                    InputSystem.QueueStateEvent(kb,new KeyboardState(Key.W));
                    InputSystem.Update();kb.MakeCurrent();
                    Assert.IsTrue(kb.wKey.isPressed,"The dedicated approach keyboard must process W before gameplay Update.");
                    maxYaw=Mathf.Max(maxYaw,Vector3.Angle(guard.transform.forward,away));
                    yield return null;
                }
                InputSystem.QueueStateEvent(kb,new KeyboardState());
                File.WriteAllText("Logs/KnifeWristLure/approach.txt",$"Player travel {Vector3.Distance(start,M.player.position):F3}m; guard travel {Vector3.Distance(post,guard.transform.position):F3}m; search yaw {maxYaw:F2}; failure {R.FailureReason}; distance {Vector3.Distance(M.player.position,guard.transform.position):F3}; player {M.player.position:F4}; guard {guard.transform.position:F4}; goal {goalHit.position:F4}");
                Assert.IsFalse(R.Failed);Assert.Less(maxYaw,12f,"Search focus must leave the rear reachable");
                Assert.Greater(Vector3.Distance(start,M.player.position),3f,"Must walk the approach, not teleport into knife range");Assert.IsTrue(guard.CanSilentTakedown());
                var focus=(M.player.position+guard.transform.position)*.5f+Vector3.up*.9f;var eye=focus+Vector3.right*3+Vector3.up*1.5f-Vector3.forward*2;
                M.gameCamera.GetComponent<ThirdPersonCamera>().SetCinematicView(eye,Quaternion.LookRotation(focus-eye),48,1);
                yield return Draw("Logs/KnifeWristLure/rear-approach.png");
                typeof(PlayerCombat).GetMethod("Attack",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(M.ModernCombat,null);
                Assert.IsFalse(guard.Alive);yield return Seconds(5);Assert.IsFalse(R.Failed);Assert.IsFalse(M.Cinematic);
            }finally{InputSystem.QueueStateEvent(kb,new KeyboardState());InputSystem.Update();InputSystem.RemoveDevice(kb);previousKeyboard?.MakeCurrent();field.SetValue(M.ModernPlayer,previousBasis);RestoreInputRouting(input);mode.SetValue(pipeline,drawer);Object.Destroy(basis.gameObject);}
        }
        static IEnumerator Seconds(float seconds){float until=Time.time+seconds;while(Time.time<until)yield return null;}
        static IEnumerator Draw(string path){
            var pump=M.gameObject.AddComponent<ExtractionCapturePump>();bool ready=false;
            pump.draw=()=>{
                var c=M.gameCamera;var rt=RenderTexture.GetTemporary(960,720,24);var old=c.targetTexture;var active=RenderTexture.active;var px=new Texture2D(960,720,TextureFormat.RGB24,false);
                try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;px.ReadPixels(new Rect(0,0,960,720),0,0);px.Apply();File.WriteAllBytes(path,px.EncodeToPNG());}
                finally{c.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(px);}ready=true;
            };yield return new WaitUntil(()=>ready);Object.Destroy(pump);
        }
    }
}
