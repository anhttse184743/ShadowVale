using System.Collections.Generic;

namespace ShadowVale.Data.Content
{
    public sealed class LootTable
    {
        public string Id { get; set; }
        /// <summary>1 = common … 3 = rare. Used by loot containers to pick a table.</summary>
        public int Tier { get; set; }
        /// <summary>How many independent weighted picks a container makes.</summary>
        public int Rolls { get; set; }
        /// <summary>Rolls are drawn in [RollsMin, Rolls]; 0 rolls = only the guaranteed (tier 5) entries.</summary>
        public int RollsMin { get; set; }
        public List<LootEntry> Entries { get; set; } = new();
    }

    public sealed class LootEntry
    {
        public string ItemId { get; set; }
        /// <summary>1–4 = roll odds of that tier, 5 = always given (Map01Loot's layout).</summary>
        public int Tier { get; set; }
        public float Weight { get; set; }
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 1;
    }
}
