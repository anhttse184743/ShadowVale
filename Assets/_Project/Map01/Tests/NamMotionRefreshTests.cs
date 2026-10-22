using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class NamMotionRefreshTests:ForestSceneTestBase
    {
        const string Work="Docs/Reviews/AnimationRefresh_Unity_20261022";
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static NamMotionRefresh Motion=>M.player.GetComponent<NamMotionRefresh>();
        static Keyboard keys;static bool space,forward;static GameObject platform;
        static Mouse throwMouse;static bool mouseDown;
        static void PumpThrow(){if(throwMouse==null)return;throwMouse.MakeCurrent();InputSystem.QueueStateEvent(throwMouse,mouseDown?new MouseState().WithButton(MouseButton.Left):new MouseState());}
        static void Pump(){if(keys==null)return;keys.MakeCurrent();InputSystem.QueueStateEvent(keys,space?new KeyboardState(Key.Space):forward?new KeyboardState(Key.W):new KeyboardState());}
        [SetUp]public void SaveIsolation()=>SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath(Work+"/TestSaves"));
        [TearDown]public void ReleaseInput(){InputSystem.onBeforeUpdate-=Pump;if(keys!=null&&keys.added)InputSystem.RemoveDevice(keys);keys=null;space=forward=false;SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);}
        [Test]public void ApprovedAssetHasAllFortySevenClipsAndExistingSkeleton()
        {
            var d=Resources.Load<NamMotionData>("Characters/NamMotionV3");Assert.NotNull(d);
            Assert.AreEqual("77727ffeab59bf989dfd9fdd568fbe859ce35541ae6df2a92e6341a32c59ac2c",d.sourceHash);
            Assert.AreEqual(47,d.clips.Length);Assert.AreEqual(47,d.clips.Select(c=>c.id).Distinct().Count());Assert.AreEqual(62,d.bones.Length);Assert.AreEqual(3208,d.frames);Assert.AreEqual(60,d.fps);
            Assert.IsTrue(d.rootFlightRemoved&&d.rootXZRemoved);
            Assert.IsTrue(d.bones.Any(t=>t.name=="LeftForeArmTwist2")&&d.bones.Any(t=>t.name=="RightHandThumb3"));
            foreach(var t in d.bones){Assert.AreEqual(d.frames,t.poses.Length);foreach(var p in t.poses){Assert.IsFalse(float.IsNaN(p.position.x)||float.IsInfinity(p.position.y));Assert.That(Quaternion.Dot(p.rotation,p.rotation),Is.InRange(.98f,1.02f));}}
            Assert.AreEqual(5,d.props.Length);
            foreach(var p in d.props){Assert.NotNull(p.prefab);Assert.IsNotEmpty(p.parts);foreach(var part in p.parts){Assert.AreEqual(3208,part.track.poses.Length);Assert.AreEqual(3208,part.visible.Length);}}
        }
        [Test]public void JumpControllerExitsOnActualGroundedAndPreservesStoryStates()
        {
            var c=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller");
            var states=c.layers[0].stateMachine.states.Select(s=>s.state).ToArray();var jump=states.Single(s=>s.name=="Jump");
            Assert.IsTrue(jump.transitions.All(t=>!t.hasExitTime&&t.conditions.Any(x=>x.parameter=="Grounded"&&x.mode==AnimatorConditionMode.If)));
            Assert.IsTrue(states.Any(s=>s.name=="Act_Takedown"));Assert.IsTrue(c.layers.Any(l=>l.name=="ActionsUpper"));Assert.IsTrue(c.layers.Any(l=>l.name==PlayerCombat.UpperBodyLayer));
        }
        [Test]public void ExportedJumpHasNoSecondBallisticArcAndPropsKeepTheirSize()
        {
            var d=Resources.Load<NamMotionData>("Characters/NamMotionV3");var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));
            try
            {
                foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;actor.GetComponentInChildren<Animator>().enabled=false;
                var map=actor.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                Assert.IsTrue(NamMotionData.FitsSkeleton(actor.GetComponentInChildren<Animator>().transform,d.bones));
                var hung=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerHung.prefab"));
                try
                {
                    var rig=hung.GetComponentInChildren<Animator>().transform;
                    Assert.IsFalse(NamMotionData.FitsSkeleton(rig,d.bones));
                    Assert.IsFalse(NamMotionData.FitsSkeleton(rig,Resources.Load<Map01PistolAnimationData>("Weapons/K54V12").bones));
                    Assert.IsFalse(NamMotionData.FitsSkeleton(rig,Resources.Load<Map01PistolAnimationData>("Weapons/MosinV13").bones));
                }
                finally{Object.DestroyImmediate(hung);}
                void Sample(float f){foreach(var track in d.bones)track.At(f).Apply(map[track.name]);}
                Sample(1);float standing=map["Hips"].position.y;
                foreach(var c in d.clips.Where(c=>c.id.StartsWith("jump_")))
                {
                    Sample(c.start+30);Assert.Less(Mathf.Abs(map["Hips"].position.y-standing),.35f,c.id+" must not contain the 1.4 m physics arc");
                }
                foreach(var p in d.props)
                {
                    var c=d.clips.First(c=>c.prop==p.key);Sample(c.start);
                    var go=Object.Instantiate(p.prefab,map["RightHand"],false);go.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=Vector3.one;
                    try
                    {
                        foreach(var part in p.parts)part.track.At(c.start).Apply(go.transform.Find(part.track.name));
                        var renderers=go.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                        Assert.That(bounds.size.magnitude,Is.InRange(.04f,.55f),p.key+" prop scale");Assert.Less(Vector3.Distance(bounds.center,map["RightHand"].position),.5f,p.key+" grip");
                    }
                    finally{Object.DestroyImmediate(go);}
                }
            }
            finally{Object.DestroyImmediate(actor);}
        }
        static void Quiet()
        {
            if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
            foreach(var e in M.Enemies){e.enabled=false;var a=e.GetComponent<NavMeshAgent>();if(a!=null)a.enabled=false;}
            M.ModernHealth.CinematicInvulnerable=true;M.GetComponent<Map01Rescue>().MarkIntroSeen();M.Cinematic=false;
            Assert.NotNull(Motion);Assert.IsTrue(M.GetComponent<Map01SaveSystem>().IsSandbox);
        }
        static void Floor()
        {
            var at=M.player.position;platform=GameObject.CreatePrimitive(PrimitiveType.Cube);platform.name="Nam v3 isolated motion platform";
            platform.transform.position=at+Vector3.up*11.9f;platform.transform.localScale=new Vector3(16,.2f,16);
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.24f,.28f,.25f)};platform.GetComponent<Renderer>().material=m;
            PlaceNam(M,at+Vector3.up*12.08f);
        }
        static void Photo(string name)
        {
            Directory.CreateDirectory(Work);
            var rig=M.player.GetComponentInChildren<Animator>();var hand=rig.GetBoneTransform(HumanBodyBones.RightHand);var foot=rig.GetBoneTransform(HumanBodyBones.RightFoot);
            File.WriteAllText(Work+"/"+name+".txt",$"motion={Motion.MotionId} frame={Motion.SourceFrame} root={M.player.position} hand={hand.position} foot={foot.position} modelLocal={rig.transform.localPosition} grounded={M.ModernPlayer.IsGrounded}");
            var camera=new GameObject("Nam v3 live verification camera").AddComponent<Camera>();camera.CopyFrom(M.gameCamera);camera.tag="Untagged";
            var at=M.player.position+Vector3.up;camera.transform.position=M.player.position+M.player.forward*3.8f+M.player.right*2f+Vector3.up*1.7f;
            camera.transform.LookAt(at);camera.fieldOfView=39;camera.nearClipPlane=.03f;
            var rt=RenderTexture.GetTemporary(1920,1080,24);var prior=RenderTexture.active;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();Directory.CreateDirectory(Work);File.WriteAllBytes(Work+"/"+name+".png",tex.EncodeToPNG());}
            finally{RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);Object.Destroy(camera.gameObject);}
        }
        [UnityTest]public IEnumerator KnifeDrawAttackHolsterAndPistolReloadUseV3WithoutChangingAmmo()
        {
            K54IntegrationTests.OpenSandbox();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Floor();yield return WaitGameSeconds(.5f);
            var bag=M.GetComponent<Map01Inventory>();Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Knife));yield return WaitGameSeconds(.35f);
            Assert.AreEqual("knife_draw",Motion.MotionId);Photo("01-knife-draw");yield return WaitGameSeconds(.8f);
            Assert.AreEqual("knife_idle",Motion.MotionId);Photo("02-knife-idle");
            M.ModernCombat.TrySpecialMelee=null;typeof(PlayerCombat).GetMethod("Attack",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(M.ModernCombat,null);yield return WaitGameSeconds(.25f);
            Assert.AreEqual("knife_attack",Motion.MotionId);Photo("03-knife-attack");
            Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Pistol));yield return WaitGameSeconds(.25f);Assert.AreEqual("knife_holster",Motion.MotionId);Photo("04-knife-holster");
            yield return WaitGameSeconds(1);Assert.That(Motion.MotionId,Does.StartWith("pistol"));Photo("05-pistol-carry");
            M.ModernCombat.RestoreMagazine(3);int count=bag.Count("ammo_pistol");Assert.IsTrue(M.ModernCombat.TryReload());yield return WaitGameSeconds(.7f);
            Assert.AreEqual("pistol_reload",Motion.MotionId);Assert.AreEqual(3,M.ModernCombat.RoundsInMagazine);Photo("06-pistol-reload");
            yield return WaitGameSeconds(1.3f);Assert.AreEqual(8,M.ModernCombat.RoundsInMagazine);Assert.AreEqual(count-5,bag.Count("ammo_pistol"));
            M.Cinematic=true;yield return null;yield return null;Assert.AreEqual(0,Motion.AppliedWeight);Assert.IsNull(Motion.ActiveProp);
            M.Cinematic=false;yield return WaitGameSeconds(.25f);Assert.Greater(Motion.AppliedWeight,.9f);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator ThrowCancelReleaseAndCrouchUseApprovedPropsAndReleaseOnce()
        {
            K54IntegrationTests.OpenSandbox();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Floor();yield return WaitGameSeconds(.5f);
            var bag=M.GetComponent<Map01Inventory>();bag.Add("grenade_f1",2);bag.SelectThrowable("grenade_f1");yield return WaitGameSeconds(.2f);
            Assert.AreEqual("f1_prepare",Motion.MotionId);Assert.AreEqual("F1",Motion.ActiveProp);Photo("07-f1-carry");
            var routing=RouteInputToGame();var physical=InputSystem.devices.OfType<Mouse>().Where(m=>m.enabled).ToArray();int beforeAim=bag.Count("grenade_f1");
            foreach(var device in physical)InputSystem.DisableDevice(device);throwMouse=InputSystem.AddDevice<Mouse>();mouseDown=true;InputSystem.onBeforeUpdate+=PumpThrow;
            try
            {
                yield return WaitGameSeconds(.05f);M.GetComponent<Map01StoneThrow>().BeginAim();yield return WaitGameSeconds(1.3f);
                Assert.AreEqual("f1_wind",Motion.MotionId);Photo("14-f1-pin");yield return WaitGameSeconds(.6f);Assert.AreEqual("f1_aim",Motion.MotionId);Photo("15-f1-aim");
                M.GetComponent<Map01StoneThrow>().Cancel();mouseDown=false;yield return WaitGameSeconds(.2f);Assert.AreEqual("f1_prepare",Motion.MotionId);Assert.AreEqual(beforeAim,bag.Count("grenade_f1"));
            }
            finally{InputSystem.onBeforeUpdate-=PumpThrow;InputSystem.RemoveDevice(throwMouse);throwMouse=null;mouseDown=false;foreach(var device in physical)if(device.added)InputSystem.EnableDevice(device);RestoreInputRouting(routing);}
            // ThrowAt drives the same real launch/timer path without requiring a held physical mouse.
            int count=bag.Count("grenade_f1");Assert.IsTrue(M.GetComponent<Map01StoneThrow>().ThrowAt(M.player.position+M.player.forward*9));
            yield return WaitGameSeconds(.06f);Assert.AreEqual("f1_throw",Motion.MotionId);
            yield return WaitGameSeconds(.28f);var held=M.player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="grenade_f1 in hand");
            Assert.IsFalse(held.gameObject.activeSelf);Assert.AreEqual(count-1,bag.Count("grenade_f1"));Photo("08-f1-release");
            yield return WaitGameSeconds(.95f);Assert.IsTrue(held.gameObject.activeSelf);
            M.ModernPlayer.RestoreMotion(true);bag.Add("smoke_bomb",1);bag.SelectThrowable("smoke_bomb");yield return WaitGameSeconds(.25f);
            Assert.AreEqual("smoke_crouch_prepare",Motion.MotionId);Assert.AreEqual("Smoke",Motion.ActiveProp);Photo("09-smoke-crouch");
            Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Primary));yield return WaitGameSeconds(.2f);Assert.IsNull(Motion.ActiveProp);
            bag.Add("stone",2);bag.SelectThrowable("stone");yield return WaitGameSeconds(.2f);
            Assert.AreEqual("stone_crouch_prepare",Motion.MotionId);Assert.AreEqual("Stone",Motion.ActiveProp);Photo("12-stone-crouch");
            int stones=bag.Count("stone");Assert.IsTrue(M.GetComponent<Map01StoneThrow>().ThrowAt(M.player.position+M.player.forward*8));yield return WaitGameSeconds(.35f);
            Assert.AreEqual("stone_crouch_throw",Motion.MotionId);Assert.AreEqual(stones-1,bag.Count("stone"));
            Assert.IsFalse(M.player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="stone in hand").gameObject.activeSelf);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator ActualJumpUsesPhysicsAndWaitsForLandingAfterTheNominalFlight()
        {
            K54IntegrationTests.OpenSandbox();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Floor();yield return WaitGameSeconds(.6f);
            Assert.IsTrue(M.GetComponent<Map01Inventory>().Draw(Map01Inventory.Slot.Pistol));yield return WaitGameSeconds(.3f);
            var routing=RouteInputToGame();keys=InputSystem.AddDevice<Keyboard>();InputSystem.onBeforeUpdate+=Pump;
            float oldCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60;
            try
            {
                uint before=M.ModernPlayer.JumpSequence;float baseY=M.player.position.y;
                space=true;yield return WaitGameSeconds(.1f);space=false;
                Assert.Greater(M.ModernPlayer.JumpSequence,before);Assert.IsFalse(M.ModernPlayer.IsGrounded);Assert.IsTrue(Motion.JumpPlaying);
                yield return WaitGameSeconds(.23f);Assert.AreEqual("jump_pistol",Motion.MotionId);float height=M.player.position.y-baseY;Assert.That(height,Is.InRange(.9f,1.6f));Photo("10-jump-pistol");
                platform.SetActive(false);yield return WaitGameSeconds(.48f);
                Assert.IsFalse(M.ModernPlayer.IsGrounded,"The lower map floor extends the real airborne phase");Assert.IsTrue(Motion.JumpPlaying);Assert.AreEqual("jump_pistol",Motion.MotionId);
                float timeout=Time.time+4;while(!M.ModernPlayer.IsGrounded&&Time.time<timeout)yield return null;
                Assert.IsTrue(M.ModernPlayer.IsGrounded);Photo("11-land-pistol");yield return WaitGameSeconds(.65f);Assert.IsFalse(Motion.JumpPlaying);
                Assert.That(Motion.MotionId,Does.StartWith("pistol"));Assert.AreEqual(1.4f,M.ModernPlayer.JumpHeight);Assert.AreEqual(-20,M.ModernPlayer.Gravity);
            }
            finally{Time.captureDeltaTime=oldCapture;InputSystem.onBeforeUpdate-=Pump;InputSystem.RemoveDevice(keys);keys=null;space=false;RestoreInputRouting(routing);}
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator SlopeJumpWeaponSwapsAndPausePreserveTheAirbornePhase()
        {
            K54IntegrationTests.OpenSandbox();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Floor();
            platform.transform.rotation=Quaternion.Euler(0,0,18);PlaceNam(M,platform.transform.position+Vector3.up);yield return WaitGameSeconds(.8f);
            var bag=M.GetComponent<Map01Inventory>();Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Knife));yield return WaitGameSeconds(1.2f);Assert.IsTrue(M.ModernPlayer.IsGrounded);
            var routing=RouteInputToGame();keys=InputSystem.AddDevice<Keyboard>();InputSystem.onBeforeUpdate+=Pump;
            try
            {
                space=true;yield return WaitGameSeconds(.1f);space=false;Assert.AreEqual("jump_knife",Motion.MotionId);
                M.SetPaused(true);float frozen=Motion.SourceFrame;for(int i=0;i<6;i++)yield return null;
                Assert.IsTrue(Motion.JumpPlaying);Assert.AreEqual("jump_knife",Motion.MotionId);Assert.AreEqual(frozen,Motion.SourceFrame,.001f);Assert.AreEqual("Knife",Motion.ActiveProp);
                M.SetPaused(false);Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Pistol));yield return WaitGameSeconds(.1f);Assert.AreEqual("jump_pistol",Motion.MotionId);
                bag.Add("grenade_f1",1);bag.SelectThrowable("grenade_f1");yield return WaitGameSeconds(.1f);Assert.AreEqual("jump_f1",Motion.MotionId);Assert.AreEqual("F1",Motion.ActiveProp);Photo("13-slope-jump-f1");
                yield return WaitGameSeconds(1.2f);Assert.IsTrue(M.ModernPlayer.IsGrounded);Assert.IsFalse(Motion.JumpPlaying);Assert.AreEqual("f1_prepare",Motion.MotionId);
            }
            finally{M.SetPaused(false);InputSystem.onBeforeUpdate-=Pump;InputSystem.RemoveDevice(keys);keys=null;space=false;RestoreInputRouting(routing);}
            yield return new ExitPlayMode();
        }
    }
}
