using UnityEngine;

namespace ShadowVale.AI.Perception
{
    /// <summary>
    /// When a hit is a silent takedown (G04): a silent weapon, from behind and close, on an enemy
    /// who has not spotted the player — even one away checking a noise, which is what a thrown
    /// stone is for — and never on a boss.
    /// </summary>
    public static class TakedownRule
    {
        public const float MaxDistance = 3f;
        /// <summary>Angle between the victim's facing and the attacker beyond which the attacker is "behind".</summary>
        public const float MinAngleFromFacing = 100f;

        public static bool IsFromBehind(Vector3 victimPosition, Vector3 victimForward, Vector3 attackerPosition)
        {
            Vector3 toAttacker = Vector3.ProjectOnPlane(attackerPosition - victimPosition, Vector3.up);
            return toAttacker.magnitude < MaxDistance && Vector3.Angle(victimForward, toAttacker) > MinAngleFromFacing;
        }

        public static bool IsSilent(Vector3 victimPosition, Vector3 victimForward, Vector3 attackerPosition,
            bool silentWeapon, bool victimEngaged, bool victimIsBoss)
            => silentWeapon && !victimEngaged && !victimIsBoss && IsFromBehind(victimPosition, victimForward, attackerPosition);
    }
}
