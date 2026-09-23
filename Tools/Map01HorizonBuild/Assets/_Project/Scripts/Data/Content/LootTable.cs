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
        public List<LootEntry> Entries { get; set; } = new();
    }

    public sealed class LootEntry
    {
        public string ItemId { get; set; }
        public float Weight { get; set; }
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 1;
    }
}
