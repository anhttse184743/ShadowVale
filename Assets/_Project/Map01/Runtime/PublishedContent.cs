using System.Collections.Generic;
using System.Linq;
using ShadowVale.Content;
using ShadowVale.Data.Content;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Tunes the maps' hand-authored data (Map01Content.json, Map01Loot.json) with the content version the
    /// designers published on the web. Rows are matched by id/code, so a value the web schema has replaces the
    /// local one and everything else stays: field crafting (field_seconds, field_max_batch), salvage and repair
    /// rules, containers placed in the scene. New recipes and loot tables are added; new weapons and enemies are
    /// not, because the maps pick those by id in code. Without a published version (offline, first run without a
    /// cache) the local files are used as they are.
    /// </summary>
    public static class PublishedContent
    {
        public static ForestBundle Apply(ForestBundle local)
        {
            if (local == null || !ContentService.TryGetPublished(out var published)) return local;
            Apply(local, published);
            Debug.Log($"[Content] map content tuned with published version {published.BundleVersion}");
            return local;
        }

        public static Map01Loot Apply(Map01Loot local)
        {
            if (local == null || !ContentService.TryGetPublished(out var published)) return local;
            var tuned = Apply(local, published);
            var problems = tuned.Problems();
            if (problems.Count == 0) return tuned;
            Debug.LogWarning("[Content] published loot ignored, it breaks Map01Loot's rules: " + string.Join("; ", problems));
            return local;
        }

        /// <summary>Replaces in place the values of <paramref name="local"/> that the published bundle has.</summary>
        public static void Apply(ForestBundle local, ContentBundle published)
        {
            var items = published.Items.ToDictionary(i => i.Id);
            foreach (var item in local.items ?? new ForestItem[0])
                if (item != null && items.TryGetValue(item.id, out var p)) item.stack_max = p.StackMax;

            var weapons = published.Weapons.ToDictionary(w => w.Id);
            foreach (var weapon in local.weapons ?? new ForestWeapon[0])
            {
                if (weapon == null || !weapons.TryGetValue(weapon.id, out var p)) continue;
                weapon.damage = p.Damage;
                weapon.range = p.Range;
                weapon.noise_radius = p.NoiseRadius;
                weapon.fire_rate = p.FireRate;
                weapon.suppressed = p.Suppressed;
                if (p.MagazineSize > 0) weapon.magazine_size = p.MagazineSize;
                weapon.reload_seconds = p.ReloadSeconds;
                weapon.durability_max = p.DurabilityMax;
                weapon.durability_per_shot = p.DurabilityPerShot;
            }

            var enemies = published.EnemyArchetypes.ToDictionary(e => e.Id);
            foreach (var enemy in local.enemy_archetypes ?? new ForestArchetype[0])
            {
                if (enemy == null || !enemies.TryGetValue(enemy.id, out var p)) continue;
                enemy.max_hp = p.MaxHp;
                enemy.move_speed = p.MoveSpeed;
                enemy.vision_range = p.VisionRange;
                enemy.vision_angle = p.VisionAngle;
                enemy.hearing_range = p.HearingRange;
                enemy.alert_decay_seconds = p.AlertDecaySeconds;
                enemy.retreat_hp_threshold = p.RetreatHpThreshold;
                enemy.is_boss = p.IsBoss;
                if (p.Boss != null) enemy.boss = ForestBossProfile.From(p.Boss);
            }

            var recipes = (local.craft_recipes ?? new ForestRecipe[0]).Where(r => r != null).ToList();
            foreach (var p in published.CraftRecipes)
            {
                var recipe = recipes.FirstOrDefault(r => r.id == p.Id);
                if (recipe == null)
                {
                    // Bench only: field crafting has no column in the web schema
                    recipe = new ForestRecipe { id = p.Id };
                    recipes.Add(recipe);
                }
                recipe.output_item_id = p.OutputItemId;
                recipe.output_count = p.OutputCount;
                recipe.craft_seconds = p.CraftSeconds;
                recipe.inputs = p.Inputs.Select(i => new ForestIngredient { item_id = i.ItemId, count = i.Count }).ToArray();
            }
            local.craft_recipes = recipes.ToArray();
        }

        /// <summary>A tuned copy of <paramref name="local"/>: loot tables and Map 1's containers from the published bundle.</summary>
        public static Map01Loot Apply(Map01Loot local, ContentBundle published)
        {
            var tuned = JsonUtility.FromJson<Map01Loot>(JsonUtility.ToJson(local));
            var tables = (tuned.lootTables ?? new Map01Loot.Table[0]).ToList();
            var known = new HashSet<string>((tuned.items ?? new Map01Loot.Item[0]).Select(i => i.code));
            foreach (var p in published.LootTables)
            {
                var table = tables.FirstOrDefault(t => t.code == p.Id);
                if (table == null)
                {
                    // Map01Loot only rolls items it knows the type, value and salvage of
                    if (p.Entries.Any(e => !known.Contains(e.ItemId))) continue;
                    table = new Map01Loot.Table { code = p.Id, name = p.Id };
                    tables.Add(table);
                }
                table.rollsMin = p.RollsMin;
                table.rollsMax = p.Rolls;
                table.entries = p.Entries.Select(e => new Map01Loot.Entry
                {
                    item = e.ItemId, tier = e.Tier, weight = e.Weight, min = e.Min, max = e.Max
                }).ToArray();
            }
            tuned.lootTables = tables.ToArray();

            var map = published.Maps.FirstOrDefault(m => m.Id == Map01Mission.MapId);
            if (map != null)
            {
                var containers = (tuned.containers ?? new Map01Loot.Container[0]).ToList();
                foreach (var placement in map.LootPlacements)
                {
                    var container = containers.FirstOrDefault(c => c.tag == placement.ContainerId);
                    if (container == null) containers.Add(container = new Map01Loot.Container { tag = placement.ContainerId });
                    container.lootTable = placement.LootTableId;
                }
                tuned.containers = containers.ToArray();
            }

            var enemies = published.EnemyArchetypes.ToDictionary(e => e.Id);
            foreach (var enemy in tuned.enemyTypes ?? new Map01Loot.EnemyType[0])
            {
                if (!enemies.TryGetValue(enemy.code, out var p)) continue;
                enemy.maxHp = Mathf.RoundToInt(p.MaxHp);
                if (!string.IsNullOrEmpty(p.LootTableId)) enemy.lootTable = p.LootTableId;
            }
            return tuned;
        }
    }
}
