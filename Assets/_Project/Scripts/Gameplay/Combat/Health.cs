using UnityEngine;
using UnityEngine.Events;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// Greybox hit points. Drives the death animation through the <c>Die</c> trigger and raises
    /// <see cref="onDied"/> so whatever owns the object can stop controlling it.
    /// Balance numbers belong in the content bundle — the value here is a testbed default.
    /// </summary>
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;

        [Tooltip("Animator to fire the death trigger on. Defaults to one in the children.")]
        [SerializeField] private Animator animator;

        [Tooltip("Seconds before a dead non-player object disappears. 0 keeps the corpse.")]
        [SerializeField] private float despawnDelay;

        [Tooltip("Seconds after dying before coming back at full health. 0 never respawns. " +
                 "Set this to the death animation's length so the corpse is not yanked upright " +
                 "mid-collapse.")]
        [SerializeField] private float respawnDelay;

        public UnityEvent onDied;
        public UnityEvent onRespawned;

        /// <summary>
        /// Name of the additive/override layer holding the weapon stance. It is muted while dying
        /// so the death take plays full-body instead of fighting a rifle-holding upper body.
        /// </summary>
        private const string UpperBodyLayer = "UpperBody";

        private float _current;
        private Coroutine _respawn;
        private int _upperBodyLayer = -1;
        private float _upperBodyWeightBeforeDeath;

        public bool IsDead => _current <= 0f;
        public float Current => _current;
        public float Max => maxHealth;
        public float Normalized => maxHealth > 0f ? Mathf.Clamp01(_current / maxHealth) : 0f;
        public void KeepCheckpointCorpse() { respawnDelay = 0; despawnDelay = 0; }
        /// <summary>Raises the health pool (a boss variant, say) and tops the current value up to match.</summary>
        public void SetMaxHealth(float value) { maxHealth = Mathf.Max(1f, value); _current = maxHealth; }
        public void RestoreHealth(float value)
        {
            value = Mathf.Clamp(value, 0, maxHealth);
            if (value <= 0) { TakeDamage(maxHealth, transform.position, null); return; }
            if (IsDead) Revive();
            _current = value;
        }

        private void Awake()
        {
            _current = maxHealth;
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            if (animator != null)
            {
                _upperBodyLayer = animator.GetLayerIndex(UpperBodyLayer);
            }
        }

        /// <summary>
        /// Mutes the weapon-stance layer while dying, then restores whatever it was — forcing it
        /// back to 1 would switch it on for characters that never had it up.
        /// </summary>
        private void SetUpperBodyActive(bool active)
        {
            if (animator == null || _upperBodyLayer <= 0)
            {
                return;
            }

            if (active)
            {
                animator.SetLayerWeight(_upperBodyLayer, _upperBodyWeightBeforeDeath);
            }
            else
            {
                _upperBodyWeightBeforeDeath = animator.GetLayerWeight(_upperBodyLayer);
                animator.SetLayerWeight(_upperBodyLayer, 0f);
            }
        }

        public void TakeDamage(float amount, Vector3 hitPoint, GameObject source)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            _current = Mathf.Max(0f, _current - amount);
            if (!IsDead)
            {
                return;
            }

            SetUpperBodyActive(false);
            if (animator != null)
            {
                animator.SetTrigger(PlayerCombat.AnimatorParams.Die);
            }
            onDied?.Invoke();

            if (despawnDelay > 0f)
            {
                Destroy(gameObject, despawnDelay);
            }
            else if (respawnDelay > 0f)
            {
                _respawn = StartCoroutine(RespawnAfterDelay());
            }
        }

        private System.Collections.IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            _respawn = null;
            Revive();
            onRespawned?.Invoke();
        }

        /// <summary>Resets to full health and snaps out of the death pose.</summary>
        public void Revive()
        {
            if (_respawn != null)
            {
                StopCoroutine(_respawn);
                _respawn = null;
            }

            _current = maxHealth;
            SetUpperBodyActive(true);
            if (animator == null)
            {
                return;
            }

            // The Die state is entered from Any State on a trigger; clear it first or the
            // controller drops straight back into dying.
            animator.ResetTrigger(PlayerCombat.AnimatorParams.Die);
            animator.Play(PlayerCombat.LocomotionState, 0, 0f);
        }

        private void OnDisable()
        {
            _respawn = null; // Coroutines do not survive the component being disabled.
        }
    }
}
