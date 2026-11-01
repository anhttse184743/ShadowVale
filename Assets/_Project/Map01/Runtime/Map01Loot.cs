using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Map 1's loot, weapon condition and base logistics, read from <c>Resources/Loot/Map01Loot.json</c>. The file is
    /// laid out like the content database (shadowvale schema: items.rarity/base_value/stats, loot_tables.rolls_min/max,
    /// loot_table_entries.tier/weight/min/max, map_loot_tables.container_tag, enemy_types.loot_table/weapon), so the
    /// same numbers can be exported to a content version (Docs/Architecture/GamePlatform/004_*.sql).
    /// <para>
    /// A roll first picks a tier with <see cref="tierOdds"/> (tier 1 common … 4 very rare), then an entry of that tier
    /// with chance weight / sum of the tier's weights (as the backend documents loot_table_entries). Entries of tier 5
    /// are given every time, outside the rolls: a guard's spare rounds, a chest's signature gun.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class Map01Loot
    {
        public string version;
        /// <summary>Percent odds of tiers 1-4 for one roll.</summary>
        public float[] tierOdds;
        public Repair repair;
        public Adaptive adaptive;
        public Logistics logistics;
        public Item[] items;
        public EnemyType[] enemyTypes;
        public Table[] lootTables;
        public Container[] containers;
        public FieldCache[] fieldCaches;

        [Serializable] public sealed class Repair
        {
            /// <summary>What a repair kit restores in the field, as a fraction of the gun's maximum (before the Engineering bonus).</summary>
            public float fieldFraction;
            public float fieldSeconds, salvageSeconds;
            /// <summary>Weapon parts per started quarter of missing condition at the bench.</summary>
            public int benchQuarterParts;
            /// <summary>Missing fraction from which a bench repair also takes a gun oil.</summary>
            public float benchOilFrom;
            /// <summary>Engineering level from which a bench repair takes one part fewer.</summary>
            public int benchDiscountLevel;
            /// <summary>Engineering level from which taking a gun apart gives one more scrap metal.</summary>
            public int salvageBonusLevel;
        }
        [Serializable] public sealed class Adaptive
        {
            /// <summary>Reserve rounds under which ammunition is twice as likely.</summary>
            public int lowAmmo;
            public float ammoBoost;
            /// <summary>Health fraction under which, with no dressing in the bag, medkitBoost multiplies their weight.</summary>
            public float lowHealth;
            public float medkitBoost;
            /// <summary>The dry-pack count that adds a dressing when Nam carries fewer than two.</summary>
            public int medkitPity;
        }
        [Serializable] public sealed class Logistics
        {
            /// <summary>Share of a weapon's base value it is worth handed in at 0% condition (100% gives all of it).</summary>
            public float turnInFloor;
            /// <summary>Credit the command sends when each operation is done: supplies, radio, commander, Hùng home.</summary>
            public int[] operationCredit;
        }
        [Serializable] public sealed class Yield { public string item; public int min, max; }
        [Serializable] public sealed class Item
        {
            public string code, type, rarity;
            public int baseValue;
            public float weight;
            /// <summary>Condition range of a gun found in a container (fractions of its maximum).</summary>
            public float foundMin, foundMax;
            public Yield[] salvage;
            /// <summary>From this condition a salvaged gun gives one more <see cref="salvageGoodItem"/>.</summary>
            public float salvageGood;
            public string salvageGoodItem;
        }
        [Serializable] public sealed class EnemyType
        {
            public string code, name, archetype, weapon, lootTable, sidearm;
            public bool boss;
            public int maxHp;
            /// <summary>Condition range of the gun he drops.</summary>
            public float dropMin, dropMax, sidearmMin, sidearmMax;
        }
        [Serializable] public sealed class Entry { public string item; public int tier; public float weight; public int min, max; }
        [Serializable] public sealed class Table { public string code, name; public int rollsMin, rollsMax; public Entry[] entries; }
        [Serializable] public sealed class Container { public string tag, lootTable; }
        [Serializable] public sealed class FieldCache { public string id, label; public float x, z; public int minStage; }

        public const string ResourcePath = "Loot/Map01Loot";
        private static Map01Loot cached;
        private static string cachedVersion;
        /// <summary>The data, read once per content version (tuned by the published one, PublishedContent).</summary>
        public static Map01Loot Load()
        {
            var version = ShadowVale.Content.ContentService.TryGetPublished(out var published) ? published.ContentVersionId : null;
            if (cached != null && cachedVersion == version) return cached;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException("Missing " + ResourcePath + ".json");
            cachedVersion = version;
            return cached = PublishedContent.Apply(Parse(asset.text));
        }
        public static Map01Loot Parse(string json)
        {
            var data = JsonUtility.FromJson<Map01Loot>(json);
            var problems = data?.Problems() ?? new List<string> { "empty" };
            if (problems.Count > 0) throw new ArgumentException("Map01Loot.json: " + string.Join("; ", problems));
            return data;
        }

        /// <summary>What is wrong with the data, checked as the database's constraints do (tier 1-5, weight &gt; 0,
        /// 1 &lt;= min &lt;= max, rolls_max &gt;= rolls_min &gt;= 0) plus references between tables.</summary>
        public List<string> Problems()
        {
            var p = new List<string>();
            if (tierOdds == null || tierOdds.Length != 4 || tierOdds.Any(o => o < 0) || tierOdds.Sum() <= 0) p.Add("tierOdds needs 4 non-negative odds");
            if (repair == null) p.Add("repair missing"); if (adaptive == null) p.Add("adaptive missing"); if (logistics == null) p.Add("logistics missing");
            var codes = new HashSet<string>((items ?? Array.Empty<Item>()).Select(i => i.code));
            var tables = new HashSet<string>((lootTables ?? Array.Empty<Table>()).Select(t => t.code));
            foreach (var t in lootTables ?? Array.Empty<Table>())
            {
                if (t.rollsMin < 0 || t.rollsMax < t.rollsMin) p.Add(t.code + ": rolls");
                var seen = new HashSet<(string, int)>();
                foreach (var e in t.entries ?? Array.Empty<Entry>())
                {
                    if (!codes.Contains(e.item)) p.Add(t.code + ": unknown item " + e.item);
                    if (e.tier < 1 || e.tier > 5) p.Add(t.code + ": tier " + e.tier);
                    if (e.weight <= 0) p.Add(t.code + ": weight " + e.item);
                    if (e.min < 1 || e.max < e.min) p.Add(t.code + ": quantity " + e.item);
                    if (!seen.Add((e.item, e.tier))) p.Add(t.code + ": " + e.item + " twice in tier " + e.tier);
                }
            }
            foreach (var e in enemyTypes ?? Array.Empty<EnemyType>())
            {
                if (!tables.Contains(e.lootTable)) p.Add(e.code + ": unknown loot table " + e.lootTable);
                if (!codes.Contains(e.weapon)) p.Add(e.code + ": unknown weapon " + e.weapon);
                if (!string.IsNullOrEmpty(e.sidearm) && !codes.Contains(e.sidearm)) p.Add(e.code + ": unknown sidearm " + e.sidearm);
                if (e.dropMin < 0 || e.dropMax > 1 || e.dropMax < e.dropMin) p.Add(e.code + ": drop condition");
            }
            foreach (var c in containers ?? Array.Empty<Container>()) if (!tables.Contains(c.lootTable)) p.Add(c.tag + ": unknown loot table " + c.lootTable);
            var cacheIds = new HashSet<string>();
            foreach (var c in fieldCaches ?? Array.Empty<FieldCache>())
            {
                if (string.IsNullOrEmpty(c.id) || !cacheIds.Add(c.id)) p.Add("field cache: missing or duplicate id");
                if (ContainerTable(c.id) == null) p.Add(c.id + ": unknown field cache container");
                if (c.minStage < 0 || c.minStage > Map01Quest.LastStage) p.Add(c.id + ": invalid stage");
            }
            return p;
        }

        public Item ItemOf(string code) => items?.FirstOrDefault(i => i.code == code);
        public Table TableOf(string code) => lootTables?.FirstOrDefault(t => t.code == code);
        public EnemyType EnemyOf(string code) => enemyTypes?.FirstOrDefault(e => e.code == code);
        public Table ContainerTable(string tag) => TableOf(containers?.FirstOrDefault(c => c.tag == tag)?.lootTable);
        public int BaseValue(string code) => ItemOf(code)?.baseValue ?? 0;

        /// <summary>Where Map 1 gives <paramref name="code"/>, read off the tables ("Túi lính địch · hòm sắt trại C1, C3 ·
        /// rã súng · kho căn cứ A"); null if nowhere.</summary>
        public string WhereFound(string code)
        {
            bool In(Table t) => t?.entries != null && t.entries.Any(e => e.item == code);
            var places = new List<string>();
            if ((enemyTypes ?? Array.Empty<EnemyType>()).Any(e => e.weapon == code || e.sidearm == code)) places.Add("lính địch gục để lại");
            else if ((enemyTypes ?? Array.Empty<EnemyType>()).Any(e => In(TableOf(e.lootTable)))) places.Add("túi lính địch");
            var camps = (containers ?? Array.Empty<Container>()).Where(c => c.tag.StartsWith("camp_chest_c", StringComparison.Ordinal) && In(TableOf(c.lootTable)))
                .Select(c => c.tag.Substring("camp_chest_".Length).ToUpperInvariant()).ToList();
            if (camps.Count > 0) places.Add("hòm sắt trại " + string.Join(", ", camps));
            if ((items ?? Array.Empty<Item>()).Any(i => i.salvageGoodItem == code || (i.salvage ?? Array.Empty<Yield>()).Any(y => y.item == code))) places.Add("rã súng");
            if (In(ContainerTable("base_start_crate"))) places.Add("thùng vật tư căn cứ A");
            if (In(ContainerTable("base_requisition"))) places.Add("kho căn cứ A");
            if (places.Count == 0) return null;
            string all = string.Join(" · ", places);
            return char.ToUpperInvariant(all[0]) + all.Substring(1);
        }
    }

    /// <summary>
    /// Rolls a loot table. Deterministic for a given seed, so a guard's pack or a chest holds the same after a load.
    /// Pure C#: tested without Play mode (LootRollerTests).
    /// </summary>
    public static class Map01LootRoller
    {
        public struct Drop { public string item; public int count; }

        /// <summary>Weight multipliers for this roll (the player's needs, Map01Loot.adaptive); null = none.</summary>
        public delegate float Boost(string item);

        /// <summary>Everything <paramref name="table"/> gives for <paramref name="seed"/>: its tier-5 entries, then
        /// rolls_min..rolls_max rolls. Drops of the same item are merged, in the order they first came.</summary>
        public static List<Drop> Roll(Map01Loot data, Map01Loot.Table table, int seed, Boost boost = null)
        {
            var random = new System.Random(seed);
            var drops = new List<Drop>();
            void Add(string item, int count)
            {
                if (count <= 0) return;
                int at = drops.FindIndex(d => d.item == item);
                if (at >= 0) drops[at] = new Drop { item = item, count = drops[at].count + count };
                else drops.Add(new Drop { item = item, count = count });
            }
            if (table?.entries == null) return drops;
            foreach (var e in table.entries.Where(e => e.tier == 5)) Add(e.item, Quantity(random, e));
            int rolls = random.Next(table.rollsMin, table.rollsMax + 1);
            for (int r = 0; r < rolls; r++)
            {
                var entry = Pick(data, table, random, boost);
                if (entry != null) Add(entry.item, Quantity(random, entry));
            }
            return drops;
        }

        private static int Quantity(System.Random random, Map01Loot.Entry e) => random.Next(e.min, e.max + 1);

        /// <summary>One roll: a tier by the odds (a tier the table lacks falls to the nearest lower one it has, then
        /// higher), then an entry of that tier by weight.</summary>
        public static Map01Loot.Entry Pick(Map01Loot data, Map01Loot.Table table, System.Random random, Boost boost = null)
        {
            var rolled = table.entries.Where(e => e.tier >= 1 && e.tier <= 4).ToList();
            if (rolled.Count == 0) return null;
            int tier = PickTier(data.tierOdds, random.NextDouble());
            var have = rolled.Select(e => e.tier).Distinct().ToList();
            if (!have.Contains(tier))
                tier = have.Where(t => t < tier).DefaultIfEmpty(have.Min()).Max();
            var pool = rolled.Where(e => e.tier == tier).ToList();
            float Weight(Map01Loot.Entry e) => e.weight * Mathf.Max(0f, boost?.Invoke(e.item) ?? 1f);
            float total = pool.Sum(Weight);
            if (total <= 0) return pool[0];
            double at = random.NextDouble() * total;
            foreach (var e in pool) { at -= Weight(e); if (at < 0) return e; }
            return pool[pool.Count - 1];
        }

        /// <summary>The tier (1-4) a uniform number in [0, 1) falls in, by percent odds.</summary>
        public static int PickTier(float[] odds, double u)
        {
            float total = odds.Sum();
            double at = u * total;
            for (int t = 0; t < odds.Length; t++) { at -= odds[t]; if (at < 0) return t + 1; }
            return odds.Length;
        }

        /// <summary>A uniform number in [min, max] from a seed: a gun's condition.</summary>
        public static float Between(int seed, float min, float max) => min + (float)new System.Random(seed).NextDouble() * Mathf.Max(0f, max - min);

        /// <summary>A stable seed from a run seed and a name (a guard's id, a chest's tag): the same every load.</summary>
        public static int Seed(int run, string name)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in name ?? "") h = (h ^ c) * 16777619;
                return h ^ (run * 31 + 7);
            }
        }
    }
}
