using ShadowVale.AI.Contracts;
using UnityEngine;

namespace ShadowVale.AI.Perception
{
    /// <summary>
    /// Line-of-sight detection shared by every enemy on every map (G04). Stateless: suspicion
    /// build-up and memory belong to the FSM, this only answers "is the player visible right now".
    /// </summary>
    public static class VisionSensor
    {
        /// <summary>
        /// Geometry only (no physics): range by stance and awareness, then the view cone.
        /// Unaware: a forward cone, shorter while the player crouches and far shorter while hidden;
        /// only brushing right past is noticed from any side. Engaged: all around, full range.
        /// </summary>
        public static bool InView(Vector3 eye, Vector3 forward, Vector3 target, bool engaged,
            bool crouched, bool hidden, in VisionProfile profile, out float distance)
        {
            Vector3 delta = target - eye;
            distance = delta.magnitude;
            if (engaged) return distance <= profile.Range;
            float range = profile.Range * (hidden ? profile.HiddenRangeScale : crouched ? profile.CrouchRangeScale : 1f);
            if (distance > range) return false;
            float touch = crouched ? profile.TouchRadiusCrouched : profile.TouchRadiusStanding;
            return distance <= touch
                || Vector3.Angle(forward, Vector3.ProjectOnPlane(delta, Vector3.up)) <= profile.ConeAngle * .5f;
        }

        /// <summary>
        /// Full check from an observer standing at <paramref name="observerPosition"/>: view cone,
        /// then map occluders (foliage — these hide the player even from a guard already hunting
        /// him), then a physics ray against <paramref name="obstructionMask"/>.
        /// </summary>
        public static bool CanSee(Vector3 observerPosition, Vector3 forward, bool engaged, in VisionProfile profile,
            IPlayerObservable player, ISightOccluder occluder, int obstructionMask, out float distance)
        {
            distance = float.PositiveInfinity;
            if (player == null || !player.IsAlive) return false;
            Vector3 eye = observerPosition + Vector3.up * profile.EyeHeight;
            Vector3 target = player.SightPoint;
            if (!InView(eye, forward, target, engaged, player.IsCrouching, player.IsHidden, profile, out distance)) return false;
            if (occluder != null && occluder.Blocks(eye, target)) return false;
            if (Physics.Raycast(eye, (target - eye).normalized, out var hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
                return player.Owns(hit.transform);
            return true;
        }
    }
}
