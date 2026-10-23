using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02CharacterHudTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(240000)]public IEnumerator SharedBagSkillsWeaponsAndDiscoverySurviveMapTwoSaveLoad()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null&&m.Bag!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Bag);Assert.NotNull(m.CharacterHud);Assert.AreEqual(0,Object.FindObjectsByType<Map01Mission>(FindObjectsSortMode.None).Length);
            var preview=Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyToMission");preview.Invoke(null,new object[]{m,2});
            Debug.Log("[SharedHUD] Preview ready in "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+" player="+m.Player);
            yield return null;
            Assert.IsTrue(m!=null,"Preview unexpectedly unloaded its mission.");
            Assert.IsNotNull(m.Player,"Preview lost its player in scene "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            var bag=m.Bag;
            Assert.NotNull(bag);var combat=m.Player.GetComponent<PlayerCombat>();Assert.NotNull(combat);
            bag.Receive("cloth",8);bag.Receive("herb",4);bag.Receive("repair_kit",2);bag.Receive("pistol_k54",1);bag.Receive("ammo_pistol",12);

            Assert.IsTrue(bag.Split(FindCell(bag,"cloth")));Assert.IsTrue(bag.Move(FindCell(bag,"cloth"),23));var layout=bag.CaptureLayout();
            bag.Wear(Map01Inventory.Slot.Primary,bag.MaxCondition(bag.SlotItem(Map01Inventory.Slot.Primary))*.7f);
            float worn=bag.Condition(Map01Inventory.Slot.Primary);Assert.IsTrue(bag.FieldRepair(Map01Inventory.Gun.InSlot(Map01Inventory.Slot.Primary)),bag.FieldRepairRefusal(Map01Inventory.Gun.InSlot(Map01Inventory.Slot.Primary)));
            Assert.AreEqual(1,bag.Count("repair_kit"));Assert.Greater(bag.Condition(Map01Inventory.Slot.Primary),worn);
            Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Pistol));combat.RestoreMagazine(3);
            var snapshot=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));snapshot.stamina=7.5f;
            var far=m.Chapter.Sites["dock"].position;m.Exploration.Reveal(far,14);snapshot.exploration=m.Exploration.Capture();
            float condition=bag.Condition(Map01Inventory.Slot.Primary);m.Restore(snapshot);
            Assert.AreEqual(WeaponKind.Pistol,combat.EquippedKind);Assert.AreEqual(3,combat.RoundsInMagazine);Assert.AreEqual(12,bag.Count("ammo_pistol"));
            Assert.AreEqual(7.5f,m.Stamina);Assert.AreEqual(condition,bag.Condition(Map01Inventory.Slot.Primary));Assert.AreEqual(layout.ids[23],bag.Cell(23)?.id);
            Assert.IsTrue(m.IsExplored(far));Assert.IsFalse(m.IsExplored(new Vector3(105,0,105)));
            int before=bag.Count("cloth");Assert.IsTrue(bag.Drop(FindCell(bag,"cloth"),1));Assert.AreEqual(before-1,bag.Count("cloth"));
            snapshot=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(snapshot);Assert.AreEqual(1,m.GroundItems.Count);
            Assert.IsTrue(m.PickupCharacterItem(m.GroundItems[0]));Assert.AreEqual(before,bag.Count("cloth"));Assert.AreEqual(0,m.GroundItems.Count);
            // A removed primary must stay absent across load, rather than legacy weapon migration respawning it.
            Assert.IsTrue(bag.DropSlot(Map01Inventory.Slot.Primary));snapshot=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(snapshot);
            Assert.IsNull(bag.SlotItem(Map01Inventory.Slot.Primary));Assert.AreEqual(1,m.GroundItems.Count);
            Assert.IsTrue(m.PickupCharacterItem(m.GroundItems[0]));Assert.IsNotNull(bag.SlotItem(Map01Inventory.Slot.Primary));
            Assert.IsFalse(bag.TryCraft("craft_medkit_small"),"Crafting must require the actual workbench.");
            Place(m,m.CharacterWorkpoints[0].transform.position);Assert.IsTrue(bag.TryCraft("craft_medkit_small"));
            var craft=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(craft);Assert.IsTrue(bag.IsCrafting);Assert.AreEqual(craft.crafting,bag.CraftingId);
            Directory.CreateDirectory("Logs/Map02Chapter/SharedHud");
            Place(m,m.Chapter.Sites["command"].position+Vector3.back*4);m.SetCharacterPanel(0);bag.Draw(Map01Inventory.Slot.Primary);
            bag.Receive("stone",2);bag.SelectThrowable("stone");var throwing=m.GetComponent<Map01StoneThrow>();
            var guardHost=new GameObject("Shared HUD hearing test");guardHost.transform.position=m.Chapter.Sites["command"].position;
            var guard=guardHost.AddComponent<Map02ChapterEnemy>();guard.Initialize(m.Chapter,"hud_hearing","hud_test",100,false);guard.enabled=false;m.Chapter.Enemies.Add(guard);
            Assert.IsTrue(throwing.ThrowAt(guardHost.transform.position));Assert.AreEqual(1,bag.Stones);Assert.Greater(throwing.WouldHear,0);
            m.Chapter.Enemies.Remove(guard);Object.Destroy(guardHost);bag.Draw(Map01Inventory.Slot.Primary);
            Assert.NotNull(m.CharacterLowHealth);
            m.Player.GetComponent<Health>().RestoreHealth(10);yield return WaitGameSeconds(.8f);
            Assert.Greater(m.CharacterLowHealth.Opacity,.2f);Assert.IsTrue(m.CharacterCrosshairVisible);
            ScreenCapture.CaptureScreenshot("Logs/Map02Chapter/SharedHud/low-health.png");yield return null;
            m.Player.GetComponent<Health>().RestoreHealth(100);
            yield return WaitGameSeconds(6);if(!m.CharacterHud.ObjectiveShown)m.CharacterHud.ToggleObjective();yield return WaitGameSeconds(.4f);ScreenCapture.CaptureScreenshot("Logs/Map02Chapter/SharedHud/gameplay.png");yield return null;
            m.SetCharacterPanel(1);yield return null;Assert.IsFalse(m.CharacterCrosshairVisible);ScreenCapture.CaptureScreenshot("Logs/Map02Chapter/SharedHud/bag.png");yield return null;
            typeof(Map01Hud).GetField("skillsTab",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(m.CharacterHud,true);
            yield return null;ScreenCapture.CaptureScreenshot("Logs/Map02Chapter/SharedHud/skills.png");yield return null;
            m.SetCharacterPanel(3);yield return null;ScreenCapture.CaptureScreenshot("Logs/Map02Chapter/SharedHud/map.png");yield return null;
            m.SetCharacterPanel(0);preview.Invoke(null,new object[]{m,3});yield return null;
            m.Chapter.HoldBinoculars(true);yield return WaitGameSeconds(6);Assert.IsFalse(m.CharacterCrosshairVisible);ScreenCapture.CaptureScreenshot("Logs/Map02Chapter/SharedHud/night-binoculars.png");yield return null;
            m.Chapter.HoldBinoculars(false);yield return new ExitPlayMode();
        }
        static int FindCell(Map01Inventory bag,string id)=>Enumerable.Range(0,Map01Inventory.BagSize).First(i=>bag.Cell(i)?.id==id);
        static void Place(Map02Mission m,Vector3 p){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=p;cc.enabled=true;m.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>().RestoreMotion(false);Physics.SyncTransforms();}
    }
}
