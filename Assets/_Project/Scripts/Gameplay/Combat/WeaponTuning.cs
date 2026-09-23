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

        [Tooltip("Seconds to swap a part-used magazine, with a round still chambered.")]
        [Min(0.01f)] public float reloadSeconds;

        [Tooltip("Seconds to reload from dry. Longer because the bolt has to be worked.")]
        [Min(0.01f)] public float emptyReloadSeconds;

        /// <summary>
        /// Real AK-47 numbers: a 30-round magazine at roughly 600 rounds per minute, and a
        /// reload a second or so slower when the bolt has locked back.
        /// </summary>
        public static WeaponTuning Rifle => new()
        {
            magazineCapacity = 30,
            reloadSeconds = 2.4f,
            emptyReloadSeconds = 2.9f,
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
            => new(magazineCapacity, reloadSeconds, emptyReloadSeconds);
    }
}
