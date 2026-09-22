using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>Anything a weapon can hurt. Kept minimal — balance numbers live in content, not here.</summary>
    public interface IDamageable
    {
        bool IsDead { get; }

        /// <param name="amount">Damage in hit points.</param>
        /// <param name="hitPoint">World-space impact point, for effects.</param>
        /// <param name="source">Who dealt it; may be null.</param>
        void TakeDamage(float amount, Vector3 hitPoint, GameObject source);
    }
}
