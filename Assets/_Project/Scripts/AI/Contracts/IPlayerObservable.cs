using UnityEngine;

namespace ShadowVale.AI.Contracts
{
    /// <summary>
    /// What enemy perception is allowed to know about the player. Implemented by Gameplay
    /// (<c>PlayerStealthAdapter</c>); AI only ever sees this interface, never the player class,
    /// so every map feeds the same sensors whatever its own stance/hiding rules are.
    /// </summary>
    public interface IPlayerObservable
    {
        Vector3 Position { get; }
        Vector3 Forward { get; }
        bool IsCrouching { get; }
        /// <summary>Concealed by the map's hiding spots (Map 1: crouched inside a bush).</summary>
        bool IsHidden { get; }
        bool IsInCover { get; }
        /// <summary>0 = invisible (darkness/foliage) … 1 = fully lit.</summary>
        float VisibilityFactor { get; }
        /// <summary>Multiplier on the player's own noises; the stealth skill lowers it (G07).</summary>
        float NoiseScale { get; }
        /// <summary>The point a guard has to see: head and shoulders, lower while crouching.</summary>
        Vector3 SightPoint { get; }
        /// <summary>Nearest tactical nav-graph node, refreshed by Gameplay; −1 until the map has a graph.</summary>
        int CurrentNavNode { get; }
        bool IsAlive { get; }
        /// <summary>True if <paramref name="t"/> is the player or part of the player (a sight ray hit him).</summary>
        bool Owns(Transform t);
    }
}
