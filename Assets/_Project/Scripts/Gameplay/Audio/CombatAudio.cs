using ShadowVale.Core.Services;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Gameplay.Audio
{
    /// <summary>
    /// Gives a weapon a voice by listening to <see cref="PlayerCombat.Attacked"/>.
    /// <para>
    /// The clips live here rather than on <c>Weapon</c> because <c>WeaponPrefabBuilder</c>
    /// regenerates the weapon prefabs from code and writes only the stats it knows about — any
    /// field added to <c>Weapon</c> is wiped the next time someone rebuilds them.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerCombat))]
    public sealed class CombatAudio : MonoBehaviour
    {
        [SerializeField] private AudioSlice gunshot;
        [SerializeField] private AudioSlice melee;

        [Range(0f, 1f)] [SerializeField] private float volume = 0.85f;

        [Tooltip("Random pitch spread, so a full magazine does not sound like one sample looped.")]
        [Range(0f, 0.3f)] [SerializeField] private float pitchJitter = 0.07f;

        private PlayerCombat _combat;
        private IAudioService _audio;

        private void Awake() => _combat = GetComponent<PlayerCombat>();

        private void OnEnable() => _combat.Attacked += OnAttacked;

        private void OnDisable() => _combat.Attacked -= OnAttacked;

        private void OnAttacked(WeaponKind kind, Vector3 position)
        {
            AudioSlice slice = kind == WeaponKind.Rifle ? gunshot : melee;
            if (!slice.IsValid) return;

            _audio ??= PooledAudioService.Resolve();
            float pitch = 1f + Random.Range(-pitchJitter, pitchJitter);

            // Positioned rather than flat: the shot comes out of the muzzle, and the same clip
            // will be wanted for enemies firing across the valley.
            _audio.PlayOneShot(slice.clip, position, volume * slice.EffectiveGain, pitch,
                pan: 0f, spatialBlend: 0.65f, startTime: slice.startTime, duration: slice.duration);
        }
    }
}
