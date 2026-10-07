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
    // Scripted camera review: real paired clips, follower, spawns, hitscan and shelter sequence.
    // Guard selection/approach is arranged by the fixture; detection is tested separately.
    public sealed class RescueVisualTests:ForestSceneTestBase
    {
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static Map01Rescue R=>M.GetComponent<Map01Rescue>();
        static Map01Quest Q=>M.GetComponent<Map01Quest>();
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly FieldInfo Storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
        static readonly MethodInfo Attack=typeof(PlayerCombat).GetMethod("Attack",Private);
        static readonly MethodInfo Aim=typeof(PlayerCombat).GetMethod("SetAiming",Private);
        static Recorder recorder;
        static void Place(Vector3 p,Quaternion rotation)
        {
            var cc=M.player.GetComponent<CharacterController>();bool enabled=cc.enabled;cc.enabled=false;
            M.player.SetPositionAndRotation(p,rotation);cc.enabled=enabled;M.ModernPlayer.RestoreMotion(true);Physics.SyncTransforms();
        }
        [UnityTest,Timeout(900000)]public IEnumerator RecordRescueAndRealEscortAndNaturalShelterReturn()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/review-saves/"+Guid.NewGuid().ToString("N")));
            EditorSettings.asyncShaderCompilation=false;ShaderUtil.allowAsyncCompilation=false;
            foreach(var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))skin.forceMatrixRecalculationPerRender=true;
            recorder=new Recorder(M);M.ModernHealth.CinematicInvulnerable=true;
            var inputRouting=RouteInputToGame();var mouse=InputSystem.AddDevice<Mouse>();
            foreach(var guard in M.Enemies)guard.enabled=false;
            for(int i=0;i<4;i++)
            {
                var guard=R.Squad[i];guard.enabled=true;yield return null;guard.ReturnToPost();guard.enabled=false;
                guard.GetComponent<NavMeshAgent>().ResetPath();guard.GetComponent<NavMeshAgent>().isStopped=true;
                var p=guard.transform.position-guard.transform.forward*1.2f;
                if(NavMesh.SamplePosition(p,out var hit,1,NavMesh.AllAreas))p=hit.position;
                Place(p,guard.transform.rotation);M.ModernCombat.Equip(WeaponKind.Knife);
                var rig=M.gameCamera.GetComponent<ThirdPersonCamera>();
                var focus=guard.transform.position+Vector3.up;
                rig.SetCinematicView(focus-guard.transform.forward*3.2f+guard.transform.right*3+Vector3.up*1.5f,
                    Quaternion.LookRotation(guard.transform.forward*3.2f-guard.transform.right*3-Vector3.up*1.5f),54,1);
                yield return recorder.Seconds(.25f,"Approach guard "+i);
                rig.ClearCinematicView();Assert.IsTrue(guard.CanSilentTakedown(),$"Guard {i}: distance {Vector3.Distance(M.player.position,guard.transform.position)}, angle {Vector3.Angle(guard.transform.forward,M.player.position-guard.transform.position)}, engaged {guard.Engaged}");Attack.Invoke(M.ModernCombat,null);
                Assert.IsTrue(guard.TakenDownSilently);
                yield return recorder.Seconds(5.15f,"Silent takedown "+i);
            }
            Assert.AreEqual(0,R.Remaining);Place(M.hung.position-M.hung.forward*.7f,M.hung.rotation);
            Q.TryRescueHung();yield return recorder.Seconds(13,"Untie and stand");Assert.AreEqual(Map01Quest.EscortStage,Q.Stage);
            M.ModernCombat.Equip(WeaponKind.Rifle);M.GetComponent<Map01Inventory>().Add("ammo_rifle",240);
            M.ModernPlayer.enabled=false;M.Crouched=false;
            var path=Map01Rescue.Path(M.player.position,R.Layout.shelterDoor.position);int segment=1;
            var actor=M.player.GetComponentInChildren<Animator>();var cameraRig=M.gameCamera.GetComponent<ThirdPersonCamera>();
            float limit=Time.time+220,lastShot=0;
            while(Q.Stage==Map01Quest.EscortStage&&Time.time<limit)
            {
                if(M.Cinematic){
                    yield return recorder.Frame("Shelter return");
                    var home=M.GetComponent<Map01RescueCinematic>();
                    if(home.CurrentMode==Map01RescueCinematic.Mode.Shelter&&home.Elapsed>6){
                        Assert.Greater(Vector3.Distance(M.gameCamera.transform.position,M.player.position+Vector3.up),1.2f,"Camera must not be pushed inside Nam.");
                        Assert.Greater(Vector3.Distance(M.gameCamera.transform.position,M.hung.position+Vector3.up),1.2f,"Camera must not be pushed inside Hung.");
                    }
                    continue;
                }
                var target=R.Pursuers.Where(g=>g!=null&&g.Alive).OrderBy(g=>Vector3.Distance(g.transform.position,M.player.position)).FirstOrDefault();
                bool combat=target!=null&&Vector3.Distance(target.transform.position,M.player.position)<16;
                if(combat)
                {
                    InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));InputSystem.Update();
                    actor.SetFloat("Speed",0);actor.SetFloat("MoveZ",0);Aim.Invoke(M.ModernCombat,new object[]{true});
                    var focus=target.transform.position+Vector3.up*1.15f;
                    var eye=M.player.position+Vector3.up*1.45f-M.player.forward*.5f+M.player.right*.35f;
                    cameraRig.SetCinematicView(eye,Quaternion.LookRotation(focus-eye),58,1);
                    M.gameCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));
                    M.player.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(focus-M.player.position,Vector3.up));
                    if(Time.time>=lastShot+.2f){Attack.Invoke(M.ModernCombat,null);lastShot=Time.time;}
                    if(M.ModernCombat.RoundsInMagazine==0)M.ModernCombat.TryReload();
                }
                else
                {
                    InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();
                    Aim.Invoke(M.ModernCombat,new object[]{false});cameraRig.ClearCinematicView();
                    if(segment<path.Length)
                    {
                        var delta=path[segment]-M.player.position;var step=Vector3.ClampMagnitude(delta,Time.deltaTime*1.8f);
                        if(Vector3.Distance(M.player.position,M.hung.position)<5)M.player.position+=step;
                        if(delta.magnitude<.2f)segment++;
                        var direction=Vector3.ProjectOnPlane(delta,Vector3.up);
                        if(direction.sqrMagnitude>.001f)M.player.rotation=Quaternion.RotateTowards(M.player.rotation,Quaternion.LookRotation(direction),Time.deltaTime*260);
                        actor.SetFloat("Speed",step.magnitude/Mathf.Max(.001f,Time.deltaTime));actor.SetFloat("MoveZ",1);actor.SetFloat("MoveX",0);actor.SetBool("Sneaking",false);actor.SetBool("Grounded",true);
                    }
                    else{actor.SetFloat("Speed",0);actor.SetFloat("MoveZ",0);}
                }
                yield return recorder.Frame(combat?"Cover Hung":"Escort home");
                Assert.IsFalse(R.Failed,"Scripted escort must reach shelter alive.");
            }
            Assert.AreEqual(Map01Quest.BriefingStage,Q.Stage);Assert.AreEqual(7,R.WaveMask);
            Assert.IsTrue(R.RewardDelivered);Assert.Less(Vector3.Distance(M.player.position,R.Layout.reportPoint.position),.15f);
            M.ModernPlayer.enabled=true;yield return recorder.Seconds(1.5f,"Delivered at base");recorder.Dispose();recorder=null;
            InputSystem.RemoveDevice(mouse);RestoreInputRouting(inputRouting);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator HeldEscapeShelterMatchesNaturalOutcomeAndPreservesHeldAim()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/skip-saves/"+Guid.NewGuid().ToString("N")));
            Q.RestoreStage(Map01Quest.EscortStage);
            M.hung.GetComponent<NavMeshAgent>().Warp(R.Layout.shelterDoor.position);
            Place(R.Layout.shelterDoor.position+Vector3.left*.7f,Quaternion.identity);
            yield return WaitGameSeconds(.6f);Assert.IsTrue(M.Cinematic);
            var routing=RouteInputToGame();var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));InputSystem.Update();
            yield return WaitGameSeconds(1.15f);
            Assert.AreEqual(Map01Quest.BriefingStage,Q.Stage);Assert.IsTrue(R.RewardDelivered);Assert.IsFalse(M.Cinematic);
            Assert.IsTrue(M.ModernCombat.IsAiming);Assert.IsFalse(R.CompleteDelivery());
            InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing);
            yield return new ExitPlayMode();
        }
        sealed class Recorder:IDisposable
        {
            readonly Map01Mission owner;readonly ExtractionCapturePump pump;
            readonly RenderTexture texture;readonly Texture2D pixels;readonly StreamWriter events,metrics;
            readonly float start;int frame;float nextCapture;bool disposed;
            public Recorder(Map01Mission owner)
            {
                this.owner=owner;pump=owner.gameObject.AddComponent<ExtractionCapturePump>();start=Time.time;
                Directory.CreateDirectory("Logs/Rescue/frames");Directory.CreateDirectory("Logs/Rescue/close");
                texture=new RenderTexture(960,540,24);pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
                events=new StreamWriter("Logs/Rescue/frames.csv");events.WriteLine("frame,time,phase,label,remaining,waves");
                metrics=new StreamWriter("Logs/Rescue/pose-metrics.csv");metrics.WriteLine("frame,time,phase,actor,headY,leftKneeY,rightKneeY,sole,ground");
            }
            public IEnumerator Frame(string label)
            {
                Time.captureDeltaTime=owner.Cinematic?1f/30f:0;
                if(Time.time<nextCapture){yield return null;yield break;}nextCapture=Time.time+1f/15;
                bool done=false;pump.draw=()=>
                {
                    float time=Time.time-start;var phase=owner.GetComponent<Map01Rescue>().CurrentPhase;
                    Capture(owner.gameCamera,"Logs/Rescue/frames/"+frame.ToString("D5")+".png");
                    if(frame%3==0&&(owner.Cinematic||phase==Map01Rescue.Phase.Escorting&&frame%30==0))
                    {
                        var camera=owner.gameCamera;var pos=camera.transform.position;var rot=camera.transform.rotation;float fov=camera.fieldOfView;
                        var focus=Vector3.Lerp(owner.player.position,owner.hung.position,phase==Map01Rescue.Phase.Captive?0:.5f)+Vector3.up*.9f;
                        var eye=focus+owner.player.right*3.8f-owner.player.forward*1.6f+Vector3.up*.7f;
                        camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));camera.fieldOfView=49;
                        Capture(camera,"Logs/Rescue/close/"+frame.ToString("D5")+".png");
                        camera.transform.SetPositionAndRotation(pos,rot);camera.fieldOfView=fov;
                    }
                    foreach(var root in new[]{owner.player,owner.hung})
                    {
                        var actor=root.GetComponentInChildren<Animator>();float sole=float.PositiveInfinity;var mesh=new Mesh();
                        foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh);foreach(var v in mesh.vertices)sole=Mathf.Min(sole,skin.transform.TransformPoint(v).y);}Object.Destroy(mesh);
                        metrics.WriteLine($"{frame},{time},{phase},{root.name},{actor.GetBoneTransform(HumanBodyBones.Head).position.y},{actor.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position.y},{actor.GetBoneTransform(HumanBodyBones.RightLowerLeg).position.y},{sole},{root.position.y}");
                    }
                    events.WriteLine($"{frame},{time},{phase},{label},{owner.GetComponent<Map01Rescue>().Remaining},{owner.GetComponent<Map01Rescue>().WaveMask}");events.Flush();metrics.Flush();frame++;done=true;
                };
                yield return new WaitUntil(()=>done);
            }
            public IEnumerator Seconds(float seconds,string label){float until=Time.time+seconds;while(Time.time<until)yield return Frame(label);}
            void Capture(Camera camera,string path)
            {
                var old=camera.targetTexture;var active=RenderTexture.active;
                try{camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
                finally{camera.targetTexture=old;RenderTexture.active=active;}
            }
            public void Dispose(){if(disposed)return;disposed=true;Time.captureDeltaTime=0;events.Dispose();metrics.Dispose();if(Application.isPlaying){Object.Destroy(pump);Object.Destroy(texture);Object.Destroy(pixels);}else{Object.DestroyImmediate(pump);Object.DestroyImmediate(texture);Object.DestroyImmediate(pixels);}}
        }
        [TearDown]public void Cleanup(){recorder?.Dispose();recorder=null;Storage.SetValue(null,null);}
    }
}
