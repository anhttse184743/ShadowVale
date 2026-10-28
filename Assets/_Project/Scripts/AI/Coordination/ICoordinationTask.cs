using System.Collections.Generic;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Tactical;

namespace ShadowVale.AI.Coordination
{
    /// <summary>
    /// What a combat group tries to achieve, expressed as the escape nodes the QUBO rewards covering.
    /// The formulation is shared; a map or squad that coordinates differently supplies its own task.
    /// </summary>
    public interface ICoordinationTask
    {
        string Id { get; }
        /// <summary>Local node indices of <paramref name="sub"/> the squad should keep in sight.</summary>
        IReadOnlyList<int> EscapeNodes(LocalSubgraph sub, IWorldQuery graph);
    }

    /// <summary>
    /// Default: contain the player — cover the ways out of his neighbourhood (its boundary ring)
    /// and any map chokepoint inside it (bridges, exits).
    /// </summary>
    public sealed class ContainPlayerTask : ICoordinationTask
    {
        public static readonly ContainPlayerTask Instance = new ContainPlayerTask();

        public string Id => "contain";

        public IReadOnlyList<int> EscapeNodes(LocalSubgraph sub, IWorldQuery graph)
        {
            var result = new SortedSet<int>(sub.BoundaryLocal);
            var chokepoints = new HashSet<int>(graph.EscapeRoutes);
            for (int i = 0; i < sub.Count; i++) if (chokepoints.Contains(sub.Nodes[i])) result.Add(i);
            result.Remove(sub.PlayerLocal);
            if (result.Count == 0) for (int i = 0; i < sub.Count; i++) if (i != sub.PlayerLocal) result.Add(i);
            return new List<int>(result);
        }
    }
}
