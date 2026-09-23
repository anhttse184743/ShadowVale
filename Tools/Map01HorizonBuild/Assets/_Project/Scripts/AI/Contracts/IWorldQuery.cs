using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.AI.Contracts
{
    /// <summary>
    /// Read-only view of the level for AI: line of sight and the tactical nav graph
    /// (NOT the NavMesh — locomotion goes through AgentMover). Implemented by Gameplay/World.
    /// </summary>
    public interface IWorldQuery
    {
        int NodeCount { get; }
        Vector3 NodePosition(int node);
        IReadOnlyList<int> Neighbors(int node);
        bool IsCoverNode(int node);
        IReadOnlyList<int> CoverNodes { get; }
        IReadOnlyList<int> EscapeRoutes { get; }
        int NearestNode(Vector3 position);
        /// <summary>Raycast against the VisionBlocker layer between two points.</summary>
        bool HasLineOfSight(Vector3 from, Vector3 to);
        /// <summary>Precomputed node-to-node visibility; input of the QUBO coverage terms.</summary>
        bool NodeSeesNode(int a, int b);
    }
}
