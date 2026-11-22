using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using ShadowVale.Data.Content;
using ShadowVale.Data.Content.V1;
using UnityEngine;

namespace ShadowVale.Content
{
    /// <summary>
    /// Turns the web backend's flat schema 1.0 bundle into the runtime <see cref="ContentBundle"/> the
    /// game already reads: rows joined by code (loot entries into tables, ingredients into recipes,
    /// rewards into quests, containers into maps), free-form objects read by the conventions of the
    /// backend's docs/game-integration.md. The backend validated the bundle before publishing it;
    /// <see cref="Check"/> only re-checks what the join needs, the runtime checks come after
    /// (<see cref="SchemaValidator"/>).
    /// </summary>
    public static class BundleV1Mapper
    {
        public static ContentBundle Map(BundleV1 v1, AiSettings ai, string contentVersionId = null, string checksum = null)
        {
            var names = v1.Items.ToDictionary(i => i.Code, i => i.Name);
            var entries = v1.LootTableEntries.ToLookup(e => e.LootTableCode);
            var ingredients = v1.CraftingRecipeIngredients.ToLookup(i => i.RecipeCode);
            var rewards = v1.QuestRewards.ToLookup(r => r.QuestCode);
            var containers = v1.MapLootTables.ToLookup(c => c.MapCode);
            var placements = v1.EnemyPlacements.ToLookup(p => p.MapCode);

            return new ContentBundle
            {
                BundleVersion = v1.VersionNo.ToString(),
                // The runtime model's version, not the wire format's
                SchemaVersion = SchemaValidator.SupportedSchemaVersion,
                Checksum = checksum,
                ContentVersionId = contentVersionId,
                Items = v1.Items.Select(i => new ItemDefinition
                {
                    Id = i.Code, DisplayName = i.Name, Category = i.ItemType, StackMax = i.MaxStack, Weight = i.Weight
                }).ToList(),
                Weapons = v1.Weapons.Select(w => new WeaponDefinition
                {
                    Id = w.ItemCode,
                    DisplayName = names.TryGetValue(w.ItemCode, out var name) ? name : w.ItemCode,
                    Class = w.WeaponClass,
                    Damage = w.Damage,
                    FireRate = w.FireRate,
                    Range = w.EffectiveRange,
                    AmmoType = w.AmmoItemCode,
                    MagazineSize = w.MagazineSize ?? 0,
                    ReloadSeconds = w.ReloadTimeS ?? 0,
                    DurabilityMax = w.MaxDurability,
                    DurabilityPerShot = w.DurabilityPerUse,
                    NoiseRadius = w.NoiseRadius,
                    Suppressed = w.IsSuppressed,
                }).ToList(),
                EnemyArchetypes = v1.EnemyTypes.Select(e => new EnemyArchetype
                {
                    Id = e.Code,
                    DisplayName = e.Name,
                    MaxHp = e.MaxHp,
                    MoveSpeed = e.MoveSpeed,
                    VisionRange = e.VisionRange,
                    VisionAngle = e.VisionAngleDeg,
                    HearingRange = e.HearingRange,
                    Accuracy = e.Accuracy,
                    WeaponId = e.WeaponItemCode,
                    AlertDecaySeconds = e.FsmParams?.Value<float?>("alert_decay_seconds") ?? 0,
                    RetreatHpThreshold = e.FsmParams?.Value<float?>("retreat_hp_threshold") ?? 0,
                    LootTableId = e.LootTableCode,
                    IsBoss = e.IsBoss,
                    Boss = e.FsmParams?["boss"] is JObject boss ? boss.ToObject<BossProfile>(Serializer) : null,
                }).ToList(),
                LootTables = v1.LootTables.Select(t => new LootTable
                {
                    Id = t.Code,
                    Tier = entries[t.Code].Select(e => e.Tier).DefaultIfEmpty(1).Min(),
                    RollsMin = t.RollsMin,
                    Rolls = t.RollsMax,
                    Entries = entries[t.Code].Select(e => new LootEntry
                    {
                        ItemId = e.ItemCode, Tier = e.Tier, Weight = e.Weight, Min = e.MinQty, Max = e.MaxQty
                    }).ToList(),
                }).ToList(),
                CraftRecipes = v1.CraftingRecipes.Select(r => new CraftRecipe
                {
                    Id = r.Code,
                    OutputItemId = r.OutputItemCode,
                    OutputCount = r.OutputQuantity,
                    RequiredSkill = r.RequiredSkillCode,
                    RequiredSkillLevel = r.RequiredSkillLevel,
                    CraftSeconds = r.CraftTimeS,
                    Inputs = ingredients[r.Code].Select(i => new CraftInput { ItemId = i.ItemCode, Count = i.Quantity }).ToList(),
                }).ToList(),
                Quests = v1.Quests.OrderBy(q => q.SortOrder).Select(q => new QuestDefinition
                {
                    Id = q.Code,
                    Title = q.Title,
                    Description = q.Description,
                    Objectives = q.Objectives?.ToObject<List<QuestObjective>>(Serializer) ?? new List<QuestObjective>(),
                    Rewards = rewards[q.Code].Select(r => new QuestReward { ItemId = r.ItemCode, Count = r.Quantity }).ToList(),
                }).ToList(),
                Maps = v1.Maps.OrderBy(m => m.SortOrder).Select(m => new MapDefinition
                {
                    Id = m.Code,
                    DisplayName = m.Name,
                    SceneName = m.SceneKey,
                    NavGraphNodes = m.NavGraph?["nodes"]?.ToObject<List<NavGraphNode>>(Serializer) ?? new List<NavGraphNode>(),
                    EscapeRoutes = m.NavGraph?["escape_routes"]?.ToObject<List<int>>() ?? new List<int>(),
                    PlayerStartNode = m.Layout?.Value<int?>("player_start_node") ?? 0,
                    // Containers are found by tag in the scene; the nav graph does not place them
                    LootPlacements = containers[m.Code].Select(c => new LootPlacement
                    {
                        ContainerId = c.ContainerTag, LootTableId = c.LootTableCode, Node = 0
                    }).ToList(),
                    SpawnGroups = placements[m.Code].GroupBy(p => (p.SquadTag, p.EnemyTypeCode)).Select(g => new SpawnGroup
                    {
                        SquadId = g.Key.SquadTag, ArchetypeId = g.Key.EnemyTypeCode, Count = g.Count()
                    }).ToList(),
                }).ToList(),
                Skills = v1.Skills.Select(s => new SkillDefinition
                {
                    Id = s.Code,
                    Type = s.SkillType,
                    MaxLevel = s.MaxLevel,
                    XpCurve = s.XpCurve?.ToObject<List<float>>() ?? new List<float>(),
                    // Only per-level number lists are effects the game reads
                    Effects = (s.Effects ?? new JObject()).Properties()
                        .Where(p => p.Value is JArray a && a.All(v => v.Type == JTokenType.Integer || v.Type == JTokenType.Float))
                        .ToDictionary(p => p.Name, p => p.Value.ToObject<List<float>>()),
                }).ToList(),
                AiSettings = ai,
            };
        }

        /// <summary>What the joins in <see cref="Map"/> rely on; empty when the bundle can be mapped.</summary>
        public static List<string> Check(BundleV1 v1)
        {
            var errors = new List<string>();
            if (v1.SchemaVersion != BundleV1.Schema)
            {
                errors.Add($"schema_version {v1.SchemaVersion ?? "null"} unsupported (client supports {BundleV1.Schema})");
                return errors;
            }
            if (v1.VersionNo < 1) errors.Add("version_no must be ≥ 1");
            Unique(v1.Items.Select(i => i.Code), "items", errors);
            Unique(v1.Weapons.Select(w => w.ItemCode), "weapons", errors);
            Unique(v1.EnemyTypes.Select(e => e.Code), "enemy_types", errors);
            Unique(v1.LootTables.Select(t => t.Code), "loot_tables", errors);
            Unique(v1.CraftingRecipes.Select(r => r.Code), "crafting_recipes", errors);
            Unique(v1.Quests.Select(q => q.Code), "quests", errors);
            Unique(v1.Maps.Select(m => m.Code), "maps", errors);
            var items = new HashSet<string>(v1.Items.Select(i => i.Code));
            foreach (var w in v1.Weapons.Where(w => !items.Contains(w.ItemCode)))
                errors.Add($"weapons[{w.ItemCode}] has no item");
            var tables = new HashSet<string>(v1.LootTables.Select(t => t.Code));
            foreach (var e in v1.LootTableEntries.Where(e => !tables.Contains(e.LootTableCode)))
                errors.Add($"loot_table_entries: table '{e.LootTableCode}' unknown");
            var recipes = new HashSet<string>(v1.CraftingRecipes.Select(r => r.Code));
            foreach (var i in v1.CraftingRecipeIngredients.Where(i => !recipes.Contains(i.RecipeCode)))
                errors.Add($"crafting_recipe_ingredients: recipe '{i.RecipeCode}' unknown");
            return errors;
        }

        private static readonly Newtonsoft.Json.JsonSerializer Serializer = Newtonsoft.Json.JsonSerializer.Create(ContentJson.Settings);

        private static void Unique(IEnumerable<string> codes, string collection, List<string> errors)
        {
            var seen = new HashSet<string>();
            foreach (var code in codes)
                if (string.IsNullOrWhiteSpace(code)) errors.Add($"{collection}: empty code");
                else if (!seen.Add(code)) errors.Add($"{collection} has duplicate code '{code}'");
        }
    }

    /// <summary>
    /// Coordination knobs until the backend assigns the solver per session (POST /api/game/sessions):
    /// the web bundle has no ai_settings, so they ship as <c>StreamingAssets/Content/ai_settings.json</c>.
    /// </summary>
    public static class AiSettingsFile
    {
        public const string RelativePath = "Content/ai_settings.json";

        public static string AbsolutePath => Path.Combine(Application.streamingAssetsPath, RelativePath);

        public static AiSettings Load()
        {
            try
            {
                if (File.Exists(AbsolutePath))
                    return ContentJson.Deserialize<AiSettings>(File.ReadAllText(AbsolutePath)) ?? new AiSettings();
                Debug.LogWarning($"[Content] {RelativePath} not found, coordination uses its defaults");
            }
            catch (Exception e) { Debug.LogWarning($"[Content] {RelativePath} unreadable: {e.Message}"); }
            return new AiSettings();
        }
    }
}
