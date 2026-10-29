using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// Magazines and reloading: one magazine per gun, the reload animation, and what a save puts back.
    /// </summary>
    public sealed partial class PlayerCombat
    {
        public Weapon WeaponFor(WeaponKind kind) => _weapons.TryGetValue(kind, out var value) ? value : null;
        public int MagazineRounds(WeaponKind kind) => _magazines.TryGetValue(kind, out var value) ? value.Rounds : 0;
        public void RestoreMagazine(WeaponKind kind, int rounds)
        {
            if (_magazines.TryGetValue(kind, out var value)) value.Refill(rounds);
        }

        /// <summary>Register an extra gun without regenerating the existing player/loadout prefab.</summary>
        public bool RegisterWeapon(Weapon weapon, WeaponTuning handling, int loadedRounds = 0)
        {
            if (weapon == null || !weapon.IsGun || _weapons.ContainsKey(weapon.Kind)) return false;
            handling = handling.OrDefault();
            var magazine = handling.Create(); magazine.Refill(loadedRounds);
            _weapons.Add(weapon.Kind, weapon); _magazines.Add(weapon.Kind, magazine);
            _tunings.Add(weapon.Kind, handling); weapon.gameObject.SetActive(false);
            return true;
        }

        public void CancelReload()
        {
            _magazine?.CancelReload();
            if (animator != null && _hasReloadParams) animator.ResetTrigger("Reload");
        }

        /// <summary>0 to 1 across a reload, for a HUD bar. 0 when not reloading.</summary>
        public float ReloadProgress => _magazine?.ReloadProgress ?? 0f;
        public System.Func<bool> ProneReloadAvailable { get; set; }

        /// <summary>Puts a saved round count back in the weapon and clears any reload.</summary>
        public void RestoreMagazine(int rounds) => _magazine?.Refill(rounds);

        /// <summary>Starts a reload if one is possible. Returns false when it is not.</summary>
        public bool TryReload()
        {
            if (_handsOccupied || _magazine == null || _equipped == null || !_equipped.IsGun) return false;
            bool proneReload = _controller != null && _controller.IsProne && (ProneReloadAvailable?.Invoke() ?? false);
            if (_controller != null && _controller.IsProne && !proneReload && !_controller.TrySetProne(false)) return false;
            bool wasEmpty = _magazine.IsEmpty;
            var skills = GetComponent<ShadowVale.Gameplay.Skills.PlayerSkills>();
            if (!_magazine.BeginReload(ReserveRounds != null ? ReserveRounds() : -1, (skills != null ? skills.ReloadMultiplier : 1f)*Mathf.Max(.1f,ReloadDurationMultiplier))) return false;
            if (!proneReload) AnimateReload(wasEmpty);
            return true;
        }

        /// <summary>
        /// Nam's own reload takes (Blender): swapping a part-used magazine, an empty one, or crouched.
        /// Played at whatever pace makes the take end as the magazine timer does.
        /// </summary>
        private void AnimateReload(bool wasEmpty)
        {
            if (_equippedKind == WeaponKind.Pistol || _equippedKind == WeaponKind.Sniper) return; // Authored pose players follow the real magazine timer.
            if (animator == null || !_hasReloadParams) return;
            bool crouched = _controller != null && _controller.IsSneaking;
            float length = ClipLength(crouched ? "Nam_Crouch_Reload" : wasEmpty ? "Nam_Rifle_Reload" : "Nam_Rifle_ReloadTactical");
            animator.SetBool("ReloadEmpty", wasEmpty);
            animator.SetFloat("ReloadSpeed", length > 0 ? length / Mathf.Max(0.1f, _magazine.ReloadRemaining) : 1f);
            animator.SetTrigger("Reload");
        }

        private float ClipLength(string clipName)
        {
            if (animator.runtimeAnimatorController == null) return 0f;
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == clipName) return clip.length;
            return 0f;
        }
        public void RestoreAttackCooldown(float remaining) => _nextAttackTime = Time.time + Mathf.Max(0, remaining);
    }
}
