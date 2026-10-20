using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class MosinIntegrationTests:ForestSceneTestBase
    {
        const string Work="Docs/Reviews/Mosin_Integration_20261018";
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();static Mouse heldMouse;static bool rightHeld;
        [SetUp] public void IsolateSaves()=>SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath(Work+"/TestSaves"));
        [TearDown] public void ReleaseSaves()=>SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
        [Test] public void ExportedMechanicalPartsMoveAndFollowTheCarryingHands()
        {
            var builder=System.Type.GetType("ShadowVale.Map01.Editor.K54IntegrationBuilder, ShadowVale.Map01.Editor");Assert.IsNotNull(builder);
            builder.GetMethod("AuditK54Mechanics",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
            builder.GetMethod("AuditMosinMechanics",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
        }
        static void HoldRight(){if(heldMouse!=null){heldMouse.MakeCurrent();var state=new MouseState();if(rightHeld)state=state.WithButton(MouseButton.Right);InputSystem.QueueStateEvent(heldMouse,state);}}
        static void PrepareMosin()
        {
            foreach(var enemy in M.Enemies)enemy.enabled=false;M.ModernHealth.CinematicInvulnerable=true;M.GetComponent<Map01Rescue>().MarkIntroSeen();
            if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
            var bag=M.GetComponent<Map01Inventory>();bag.Add("rifle_mosin",1);bag.Add("ammo_sniper",10);
            int cell=Enumerable.Range(0,Map01Inventory.BagSize).First(i=>bag.Cell(i)?.id=="rifle_mosin");
            Assert.IsTrue(bag.EquipFromCell(cell));Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Primary));Assert.AreEqual(WeaponKind.Sniper,M.ModernCombat.EquippedKind);Assert.IsTrue(M.GetComponent<Map01SaveSystem>().IsSandbox);
        }
        [UnityTest] public IEnumerator MosinInsertsIndividuallyKeepsAKInTheBagAndSavesThreeIndependentMagazines()
        {
            K54IntegrationTests.OpenSandbox();yield return new EnterPlayMode();yield return null;PrepareMosin();yield return WaitGameSeconds(.3f);
            var combat=M.ModernCombat;var bag=M.GetComponent<Map01Inventory>();var mosin=M.player.GetComponent<Map01Mosin>();
            Assert.IsNotNull(mosin);Assert.AreEqual(95,mosin.Weapon.Damage);Assert.AreEqual(80,mosin.Weapon.Range);Assert.AreEqual(1f/.7f,mosin.Weapon.Cooldown,.0001f);Assert.AreEqual(5,combat.MagazineCapacity);Assert.AreEqual(0,combat.RoundsInMagazine);
            bool akInBag=false;for(int i=0;i<Map01Inventory.BagSize;i++)akInBag|=bag.Cell(i)?.id=="rifle_standard";
            Assert.IsTrue(akInBag,"AK is stored in the existing bag, not attached to a new rig/back slot.");
            Assert.IsTrue(mosin.Weapon.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="bolt"));Assert.IsTrue(mosin.Weapon.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Cartridge"));
            int reserve=bag.Count("ammo_sniper"),akRounds=combat.MagazineRounds(WeaponKind.Rifle);var avatar=M.player.GetComponentInChildren<Animator>().avatar;
            Assert.IsTrue(combat.TryReload());yield return WaitGameSeconds(1.6f);Assert.AreEqual(1,combat.RoundsInMagazine);Assert.AreEqual(reserve-1,bag.Count("ammo_sniper"));
            bag.Draw(Map01Inventory.Slot.Pistol);yield return WaitGameSeconds(.3f);Assert.AreEqual(1,combat.MagazineRounds(WeaponKind.Sniper));Assert.AreEqual(akRounds,combat.MagazineRounds(WeaponKind.Rifle));
            bag.Draw(Map01Inventory.Slot.Primary);Assert.IsTrue(combat.TryReload());yield return WaitGameSeconds(4.6f);Assert.AreEqual(5,combat.RoundsInMagazine);Assert.AreEqual(reserve-5,bag.Count("ammo_sniper"));
            string json=M.GetComponent<Map01SaveSystem>().CaptureInteractionCheckpoint();StringAssert.Contains("\"mosinRoundsInMagazine\":5",json);StringAssert.Contains("\"roundsInMagazine\":"+akRounds,json);
            var save=M.GetComponent<Map01SaveSystem>();var type=typeof(Map01SaveSystem).GetNestedType("CheckpointData",BindingFlags.NonPublic);var state=JsonUtility.FromJson(json,type);combat.RestoreMagazine(WeaponKind.Sniper,0);
            typeof(Map01SaveSystem).GetMethod("RestoreGameplay",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(save,new[]{state});Assert.AreEqual(5,combat.MagazineRounds(WeaponKind.Sniper));Assert.AreSame(avatar,M.player.GetComponentInChildren<Animator>().avatar);
            type.GetField("equippedWeapon",BindingFlags.Public|BindingFlags.Instance).SetValue(state,(int)WeaponKind.Rifle);
            typeof(Map01SaveSystem).GetMethod("RestoreGameplay",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(save,new[]{state});
            Assert.AreEqual(WeaponKind.Sniper,combat.EquippedKind,"Legacy rifle enum follows the saved Mosin primary slot.");
            Assert.AreEqual(akRounds,combat.MagazineRounds(WeaponKind.Rifle));Assert.AreEqual(5,combat.MagazineRounds(WeaponKind.Sniper));
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator PUScopeHasReal35MagnificationAndBoltCycleReturnsToScopeWithoutASecondCamera()
        {
            K54IntegrationTests.OpenSandbox();SetPreviewResolution(1920,1080);yield return new EnterPlayMode();yield return null;PrepareMosin();M.ModernCombat.RestoreMagazine(5);yield return WaitGameSeconds(.5f);
            var combat=M.ModernCombat;var mosin=M.player.GetComponent<Map01Mosin>();var rig=M.gameCamera.GetComponent<ThirdPersonCamera>();int cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;float hipFov=M.gameCamera.fieldOfView;
            Directory.CreateDirectory(Work);CaptureCloseup("unity_mosin_carry");
            var routing=RouteInputToGame();heldMouse=InputSystem.AddDevice<Mouse>();rightHeld=true;InputSystem.onBeforeUpdate+=HoldRight;
            try
            {
                yield return WaitGameSeconds(1f);Assert.IsTrue(mosin.Scoped);Assert.IsTrue(rig.IsWeaponScope);Assert.IsFalse(rig.IsBinoculars);Assert.AreEqual(rig.WeaponScopeFieldOfView,M.gameCamera.fieldOfView,.2f);
                Assert.AreEqual(2*Mathf.Atan(Mathf.Tan(hipFov*Mathf.Deg2Rad*.5f)/3.5f)*Mathf.Rad2Deg,M.gameCamera.fieldOfView,.2f);
                rightHeld=false;yield return WaitGameSeconds(.25f);Assert.IsTrue(combat.IsAiming);Assert.IsTrue(mosin.Scoped,"Releasing the button keeps the PU scope toggled on.");
                yield return CaptureScopedPose("unity_mosin_aim");ScreenCapture.CaptureScreenshot(Work+"/unity_mosin_PU35.png");yield return null;yield return null;
                typeof(PlayerCombat).GetMethod("Attack",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(combat,null);yield return WaitGameSeconds(.65f);
                Assert.AreEqual(4,combat.RoundsInMagazine);Assert.IsFalse(mosin.Scoped);Assert.That(mosin.SourceFrame,Is.InRange(163f,249f));CaptureCloseup("unity_mosin_bolt");
                yield return WaitGameSeconds(1f);Assert.IsTrue(mosin.Scoped);Assert.AreEqual(cameras,Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length);
                Assert.IsTrue(combat.TryReload());yield return WaitGameSeconds(1.6f);Assert.AreEqual(5,combat.RoundsInMagazine);Assert.IsTrue(combat.IsReloading);Assert.IsFalse(mosin.Scoped);CaptureCloseup("unity_mosin_single_load");
                yield return WaitGameSeconds(1.2f);Assert.IsFalse(combat.IsReloading);Assert.IsTrue(mosin.Scoped);
                M.ModernPlayer.RestoreMotion(true);M.Crouched=true;yield return WaitGameSeconds(.6f);Assert.IsTrue(mosin.Scoped);yield return CaptureScopedPose("unity_mosin_crouch_aim");
                rightHeld=true;yield return WaitGameSeconds(.5f);Assert.IsFalse(combat.IsAiming);Assert.IsFalse(mosin.Scoped,"A second press closes the PU scope.");
                M.GetComponent<Map01Inventory>().Draw(Map01Inventory.Slot.Pistol);yield return WaitGameSeconds(.4f);Assert.IsFalse(mosin.Scoped);Assert.IsFalse(rig.IsWeaponScope);
            }
            finally{InputSystem.onBeforeUpdate-=HoldRight;InputSystem.RemoveDevice(heldMouse);heldMouse=null;RestoreInputRouting(routing);}
            yield return new ExitPlayMode();
        }
        static IEnumerator CaptureScopedPose(string name)
        {
            // MeshRenderer culling for the scope was already submitted this frame. Give it a frame
            // after leaving the scope before the diagnostic camera renders this frozen authored pose.
            var mosin=M.player.GetComponent<Map01Mosin>();var animator=M.player.GetComponentInChildren<Animator>();var twist=M.player.GetComponentInChildren<ForearmTwist>();
            bool animatorEnabled=animator.enabled,twistEnabled=twist!=null&&twist.enabled;
            mosin.enabled=false;animator.enabled=false;if(twist!=null)twist.enabled=false;
            try{yield return null;CaptureCloseup(name);}
            finally{animator.enabled=animatorEnabled;if(twist!=null)twist.enabled=twistEnabled;mosin.enabled=true;}
            yield return null;
        }
        static void CaptureCloseup(string name)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            var rig=M.gameCamera.GetComponent<ThirdPersonCamera>();bool scoped=rig.IsWeaponScope;if(scoped)rig.SetWeaponScope(false,3.5f,0);
            var camera=new GameObject("Temporary Mosin QA camera").AddComponent<Camera>();camera.CopyFrom(M.gameCamera);camera.tag="Untagged";camera.nearClipPlane=.03f;camera.fieldOfView=40;
            camera.transform.position=M.player.position+M.player.forward*2f+M.player.right*1.1f+Vector3.up*(M.Crouched ? 1.15f : 1.6f);camera.transform.LookAt(M.player.position+Vector3.up*(M.Crouched ? .9f : 1.35f));
            camera.ResetProjectionMatrix();camera.ResetWorldToCameraMatrix();camera.ResetCullingMatrix();camera.useOcclusionCulling=false;
            var rt=RenderTexture.GetTemporary(1920,1080,24);var active=RenderTexture.active;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                var renderers=M.player.GetComponent<Map01Mosin>().Weapon.GetComponentsInChildren<Renderer>();
                File.WriteAllLines(Work+"/"+name+"_renderers.txt",renderers.Select(r=>r.name+" active="+r.gameObject.activeInHierarchy+" enabled="+r.enabled+" forcedOff="+r.forceRenderingOff+" shadows="+r.shadowCastingMode+" position="+r.transform.position+" bounds="+r.bounds));
                Assert.IsTrue(renderers.Where(r=>r.name!="Cartridge"&&r.GetComponentInParent<MuzzleFlash>()==null).All(r=>r.enabled&&!r.forceRenderingOff&&r.shadowCastingMode!=ShadowCastingMode.ShadowsOnly),"Leaving the scope restores the held Mosin renderers.");
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(Work+"/"+name+".png",tex.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);Object.Destroy(camera.gameObject);if(scoped)rig.SetWeaponScope(true,3.5f,M.GetComponent<Map01Scouting>().Eye.y-M.player.position.y);}
        }
    }
}
