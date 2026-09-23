using UnityEngine;

namespace ShadowVale.AI.Contracts
{
    /// <summary>
    /// What enemy perception is allowed to know about the player. Implemented by Gameplay
    /// (PlayerController); AI only ever sees this interface, never the player class.
    /// </summary>
    public interface IPlayerObservable
    {
        Vector3 Position { get; }
        Vector3 Forward { get; }
        bool IsCrouching { get; }
        bool IsInCover { get; }
        /// <summary>0 = invisible (darkness/foliage) … 1 = fully lit.</summary>
        float VisibilityFactor { get; }
        /// <summary>Nearest tactical nav-graph node, refreshed by Gameplay each frame.</summary>
        int CurrentNavNode { get; }
        bool IsAlive { get; }
    }
}
