using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Content;
using ShadowVale.Core.Services;
using ShadowVale.Data.Content;
using ShadowVale.Gameplay.Skills;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// A version published on the web tunes Map 1's own data (PublishedContent): the imported bundle changes
    /// nothing, an edited value reaches the map, values the web has no column for stay, and without a published
    /// version the files are used as they are.
    /// </summary>
    public sealed class PublishedContentTests
    {
        private const string ContentPath = "Assets/_Project/Map01/Generated/Map01Content.json";

        private static ForestBundle Local() => JsonUtility.FromJson<ForestBundle>(File.ReadAllText(ContentPath));

        private static ContentBundle Imported()
        {
            Assert.IsTrue(FallbackBundleProvider.TryParse(File.ReadAllText(FallbackBundleProvider.AbsolutePath), out var bundle, out var error), error);
            return bundle;
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Unregister<ContentService>();

        [Test]
        public void TheImportedBundleLeavesMapOnesContentAsItIs()
        {
            var tuned = Local();
            PublishedContent.Apply(tuned, Imported());
            Assert.AreEqual(JsonUtility.ToJson(Local()), JsonUtility.ToJson(tuned),
                "The bundle imported from Map01Content must carry every value it has the same.");
        }

        [Test]
        public void AnEditedValueReachesTheMapAndFieldCraftingStays()
        {
            var published = Imported();
            published.Weapons.Single(w => w.Id == "rifle_standard").Damage = 31;
            published.EnemyArchetypes.Single(e => e.Id == "grunt").VisionRange = 19;
            published.EnemyArchetypes.Single(e => e.Id == "boss_commander").Boss.ReinforceCount = 5;
            var recover = published.CraftRecipes.Single(r => r.Id == "recover_ammo_556");
            recover.CraftSeconds = 7;
            published.CraftRecipes.Add(new CraftRecipe
            {
                Id = "craft_gun_oil", OutputItemId = "gun_oil", OutputCount = 1, CraftSeconds = 5,
                Inputs = { new CraftInput { ItemId = "herb", Count = 2 } }
            });

            var tuned = Local();
            PublishedContent.Apply(tuned, published);
            Assert.AreEqual(31f, tuned.weapons.Single(w => w.id == "rifle_standard").damage);
            Assert.AreEqual(19f, tuned.enemy_archetypes.Single(e => e.id == "grunt").vision_range);
            Assert.AreEqual(5, tuned.enemy_archetypes.Single(e => e.id == "boss_commander").boss.reinforce_count);
            var recipe = tuned.craft_recipes.Single(r => r.id == "recover_ammo_556");
            Assert.AreEqual(7f, recipe.craft_seconds);
            Assert.AreEqual(3f, recipe.field_seconds, "field_seconds has no web column: Map01Content's value stays.");
            Assert.AreEqual(3, recipe.field_max_batch);
            var added = tuned.craft_recipes.Single(r => r.id == "craft_gun_oil");
            Assert.AreEqual(0f, added.field_seconds, "A recipe made on the web is a bench recipe.");
        }

        [Test]
        public void PublishedLootTablesAndContainersReachMapOnesLoot()
        {
            var published = Imported();
            var patrol = published.LootTables.Single(t => t.Id == "lt_m1_patrol");
            patrol.Rolls = 2;
            patrol.Entries.First(e => e.ItemId == "cloth").Weight = 99;
            published.Maps.Single(m => m.Id == "map01").LootPlacements.Single(p => p.ContainerId == "camp_chest_c1").LootTableId = "lt_m1_chest_c2";
            // A table of items Map01Loot does not know (no type, value or salvage) is not added
            published.LootTables.Add(new LootTable { Id = "lt_unknown", Rolls = 1, Entries = { new LootEntry { ItemId = "plasma_cell", Tier = 1, Weight = 1 } } });

            var tuned = PublishedContent.Apply(Map01Loot.Load(), published);
            CollectionAssert.IsEmpty(tuned.Problems());
            var table = tuned.lootTables.Single(t => t.code == "lt_m1_patrol");
            Assert.AreEqual(2, table.rollsMax);
            Assert.AreEqual(99f, table.entries.First(e => e.item == "cloth").weight);
            Assert.AreEqual("lt_m1_chest_c2", tuned.containers.Single(c => c.tag == "camp_chest_c1").lootTable);
            Assert.IsFalse(tuned.lootTables.Any(t => t.code == "lt_unknown"));
            Assert.AreNotEqual(2, Map01Loot.Load().lootTables.Single(t => t.code == "lt_m1_patrol").rollsMax, "The loaded data is never changed in place.");
        }

        [Test]
        public void PublishedSkillCurvesReplaceSkillBalance()
        {
            var local = JsonUtility.FromJson<SkillBalance>(Resources.Load<TextAsset>("Skills/SkillBalance").text);
            var published = Imported();
            var shooting = published.Skills.Single(s => s.Type == "shooting");
            shooting.XpCurve = local.shootingThresholds.Select((x, i) => (float)(x * 2)).ToList();

            var tuned = PlayerSkills.Apply(local, published);
            Assert.IsTrue(tuned.IsValid());
            CollectionAssert.AreEqual(local.shootingThresholds.Select(x => x * 2), tuned.shootingThresholds);
            CollectionAssert.AreEqual(local.stealthThresholds, tuned.stealthThresholds);
            CollectionAssert.AreEqual(local.recoilMultipliers, tuned.recoilMultipliers);
        }

        [Test]
        public void WithoutAPublishedVersionTheFilesAreUsedAsTheyAre()
        {
            var shipped = new ContentService();
            shipped.Apply(Imported(), "fallback");
            ServiceLocator.Register(shipped);
            Assert.IsFalse(ContentService.TryGetPublished(out _), "The shipped bundle is no published version.");

            var local = Local();
            Assert.AreSame(local, PublishedContent.Apply(local));
        }
    }
}
