using System.Collections;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map02M16SaveCompatibilityTests : ForestSceneTestBase
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator SavedM16KeepsItsModelAndCaliberWithoutTheArrivalObject()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
            yield return new EnterPlayMode();
            yield return CheckRoundTrip();
            yield return new ExitPlayMode();
        }

        static IEnumerator CheckRoundTrip()
        {
            yield return Ready();
            var mission = Object.FindFirstObjectByType<Map02Mission>();
            Assert.IsNull(Object.FindFirstObjectByType<Map02Arrival>());
            var combat = mission.Player.GetComponent<PlayerCombat>();
            var gun = combat.WeaponFor(WeaponKind.Rifle);
            Assert.NotNull(gun);
            mission.Bag.Add(Map01M16.Id, 1);
            Assert.IsTrue(mission.Bag.EquipItem(Map01M16.Id));
            Assert.AreEqual(Map01M16.Id, gun.GetComponent<Map01RifleVisual>()?.ItemId);
            combat.RestoreMagazine(WeaponKind.Rifle, 9);
            mission.Bag.Spend("ammo_rifle", mission.Bag.Count("ammo_rifle")); mission.Bag.Add("ammo_rifle", 13);
            mission.Bag.Spend(Map01M16.Ammo, mission.Bag.Count(Map01M16.Ammo)); mission.Bag.Add(Map01M16.Ammo, 55);
            Assert.AreEqual(55, combat.ReserveRounds(), "M16 must use its own caliber even without an arrival object.");
            Assert.AreEqual(7, combat.DrawRounds(7));
            Assert.AreEqual(48, mission.Inventory[Map01M16.Ammo]);
            Assert.AreEqual(13, mission.Inventory["ammo_rifle"]);
            Assert.IsTrue(mission.SaveSlot(2, out string error), error);
            var saved = ForestSaveSlots.Read(2);
            Map02Mission.BeginGame(saved);
            yield return null;
            yield return Ready();
            mission = Object.FindFirstObjectByType<Map02Mission>();
            Assert.IsNull(Object.FindFirstObjectByType<Map02Arrival>());
            combat = mission.Player.GetComponent<PlayerCombat>();
            gun = combat.WeaponFor(WeaponKind.Rifle);
            Assert.AreEqual(Map01M16.Id, gun.GetComponent<Map01RifleVisual>()?.ItemId);
            Assert.AreEqual(1, gun.GetComponentsInChildren<MagazineReloadProfile>().Length);
            Assert.AreEqual(WeaponKind.Rifle, combat.EquippedKind);
            Assert.AreEqual(9, combat.RoundsInMagazine);
            Assert.AreEqual(48, combat.ReserveRounds());
            Assert.AreEqual(13, mission.Inventory["ammo_rifle"]);

            string legacyJson = saved.checkpoint.Replace("\"rifleItemId\":\"rifle_m16\",", "");
            var legacy = Map02Mission.Parse(legacyJson);
            mission.Restore(legacy);
            Assert.AreEqual("rifle_standard", gun.GetComponent<Map01RifleVisual>().ItemId, "Older Map 2 saves retain the original AK fallback.");
            Assert.AreEqual(13, combat.ReserveRounds());
        }

        static IEnumerator Ready()
        {
            float until = Time.realtimeSinceStartup + 45;
            while (Time.realtimeSinceStartup < until)
            {
                var mission = Object.FindFirstObjectByType<Map02Mission>();
                if (mission != null && mission.Ready) yield break;
                yield return null;
            }
            Assert.Fail("Map 2 did not become ready.");
        }
    }
}
