using UnityEngine;

namespace ShadowVale.AI.Fsm.States
{
    /// <summary>
    /// Run to the assigned position (squad plan or nearby cover), firing on the way when the
    /// player is in sight and range. Arriving hands over to Engage, which then holds the position.
    /// With <see cref="EnemyTuning.FireOnTheMove"/> (a commander) he fires only at a player ahead of him,
    /// walking slower and turned towards him; with the player behind or beside him he just runs.
    /// </summary>
    public sealed class TakeCoverState : EnemyStateBase
    {
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            var m = ctx.Memory; var t = ctx.Tuning; var body = ctx.Body; var s = ctx.Sense;
            body.MoveToIfNew(m.AssignedPosition);
            bool canFire = s.SeesPlayer && s.Distance <= t.AttackRange;
            if (t.FireOnTheMove)
            {
                Vector3 heading = Heading(body, m.AssignedPosition);
                Vector3 toPlayer = s.PlayerPosition - body.Position; toPlayer.y = 0;
                bool ahead = canFire && heading.sqrMagnitude > 1e-4f && toPlayer.sqrMagnitude > 1e-4f
                             && Vector3.Angle(heading, toPlayer) <= t.MoveFireConeDegrees;
                body.SetPace(ahead ? t.MoveFirePace : t.InvestigatePace);
                body.FacesManually = ahead;
                if (ahead)
                {
                    body.Face(body.Position + Twist(heading, toPlayer, t.MoveFireTwistDegrees));
                    ctx.Weapon.FireAtPlayer();
                }
            }
            else
            {
                body.SetPace(t.InvestigatePace);
                if (canFire) ctx.Weapon.FireAtPlayer();
            }
            if (body.HasArrived(t.CoverArriveDistance))
            {
                m.ReachedAssignment = true;
                body.Stop();
                body.SetPace(1f);
                body.FacesManually = false;
            }
            body.Animate(true);
        }

        public override void Exit(EnemyContext ctx) => ctx.Body.FacesManually = false;

        /// <summary>Which way he walks: his velocity, or towards where he is going while he starts off.</summary>
        public static Vector3 Heading(ShadowVale.AI.Contracts.IEnemyBody body, Vector3 destination)
        {
            Vector3 v = body.Velocity; v.y = 0;
            if (v.sqrMagnitude > .01f) return v.normalized;
            Vector3 to = destination - body.Position; to.y = 0;
            return to.sqrMagnitude > 1e-4f ? to.normalized : Vector3.zero;
        }

        /// <summary><paramref name="heading"/> turned towards <paramref name="target"/> by at most <paramref name="maxDegrees"/>.</summary>
        public static Vector3 Twist(Vector3 heading, Vector3 target, float maxDegrees)
            => Vector3.RotateTowards(heading.normalized, target.normalized, maxDegrees * Mathf.Deg2Rad, 0f);
    }
}
