using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// Where each weapon sits inside the hand. Hand-tuned values live here rather than in the
    /// builders, because the builders regenerate <c>Player.prefab</c> and the weapon prefabs from
    /// scratch on every run — anything nudged in the Inspector on those would be wiped. This asset
    /// is never regenerated, so it is the one safe place to keep the numbers.
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowVale/Weapon Grip Config", fileName = "WeaponGripConfig")]
    public sealed class WeaponGripConfig : ScriptableObject
    {
        [System.Serializable]
        public struct GripOffset
        {
            public WeaponKind kind;

            [Tooltip("Position relative to the hand anchor, in metres.")]
            public Vector3 localPosition;

            [Tooltip("Rotation relative to the hand anchor, in degrees.")]
            public Vector3 localEuler;

            [Tooltip("Position while aiming or attacking. Leave equal to localPosition to keep one pose.")]
            public Vector3 aimLocalPosition;

            [Tooltip("Rotation while aiming or attacking — the weapon levelled at the target. " +
                     "The resting values above are the carry pose used while just moving around.")]
            public Vector3 aimLocalEuler;

            [Tooltip("Off means the weapon keeps one pose whatever the player is doing.")]
            public bool hasAimPose;
        }

        [Header("Hand anchor")]
        [Tooltip("The anchor itself, relative to the right hand bone. Shared by every weapon — " +
                 "prefer tuning the per-weapon offsets below.")]
        [SerializeField] private Vector3 anchorPosition;

        [Tooltip("Twist of the anchor. Its +Z is aligned to the character's facing before this " +
                 "is applied, so this is the leftover roll around the barrel.")]
        [SerializeField] private Vector3 anchorEuler;

        [Header("Per weapon")]
        [SerializeField] private GripOffset[] weapons = System.Array.Empty<GripOffset>();

        public Vector3 AnchorPosition => anchorPosition;
        public Vector3 AnchorEuler => anchorEuler;

        public bool TryGetOffset(WeaponKind kind, out GripOffset offset)
        {
            foreach (GripOffset candidate in weapons)
            {
                if (candidate.kind == kind)
                {
                    offset = candidate;
                    return true;
                }
            }
            offset = default;
            return false;
        }

        /// <summary>Applies the stored offset, or leaves the weapon at the anchor's origin.</summary>
        public void Apply(Transform weapon, WeaponKind kind, bool aiming = false)
        {
            if (TryGetOffset(kind, out GripOffset offset))
            {
                GetPose(offset, aiming, out Vector3 position, out Quaternion rotation);
                weapon.SetLocalPositionAndRotation(position, rotation);
            }
            else
            {
                weapon.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            weapon.localScale = Vector3.one;
        }

        /// <summary>
        /// The carry pose, or the aim pose when the weapon is levelled. Callers blend between the
        /// two themselves so the swap is not a snap.
        /// </summary>
        public static void GetPose(GripOffset offset, bool aiming,
            out Vector3 position, out Quaternion rotation)
        {
            bool useAim = aiming && offset.hasAimPose;
            position = useAim ? offset.aimLocalPosition : offset.localPosition;
            rotation = Quaternion.Euler(useAim ? offset.aimLocalEuler : offset.localEuler);
        }

        /// <summary>Editor hook: records the aim pose separately from the carry pose.</summary>
        public void SetAimOffset(WeaponKind kind, Vector3 localPosition, Vector3 localEuler)
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i].kind != kind)
                {
                    continue;
                }
                weapons[i].aimLocalPosition = localPosition;
                weapons[i].aimLocalEuler = localEuler;
                weapons[i].hasAimPose = true;
                return;
            }
        }

        /// <summary>Editor hook: records a tuned transform back into the asset.</summary>
        public void SetOffset(WeaponKind kind, Vector3 localPosition, Vector3 localEuler)
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i].kind != kind)
                {
                    continue;
                }
                weapons[i].localPosition = localPosition;
                weapons[i].localEuler = localEuler;
                return;
            }

            var grown = new GripOffset[weapons.Length + 1];
            weapons.CopyTo(grown, 0);
            grown[^1] = new GripOffset
            {
                kind = kind,
                localPosition = localPosition,
                localEuler = localEuler,
            };
            weapons = grown;
        }
    }
}
