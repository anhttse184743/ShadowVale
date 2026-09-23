using System.Collections.Generic;
using ShadowVale.Data.Content;

namespace ShadowVale.Content
{
    /// <summary>
    /// The public read API for content. Gameplay never touches the raw bundle; it asks this service
    /// by id. Lookups throw a KeyNotFoundException naming the missing id so bad content fails loudly.
    /// </summary>
    public sealed class ContentService
    {
        private readonly Dictionary<string, ItemDefinition> _items = new();
        private readonly Dictionary<string, WeaponDefinition> _weapons = new();
        private readonly Dictionary<string, EnemyArchetype> _enemies = new();
        private readonly Dictionary<string, LootTable> _loot = new();
        private readonly Dictionary<string, CraftRecipe> _recipes = new();
        private readonly Dictionary<string, QuestDefinition> _quests = new();
        private readonly Dictionary<string, MapDefinition> _maps = new();

        public ContentBundle Bundle { get; private set; }
        public string SourceName { get; private set; } = "none";
        public bool IsLoaded => Bundle != null;
        public string Version => Bundle?.BundleVersion ?? "unloaded";
        public AiSettings Ai => Bundle?.AiSettings;

        /// <summary>Try each source in order; the first that loads wins.</summary>
        public bool Load(IEnumerable<IBundleSource> sources, out string log)
        {
            var lines = new List<string>();
            foreach (var src in sources)
            {
                if (src.TryLoad(out var bundle, out var error))
                {
                    Apply(bundle, src.SourceName);
                    lines.Add($"{src.SourceName}: ok (v{bundle.BundleVersion})");
                    log = string.Join(" | ", lines);
                    return true;
                }
                lines.Add($"{src.SourceName}: {error}");
            }
            log = string.Join(" | ", lines);
            return false;
        }

        public void Apply(ContentBundle bundle, string sourceName)
        {
            Bundle = bundle;
            SourceName = sourceName;
            Index(_items, bundle.Items, i => i.Id);
            Index(_weapons, bundle.Weapons, w => w.Id);
            Index(_enemies, bundle.EnemyArchetypes, e => e.Id);
            Index(_loot, bundle.LootTables, l => l.Id);
            Index(_recipes, bundle.CraftRecipes, r => r.Id);
            Index(_quests, bundle.Quests, q => q.Id);
            Index(_maps, bundle.Maps, m => m.Id);
        }

        public ItemDefinition GetItem(string id) => Get(_items, id, "item");
        public WeaponDefinition GetWeapon(string id) => Get(_weapons, id, "weapon");
        public EnemyArchetype GetEnemy(string id) => Get(_enemies, id, "enemy_archetype");
        public LootTable GetLootTable(string id) => Get(_loot, id, "loot_table");
        public CraftRecipe GetRecipe(string id) => Get(_recipes, id, "craft_recipe");
        public QuestDefinition GetQuest(string id) => Get(_quests, id, "quest");
        public MapDefinition GetMap(string id) => Get(_maps, id, "map");

        public bool TryGetWeapon(string id, out WeaponDefinition w) => _weapons.TryGetValue(id ?? "", out w);
        public bool TryGetItem(string id, out ItemDefinition i) => _items.TryGetValue(id ?? "", out i);

        public IReadOnlyCollection<WeaponDefinition> AllWeapons => _weapons.Values;
        public IReadOnlyCollection<EnemyArchetype> AllEnemies => _enemies.Values;
        public IReadOnlyCollection<CraftRecipe> AllRecipes => _recipes.Values;
        public IReadOnlyCollection<MapDefinition> AllMaps => _maps.Values;

        private static void Index<T>(Dictionary<string, T> dict, IEnumerable<T> items, System.Func<T, string> key)
        {
            dict.Clear();
            foreach (var it in items) dict[key(it)] = it;
        }

        private T Get<T>(Dictionary<string, T> dict, string id, string kind)
        {
            if (!IsLoaded) throw new System.InvalidOperationException("content bundle not loaded");
            if (id != null && dict.TryGetValue(id, out var v)) return v;
            throw new KeyNotFoundException($"{kind} '{id}' not in content bundle v{Version}");
        }
    }
}
