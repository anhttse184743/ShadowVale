using System;
using System.IO;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class M16IntegrationTests : ForestSceneTestBase
    {
        [Test] public void EveryShuffledGroupHasSevenM16AndThreeAK()
        {
            for(int seed=1;seed<=40;seed++) for(int group=0;group<10;group++)
                Assert.AreEqual(7,Enumerable.Range(group*10,10).Count(i=>Map01M16.RifleAt(seed,i)==Map01M16.Id));
            Assert.AreNotEqual(string.Join(",",Enumerable.Range(0,20).Select(i=>Map01M16.RifleAt(1,i))),string.Join(",",Enumerable.Range(0,20).Select(i=>Map01M16.RifleAt(2,i))));
            CollectionAssert.IsEmpty(Map01Loot.Load().Problems());
            CollectionAssert.AreEqual(Map01Loot.Load().items.First(i=>i.code=="rifle_standard").salvage.Select(y=>(y.item,y.min,y.max)),Map01Loot.Load().items.First(i=>i.code==Map01M16.Id).salvage.Select(y=>(y.item,y.min,y.max)));
            Assert.NotNull(Map01ItemCatalog.Load().Model(Map01M16.Id)); Assert.NotNull(Map01ItemCatalog.Load().Model(Map01M16.Ammo));
            foreach(var entry in Map01ItemCatalog.Load().items) Assert.NotNull(Resources.Load<Texture2D>("Hud/ItemIcons/"+entry.id),entry.id);
        }
        static bool HasM16Drop(Map01Mission mission) => Map01Drops.For(mission).LaidGuns.SelectMany(p=>p.items).Any(i=>i.item_id==Map01M16.Id);
        static int CellOf(Map01Inventory inventory,string id) { for(int i=0;i<Map01Inventory.BagSize;i++) if(inventory.Cell(i)?.id==id)return i; return -1; }
        [UnityTest] public IEnumerator LootEquipCaliberReloadAndSaveRoundTrip()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            Map01Mission mission = null;
            float readyUntil = Time.realtimeSinceStartup + 15;
            do { yield return null; mission=Object.FindFirstObjectByType<Map01Mission>(); }
            while ((mission == null || mission.ModernCombat == null) && Time.realtimeSinceStartup < readyUntil);
            Assert.NotNull(mission,"Map 1 mission loaded"); Assert.IsTrue(mission.IsInitialized); Assert.NotNull(mission.ModernCombat,"Mission Start binds the player combat");
            var inv=mission.GetComponent<Map01Inventory>();var combat=mission.ModernCombat;
            var gun=combat.GetComponentsInChildren<Weapon>(true).First(w=>w.IsGun);float akDamage=gun.Damage;
            Assert.AreEqual("rifle_standard",inv.SlotItem(Map01Inventory.Slot.Primary),"A new player starts with an AK47");
            Assert.AreEqual(0,inv.Count(Map01M16.Id),"M16 is not part of the player's starting inventory");
            Assert.AreEqual("rifle_standard",gun.GetComponent<Map01RifleVisual>().ItemId,"The starting model also remains AK47");
            var guards=mission.Enemies.Where(e=>e!=null).OrderBy(e=>e.SaveId,StringComparer.Ordinal).Take(10).ToArray();
            Assert.AreEqual(7,guards.Count(e=>e.RifleItemId==Map01M16.Id));
            var guard=mission.Enemies.First(e=>e.RifleItemId==Map01M16.Id && e.enabled && e.gameObject.activeInHierarchy && !e.suppressOperationLoot);var logistics=Map01Logistics.For(mission);
            Assert.AreEqual(Map01M16.Id,logistics.GuardGuns(guard,"m16_test_gun").First().item_id);
            var loot=logistics.GuardPack(guard,"m16_test_pack");Assert.IsFalse(loot.Any(i=>i.item_id=="ammo_rifle"));Assert.IsTrue(loot.Any(i=>i.item_id==Map01M16.Ammo));
            guard.GetComponent<Health>().CinematicInvulnerable=false; guard.DebugDamage(2); Assert.IsFalse(guard.Alive);
            float lootUntil=Time.realtimeSinceStartup+3; while(!HasM16Drop(mission)&&Time.realtimeSinceStartup<lootUntil)yield return null;
            Assert.IsTrue(HasM16Drop(mission),"A killed M16 guard leaves his M16 on the ground");
            combat.RestoreMagazine(17);int akReserve=inv.Count("ammo_rifle");inv.Add(Map01M16.Id,1,20);inv.Add(Map01M16.Ammo,60);
            int cell=CellOf(inv,Map01M16.Id);
            Assert.IsTrue(inv.EquipFromCell(cell));Assert.IsTrue(inv.Draw(Map01Inventory.Slot.Primary));
            Assert.AreEqual(akReserve+17,inv.Count("ammo_rifle"));Assert.AreEqual(0,combat.RoundsInMagazine);
            Assert.AreEqual(60,combat.ReserveRounds());Assert.AreEqual(Map01M16.Ammo,inv.PrimaryAmmo);
            Assert.AreEqual(akDamage*.9f,gun.Damage,.001f);Assert.AreEqual(60f/700f,gun.Cooldown,.00001f);
            Assert.IsTrue(combat.TryReload());float deadline=Time.time+6;while(combat.IsReloading&&Time.time<deadline)yield return null;
            Assert.AreEqual(30,combat.RoundsInMagazine);Assert.AreEqual(30,inv.Count(Map01M16.Ammo));Assert.AreEqual(akReserve+17,inv.Count("ammo_rifle"));
            var items=inv.CaptureItems();var layout=inv.CaptureLayout();
            inv.RestoreFromSave(items,inv.Stones,null,0);inv.RestoreLayout(layout);inv.ApplyPrimaryRifle(false);combat.RestoreMagazine(30);
            Assert.AreEqual(Map01M16.Id,inv.SlotItem(Map01Inventory.Slot.Primary));Assert.AreEqual(20,inv.Condition(Map01Inventory.Slot.Primary));Assert.AreEqual(30,inv.Count(Map01M16.Ammo));
            var yields=inv.SalvageYield(Map01Inventory.Gun.InSlot(Map01Inventory.Slot.Primary));Assert.IsTrue(yields.Any(d=>d.item=="scrap_metal"));Assert.IsTrue(yields.Any(d=>d.item=="weapon_parts"));
            mission.SetInventoryOpen(true);yield return null;
            ScreenCapture.CaptureScreenshot("Tools/M16Reports/inventory.png");yield return null;
            mission.SetInventoryOpen(false);
            yield return null;
            ScreenCapture.CaptureScreenshot("Tools/M16Reports/m16-hud.png");yield return null;
            yield return new ExitPlayMode();
        }
    }
    [InitializeOnLoad] public static class M16TestRunner
    {
        const string Key="ShadowVale.M16Tests";static TestRunnerApi runner;
        static M16TestRunner(){EditorApplication.update+=Tick;EditorApplication.delayCall+=Reconnect;}
        static void Reconnect(){if(SessionState.GetBool(Key,false))Register();}
        static void Register(){if(runner!=null)return;runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results());}
        static void Tick()
        {
            if(!File.Exists("Tools/M16Tests.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete("Tools/M16Tests.request");SessionState.SetBool(Key,true);Register();
            runner.Execute(new ExecutionSettings(new Filter{testMode=UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,testNames=new[]{"ShadowVale.Map01.Tests.M16IntegrationTests","ShadowVale.Map01.Tests.LootRollerTests"}}));
        }
        class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor tests){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){if(!result.HasChildren)File.AppendAllText("Tools/M16Reports/tests.txt",result.FullName+": "+result.TestStatus+" "+result.Message+"\n");}
            public void RunFinished(ITestResultAdaptor result){TestRunnerApi.SaveResultToFile(result,Path.GetFullPath("Tools/M16Reports/results.xml"));SessionState.SetBool(Key,false);}
        }
    }
}
