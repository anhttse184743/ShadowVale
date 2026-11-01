using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// The weapon's pose: its seat in the hand (carry vs levelled), where the body faces, and the weight of
    /// the upper-body stance layer.
    /// </summary>
    public sealed partial class PlayerCombat
    {
        /// <summary>
        /// Swings the weapon between the carry pose it rides in while moving and the levelled pose
        /// it takes to shoot. The arms come from the animation; only the weapon's seat in the hand
        /// changes, which is what separates "slung over the shoulder" from "aimed".
        /// </summary>
        private void UpdateGripPose()
        {
            if (_equipped == null || gripConfig == null
                || !gripConfig.TryGetOffset(_equippedKind, out WeaponGripConfig.GripOffset offset)
                || !offset.hasAimPose)
            {
                return;
            }

            bool levelled = _aiming || _firingHeld || Time.time < _upperBodyHoldUntil;
            _aimPoseBlend = Mathf.MoveTowards(
                _aimPoseBlend, levelled ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, gripBlendTime));

            WeaponGripConfig.GetPose(offset, false, out Vector3 carryPos, out Quaternion carryRot);
            WeaponGripConfig.GetPose(offset, true, out Vector3 aimPos, out Quaternion aimRot);
            _equipped.transform.SetLocalPositionAndRotation(
                Vector3.Lerp(carryPos, aimPos, _aimPoseBlend),
                Quaternion.Slerp(carryRot, aimRot, _aimPoseBlend));
        }

        /// <summary>
        /// Who the body faces. Walking, it follows the legs. Aiming or shooting, it locks to the
        /// camera's yaw so the barrel points where the player is looking — that turn is what puts
        /// the character into the bladed stance the Gunplay take was authored around.
        /// Pushed every frame rather than from the aim toggle alone, because firing turns the body
        /// too and that turn has to time out on its own.
        /// </summary>
        private void UpdateFacing()
        {
            if (_controller == null)
            {
                return;
            }

            bool locked = _aiming || _firingHeld || Time.time < _faceCameraHoldUntil;
            bool holdingGun = _equipped != null && _equipped.IsGun;
            _controller.SetFaceCameraYaw(locked, locked && holdingGun ? -PoseAimYaw() : 0f);
        }

        /// <summary>
        /// How far round from the character's own forward the pose is pointing the rifle. Read
        /// straight off the barrel, because that is the thing that has to end up on the crosshair.
        /// The Mixamo shooting take aims about 87 degrees to the character's left, which is why
        /// firing has to stand the body side-on to the target.
        /// <para>
        /// Measured live rather than stored as a constant: re-seat the weapon in the hand or swap
        /// the shooting clip for one that aims straight ahead, and this follows on its own instead
        /// of leaving a stale number turning the body for no reason.
        /// </para>
        /// </summary>
        private float PoseAimYaw()
        {
            if (_controller != null && _controller.IsProne) return 0f;
            // Recoil moves the arms, not the feet. Keep the last resting aim reference through each kick.
            if (RecoilPosePlaying) return _stanceYaw;
            Transform muzzle = _equipped != null ? _equipped.Muzzle : null;
            if (muzzle == null)
            {
                return 0f;
            }

            // The barrel as the take holds it. BarrelAim bends the spine afterwards to put it on the
            // crosshair, and reading that back would count the correction as the pose's own: the body
            // would stop turning and leave the spine to twist the whole way.
            if (_barrelAim != null && _barrelAim.TryGetPoseYaw(out float posed))
            {
                return _stanceYaw = posed;
            }

            Vector3 barrel = Vector3.ProjectOnPlane(muzzle.forward, Vector3.up);
            // Barrel straight up or down carries no yaw; leave the body facing the camera.
            return _stanceYaw = barrel.sqrMagnitude < 0.0004f
                ? 0f
                : Vector3.SignedAngle(transform.forward, barrel.normalized, Vector3.up);
        }

        /// <summary>
        /// The stance layer is faded out when empty-handed. An override layer whose state has no
        /// motion writes nothing, so it would otherwise hold the last armed pose forever — which
        /// is exactly what made putting the knife away leave the character still gripping it.
        /// The layer is forced back up while an attack plays, so the punch still reaches the arms.
        /// </summary>
        private void UpdateUpperBodyWeight()
        {
            if (animator == null || _upperBodyLayer <= 0)
            {
                return;
            }

            bool attacking = Time.time < _upperBodyHoldUntil;
            // With Nam's own AK carry in the base layer (Locomotion_Rifle), his arms already hold the
            // rifle: the stance layer only comes up to aim, fire, reload or take a hit.
            bool carried = _rifleBody && _equippedKind == WeaponKind.Rifle && !_aiming && !_firingHeld && !attacking
                && (_magazine == null || !_magazine.IsReloading);
            // The cheap tests first: this runs every frame, the animator is only asked with the knife out.
            if(_equippedKind==WeaponKind.Knife&&!attacking&&HasKnifeCarry) {
                var state=animator.GetCurrentAnimatorStateInfo(0);
                carried|=state.IsName("Locomotion_Knife")||state.IsName("Sneak_Knife");
            }
            float target = (_equippedKind != WeaponKind.Unarmed || attacking) && !carried ? 1f : 0f;
            _upperBodyWeight = Mathf.MoveTowards(
                _upperBodyWeight, target, Time.deltaTime / Mathf.Max(0.01f, stanceBlendTime));
            animator.SetLayerWeight(_upperBodyLayer, _upperBodyWeight);
        }
    }
}
