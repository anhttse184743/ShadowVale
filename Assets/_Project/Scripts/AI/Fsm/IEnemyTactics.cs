using UnityEngine;

namespace ShadowVale.AI.Fsm
{
    /// <summary>
    /// The map's say in how an enemy fights. The six states are shared by every map; these hooks are
    /// where a map adds its own rules (Map 1: shooting the hostage, its few cover points).
    /// </summary>
    public interface IEnemyTactics
    {
        /// <summary>Engaged but the player is out of sight: something else to fight? (pure query)</summary>
        bool TryGetAlternativeTarget(EnemyContext ctx, out Vector3 target);

        /// <summary>Fire at the target returned by <see cref="TryGetAlternativeTarget"/>; the body already faces it.</summary>
        void FireAtAlternative(EnemyContext ctx, Vector3 target);

        /// <summary>A cover position to take on first contact when no squad plan assigned one.</summary>
        bool TryFindCover(EnemyContext ctx, Vector3 threat, out Vector3 cover);

        /// <summary>Where to fall back to when badly hurt; false = stand and fight.</summary>
        bool TryFindRetreatPoint(EnemyContext ctx, Vector3 threat, out Vector3 point);
    }

    /// <summary>No alternative targets, no local cover, retreat straight away from the threat.</summary>
    public class DefaultEnemyTactics : IEnemyTactics
    {
        public static readonly DefaultEnemyTactics Instance = new DefaultEnemyTactics();

        public virtual bool TryGetAlternativeTarget(EnemyContext ctx, out Vector3 target)
        {
            target = default;
            return false;
        }

        public virtual void FireAtAlternative(EnemyContext ctx, Vector3 target) { }

        public virtual bool TryFindCover(EnemyContext ctx, Vector3 threat, out Vector3 cover)
        {
            cover = default;
            return false;
        }

        public virtual bool TryFindRetreatPoint(EnemyContext ctx, Vector3 threat, out Vector3 point)
        {
            Vector3 away = Vector3.ProjectOnPlane(ctx.Body.Position - threat, Vector3.up);
            if (away.sqrMagnitude < .01f) away = -ctx.Body.Forward;
            Vector3 desired = ctx.Body.Position + away.normalized * ctx.Tuning.RetreatDistance;
            return ctx.Body.TrySampleReachable(desired, 4f, out point);
        }
    }
}
