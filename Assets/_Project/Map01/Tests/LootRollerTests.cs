using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Skills;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// Map 1's loot data and its roller (Map01Loot.cs), without a scene: the shipped Map01Loot.json passes the content
    /// database's constraints; tiers come up 60/26/11/3; an entry comes up by its weight within its tier, and twice as
    /// often when boosted; tier 5 is always given; the same seed rolls the same; the guns carry durability in the
    /// content bundle; and saved data written before a field existed keeps that field's default.
    /// </summary>
    public sealed class LootRollerTests
    {
        private static Map01Loot Data => Map01Loot.Load();
        private static Map01Loot.Entry E(string item, int tier, float weight, int min = 1, int max = 1) =>
            new Map01Loot.Entry { item = item, tier = tier, weight = weight, min = min, max = max };
        private static string Describe(List<Map01LootRoller.Drop> drops) => string.Join(",", drops.Select(d => d.item + "×" + d.count));

        [Test]
        public void TheShippedDataPassesTheDatabaseConstraints()
        {
            CollectionAssert.IsEmpty(Data.Problems());
            // Every item the tables give exists in the game, with a name of its own.
            foreach (var item in Data.lootTables.SelectMany(t => t.entries).Select(e => e.item).Distinct())
                Assert.AreNotEqual(item, ForestInventory.Name(item), item + " has no name.");
            // A broken table is refused as the database would refuse it.
            var bad = JsonUtility.FromJson<Map01Loot>(JsonUtility.ToJson(Data));
            bad.lootTables[0].entries[0].tier = 6;
            bad.lootTables[1].entries[0].weight = 0;
            Assert.AreEqual(2, bad.Problems().Count);
        }

        [Test]
        public void TiersComeUpSixtyTwentySixElevenThree()
        {
            var counts = new int[5];
            var random = new System.Random(7);
            const int n = 100000;
            for (int i = 0; i < n; i++) counts[Map01LootRoller.PickTier(Data.tierOdds, random.NextDouble())]++;
            Assert.AreEqual(.60, counts[1] / (double)n, .01);
            Assert.AreEqual(.26, counts[2] / (double)n, .01);
            Assert.AreEqual(.11, counts[3] / (double)n, .01);
            Assert.AreEqual(.03, counts[4] / (double)n, .005);
        }

        [Test]
        public void WithinATierAnEntryComesUpByItsWeightAndTwiceAsOftenWhenBoosted()
        {
            var data = JsonUtility.FromJson<Map01Loot>(JsonUtility.ToJson(Data));
            data.tierOdds = new[] { 100f, 0f, 0f, 0f };
            var table = new Map01Loot.Table { code = "t", rollsMin = 1, rollsMax = 1, entries = new[] { E("a", 1, 50), E("b", 1, 25), E("c", 1, 25) } };
            Assert.AreEqual(.5, Share(data, table, "a", null), .015);
            Assert.AreEqual(.25, Share(data, table, "b", null), .015);
            // Rounds when Nam is short of them: weight × 2 → 100 / 150.
            Assert.AreEqual(2 / 3.0, Share(data, table, "a", item => item == "a" ? 2f : 1f), .015);
        }

        [Test]
        public void ATierTheTableLacksFallsToTheNearestLowerOne()
        {
            // Tiers 1 and 3 only: tier 2 rolls fall to 1 (60+26), tier 4 to 3 (11+3).
            var table = new Map01Loot.Table { code = "t", rollsMin = 1, rollsMax = 1, entries = new[] { E("common", 1, 1), E("rare", 3, 1) } };
            Assert.AreEqual(.86, Share(Data, table, "common", null), .015);
            Assert.AreEqual(.14, Share(Data, table, "rare", null), .015);
        }

        private static double Share(Map01Loot data, Map01Loot.Table table, string item, Map01LootRoller.Boost boost)
        {
            var random = new System.Random(11);
            const int n = 40000;
            int hits = 0;
            for (int i = 0; i < n; i++) if (Map01LootRoller.Pick(data, table, random, boost).item == item) hits++;
            return hits / (double)n;
        }

        [Test]
        public void EveryTableGivesItsTierFiveAndOnlyItsOwnItemsInRange()
        {
            foreach (var table in Data.lootTables)
                for (int seed = 0; seed < 300; seed++)
                {
                    var drops = Map01LootRoller.Roll(Data, table, seed);
                    foreach (var e in table.entries.Where(e => e.tier == 5))
                        Assert.GreaterOrEqual(drops.Where(d => d.item == e.item).Sum(d => d.count), e.min, table.code + " always gives " + e.item);
                    foreach (var d in drops)
                    {
                        var mine = table.entries.Where(e => e.item == d.item).ToList();
                        Assert.IsNotEmpty(mine, table.code + " gave " + d.item);
                        int most = mine.Where(e => e.tier == 5).Sum(e => e.max) + table.rollsMax * mine.Where(e => e.tier < 5).Select(e => e.max).DefaultIfEmpty(0).Max();
                        Assert.LessOrEqual(d.count, most, table.code + " " + d.item);
                    }
                }
        }

        [Test]
        public void TheSameSeedRollsTheSameAndOtherSeedsVary()
        {
            var table = Data.TableOf("lt_m1_camp_guard");
            for (int seed = 1; seed < 50; seed++)
                Assert.AreEqual(Describe(Map01LootRoller.Roll(Data, table, seed)), Describe(Map01LootRoller.Roll(Data, table, seed)));
            Assert.Greater(Enumerable.Range(1, 60).Select(s => Describe(Map01LootRoller.Roll(Data, table, s))).Distinct().Count(), 8, "Different guards, different packs.");
            Assert.AreEqual(Map01LootRoller.Seed(42, "loot_a"), Map01LootRoller.Seed(42, "loot_a"));
            Assert.AreNotEqual(Map01LootRoller.Seed(42, "loot_a"), Map01LootRoller.Seed(43, "loot_a"), "Another run, another roll.");
        }

        [Test]
        public void AGuardsPackIsAFewRoundsNotAMagazine()
        {
            // The economy the design sets: a patrol's pack averages 9-16 rifle rounds (the old fixed drop was 30), so
            // rounds come from fighting several guards, the camp chests and the base store.
            var table = Data.TableOf("lt_m1_patrol");
            double rounds = Enumerable.Range(0, 4000).Average(s => Map01LootRoller.Roll(Data, table, s).Where(d => d.item == "ammo_rifle").Sum(d => d.count));
            Assert.That(rounds, Is.InRange(9.0, 16.0));
            foreach (var kind in Data.enemyTypes)
            {
                Assert.That(kind.dropMax, Is.LessThan(Map01Inventory.WornBelow + .1f), kind.code + "'s rifle drops worn: it needs a repair or goes for parts.");
                Assert.Greater(kind.dropMin, 0f, kind.code + "'s rifle still fires when picked up.");
            }
        }

        [Test]
        public void GunsCarryDurabilityInTheContentBundleAndSkillsCapFieldRepairs()
        {
            var content = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Map01/Generated/Map01Content.json");
            var bundle = JsonUtility.FromJson<ForestBundle>(content.text);
            foreach (var id in new[] { "rifle_standard", "pistol_k54", "rifle_mosin" })
            {
                var weapon = bundle.weapons.Single(w => w.id == id);
                Assert.Greater(weapon.durability_max, 0, id);
                Assert.Greater(weapon.durability_per_shot, 0, id);
                Assert.IsNotNull(Data.ItemOf(id)?.salvage, id + " can be taken apart.");
            }
            var balance = JsonUtility.FromJson<SkillBalance>(Resources.Load<TextAsset>("Skills/SkillBalance").text);
            Assert.IsTrue(balance.IsValid());
            Assert.AreEqual(.8f, balance.fieldRepairCaps[0], 1e-4f, "A repair kit brings a gun to 80% at Engineering 0.");
            Assert.AreEqual(1f, balance.fieldRepairCaps[balance.fieldRepairCaps.Length - 1], 1e-4f);
            balance.fieldRepairCaps = new[] { .8f, .7f, .9f, .95f, 1f };
            Assert.IsFalse(balance.IsValid(), "A cap never falls as Engineering rises.");
        }

        [Test]
        public void TheThreeRoundsShareGunpowderAndAMosinRoundCostsMost()
        {
            var content = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Map01/Generated/Map01Content.json");
            var recipes = JsonUtility.FromJson<ForestBundle>(content.text).craft_recipes;
            float PowderPerRound(string id)
            {
                var r = recipes.Single(x => x.id == id);
                return r.inputs.Where(i => i.item_id == "gunpowder").Sum(i => i.count) / (float)r.output_count;
            }
            // K-54 1/8, AK 2/10, Mosin 3/5 of a tin a round: every tin spent on one gun is a tin the others lack.
            Assert.Less(PowderPerRound("craft_ammo_pistol"), PowderPerRound("craft_ammo_rifle"));
            Assert.Less(PowderPerRound("craft_ammo_rifle") * 2.5f, PowderPerRound("craft_ammo_sniper"), "A Mosin round stays dear.");
            CollectionAssert.Contains(Map01Inventory.BenchRecipes, "craft_ammo_sniper");

            // Throwables ready-made: operation guards carry smoke bombs now and then, an F1 rarely.
            var guards = Data.TableOf("lt_m1_operation_guard").entries;
            Assert.AreEqual(2, guards.Single(e => e.item == "smoke_bomb").tier);
            Assert.AreEqual(4, guards.Single(e => e.item == "grenade_f1").tier);
        }

        [System.Serializable] private sealed class Wrapper { public ForestIngredient[] items; public Map01Drops.Pile[] piles; }

        [Test]
        public void SavedDataFromBeforeGunsWoreReadsAsNew()
        {
            // JSON written before the condition field existed: nothing found or dropped comes back broken.
            var old = JsonUtility.FromJson<Wrapper>("{\"items\":[{\"item_id\":\"pistol_k54\",\"count\":1}],\"piles\":[{\"id\":\"rifle_standard\",\"count\":1}]}");
            Assert.AreEqual(Map01Inventory.NoCondition, old.items[0].condition);
            Assert.AreEqual(Map01Inventory.NoCondition, old.piles[0].condition);
        }
    }
}
