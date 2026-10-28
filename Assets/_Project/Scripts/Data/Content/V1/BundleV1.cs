using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ShadowVale.Data.Content.V1
{
    /// <summary>
    /// The bundle exactly as the web backend publishes it (schema "1.0",
    /// ShadowVale_web backend/shadowvale_be/ShadowVale.BLL/Schemas/content-bundle-1.0.schema.json):
    /// flat snake_case lists whose rows refer to each other by <c>code</c>. Gameplay never reads this;
    /// <c>BundleV1Mapper</c> turns it into the runtime <see cref="ContentBundle"/>.
    /// Free-form objects (nav_graph, layout, fsm_params, objectives, stats, effects) stay as JSON and are
    /// read by the conventions of the backend's docs/game-integration.md.
    /// </summary>
    public sealed class BundleV1
    {
        public const string Schema = "1.0";

        public long VersionNo { get; set; }
        public string Label { get; set; }
        public string SchemaVersion { get; set; }
        public string Changelog { get; set; }
        public List<ItemV1> Items { get; set; } = new();
        public List<WeaponV1> Weapons { get; set; } = new();
        public List<ConsumableV1> Consumables { get; set; } = new();
        public List<SkillV1> Skills { get; set; } = new();
        public List<LootTableV1> LootTables { get; set; } = new();
        public List<LootTableEntryV1> LootTableEntries { get; set; } = new();
        public List<MapV1> Maps { get; set; } = new();
        public List<MapLootTableV1> MapLootTables { get; set; } = new();
        public List<EnemyTypeV1> EnemyTypes { get; set; } = new();
        public List<EnemyPlacementV1> EnemyPlacements { get; set; } = new();
        public List<CraftingRecipeV1> CraftingRecipes { get; set; } = new();
        public List<CraftingRecipeIngredientV1> CraftingRecipeIngredients { get; set; } = new();
        public List<QuestV1> Quests { get; set; } = new();
        public List<QuestRewardV1> QuestRewards { get; set; } = new();
    }

    public sealed class ItemV1
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        /// <summary>weapon | ammo | consumable | material | tool | armor | quest_item</summary>
        public string ItemType { get; set; }
        public string Rarity { get; set; }
        public int MaxStack { get; set; }
        public float Weight { get; set; }
        public int BaseValue { get; set; }
        public JObject Stats { get; set; }
        public string IconKey { get; set; }
    }

    public sealed class WeaponV1
    {
        public string ItemCode { get; set; }
        /// <summary>pistol | smg | rifle | shotgun | sniper | melee</summary>
        public string WeaponClass { get; set; }
        public float Damage { get; set; }
        public float FireRate { get; set; }
        public float EffectiveRange { get; set; }
        public int? MagazineSize { get; set; }
        public float? ReloadTimeS { get; set; }
        public string AmmoItemCode { get; set; }
        public int MaxDurability { get; set; }
        public float DurabilityPerUse { get; set; }
        public float NoiseRadius { get; set; }
        public bool IsSuppressed { get; set; }
    }

    public sealed class ConsumableV1
    {
        public string ItemCode { get; set; }
        public int HealHp { get; set; }
        public int RestoreStamina { get; set; }
        public float UseTimeS { get; set; }
        public List<string> Cures { get; set; } = new();
        public JObject ExtraEffects { get; set; }
    }

    public sealed class SkillV1
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string SkillType { get; set; }
        public int MaxLevel { get; set; }
        public JArray XpCurve { get; set; }
        public JObject Effects { get; set; }
    }

    public sealed class LootTableV1
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public int RollsMin { get; set; }
        public int RollsMax { get; set; }
    }

    public sealed class LootTableEntryV1
    {
        public string LootTableCode { get; set; }
        public string ItemCode { get; set; }
        public int Tier { get; set; }
        public float Weight { get; set; }
        public int MinQty { get; set; }
        public int MaxQty { get; set; }
    }

    public sealed class MapV1
    {
        public string Code { get; set; }
        public string Name { get; set; }
        /// <summary>Unity scene name.</summary>
        public string SceneKey { get; set; }
        public bool IsSafeCamp { get; set; }
        public int SortOrder { get; set; }
        /// <summary><c>{ nodes: [{ id, x, y, z, is_cover, cover_facing, neighbors }], escape_routes: [id] }</c></summary>
        public JObject NavGraph { get; set; }
        /// <summary><c>{ player_start_node }</c></summary>
        public JObject Layout { get; set; }
    }

    public sealed class MapLootTableV1
    {
        public string MapCode { get; set; }
        public string LootTableCode { get; set; }
        public string ContainerTag { get; set; }
    }

    public sealed class EnemyTypeV1
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Archetype { get; set; }
        public bool IsBoss { get; set; }
        public int MaxHp { get; set; }
        public float MoveSpeed { get; set; }
        public float VisionRange { get; set; }
        public float VisionAngleDeg { get; set; }
        public float HearingRange { get; set; }
        public float Accuracy { get; set; }
        public string WeaponItemCode { get; set; }
        /// <summary><c>{ alert_decay_seconds, retreat_hp_threshold, accuracy?, boss? }</c></summary>
        public JObject FsmParams { get; set; }
        public string LootTableCode { get; set; }
    }

    public sealed class EnemyPlacementV1
    {
        public string Id { get; set; }
        public string MapCode { get; set; }
        public string EnemyTypeCode { get; set; }
        public string SquadTag { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float FacingDeg { get; set; }
        public JArray PatrolRoute { get; set; }
        public JToken SpawnCondition { get; set; }
    }

    public sealed class CraftingRecipeV1
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string OutputItemCode { get; set; }
        public int OutputQuantity { get; set; }
        public float CraftTimeS { get; set; }
        public string RequiredSkillCode { get; set; }
        public int RequiredSkillLevel { get; set; }
        public string Station { get; set; }
    }

    public sealed class CraftingRecipeIngredientV1
    {
        public string RecipeCode { get; set; }
        public string ItemCode { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class QuestV1
    {
        public string Code { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsMain { get; set; }
        public int SortOrder { get; set; }
        /// <summary><c>[{ id, type, target_id, count }]</c>; the index is the funnel's <c>payload.index</c>.</summary>
        public JArray Objectives { get; set; }
        public List<string> Prerequisites { get; set; } = new();
        public int RewardXp { get; set; }
    }

    public sealed class QuestRewardV1
    {
        public string QuestCode { get; set; }
        public string ItemCode { get; set; }
        public int Quantity { get; set; }
    }
}
