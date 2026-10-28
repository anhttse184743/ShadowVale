using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShadowVale.Map01.Tests
{
    public sealed class WeaponAnimationIntegrationTests:ForestSceneTestBase
    {
        [SetUp] public void IsolateIntegrationSaves()=>UnityEditor.SessionState.SetString(ForestSaveSlots.EditorTestRootKey,System.IO.Path.GetFullPath("Docs/Reviews/K54_Integration_20261018/TestSaves"));
        [TearDown] public void ReleaseIntegrationSaves()=>UnityEditor.SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static void Quiet(){foreach(var enemy in M.Enemies)enemy.enabled=false;M.ModernHealth.CinematicInvulnerable=true;M.GetComponent<Map01Rescue>().MarkIntroSeen();}
        sealed class ThrowKeyHold:System.IDisposable
        {
            bool pressed=true;readonly Mouse device;readonly Mouse[] physical;readonly System.Action restore;
            public ThrowKeyHold(System.Action restore){
                this.restore=restore;physical=InputSystem.devices.OfType<Mouse>().Where(m=>m.enabled).ToArray();
                foreach(var mouse in physical)InputSystem.DisableDevice(mouse);
                device=InputSystem.AddDevice<Mouse>("HeldThrowableMouse");InputSystem.onBeforeUpdate+=Pump;
                Pump();InputSystem.Update();
            }
            void Pump(){device.MakeCurrent();InputSystem.QueueStateEvent(device,pressed?new MouseState().WithButton(MouseButton.Left):new MouseState());}
            public void Release(){pressed=false;Pump();InputSystem.Update();}
            public void Press(){pressed=true;Pump();InputSystem.Update();}
            public void Dispose(){InputSystem.onBeforeUpdate-=Pump;InputSystem.RemoveDevice(device);foreach(var mouse in physical)if(mouse.added)InputSystem.EnableDevice(mouse);restore();}
        }
        static ThrowKeyHold HoldThrowKey()
        {
            var previous=RouteInputToGame();return new ThrowKeyHold(()=>RestoreInputRouting(previous));
        }
        static GameObject Held(string id)=>M.player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>(t.name=="Throwable in hand — "+id||t.name==id+" in hand"))?.gameObject;
        [UnityTest]
        public IEnumerator GrenadeUsesItsOwnModelReleasesOnceAndKeepsHandsBusyThroughRecovery()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;Quiet();
            var bag=M.GetComponent<Map01Inventory>();var thrower=M.GetComponent<Map01StoneThrow>();bag.Add("grenade_f1",2);bag.SelectThrowable("grenade_f1");int count=bag.Count("grenade_f1");int rounds=M.ModernCombat.RoundsInMagazine;
            using(var heldKey=HoldThrowKey()){
            thrower.BeginAim();yield return WaitGameSeconds(.30f);
            Assert.IsTrue(thrower.Aiming,"The virtual left mouse button is held for the whole aiming gesture.");Assert.IsNotNull(Held("grenade_f1"));Assert.IsTrue(Held("grenade_f1").activeSelf);Assert.GreaterOrEqual(Held("grenade_f1").GetComponentsInChildren<Renderer>().Length,3);
            Assert.AreEqual(count,bag.Count("grenade_f1"),"Aiming does not spend the grenade.");
            thrower.Cancel();heldKey.Release();yield return WaitGameSeconds(.35f);Assert.IsTrue(Held("grenade_f1").activeSelf,"Cancelling aim keeps the selected grenade in hand.");Assert.AreEqual(count,bag.Count("grenade_f1"));
            Assert.IsTrue(bag.ThrowableEquipped);Assert.IsFalse(M.ModernCombat.InputAllowed(),"The selected throwable still owns the hands.");
            heldKey.Press();thrower.BeginAim();yield return WaitGameSeconds(.30f);thrower.Release();heldKey.Release();yield return WaitGameSeconds(.05f);
            Assert.IsFalse(thrower.Aiming);Assert.IsTrue(thrower.PresentingThrow);Assert.IsFalse(M.ModernCombat.InputAllowed(),"The gun cannot fire while the hand is following through.");Assert.AreEqual(count-1,bag.Count("grenade_f1"));
            yield return WaitGameSeconds(.30f);
            var projectile=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="Thrown grenade_f1");Assert.IsNotNull(projectile);
            Assert.IsFalse(projectile.GetComponentsInChildren<Transform>().Any(t=>t.name=="ring"||t.name.StartsWith("ring.")),"The real imported F1 ring.001 is removed from the airborne grenade.");
            Assert.IsFalse(Held("grenade_f1").activeSelf);Assert.AreEqual(rounds,M.ModernCombat.RoundsInMagazine);Assert.AreEqual(count-1,bag.Count("grenade_f1"),"One release spends exactly one item.");
            yield return WaitGameSeconds(1f);Assert.IsFalse(thrower.PresentingThrow);Assert.IsFalse(M.ModernCombat.InputAllowed());Assert.IsTrue(Held("grenade_f1").activeSelf,"One grenade remains selected after recovery.");
            Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Primary));yield return null;Assert.IsTrue(M.ModernCombat.InputAllowed());
            Assert.IsTrue(M.player.GetComponentsInChildren<Weapon>(true).Where(w=>w.gameObject.activeInHierarchy).SelectMany(w=>w.GetComponentsInChildren<Renderer>()).All(r=>!r.forceRenderingOff),"Drawing the primary restores its rendering.");
            }
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator SmokeIsACanisterInTheHandAndAimingCancelsTheReloadWithoutSpendingAmmo()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;Quiet();
            var bag=M.GetComponent<Map01Inventory>();bag.Add("ammo_rifle",60);bag.Add("smoke_bomb",1);
            M.ModernCombat.RestoreMagazine(12);int reserve=bag.Count("ammo_rifle");Assert.IsTrue(M.ModernCombat.TryReload());yield return WaitGameSeconds(.20f);bag.SelectThrowable("smoke_bomb");
            using(var heldKey=HoldThrowKey()){
            var thrower=M.GetComponent<Map01StoneThrow>();thrower.BeginAim();yield return WaitGameSeconds(.35f);
            Assert.IsNotNull(Held("smoke_bomb"));Assert.GreaterOrEqual(Held("smoke_bomb").GetComponentsInChildren<Renderer>().Length,2);Assert.IsFalse(M.ModernCombat.IsReloading);Assert.AreEqual(12,M.ModernCombat.RoundsInMagazine);Assert.AreEqual(reserve,bag.Count("ammo_rifle"));
            thrower.Cancel();yield return WaitGameSeconds(.35f);Assert.IsTrue(Held("smoke_bomb").activeSelf);Assert.AreEqual(1,bag.Count("smoke_bomb"));
            Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Primary));yield return null;Assert.IsFalse(Held("smoke_bomb").activeSelf);}
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator HolsteringAnAKInterruptsItsMagazineGestureAndDoesNotFinishAHiddenReload()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;Quiet();
            var bag=M.GetComponent<Map01Inventory>();bag.Add("ammo_rifle",60);M.ModernCombat.RestoreMagazine(12);int reserve=bag.Count("ammo_rifle");Assert.IsTrue(M.ModernCombat.TryReload());yield return WaitGameSeconds(.35f);
            Assert.IsTrue(M.ModernCombat.Equip(WeaponKind.Knife));yield return WaitGameSeconds(.25f);Assert.IsFalse(M.ModernCombat.IsReloading);
            yield return WaitGameSeconds(M.ModernCombat.BaseReloadSeconds+.2f);Assert.AreEqual(12,M.ModernCombat.RoundsInMagazine);Assert.AreEqual(reserve,bag.Count("ammo_rifle"));
            M.ModernCombat.Equip(WeaponKind.Rifle);yield return WaitGameSeconds(.25f);Assert.IsTrue(M.ModernCombat.TryReload());yield return WaitGameSeconds(M.ModernCombat.BaseReloadSeconds+.2f);Assert.AreEqual(M.ModernCombat.MagazineCapacity,M.ModernCombat.RoundsInMagazine);Assert.AreEqual(reserve-(M.ModernCombat.MagazineCapacity-12),bag.Count("ammo_rifle"));
            Assert.IsTrue(Map01Inventory.Drawable("pistol_k54"));Assert.IsTrue(Map01Inventory.Drawable("rifle_mosin"),"The user explicitly included Mosin in the integration.");yield return new ExitPlayMode();
        }
    }
}
