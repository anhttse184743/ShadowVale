using System.Collections.Generic;

namespace ShadowVale.Data.Content
{
    /// <summary>
    /// Data side of a map. The Unity scene (<see cref="SceneName"/>) owns geometry, props and the
    /// baked NavMesh; this object owns everything a designer tunes on the web: spawns, loot,
    /// and the tactical nav graph (20–100 nodes) that is the input of the QUBO.
    /// Node ids are 0-based indices into <see cref="NavGraphNodes"/>.
    /// </summary>
    public sealed class MapDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string SceneName { get; set; }
        public List<SpawnGroup> SpawnGroups { get; set; } = new();
        public List<LootPlacement> LootPlacements { get; set; } = new();
        public List<NavGraphNode> NavGraphNodes { get; set; } = new();
        public List<int> EscapeRoutes { get; set; } = new();
        public int PlayerStartNode { get; set; }
        public string BossArchetypeId { get; set; }
    }

    public sealed class SpawnGroup
    {
        public string SquadId { get; set; }
        public string ArchetypeId { get; set; }
        public int Count { get; set; }
        /// <summary>Nav graph node ids where the squad starts patrolling.</summary>
        public List<int> StartNodes { get; set; } = new();
    }

    public sealed class LootPlacement
    {
        public string ContainerId { get; set; }
        public string LootTableId { get; set; }
        public int Node { get; set; }
    }

    public sealed class NavGraphNode
    {
        public int Id { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public bool IsCover { get; set; }
        /// <summary>Direction (degrees, world yaw) the cover protects from; ignored when !IsCover.</summary>
        public float CoverFacing { get; set; }
        public List<int> Neighbors { get; set; } = new();
    }
}
