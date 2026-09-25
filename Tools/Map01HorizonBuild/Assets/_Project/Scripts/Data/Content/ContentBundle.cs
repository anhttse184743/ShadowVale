using System.Collections.Generic;

namespace ShadowVale.Data.Content
{
    /// <summary>
    /// Root of the published content bundle (schema v1). Every balance number in the game
    /// comes from here; no C# file may hard-code damage, HP, loot weights or timings.
    /// JSON keys are snake_case; the serializer maps them onto these PascalCase members.
    /// </summary>
    public sealed class ContentBundle
    {
        public string BundleVersion { get; set; }
        public int SchemaVersion { get; set; }
        public string PublishedAt { get; set; }
        public string Checksum { get; set; }

        public List<ItemDefinition> Items { get; set; } = new();
        public List<WeaponDefinition> Weapons { get; set; } = new();
        public List<EnemyArchetype> EnemyArchetypes { get; set; } = new();
        public List<LootTable> LootTables { get; set; } = new();
        public List<CraftRecipe> CraftRecipes { get; set; } = new();
        public List<QuestDefinition> Quests { get; set; } = new();
        public List<MapDefinition> Maps { get; set; } = new();
        public AiSettings AiSettings { get; set; } = new();
    }

    public sealed class ItemDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        /// <summary>ammo | material | consumable | tool</summary>
        public string Category { get; set; }
        public int StackMax { get; set; } = 1;
        public float Weight { get; set; }
    }
}
