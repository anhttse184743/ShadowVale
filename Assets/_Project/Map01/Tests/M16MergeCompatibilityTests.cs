using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class M16MergeCompatibilityTests:ForestSceneTestBase
    {
        [Test]
        public void CloningAnM16ThenSelectingAkRestoresOneCorrectModelAndItsBaseline()
        {
            var original=Object.Instantiate(Map01ActorAssets.Load().rifle.gameObject);
            GameObject copy=null;
            try
            {
                var gun=original.GetComponent<Weapon>();float damage=gun.Damage,cooldown=gun.Cooldown;
                Map01RifleVisual.Apply(gun,Map01M16.Id);
                Assert.AreEqual(1,original.GetComponentsInChildren<MagazineReloadProfile>().Length);
                copy=Object.Instantiate(original);var clone=copy.GetComponent<Weapon>();
                Map01RifleVisual.Apply(clone,"rifle_standard");
                Assert.AreEqual(damage,clone.Damage,.0001f);
                Assert.AreEqual(cooldown,clone.Cooldown,.0001f);
                Assert.AreEqual(0,copy.GetComponentsInChildren<MagazineReloadProfile>().Length,"An AK clone must not keep its template's M16 visible.");
                Map01RifleVisual.Apply(clone,Map01M16.Id);
                Assert.AreEqual(1,copy.GetComponentsInChildren<MagazineReloadProfile>(true).Length,"Switching a clone must reuse the one model.");
                Assert.AreEqual(damage*.9f,clone.Damage,.0001f);
            }
            finally {if(copy!=null)Object.DestroyImmediate(copy);Object.DestroyImmediate(original);}
        }

        [UnityTest]
        public IEnumerator PrimaryChangesPreserveK54MosinAndTheReinforcedGarrisonsSavedRifles()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return WaitForMap01Ready();
            yield return ReviewCompatibility();
            yield return new ExitPlayMode();
        }

        static IEnumerator ReviewCompatibility()
        {
            var mission=Object.FindFirstObjectByType<Map01Mission>();
            var inventory=mission.GetComponent<Map01Inventory>();var combat=mission.ModernCombat;
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();mission.ModernHealth.CinematicInvulnerable=true;
            foreach(var guard in mission.Enemies)BodyDiscoveryTests.Hold(guard,true);
            float ready=Time.time+20;
            while((combat.WeaponFor(WeaponKind.Pistol)==null||combat.WeaponFor(WeaponKind.Sniper)==null)&&Time.time<ready)yield return null;
            Assert.NotNull(combat.WeaponFor(WeaponKind.Pistol));Assert.NotNull(combat.WeaponFor(WeaponKind.Sniper));
            if(inventory.Count("pistol_k54")==0)inventory.Add("pistol_k54",1);
            inventory.Add("rifle_mosin",1);inventory.Add(Map01M16.Id,1);inventory.Add(Map01M16.Ammo,60);
            combat.RestoreMagazine(WeaponKind.Rifle,17);combat.RestoreMagazine(WeaponKind.Pistol,5);combat.RestoreMagazine(WeaponKind.Sniper,3);
            Assert.IsTrue(inventory.Draw(Map01Inventory.Slot.Pistol));int akReserve=inventory.Count("ammo_rifle");
            Assert.IsTrue(inventory.EquipFromCell(CellOf(inventory,Map01M16.Id)));
            Assert.AreEqual(5,combat.MagazineRounds(WeaponKind.Pistol));Assert.AreEqual(3,combat.MagazineRounds(WeaponKind.Sniper));
            Assert.AreEqual(0,combat.MagazineRounds(WeaponKind.Rifle));Assert.AreEqual(akReserve+17,inventory.Count("ammo_rifle"));
            Assert.IsTrue(inventory.Draw(Map01Inventory.Slot.Primary));
            Assert.AreEqual(Map01M16.Ammo,inventory.HeldAmmoId);combat.RestoreMagazine(WeaponKind.Rifle,9);
            var c3=mission.GetComponent<Map01C3Mission>();int originalCount=mission.Enemies.Length;
            c3.CallReinforcements();float wakeUntil=Time.time+15;
            while(mission.Enemies.Length<originalCount+3&&Time.time<wakeUntil)yield return null;
            Assert.GreaterOrEqual(mission.Enemies.Length,originalCount+3,"Save after real sleeper activation changes the roster.");
            foreach(var guard in mission.Enemies)BodyDiscoveryTests.Hold(guard,true);
            var rifles=mission.Enemies.ToDictionary(e=>e.SaveId,e=>e.RifleItemId);
            float idleUntil=Time.time+5;while(Map01NamActions.For(mission).Busy&&Time.time<idleUntil)yield return null;
            Assert.IsTrue(mission.GetComponent<Map01SaveSystem>().SaveSlot(2,out var error,true),error);
            Map01SaveSystem.BeginGame(2);
            yield return null;float loadedUntil=Time.realtimeSinceStartup+40;
            while((Object.FindFirstObjectByType<Map01Mission>()==null||Map01SaveSystem.IsRestoring)&&Time.realtimeSinceStartup<loadedUntil)yield return null;
            mission=Object.FindFirstObjectByType<Map01Mission>();Assert.NotNull(mission);Assert.IsTrue(mission.IsInitialized);
            inventory=mission.GetComponent<Map01Inventory>();combat=mission.ModernCombat;
            Assert.AreEqual(Map01M16.Id,inventory.SlotItem(Map01Inventory.Slot.Primary));Assert.AreEqual(WeaponKind.Rifle,combat.EquippedKind);
            Assert.AreEqual(9,combat.MagazineRounds(WeaponKind.Rifle));Assert.AreEqual(5,combat.MagazineRounds(WeaponKind.Pistol));Assert.AreEqual(3,combat.MagazineRounds(WeaponKind.Sniper));
            foreach(var saved in rifles)
            {
                var guard=mission.Enemies.Single(e=>e.SaveId==saved.Key);
                Assert.AreEqual(saved.Value,guard.RifleItemId,saved.Key+" must keep its saved rifle after reinforcement changes the roster.");
                BodyDiscoveryTests.Hold(guard,true);
            }
            var legacyGuard=mission.Enemies.First(e=>e.Alive&&!e.IsBoss);
            var current=legacyGuard.Capture();
            string oldJson=JsonUtility.ToJson(current).Replace("\"rifleItemId\":\""+current.rifleItemId+"\",","");
            var legacy=JsonUtility.FromJson<Map01EnemyController.Snapshot>(oldJson);
            Assert.IsNull(legacy.rifleItemId,"Exercise an actual snapshot without the new optional field.");
            legacyGuard.RestoreSnapshot(legacy);
            Assert.AreEqual(Map01M16.EnemyRifle(mission,legacyGuard),legacyGuard.RifleItemId,"Old snapshots retain the seeded fallback.");
            legacyGuard.RestoreSnapshot(current);
            Assert.AreEqual(current.rifleItemId,legacyGuard.RifleItemId);
            int reserve=inventory.Count(Map01M16.Ammo);
            Assert.IsTrue(inventory.EquipFromCell(CellOf(inventory,"rifle_mosin")));Assert.IsTrue(inventory.Draw(Map01Inventory.Slot.Primary));
            Assert.AreEqual(WeaponKind.Sniper,combat.EquippedKind);Assert.AreEqual("ammo_sniper",inventory.HeldAmmoId);
            Assert.AreEqual(reserve+9,inventory.Count(Map01M16.Ammo));Assert.AreEqual(3,combat.RoundsInMagazine);
            Assert.IsTrue(inventory.Draw(Map01Inventory.Slot.Pistol));Assert.AreEqual("ammo_pistol",inventory.HeldAmmoId);Assert.AreEqual(5,combat.RoundsInMagazine);
        }

        static int CellOf(Map01Inventory inventory,string id)
        {
            for(int i=0;i<Map01Inventory.BagSize;i++)if(inventory.Cell(i)?.id==id)return i;
            Assert.Fail("Missing bag item "+id);return -1;
        }
    }
}
