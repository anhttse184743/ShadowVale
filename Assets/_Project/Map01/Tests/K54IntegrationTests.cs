using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class K54IntegrationTests:ForestSceneTestBase
    {
        const string Sandbox="Assets/_Project/Map01/Tests/Generated/K54 Integration Sandbox.unity";
        const string Work="Docs/Reviews/K54_Integration_20261018";
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static int attacks;
        static Mouse heldMouse;
        static bool rightDown;
        static void CountAttack(WeaponKind kind,Vector3 point){if(kind==WeaponKind.Pistol)attacks++;}
        static void HoldRight(){if(heldMouse!=null){heldMouse.MakeCurrent();var state=new MouseState();if(rightDown)state=state.WithButton(MouseButton.Right);InputSystem.QueueStateEvent(heldMouse,state);}}
        [SetUp] public void IsolateSaveStorage()=>SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath(Work+"/TestSaves"));
        [TearDown] public void ReleaseSaveStorage()=>SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
        public static void OpenSandbox()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Sandbox));
            if(!File.Exists(Sandbox))AssetDatabase.CopyAsset("Assets/_Project/Scenes/Maps/Map 1.unity",Sandbox);
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene(Sandbox);
        }
        static void Quiet()
        {
            // Batchmode can have no keyboard. Map01PlayerInteraction needs one to clear the HUD-click gate.
            if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();
            if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
            foreach(var enemy in M.Enemies)enemy.enabled=false;
            M.ModernHealth.CinematicInvulnerable=true;M.GetComponent<Map01Rescue>().MarkIntroSeen();
            Assert.IsTrue(M.GetComponent<Map01SaveSystem>().IsSandbox,"All test saves must be isolated.");
        }
        [UnityTest]
        public IEnumerator K54HasItsOwnMagazineAndReloadUsesOnlyPistolReserve()
        {
            OpenSandbox();yield return new EnterPlayMode();yield return null;Quiet();
            var bag=M.GetComponent<Map01Inventory>();var combat=M.ModernCombat;var pistol=M.player.GetComponent<Map01Pistol>();
            Assert.IsNotNull(pistol);Assert.IsNotNull(pistol.Weapon);Assert.AreEqual(22,pistol.Weapon.Damage);Assert.AreEqual(25,pistol.Weapon.Range);Assert.AreEqual(1f/3,pistol.Weapon.Cooldown,.0001f);
            Assert.IsTrue(pistol.Weapon.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="magazine"&&t.GetComponentInChildren<Renderer>(true)!=null),"The detached Blender magazine must survive FBX export and prefab extraction.");
            int rifle=combat.MagazineRounds(WeaponKind.Rifle),rifleReserve=bag.Count("ammo_rifle"),pistolReserve=bag.Count("ammo_pistol");
            Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Pistol));yield return WaitGameSeconds(.3f);
            Assert.AreEqual(WeaponKind.Pistol,combat.EquippedKind);Assert.AreEqual(8,combat.MagazineCapacity);Assert.AreEqual(1.8f,combat.BaseReloadSeconds,.001f);Assert.AreEqual(0,combat.RoundsInMagazine,"No free rounds are created on installation.");
            Assert.IsTrue(combat.TryReload());yield return WaitGameSeconds(2f);
            Assert.AreEqual(8,combat.RoundsInMagazine);Assert.AreEqual(pistolReserve-8,bag.Count("ammo_pistol"));Assert.AreEqual(rifleReserve,bag.Count("ammo_rifle"));
            Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Primary));yield return null;Assert.AreEqual(rifle,combat.RoundsInMagazine);Assert.AreEqual(30,combat.MagazineCapacity);
            Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Pistol));yield return null;Assert.AreEqual(8,combat.RoundsInMagazine);
            var json=M.GetComponent<Map01SaveSystem>().CaptureInteractionCheckpoint();
            StringAssert.Contains("\"roundsInMagazine\":"+rifle,json);StringAssert.Contains("\"pistolRoundsInMagazine\":8",json);
            // Exercise the real save restore method in memory; it never writes or reloads the user's map.
            combat.RestoreMagazine(WeaponKind.Rifle,1);combat.RestoreMagazine(WeaponKind.Pistol,2);
            var save=M.GetComponent<Map01SaveSystem>();var nested=typeof(Map01SaveSystem).GetNestedType("CheckpointData",BindingFlags.NonPublic);
            var data=JsonUtility.FromJson(json,nested);typeof(Map01SaveSystem).GetMethod("RestoreGameplay",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(save,new[]{data});
            Assert.AreEqual(rifle,combat.MagazineRounds(WeaponKind.Rifle));Assert.AreEqual(8,combat.MagazineRounds(WeaponKind.Pistol));
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator SwitchingAndThrowingCancelK54ReloadAndKeepBothAmmunitionPools()
        {
            OpenSandbox();yield return new EnterPlayMode();yield return null;Quiet();
            var bag=M.GetComponent<Map01Inventory>();var combat=M.ModernCombat;
            bag.Draw(Map01Inventory.Slot.Pistol);combat.RestoreMagazine(3);int reserve=bag.Count("ammo_pistol");
            Assert.IsTrue(combat.TryReload());yield return WaitGameSeconds(.4f);bag.Draw(Map01Inventory.Slot.Knife);yield return WaitGameSeconds(2f);
            Assert.IsFalse(combat.IsReloading);Assert.AreEqual(3,combat.MagazineRounds(WeaponKind.Pistol));Assert.AreEqual(reserve,bag.Count("ammo_pistol"));
            bag.Draw(Map01Inventory.Slot.Pistol);Assert.IsTrue(combat.TryReload());yield return WaitGameSeconds(.2f);
            var routing=RouteInputToGame();var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q));InputSystem.Update();bag.Add("smoke_bomb",1);bag.SelectThrowable("smoke_bomb");
                M.GetComponent<Map01StoneThrow>().BeginAim();yield return WaitGameSeconds(.35f);
                Assert.IsFalse(combat.IsReloading);Assert.IsTrue(M.GetComponent<Map01StoneThrow>().PresentingThrow);Assert.Less(M.player.GetComponent<Map01Pistol>().PoseWeight,.01f);
                Assert.AreEqual(3,combat.MagazineRounds(WeaponKind.Pistol));Assert.AreEqual(reserve,bag.Count("ammo_pistol"));
                M.GetComponent<Map01StoneThrow>().Cancel();
            }
            finally{InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing);}
            yield return WaitGameSeconds(.5f);Assert.IsTrue(Map01Inventory.Drawable("pistol_k54"));Assert.IsTrue(Map01Inventory.Drawable("rifle_mosin"));
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator K54FiresOneRoundDealsConfiguredDamageAndSurvivesCrouchAndStoryInterruptions()
        {
            OpenSandbox();yield return new EnterPlayMode();yield return null;Quiet();
            var combat=M.ModernCombat;M.GetComponent<Map01Inventory>().Draw(Map01Inventory.Slot.Pistol);combat.RestoreMagazine(8);yield return WaitGameSeconds(.4f);
            Assert.IsNotNull(combat,"The Map1 combat reference must survive game-time waits.");
            // Avoid iterator closures: EnterPlayMode reloads the domain and does not preserve display classes.
            attacks=0;combat.Attacked+=CountAttack;
            var camera=M.gameCamera;var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;
            camera.transform.SetPositionAndRotation(M.player.position+Vector3.up*100,Quaternion.identity);
            var target=GameObject.CreatePrimitive(PrimitiveType.Sphere);target.name="Isolated K54 damage target";target.transform.position=camera.transform.position+Vector3.forward*6;target.transform.localScale=Vector3.one*3;var hp=target.AddComponent<Health>();
            Physics.SyncTransforms();var fire=typeof(PlayerCombat).GetMethod("Attack",BindingFlags.NonPublic|BindingFlags.Instance);Assert.IsNotNull(fire,"Use the actual combat attack implementation.");fire.Invoke(combat,null);typeof(PlayerCombat).GetMethod("ResolvePendingGunfire",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(combat,null);
            Assert.AreEqual(78,hp.Current,.001f,"The actual hitscan deals the existing K54 damage22.");Object.Destroy(target);camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);yield return null;
            Assert.AreEqual(7,combat.RoundsInMagazine);Assert.AreEqual(1,attacks);Assert.AreEqual(WeaponKind.Pistol,combat.EquippedKind);
            M.ModernPlayer.RestoreMotion(true);M.Crouched=true;yield return WaitGameSeconds(.5f);
            Assert.IsFalse(float.IsNaN(combat.WeaponFor(WeaponKind.Pistol).Muzzle.position.x));Assert.AreEqual(1,M.player.GetComponent<Map01Pistol>().PoseWeight,.01f);
            M.Cinematic=true;yield return null;Assert.AreEqual(0,M.player.GetComponent<Map01Pistol>().PoseWeight,.001f);
            M.Cinematic=false;yield return WaitGameSeconds(.3f);Assert.AreEqual(1,M.player.GetComponent<Map01Pistol>().PoseWeight,.01f);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator CaptureK54InTheRealMapLighting()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("Visual proof requires the real graphics device.");
            OpenSandbox();SetPreviewResolution(1920,1080);yield return new EnterPlayMode();yield return null;Quiet();
            var combat=M.ModernCombat;var bag=M.GetComponent<Map01Inventory>();bag.Draw(Map01Inventory.Slot.Pistol);combat.RestoreMagazine(3);yield return WaitGameSeconds(.7f);
            Capture("unity_k54_carry");
            var routing=RouteInputToGame();var mouse=InputSystem.AddDevice<Mouse>();heldMouse=mouse;rightDown=false;InputSystem.onBeforeUpdate+=HoldRight;
            try
            {
                // Aim now follows the held button on both maps. Keep it down through
                // the captures and reload, then verify that releasing lowers the sights.
                yield return WaitGameSeconds(.2f);rightDown=true;yield return WaitGameSeconds(.8f);
                File.WriteAllText(Work+"/aim_diagnostics.txt","combat="+combat.enabled+" kind="+combat.EquippedKind+" aim="+combat.IsAiming+" input="+combat.InputAllowed()+" stopped="+M.Stopped+" cinematic="+M.Cinematic+" paused="+M.Paused+" menu="+ForestMenu.Visible+" suppress="+bag.SuppressFire+" mouse="+Mouse.current?.name+" right="+Mouse.current?.rightButton.isPressed+" sourceFrame="+M.player.GetComponent<Map01Pistol>().SourceFrame);
                Capture("unity_k54_aim");
                Assert.IsTrue(combat.IsAiming);Assert.IsTrue(combat.TryReload());yield return WaitGameSeconds(.55f);Capture("unity_k54_reload_1");yield return WaitGameSeconds(.4f);Capture("unity_k54_reload_2");yield return WaitGameSeconds(1f);
                M.ModernPlayer.RestoreMotion(true);M.Crouched=true;yield return WaitGameSeconds(.7f);Capture("unity_k54_crouch_aim");
                rightDown=false;yield return WaitGameSeconds(.3f);Assert.IsFalse(combat.IsAiming);
            }
            finally{rightDown=false;InputSystem.onBeforeUpdate-=HoldRight;heldMouse=null;InputSystem.RemoveDevice(mouse);RestoreInputRouting(routing);}
            yield return new ExitPlayMode();
        }
        static void Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Work+"/"+name+"_gameview.png");
            // A second, close view uses the same live rig and map lighting to expose finger/contact defects.
            var camera=new GameObject("Temporary K54 QA camera").AddComponent<Camera>();camera.CopyFrom(M.gameCamera);camera.tag="Untagged";
            camera.transform.position=M.player.position+M.player.forward*1.8f+M.player.right*.8f+Vector3.up*1.55f;
            camera.transform.LookAt(M.player.position+Vector3.up*1.28f);camera.fieldOfView=35;camera.nearClipPlane=.03f;
            var rt=RenderTexture.GetTemporary(1920,1080,24);var prior=camera.targetTexture;var active=RenderTexture.active;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();Directory.CreateDirectory(Work);File.WriteAllBytes(Work+"/"+name+".png",tex.EncodeToPNG());}
            finally{camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);Object.Destroy(camera.gameObject);}
        }
    }
}
