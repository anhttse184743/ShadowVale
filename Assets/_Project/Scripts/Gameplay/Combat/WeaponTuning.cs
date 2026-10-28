using System;
using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// The handling numbers for the one gun the game has: an AK-pattern rifle.
    /// <para>
    /// These do not live on <see cref="Weapon"/>. <c>WeaponPrefabBuilder</c> regenerates the
    /// weapon prefabs from code and writes only the stats it knows about, so anything added to
    /// <c>Weapon</c> disappears the next time someone rebuilds them. Keeping the defaults in this
    /// struct means a component that forgets to fill it in still behaves like a rifle.
    /// </para>
    /// </summary>
    [Serializable]
    public struct WeaponTuning
    {
        [Tooltip("Rounds in a full magazine.")]
        [Min(1)] public int magazineCapacity;

        [Tooltip("Seconds for any reload, whether the magazine is empty or partly loaded.")]
        [Min(0.01f)] public float reloadSeconds;

        /// <summary>Optional authored insertion times, as fractions of a full reload. Null uses a detachable magazine.</summary>
        public float[] individualReloadCommits;

        /// <summary>
        /// A 30-round rifle magazine with one shared reload duration.
        /// </summary>
        public static WeaponTuning Rifle => new()
        {
            magazineCapacity = 30,
            reloadSeconds = 2.4f,
        };

        /// <summary>
        /// A struct deserialised from a component that predates this field arrives all zeroes,
        /// and Unity ignores field initialisers when it does. Fall back to the rifle rather than
        /// shipping a weapon that cannot hold a round.
        /// <para>
        /// A scene with no ammo reserve is handled elsewhere, by reloading against an unlimited
        /// supply, so there is no reason for a weapon to want a magazine of nothing.
        /// </para>
        /// </summary>
        public WeaponTuning OrDefault()
            => magazineCapacity <= 0 || reloadSeconds <= 0f ? Rifle : this;

        public Magazine Create()
            => new(magazineCapacity, reloadSeconds, individualReloadCommits);
    }
}
