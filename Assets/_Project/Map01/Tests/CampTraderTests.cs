using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    public sealed class CampTraderTests
    {
        static void Tick(CampTrader trader) => typeof(CampTrader).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trader, null);

        [Test]
        public void LogisticsRewardsCoverEachFeaturedWeaponAndCannotBeClaimedTwice()
        {
            var loot = Map01Loot.Load();
            var logistics = loot.logistics;
            Assert.AreEqual(loot.BaseValue("rifle_mosin"), logistics.operationCredit.Take(3).Sum());
            var completed = new HashSet<string> { "D1_06", "N1_04", "D2_07", "N2_05", "D3_06" };
            var claimed = new HashSet<string>();
            int earned = logistics.ClaimMapTwoRewards(completed.Contains, claimed);
            Assert.AreEqual(75, earned);
            Assert.GreaterOrEqual(earned, loot.BaseValue(Map01M16.Id));
            var saved = new Map02Mission.Snapshot { logisticsCredit = earned - loot.BaseValue(Map01M16.Id), claimedLogisticsRewards = claimed.ToArray() };
            var loaded = JsonUtility.FromJson<Map02Mission.Snapshot>(JsonUtility.ToJson(saved));
            Assert.AreEqual(15, loaded.logisticsCredit);
            claimed = new HashSet<string>(loaded.claimedLogisticsRewards);
            Assert.AreEqual(0, logistics.ClaimMapTwoRewards(completed.Contains, claimed));
            completed.Add("N3_09");
            Assert.AreEqual(35, logistics.ClaimMapTwoRewards(completed.Contains, claimed));
        }

        [TestCase("m1_c3_mosin_order", "quân nhu")]
        [TestCase("camp_trade_greeting", "Mosin")]
        [TestCase("camp_trade_night", "M mười sáu")]
        public void TradeVoiceLoadsAndMatchesTheDisplayedLine(string id, string phrase)
        {
            var line = DialogueVoice.Find(id);
            Assert.NotNull(line?.clip);
            StringAssert.Contains(phrase, line.caption);
            Assert.Greater(line.clip.length, 1f);
        }

        [TestCase(false, "rifle_mosin", "ammo_sniper", 5)]
        [TestCase(true, "rifle_m16", "ammo_556", 20)]
        public void PurchasesAndSalesRequireAnOpenTraderAndKeepBagAndCreditInSync(
            bool mapTwo, string weapon, string ammo, int rounds)
        {
            var host = new GameObject("Camp trader test");
            host.transform.position = new Vector3(9000, 0, 9000);
            try
            {
                var bag = host.AddComponent<Map01Inventory>();
                bag.Add("rifle_standard", 2, 25);
                bag.Add("scrap_metal", 2);
                var spare = bag.Guns().Single(g => g.Slot == null);
                bool available = false, open = false;
                int credit = 0;
                var trader = CampTrader.Install(host, bag, mapTwo, () => available, () => open,
                    () => host.transform, () => host.transform.position, () => credit, delta => credit += delta);

                Tick(trader);
                Assert.AreEqual(0, host.transform.childCount);
                trader.SellMaterial("scrap_metal");
                Assert.AreEqual(2, bag.Count("scrap_metal"));

                available = open = true;
                Tick(trader);
                Assert.IsTrue(trader.Near);
                trader.Buy(1);
                Assert.AreEqual(0, bag.Count(weapon));
                trader.SellMaterial("scrap_metal");
                Assert.AreEqual(1, bag.Count("scrap_metal"));
                int gunValue = trader.GunValue(spare);
                trader.SellGun(spare);
                Assert.AreEqual(gunValue + 1, credit);
                Assert.AreEqual(1, bag.Count("rifle_standard"));

                int price = trader.Stock[1].Price;
                credit = price + 4;
                trader.Buy(1);
                Assert.AreEqual(4, credit);
                Assert.AreEqual(rounds, bag.Count(ammo));
                var issued = bag.Guns().Single(g => bag.IdOf(g) == weapon);
                Assert.AreEqual(1f, bag.FractionOf(issued));
                Assert.Less(trader.GunValue(issued), price);
                Assert.AreEqual(0, trader.GunValue(Map01Inventory.Gun.InSlot(Map01Inventory.Slot.Primary)));
                bag.Add("cloth", Map01Inventory.BagSize * bag.StackLimit("cloth"));
                credit = price;
                trader.Buy(1);
                Assert.AreEqual(price, credit);
                Assert.AreEqual(1, bag.Count(weapon));
                Assert.AreEqual(rounds, bag.Count(ammo));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
