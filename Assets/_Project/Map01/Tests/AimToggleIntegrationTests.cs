using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class AimToggleIntegrationTests : ForestSceneTestBase
    {
        static Map01Mission Mission => Object.FindFirstObjectByType<Map01Mission>();
        static Mouse mouse;
        static bool rightDown;
        string previousSaveRoot;

        [SetUp] public void IsolateSaves()
        {
            previousSaveRoot=SessionState.GetString(ForestSaveSlots.EditorTestRootKey, "");
            SessionState.SetString(ForestSaveSlots.EditorTestRootKey, Path.GetFullPath("Docs/Reviews/AimToggle_20261018/TestSaves"));
        }
        [TearDown] public void RestoreSaves()
        {
            if (string.IsNullOrEmpty(previousSaveRoot)) SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
            else SessionState.SetString(ForestSaveSlots.EditorTestRootKey, previousSaveRoot);
        }
        static void PumpMouse()
        {
            mouse.MakeCurrent();
            var state=new MouseState();
            if (rightDown) state=state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, state);
        }
        static IEnumerator CheckToggle(WeaponKind kind)
        {
            Assert.AreEqual(kind, Mission.ModernCombat.EquippedKind);
            Assert.IsFalse(Mission.ModernCombat.IsAiming);
            rightDown=true; yield return WaitGameSeconds(.7f);
            Assert.IsTrue(Mission.ModernCombat.IsAiming, kind+": first press enables aim.");
            yield return WaitGameSeconds(.3f);
            Assert.IsTrue(Mission.ModernCombat.IsAiming, kind+": holding does not repeatedly toggle.");
            rightDown=false; yield return WaitGameSeconds(.3f);
            Assert.IsTrue(Mission.ModernCombat.IsAiming, kind+": release keeps aim enabled.");
            rightDown=true; yield return WaitGameSeconds(.7f);
            Assert.IsFalse(Mission.ModernCombat.IsAiming, kind+": second press disables aim.");
            rightDown=false; yield return WaitGameSeconds(.3f);
            Assert.IsFalse(Mission.ModernCombat.IsAiming, kind+": release keeps aim disabled.");
        }
        [UnityTest] public IEnumerator AKK54AndMosinToggleOnPressAndClearWhenGameplayIsInterrupted()
        {
            K54IntegrationTests.OpenSandbox();
            yield return new EnterPlayMode(); yield return null;
            foreach (var enemy in Mission.Enemies) enemy.enabled=false;
            Mission.ModernHealth.CinematicInvulnerable=true;
            Mission.GetComponent<Map01Rescue>().MarkIntroSeen();
            if (Keyboard.current==null) InputSystem.AddDevice<Keyboard>();
            var routing=RouteInputToGame();mouse=InputSystem.AddDevice<Mouse>();rightDown=false;
            InputSystem.onBeforeUpdate+=PumpMouse;
            try
            {
                var bag=Mission.GetComponent<Map01Inventory>();
                bag.Draw(Map01Inventory.Slot.Primary);yield return WaitGameSeconds(.3f);yield return CheckToggle(WeaponKind.Rifle);
                bag.Draw(Map01Inventory.Slot.Pistol);yield return WaitGameSeconds(.3f);yield return CheckToggle(WeaponKind.Pistol);
                bag.Add("rifle_mosin",1);
                int cell=Enumerable.Range(0,Map01Inventory.BagSize).First(i=>bag.Cell(i)?.id=="rifle_mosin");
                Assert.IsTrue(bag.EquipFromCell(cell));bag.Draw(Map01Inventory.Slot.Primary);
                yield return WaitGameSeconds(.3f);yield return CheckToggle(WeaponKind.Sniper);
                rightDown=true;yield return WaitGameSeconds(.7f);Assert.IsTrue(Mission.ModernCombat.IsAiming);
                Mission.Cinematic=true;yield return WaitGameSeconds(.2f);Assert.IsFalse(Mission.ModernCombat.IsAiming);
                Mission.Cinematic=false;yield return WaitGameSeconds(.5f);
                Assert.IsFalse(Mission.ModernCombat.IsAiming,"Held input cannot reactivate aim after an interruption.");
                rightDown=false;yield return WaitGameSeconds(.2f);
                bag.Draw(Map01Inventory.Slot.Knife);rightDown=true;yield return WaitGameSeconds(.3f);
                Assert.IsFalse(Mission.ModernCombat.IsAiming,"A blade cannot enter gun aim mode.");
            }
            finally
            {
                InputSystem.onBeforeUpdate-=PumpMouse;InputSystem.RemoveDevice(mouse);mouse=null;
                RestoreInputRouting(routing);
            }
            yield return new ExitPlayMode();
        }
    }
}
