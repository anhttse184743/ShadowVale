using System;
using UnityEngine;

namespace ShadowVale.Gameplay.Player
{
    /// <summary>
    /// Turns the forearm twist bones with the hand.
    /// <para>
    /// Nam's sleeve is skinned to two bones along each forearm that take 40% and 80% of the hand's roll, so a
    /// turned palm spreads along the sleeve instead of wringing it at one joint. Blender bakes that roll into the
    /// clips it exports, but a clip posed in Unity (knife carry, the crouched throw) or played as a pure Humanoid
    /// one carries none, and layers and cross-fades mix the baked values differently from the hand they end up
    /// with. So the bones are set here, from the hand as it is once everything has animated it.
    /// </para>
    /// Filled in by NamModelSetup when the model is put into the prefab.
    /// </summary>
    [DefaultExecutionOrder(9000)]
    public sealed class ForearmTwist : MonoBehaviour
    {
        [Serializable]
        public struct Limb
        {
            public Transform hand;
            [Tooltip("The hand's local rotation in the model's rest pose: its roll is counted from there.")]
            public Quaternion handRest;
            [Tooltip("Middle of the roll this hand really uses, in degrees. A roll is only known to within a full " +
                     "turn: it is counted within half a turn either side of this, so the cut falls where the " +
                     "wrist never goes instead of at 'palm up', which the support hand passes in every reload.")]
            public float centre;
            [Tooltip("Twist bones under the forearm, elbow end first.")]
            public Transform[] bones;
            public Quaternion[] bonesRest;
            [Tooltip("Share of the hand's roll each bone takes.")]
            public float[] shares;
        }

        [SerializeField] private Limb[] limbs = Array.Empty<Limb>();

        public void Configure(Limb[] value) => limbs = value ?? Array.Empty<Limb>();

        private void LateUpdate() => Apply();

        /// <summary>Sets every twist bone from its hand's current roll.</summary>
        public void Apply()
        {
            foreach (Limb limb in limbs)
            {
                if (limb.hand == null || limb.bones == null) continue;
                float roll = Roll(Quaternion.Inverse(limb.handRest) * limb.hand.localRotation, limb.centre);
                for (int b = 0; b < limb.bones.Length; b++)
                {
                    if (limb.bones[b] == null) continue;
                    limb.bones[b].localRotation = limb.bonesRest[b] * Quaternion.AngleAxis(roll * limb.shares[b], Vector3.up);
                }
            }
        }

        /// <summary>
        /// Degrees a rotation turns about the bone's own length (its local Y), the bend left out: the twist part
        /// of a swing-twist split, the measure Blender's drivers use for these bones. Counted within half a turn
        /// either side of <paramref name="centre"/>.
        /// </summary>
        public static float Roll(Quaternion rotation, float centre = 0f)
        {
            float roll = 2f * Mathf.Atan2(rotation.y, rotation.w) * Mathf.Rad2Deg;
            return centre + Mathf.DeltaAngle(centre, roll);
        }
    }
}
