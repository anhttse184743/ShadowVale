using System.Collections.Generic;

namespace ShadowVale.Data.Content
{
    public sealed class QuestDefinition
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string GiverNpcId { get; set; }
        public string MapId { get; set; }
        public List<QuestObjective> Objectives { get; set; } = new();
        public List<QuestReward> Rewards { get; set; } = new();
    }

    public sealed class QuestObjective
    {
        public string Id { get; set; }
        /// <summary>reach_node | kill_archetype | collect_item | escape_map | stealth_no_alert</summary>
        public string Type { get; set; }
        public string TargetId { get; set; }
        public int Count { get; set; } = 1;
    }

    public sealed class QuestReward
    {
        public string ItemId { get; set; }
        public int Count { get; set; }
    }
}
