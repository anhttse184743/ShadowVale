using System;
using ShadowVale.AI.Contracts;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Gameplay.Player
{
    /// <summary>
    /// The player as enemy perception sees it (<see cref="IPlayerObservable"/>). Map code decides
    /// what crouching, hiding and cover mean on that map by setting the probes; the sensors in
    /// <c>ShadowVale.AI.Perception</c> only read this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStealthAdapter : MonoBehaviour, IPlayerObservable, IPlayerCombatState
    {
        [Tooltip("Height of the point guards must see when the player stands.")]
        [SerializeField] private float standingSightHeight = 1.1f;
        [Tooltip("Crouched, head and shoulders are lower: low cover hides the player then.")]
        [SerializeField] private float crouchedSightHeight = .75f;

        public Func<bool> CrouchProbe, HiddenProbe, CoverProbe;

        public float NoiseScale { get; set; } = 1f;
        public int CurrentNavNode { get; set; } = -1;

        private Health _health;
        private Health Health => _health != null ? _health : _health = GetComponent<Health>();
        private PlayerCombat _combat;
        private PlayerCombat Combat => _combat != null ? _combat : _combat = GetComponent<PlayerCombat>();

        /// <summary>The adapter on <paramref name="player"/>, added on first use.</summary>
        public static PlayerStealthAdapter For(GameObject player)
            => player.TryGetComponent(out PlayerStealthAdapter adapter) ? adapter : player.AddComponent<PlayerStealthAdapter>();

        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public bool IsCrouching => CrouchProbe != null && CrouchProbe();
        public bool IsHidden => HiddenProbe != null && HiddenProbe();
        public bool IsInCover => CoverProbe != null && CoverProbe();
        /// <summary>No lighting model yet: the player is always fully lit.</summary>
        public float VisibilityFactor => 1f;
        public Vector3 SightPoint => transform.position + Vector3.up * (IsCrouching ? crouchedSightHeight : standingSightHeight);
        public bool IsAlive => Health == null || !Health.IsDead;
        public bool Owns(Transform t) => t != null && (t == transform || t.IsChildOf(transform));
        public bool IsReloading => Combat != null && Combat.IsReloading;
        public bool IsAiming => Combat != null && Combat.IsAiming;
    }
}
