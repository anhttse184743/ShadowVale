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
        [TestCase("rifle_standard", "ammo_rifle")]
        [TestCase("rifle_m16", "ammo_556")]
        public void BackpackCaliberMatchesRifleAndKeepsK54(string rifle, string ammo)
        {
            var oldPack = new[] {
                new ForestIngredient { item_id="ammo_rifle",count=6 },
                new ForestIngredient { item_id="ammo_556",count=4 },
                new ForestIngredient { item_id="ammo_sniper",count=3 },
                new ForestIngredient { item_id="ammo_pistol",count=8 },
                new ForestIngredient { item_id="cloth",count=2 },
            };
            var pack=Map01Logistics.MatchGuardAmmo(rifle,oldPack);
            Assert.AreEqual(13,pack.Single(i=>i.item_id==ammo).count);
            Assert.AreEqual(8,pack.Single(i=>i.item_id=="ammo_pistol").count,"K-54 rounds remain in the pack");
            Assert.AreEqual(2,pack.Single(i=>i.item_id=="cloth").count);
            Assert.AreEqual(3,pack.Length);
            Assert.AreEqual("ammo_rifle",oldPack[0].item_id,"Saved input is not mutated");
            CollectionAssert.AreEqual(pack.Select(i=>(i.item_id,i.count)),Map01Logistics.MatchGuardAmmo(rifle,pack).Select(i=>(i.item_id,i.count)));
        }
        [Test] public void M16HasAmmoIconAndSharesAkFoundConditionLimits()
        {
            var icon=Resources.Load<Texture2D>("Hud/ItemIcons/"+Map01M16.Ammo);
            Assert.NotNull(icon);Assert.Greater(icon.width,0);Assert.Greater(icon.height,0);
            Assert.AreNotSame(Resources.Load<Texture2D>("Hud/ItemIcons/ammo_rifle"),icon);
            Assert.AreNotEqual(ForestInventory.Name(Map01M16.Ammo),ForestInventory.Name("ammo_rifle"));
            var data=Map01Loot.Load();var ak=data.ItemOf("rifle_standard");var m16=data.ItemOf(Map01M16.Id);
            Assert.AreEqual(ak.foundMin,m16.foundMin);Assert.AreEqual(ak.foundMax,m16.foundMax);
        }
        static void CheckConditionDistribution(Map01Mission mission,Map01EnemyController guard,Map01Logistics logistics)
        {
            var inv=mission.GetComponent<Map01Inventory>();
            Assert.AreEqual(inv.MaxCondition("rifle_standard"),inv.MaxCondition(Map01M16.Id));
            var kind=Map01Loot.Load().EnemyOf(Map01Logistics.KindOf(guard));
            var values=Enumerable.Range(0,200).Select(i=>logistics.GuardGuns(guard,"condition_check_"+i).First().condition/inv.MaxCondition(guard.RifleItemId)).ToArray();
            Assert.IsTrue(values.All(v=>v>=kind.dropMin&&v<=kind.dropMax));
            float midpoint=(kind.dropMin+kind.dropMax)*.5f;
            Assert.Less(Mathf.Abs(values.Average()-midpoint),.025f);
            Assert.IsTrue(values.Any(v=>v<midpoint)&&values.Any(v=>v>midpoint));
            Assert.AreEqual(values[0],logistics.GuardGuns(guard,"condition_check_0").First().condition/inv.MaxCondition(guard.RifleItemId));
        }
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
            var guard=mission.Enemies.First(e=>e.RifleItemId==Map01M16.Id && e.enabled && e.gameObject.activeInHierarchy && !e.suppressOperationLoot && !e.IsBoss);var logistics=Map01Logistics.For(mission);
            Assert.AreEqual(Map01M16.Id,logistics.GuardGuns(guard,"m16_test_gun").First().item_id);
            var loot=logistics.GuardPack(guard,"m16_test_pack");Assert.IsFalse(loot.Any(i=>i.item_id=="ammo_rifle"));Assert.IsTrue(loot.Any(i=>i.item_id==Map01M16.Ammo));
            Assert.IsTrue(loot.Where(i=>i.item_id.StartsWith("ammo_")).All(i=>i.item_id==Map01M16.Ammo||i.item_id=="ammo_pistol"));
            CheckConditionDistribution(mission,guard,logistics);
            var akGuard=mission.Enemies.First(e=>e.RifleItemId=="rifle_standard"&&e.enabled&&e.gameObject.activeInHierarchy&&!e.suppressOperationLoot&&!e.IsBoss);
            var akLoot=logistics.GuardPack(akGuard,"ak_test_pack");
            Assert.IsTrue(akLoot.Where(i=>i.item_id.StartsWith("ammo_")).All(i=>i.item_id=="ammo_rifle"||i.item_id=="ammo_pistol"));
            CheckConditionDistribution(mission,akGuard,logistics);
            var oldState=logistics.Capture();
            oldState.rolled.First(p=>p.point=="m16_test_pack").items=new[] {
                new ForestIngredient { item_id="ammo_rifle",count=5 },new ForestIngredient { item_id="ammo_pistol",count=4 }
            };
            logistics.Restore(oldState);
            var restoredPack=logistics.GuardPack(guard,"m16_test_pack");
            Assert.AreEqual(5,restoredPack.Single(i=>i.item_id==Map01M16.Ammo).count);
            Assert.AreEqual(4,restoredPack.Single(i=>i.item_id=="ammo_pistol").count);
            guard.GetComponent<Health>().CinematicInvulnerable=false; guard.DebugDamage(2); Assert.IsFalse(guard.Alive);
            float lootUntil=Time.realtimeSinceStartup+3; while(!HasM16Drop(mission)&&Time.realtimeSinceStartup<lootUntil)yield return null;
            Assert.IsTrue(HasM16Drop(mission),"A killed M16 guard leaves his M16 on the ground");
            var fallen=Map01Drops.For(mission).LaidGuns.First(p=>p.items.Any(i=>i.item_id==Map01M16.Id));
            Assert.NotNull(fallen.GetComponentInChildren<MagazineReloadProfile>(),"The ground pickup displays the real M16 model");
            akGuard.GetComponent<Health>().CinematicInvulnerable=false;akGuard.DebugDamage(2);
            float akUntil=Time.realtimeSinceStartup+3;while(!HasAkDrop(mission)&&Time.realtimeSinceStartup<akUntil)yield return null;
            Assert.IsTrue(HasAkDrop(mission),"An AK guard still leaves an AK on the ground");
            CheckRestoredDropModel(mission,fallen);
            combat.RestoreMagazine(17);int akReserve=inv.Count("ammo_rifle");inv.Add(Map01M16.Id,1,20);inv.Add(Map01M16.Ammo,60);
            int cell=CellOf(inv,Map01M16.Id);
            Assert.IsTrue(inv.EquipFromCell(cell));Assert.IsTrue(inv.Draw(Map01Inventory.Slot.Primary));
            Assert.AreEqual(akReserve+17,inv.Count("ammo_rifle"));Assert.AreEqual(0,combat.RoundsInMagazine);
            Assert.AreEqual(60,combat.ReserveRounds());Assert.AreEqual(Map01M16.Ammo,inv.PrimaryAmmo);
            Assert.AreEqual(Map01M16.Damage,gun.Damage,.001f);Assert.AreEqual(Map01M16.ShotSeconds,gun.Cooldown,.00001f);
            Assert.AreEqual(Map01M16.Range,gun.Range);Assert.IsTrue(gun.UsesM16Handling);
            yield return null;
            var carrier = combat.GetComponentInChildren<ReloadMagazineCarrier>(true);
            Assert.NotNull(carrier);
            Assert.IsTrue(carrier.Magazine.IsChildOf(gun.ReloadModel),"Reload controller switches to the M16 magazine while keeping the same weapon slot");
            Assert.IsTrue(combat.TryReload());float deadline=Time.time+6;while(combat.IsReloading&&Time.time<deadline)yield return null;
            Assert.AreEqual(30,combat.RoundsInMagazine);Assert.AreEqual(30,inv.Count(Map01M16.Ammo));Assert.AreEqual(akReserve+17,inv.Count("ammo_rifle"));
            var items=inv.CaptureItems();var layout=inv.CaptureLayout();
            inv.RestoreFromSave(items,inv.Stones,null,0);inv.RestoreLayout(layout);inv.ApplyPrimaryRifle(false);combat.RestoreMagazine(30);
            Assert.AreEqual(Map01M16.Id,inv.SlotItem(Map01Inventory.Slot.Primary));Assert.AreEqual(20,inv.Condition(Map01Inventory.Slot.Primary));Assert.AreEqual(30,inv.Count(Map01M16.Ammo));
            var yields=inv.SalvageYield(Map01Inventory.Gun.InSlot(Map01Inventory.Slot.Primary));Assert.IsTrue(yields.Any(d=>d.item=="scrap_metal"));Assert.IsTrue(yields.Any(d=>d.item=="weapon_parts"));
            mission.SetInventoryOpen(true);yield return null;
            ScreenCapture.CaptureScreenshot(M16TestRunner.ReportPath("inventory.png"));yield return null;
            mission.SetInventoryOpen(false);
            yield return null;
            ScreenCapture.CaptureScreenshot(M16TestRunner.ReportPath("m16-hud.png"));yield return null;
            yield return new ExitPlayMode();
        }
        static bool HasAkDrop(Map01Mission mission) => Map01Drops.For(mission).LaidGuns.SelectMany(p=>p.items).Any(i=>i.item_id=="rifle_standard");
        static void CheckRestoredDropModel(Map01Mission mission,ForestPoint original)
        {
            var drops=Map01Drops.For(mission);string id=original.id;var position=original.transform.position;
            float condition=original.items[0].condition;
            drops.Lay(id,new ForestIngredient { item_id="rifle_standard",count=1,condition=condition },position,0);
            var ak=drops.LaidGuns.Single(p=>p.id==id);
            Assert.AreEqual("rifle_standard",ak.items[0].item_id);Assert.IsNull(ak.GetComponentInChildren<MagazineReloadProfile>());
            drops.Lay(id,new ForestIngredient { item_id=Map01M16.Id,count=1,condition=condition },position,0);
            Assert.NotNull(drops.LaidGuns.Single(p=>p.id==id).GetComponentInChildren<MagazineReloadProfile>());
        }
    }
    [InitializeOnLoad] public static class M16TestRunner
    {
        const string Key="ShadowVale.M16Tests";static TestRunnerApi runner;
        public static string ReportPath(string file) => "Tools/M16Reports/"+SessionState.GetString(Key+".prefix","")+file;
        static M16TestRunner(){EditorApplication.update+=Tick;EditorApplication.delayCall+=Reconnect;}
        static void Reconnect(){if(SessionState.GetBool(Key,false))Register();}
        static void Register(){if(runner!=null)return;runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results());}
        static void Tick()
        {
            if(!File.Exists("Tools/M16Tests.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            bool lootOnly=File.ReadAllText("Tools/M16Tests.request").Trim()=="loot";
            File.Delete("Tools/M16Tests.request");SessionState.SetString(Key+".prefix",lootOnly?"loot-check-":"");SessionState.SetBool(Key,true);Register();
            var names=lootOnly ? new[]{"ShadowVale.Map01.Tests.M16IntegrationTests","ShadowVale.Map01.Tests.LootRollerTests"}
                : new[]{"ShadowVale.Map01.Tests.M16IntegrationTests","ShadowVale.Map01.Tests.LootRollerTests","ShadowVale.Map01.Tests.ReloadMagazineTests"};
            runner.Execute(new ExecutionSettings(new Filter{testMode=UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,testNames=names}));
        }
        class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor tests){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){if(!result.HasChildren)File.AppendAllText(ReportPath("tests.txt"),result.FullName+": "+result.TestStatus+" "+result.Message+"\n");}
            public void RunFinished(ITestResultAdaptor result){TestRunnerApi.SaveResultToFile(result,Path.GetFullPath(ReportPath("results.xml")));SessionState.SetBool(Key,false);}
        }
    }
}
