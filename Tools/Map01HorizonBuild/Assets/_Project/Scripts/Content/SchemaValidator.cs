using System.Collections.Generic;
using System.Linq;
using ShadowVale.Data.Content;

namespace ShadowVale.Content
{
    /// <summary>
    /// Structural and referential checks on a parsed bundle. The backend runs the full JSON Schema
    /// before publishing (UC-24); the client re-checks the parts that would crash gameplay if wrong,
    /// so a corrupted cache or a hand-edited fallback is rejected with a readable message instead of a
    /// NullReference three scenes later.
    /// </summary>
    public static class SchemaValidator
    {
        public const int SupportedSchemaVersion = 1;

        public static List<string> Validate(ContentBundle b)
        {
            var errors = new List<string>();
            if (b == null) { errors.Add("bundle is null"); return errors; }

            if (b.SchemaVersion != SupportedSchemaVersion)
                errors.Add($"schema_version {b.SchemaVersion} unsupported (client supports {SupportedSchemaVersion})");
            if (string.IsNullOrWhiteSpace(b.BundleVersion))
                errors.Add("bundle_version missing");

            var items = UniqueIds(b.Items.Select(i => i.Id), "items", errors);
            var weapons = UniqueIds(b.Weapons.Select(w => w.Id), "weapons", errors);
            var enemies = UniqueIds(b.EnemyArchetypes.Select(e => e.Id), "enemy_archetypes", errors);
            var loot = UniqueIds(b.LootTables.Select(l => l.Id), "loot_tables", errors);
            UniqueIds(b.CraftRecipes.Select(r => r.Id), "craft_recipes", errors);
            UniqueIds(b.Quests.Select(q => q.Id), "quests", errors);
            var maps = UniqueIds(b.Maps.Select(m => m.Id), "maps", errors);

            foreach (var w in b.Weapons)
            {
                if (!items.Contains(w.AmmoType)) errors.Add($"weapons[{w.Id}].ammo_type '{w.AmmoType}' is not an item");
                if (w.Damage <= 0) errors.Add($"weapons[{w.Id}].damage must be > 0");
                if (w.MagazineSize <= 0) errors.Add($"weapons[{w.Id}].magazine_size must be > 0");
                if (w.DurabilityMax <= 0) errors.Add($"weapons[{w.Id}].durability_max must be > 0");
            }

            foreach (var e in b.EnemyArchetypes)
            {
                if (!weapons.Contains(e.WeaponId)) errors.Add($"enemy_archetypes[{e.Id}].weapon_id '{e.WeaponId}' unknown");
                if (e.MaxHp <= 0) errors.Add($"enemy_archetypes[{e.Id}].max_hp must be > 0");
                if (e.RetreatHpThreshold < 0 || e.RetreatHpThreshold > 1) errors.Add($"enemy_archetypes[{e.Id}].retreat_hp_threshold must be in [0,1]");
                if (!string.IsNullOrEmpty(e.LootTableId) && !loot.Contains(e.LootTableId)) errors.Add($"enemy_archetypes[{e.Id}].loot_table_id '{e.LootTableId}' unknown");
            }

            foreach (var t in b.LootTables)
            {
                if (t.Rolls <= 0) errors.Add($"loot_tables[{t.Id}].rolls must be > 0");
                if (t.Entries.Count == 0) errors.Add($"loot_tables[{t.Id}] has no entries");
                foreach (var en in t.Entries)
                {
                    if (!items.Contains(en.ItemId)) errors.Add($"loot_tables[{t.Id}] entry item_id '{en.ItemId}' unknown");
                    if (en.Weight <= 0) errors.Add($"loot_tables[{t.Id}] entry '{en.ItemId}' weight must be > 0");
                    if (en.Min < 1 || en.Max < en.Min) errors.Add($"loot_tables[{t.Id}] entry '{en.ItemId}' min/max invalid");
                }
            }

            foreach (var r in b.CraftRecipes)
            {
                if (!items.Contains(r.OutputItemId)) errors.Add($"craft_recipes[{r.Id}].output_item_id '{r.OutputItemId}' unknown");
                if (r.Inputs.Count == 0) errors.Add($"craft_recipes[{r.Id}] has no inputs");
                foreach (var i in r.Inputs)
                    if (!items.Contains(i.ItemId)) errors.Add($"craft_recipes[{r.Id}] input '{i.ItemId}' unknown");
            }

            foreach (var q in b.Quests)
                if (!string.IsNullOrEmpty(q.MapId) && !maps.Contains(q.MapId))
                    errors.Add($"quests[{q.Id}].map_id '{q.MapId}' unknown");

            foreach (var m in b.Maps)
            {
                if (string.IsNullOrWhiteSpace(m.SceneName)) errors.Add($"maps[{m.Id}].scene_name missing");
                var n = m.NavGraphNodes.Count;
                for (var i = 0; i < n; i++)
                {
                    var node = m.NavGraphNodes[i];
                    if (node.Id != i) errors.Add($"maps[{m.Id}].nav_graph_nodes[{i}].id must equal its index ({node.Id})");
                    foreach (var nb in node.Neighbors)
                        if (nb < 0 || nb >= n) errors.Add($"maps[{m.Id}] node {i} neighbor {nb} out of range");
                }
                foreach (var e in m.EscapeRoutes)
                    if (e < 0 || e >= n) errors.Add($"maps[{m.Id}].escape_routes contains {e}, out of range");
                if (n > 0 && (m.PlayerStartNode < 0 || m.PlayerStartNode >= n))
                    errors.Add($"maps[{m.Id}].player_start_node out of range");
                foreach (var sg in m.SpawnGroups)
                {
                    if (!enemies.Contains(sg.ArchetypeId)) errors.Add($"maps[{m.Id}] spawn_group '{sg.SquadId}' archetype '{sg.ArchetypeId}' unknown");
                    if (sg.Count <= 0) errors.Add($"maps[{m.Id}] spawn_group '{sg.SquadId}' count must be > 0");
                    foreach (var s in sg.StartNodes)
                        if (s < 0 || s >= n) errors.Add($"maps[{m.Id}] spawn_group '{sg.SquadId}' start node {s} out of range");
                }
                foreach (var lp in m.LootPlacements)
                {
                    if (!loot.Contains(lp.LootTableId)) errors.Add($"maps[{m.Id}] loot placement '{lp.ContainerId}' table '{lp.LootTableId}' unknown");
                    if (lp.Node < 0 || lp.Node >= n) errors.Add($"maps[{m.Id}] loot placement '{lp.ContainerId}' node out of range");
                }
                if (!string.IsNullOrEmpty(m.BossArchetypeId) && !enemies.Contains(m.BossArchetypeId))
                    errors.Add($"maps[{m.Id}].boss_archetype_id '{m.BossArchetypeId}' unknown");
            }

            var ai = b.AiSettings;
            if (ai == null) errors.Add("ai_settings missing");
            else
            {
                if (string.IsNullOrWhiteSpace(ai.DefaultSolverVariant)) errors.Add("ai_settings.default_solver_variant missing");
                if (ai.LatencyBudgetMs <= 0) errors.Add("ai_settings.latency_budget_ms must be > 0");
                if (ai.ReplanCooldownMs < 0) errors.Add("ai_settings.replan_cooldown_ms must be >= 0");
            }

            return errors;
        }

        private static HashSet<string> UniqueIds(IEnumerable<string> ids, string collection, List<string> errors)
        {
            var set = new HashSet<string>();
            var i = 0;
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id)) errors.Add($"{collection}[{i}].id missing");
                else if (!set.Add(id)) errors.Add($"{collection} has duplicate id '{id}'");
                i++;
            }
            return set;
        }
    }
}
