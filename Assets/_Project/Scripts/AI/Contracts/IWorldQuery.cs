using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.AI.Contracts
{
    /// <summary>
    /// Read-only view of the level for AI: line of sight and the tactical nav graph
    /// (NOT the NavMesh — locomotion goes through the enemy body). Implemented by
    /// <c>ShadowVale.AI.Tactical.TacticalGraph</c>, baked per map.
    /// </summary>
    public interface IWorldQuery
    {
        int NodeCount { get; }
        Vector3 NodePosition(int node);
        IReadOnlyList<int> Neighbors(int node);
        bool IsCoverNode(int node);
        /// <summary>World yaw (degrees) of the direction the cover at <paramref name="node"/> protects from.</summary>
        float CoverFacing(int node);
        IReadOnlyList<int> CoverNodes { get; }
        /// <summary>Chokepoints the player escapes through (bridges, exits, narrow passes).</summary>
        IReadOnlyList<int> EscapeRoutes { get; }
        int NearestNode(Vector3 position);
        /// <summary>Graph distance in edges; <see cref="int.MaxValue"/> when unreachable.</summary>
        int Hops(int a, int b);
        /// <summary>Raycast against the obstruction layers between two points.</summary>
        bool HasLineOfSight(Vector3 from, Vector3 to);
        /// <summary>Precomputed node-to-node visibility; input of the QUBO coverage terms.</summary>
        bool NodeSeesNode(int a, int b);
        /// <summary>False while the visibility matrix is still being computed after the map loaded.</summary>
        bool VisibilityReady { get; }
    }
}
