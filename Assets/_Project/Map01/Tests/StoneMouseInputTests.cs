using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class StoneMouseInputTests : ForestSceneTestBase
    {
        static readonly System.Collections.Generic.List<InputDevice> isolatedMice=new();
        static void IsolateMice(Mouse mouse)
        {
            foreach(var device in InputSystem.devices.OfType<Mouse>().Where(d=>d!=mouse && d.enabled).ToArray()) {
                isolatedMice.Add(device);InputSystem.DisableDevice(device);
            }
        }
        [TearDown] public void RestoreMice()
        {
            foreach(var device in isolatedMice)if(device!=null&&device.added)InputSystem.EnableDevice(device);
            isolatedMice.Clear();
        }
        static IEnumerator KeyPress(Keyboard keyboard,Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        }
        static IEnumerator MouseButtons(Mouse mouse,bool left,bool right=false)
        {
            var state=new MouseState();if(left)state=state.WithButton(MouseButton.Left);if(right)state=state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse,state);yield return null;yield return null;
        }
        // The item in Nam's hand, by whichever prop shows it: NamMotionRefresh ("stone in hand"), Map01NamActions ("Stone in hand"), Map01ThrowableVisual.
        static bool InHand(Transform t,string item)=>string.Equals(t.name,item+" in hand",System.StringComparison.OrdinalIgnoreCase)||t.name=="Throwable in hand — "+item;
        static void CaptureHand(Transform player,string name)
        {
            var actor=player.GetComponentInChildren<Animator>();
            var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
            var go=new GameObject("Stone hand verification camera");var camera=go.AddComponent<Camera>();
            var texture=RenderTexture.GetTemporary(512,512,24);var image=new Texture2D(512,512,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try {
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.17f);
                camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.orthographic=true;camera.orthographicSize=.18f;
                var focus=hand.position+player.up*.025f;
                camera.transform.position=focus+player.forward*.35f+player.right*.45f+player.up*.1f;
                camera.transform.LookAt(focus);camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();
                WriteReviewFile("Tools/StoneInputReports/"+name+".png",image.EncodeToPNG());
                focus=player.position+Vector3.up;camera.orthographicSize=1.1f;
                camera.transform.position=focus+player.forward*3+player.right*2+Vector3.up*.4f;camera.transform.LookAt(focus);
                camera.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();
                WriteReviewFile("Tools/StoneInputReports/"+name+"-full.png",image.EncodeToPNG());
            } finally {RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(texture);Object.Destroy(go);Object.Destroy(image);}
        }
        [UnityTest]
        public IEnumerator SlotFourAndMouseThrowWithoutFiringOrLosingCancelledStones()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;
            var routing=RouteInputToGame();var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            IsolateMice(mouse);
            try
            {
                InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                var mission=Object.FindFirstObjectByType<Map01Mission>();foreach(var enemy in mission.Enemies)enemy.enabled=false;
                mission.GetComponent<Map01Rescue>().MarkIntroSeen();
                var inventory=mission.GetComponent<Map01Inventory>();var stones=mission.GetComponent<Map01StoneThrow>();
                Assert.IsTrue(mission.ModernCombat.Equip(WeaponKind.Rifle));
                inventory.Add("stone",3);inventory.Add("smoke_bomb",1);
                int before=inventory.Stones,rounds=mission.ModernCombat.RoundsInMagazine;
                yield return KeyPress(keyboard,Key.Digit4);
                Assert.IsTrue(inventory.ThrowableEquipped);Assert.AreEqual("stone",inventory.Throwable);Assert.IsFalse(stones.Aiming);
                var readyStone=mission.player.GetComponentsInChildren<Transform>(true).First(t=>InHand(t,"stone"));
                Assert.IsTrue(readyStone.gameObject.activeInHierarchy,"Selecting slot 4 must already show a stone.");
                yield return new WaitForEndOfFrame();CaptureHand(mission.player,"stone-ready");
                yield return MouseButtons(mouse,true);yield return WaitGameSeconds(.25f);
                Assert.IsTrue(stones.Aiming);Assert.Greater(stones.Arc.Count,1);Assert.AreEqual(before,inventory.Stones);Assert.AreEqual(rounds,mission.ModernCombat.RoundsInMagazine);
                var handStone=mission.player.GetComponentsInChildren<Transform>(true).First(t=>InHand(t,"stone"));
                Assert.IsTrue(handStone.gameObject.activeInHierarchy);
                Assert.AreEqual(mission.player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand),handStone.parent);
                Assert.IsFalse(handStone.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh!=null&&f.sharedMesh.name=="Sphere"),"Use the approved stone model, not a sphere.");
                var stoneBounds=handStone.GetComponentInChildren<Renderer>().bounds;
                Assert.That(Mathf.Max(stoneBounds.size.x,stoneBounds.size.y,stoneBounds.size.z),Is.InRange(.05f,.16f));
                // The refreshed motions (NamMotionRefresh) own the throw: prepare, wind, then the held aim.
                var motion=mission.player.GetComponent<NamMotionRefresh>();float aimBy=Time.time+3;
                while(motion.MotionId!="stone_aim"&&Time.time<aimBy)yield return null;
                Assert.AreEqual("stone_aim",motion.MotionId,"Holding left mouse winds up into the held throw.");
                yield return new WaitForEndOfFrame();CaptureHand(mission.player,"stone-aim");
                var actor=mission.player.GetComponentInChildren<Animator>();
                Assert.Greater(actor.GetBoneTransform(HumanBodyBones.RightHand).position.y,actor.GetBoneTransform(HumanBodyBones.Chest).position.y,"The throwing hand must be raised.");
                var index1=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="RightHandIndex1");
                var index2=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="RightHandIndex2");
                var index3=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="RightHandIndex3");
                Assert.Greater(Vector3.Angle(index2.position-index1.position,index3.position-index2.position),35f,"The index finger must bend around the stone instead of pointing straight.");
                var palmNormalLocal=(Vector3)typeof(Map01NamActions).GetField("palmNormalLocal",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(Map01NamActions.For(mission));
                var palmNormal=actor.GetBoneTransform(HumanBodyBones.RightHand).TransformDirection(palmNormalLocal);
                Assert.Greater(Vector3.Dot(index2.position-index1.position,palmNormal),.005f,"The proximal index joint must bend toward the palm, not backwards.");
                Assert.Greater(Vector3.Dot(index3.position-index2.position,palmNormal),.005f,"The middle index joint must continue around the palm.");
                var thumb1=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="RightHandThumb1");
                var thumb2=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="RightHandThumb2");
                var thumb3=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="RightHandThumb3");
                Assert.Greater(Vector3.Angle(thumb2.position-thumb1.position,thumb3.position-thumb2.position),35f,"The thumb's middle joint must flex, not stay straight.");
                var thumbEnd=thumb3.position+thumb3.up*Vector3.Distance(thumb2.position,thumb3.position)*.8f;
                Assert.Greater(Vector3.Angle(thumb3.position-thumb2.position,thumbEnd-thumb3.position),30f,"The thumb tip must curl too.");
                // Toward the stone itself: the refreshed prop's root sits on the hand bone, not in the palm as the old one did.
                var stoneCentre=handStone.GetComponentInChildren<Renderer>().bounds.center;
                Assert.Less(Vector3.Distance(thumbEnd,stoneCentre),Vector3.Distance(thumb1.position,stoneCentre),"The thumb must close toward the held item.");
                var rig=mission.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>();
                var pivot=mission.player.position;
                rig.SetCinematicView(pivot+new Vector3(-3,3,0),Quaternion.LookRotation(new Vector3(8,-2,0)),50,1);
                yield return WaitGameSeconds(.2f);
                var aimTarget=(Vector3)typeof(Map01StoneThrow).GetMethod("AimTarget",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stones,null);
                Assert.Less(Vector3.Angle(mission.player.forward,Vector3.ProjectOnPlane(aimTarget-mission.player.position,Vector3.up)),1f,"Nam must face the current mouse aim, even when standing still.");
                rig.SetCinematicView(pivot+new Vector3(0,3,-3),Quaternion.LookRotation(new Vector3(0,-2,8)),50,1);
                yield return WaitGameSeconds(.2f);
                aimTarget=(Vector3)typeof(Map01StoneThrow).GetMethod("AimTarget",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stones,null);
                Assert.Less(Vector3.Angle(mission.player.forward,Vector3.ProjectOnPlane(aimTarget-mission.player.position,Vector3.up)),1f,"Turning the camera while holding must turn Nam as well.");
                yield return MouseButtons(mouse,false);
                Assert.IsFalse(stones.Aiming);Assert.AreEqual(before-1,inventory.Stones);Assert.AreEqual(rounds,mission.ModernCombat.RoundsInMagazine);
                yield return WaitGameSeconds(.25f);Assert.IsFalse(handStone.gameObject.activeSelf,"The stone leaves the hand at release.");
                rig.ClearCinematicView();
                yield return KeyPress(keyboard,Key.Digit1);
                Assert.IsFalse(inventory.ThrowableEquipped);
                Assert.IsFalse(mission.player.GetComponentsInChildren<Renderer>().Any(r=>r.GetComponentInParent<Weapon>()!=null&&r.forceRenderingOff),"Drawing a weapon immediately after throwing must restore its renderers.");
                yield return KeyPress(keyboard,Key.Digit4);
                yield return WaitGameSeconds(1.2f);
                int retained=inventory.Stones;
                yield return MouseButtons(mouse,true);
                yield return MouseButtons(mouse,true,true);
                Assert.IsFalse(stones.Aiming);
                Assert.IsTrue(handStone.gameObject.activeSelf,"Cancelling aim keeps the selected stone in hand.");
                yield return MouseButtons(mouse,false);
                Assert.AreEqual(retained,inventory.Stones,"Right-click cancels without spending a stone.");
                yield return MouseButtons(mouse,true);Assert.IsTrue(stones.Aiming);
                yield return KeyPress(keyboard,Key.Digit1);
                Assert.IsFalse(inventory.ThrowableEquipped);Assert.IsFalse(stones.Aiming);
                yield return MouseButtons(mouse,false);
                Assert.AreEqual(retained,inventory.Stones,"Switching weapons cancels the pending throw.");Assert.AreEqual(rounds,mission.ModernCombat.RoundsInMagazine,"Switching while holding the mouse must not shoot.");
                yield return KeyPress(keyboard,Key.Q);Assert.IsFalse(stones.Aiming,"Q is no longer the throw control.");
                yield return KeyPress(keyboard,Key.Digit4);
                inventory.Spend("stone",inventory.Stones);
                int smoke=inventory.Count("smoke_bomb");
                yield return MouseButtons(mouse,true);yield return MouseButtons(mouse,false);
                Assert.IsFalse(stones.Aiming);Assert.AreEqual("stone",inventory.Throwable);Assert.AreEqual(smoke,inventory.Count("smoke_bomb"),"Empty stones must not silently throw a different item.");
                inventory.Add("grenade_f1",1);
                yield return KeyPress(keyboard,Key.Digit4);
                Assert.IsTrue(inventory.ThrowableEquipped);Assert.AreEqual("smoke_bomb",inventory.Throwable,"4 skips empty stones.");
                yield return new WaitForEndOfFrame();
                Assert.IsTrue(mission.player.GetComponentsInChildren<Transform>().Any(t=>InHand(t,"smoke_bomb")),"Selecting smoke must also put its real item model in hand.");
                CaptureHand(mission.player,"smoke-ready");
                yield return MouseButtons(mouse,true);Assert.IsTrue(stones.Aiming);Assert.AreEqual("smoke_bomb",stones.Item);
                yield return KeyPress(keyboard,Key.Digit4);
                Assert.IsFalse(stones.Aiming);Assert.AreEqual(smoke,inventory.Count("smoke_bomb"),"Cycling while aiming cancels without spending the previous item.");
                Assert.AreEqual("grenade_f1",inventory.Throwable);
                yield return new WaitForEndOfFrame();
                Assert.IsTrue(mission.player.GetComponentsInChildren<Transform>().Any(t=>InHand(t,"grenade_f1")),"Selecting F1 must put the grenade in hand.");
                CaptureHand(mission.player,"f1-ready");
                yield return MouseButtons(mouse,false);yield return MouseButtons(mouse,true);
                Assert.IsTrue(stones.Aiming);Assert.AreEqual("grenade_f1",stones.Item);
                yield return MouseButtons(mouse,false);
                Assert.AreEqual(0,inventory.Count("grenade_f1"));Assert.AreEqual(rounds,mission.ModernCombat.RoundsInMagazine);
                yield return KeyPress(keyboard,Key.Digit4);Assert.AreEqual("smoke_bomb",inventory.Throwable);
                yield return KeyPress(keyboard,Key.Digit1);
                inventory.Spend("smoke_bomb",inventory.Count("smoke_bomb"));
                yield return KeyPress(keyboard,Key.Digit4);Assert.IsFalse(inventory.ThrowableEquipped,"Empty slot 4 leaves the weapon equipped.");
            }
            finally { RestoreMice();InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RapidGunToThrowableSwitchClearsAimFireAndReloadPoses()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            var routing=RouteInputToGame();var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            IsolateMice(mouse);
            try {
                var mission=Object.FindFirstObjectByType<Map01Mission>();foreach(var enemy in mission.Enemies)enemy.enabled=false;
                mission.GetComponent<Map01Rescue>().MarkIntroSeen();
                var inventory=mission.GetComponent<Map01Inventory>();var combat=mission.ModernCombat;
                var actor=mission.player.GetComponentInChildren<Animator>();
                inventory.Add("stone",4);inventory.Add("ammo_rifle",60);
                for(int mode=0;mode<3;mode++) {
                    yield return MouseButtons(mouse,false);yield return KeyPress(keyboard,Key.Digit1);
                    Assert.IsFalse(inventory.ThrowableEquipped);Assert.AreEqual(2,actor.GetInteger("Weapon"),"Drawing the same hidden rifle must restore its animator mode.");
                    if(mode<2){yield return MouseButtons(mouse,mode==1,true);yield return WaitGameSeconds(.08f);}
                    else {combat.RestoreMagazine(10);Assert.IsTrue(combat.TryReload());yield return null;yield return null;}
                    yield return KeyPress(keyboard,Key.Digit4);
                    Assert.IsTrue(inventory.ThrowableEquipped,"Slot 4 must work even while a previous equip is suppressing held fire.");
                    Assert.IsFalse(combat.IsAiming);Assert.IsFalse(combat.IsReloading);
                    int rounds=combat.RoundsInMagazine,stones=inventory.Stones;
                    yield return WaitGameSeconds(.25f);yield return new WaitForEndOfFrame();
                    Assert.AreEqual(0,actor.GetInteger("Weapon"));Assert.Less(actor.GetLayerWeight(actor.GetLayerIndex("UpperBody")),.01f);
                    Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"),"Throwable ready stance must leave rifle locomotion.");
                    Assert.Less(actor.GetBoneTransform(HumanBodyBones.RightHand).position.y,actor.GetBoneTransform(HumanBodyBones.Chest).position.y,"The rifle aim must release the right arm.");
                    Assert.AreEqual(rounds,combat.RoundsInMagazine);Assert.AreEqual(stones,inventory.Stones);
                    CaptureHand(mission.player,"quick-switch-"+mode);
                }
                yield return MouseButtons(mouse,false);
                yield return MouseButtons(mouse,true);yield return WaitGameSeconds(.3f);
                Assert.IsTrue(mission.GetComponent<Map01StoneThrow>().Aiming);
                Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(actor.GetLayerIndex("ActionsUpper")).IsName("Act_ThrowAim"));
                yield return MouseButtons(mouse,false);
                yield return KeyPress(keyboard,Key.Digit1);
                Assert.AreEqual(2,actor.GetInteger("Weapon"));Assert.IsFalse(inventory.ThrowableEquipped);
            } finally {RestoreMice();InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing);}
            yield return new ExitPlayMode();
        }
    }
}
