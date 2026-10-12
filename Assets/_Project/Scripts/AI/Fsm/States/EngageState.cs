namespace ShadowVale.AI.Fsm.States
{
    /// <summary>
    /// Fighting. In sight: aimed fire within range; out of range he closes in — unless he holds a
    /// position whose role holds (<see cref="EnemyTuning.HoldRoles"/>: blocking an escape route,
    /// by default). Out of sight: the map's alternative target if any
    /// (Map 1: the hostage), otherwise suppressive fire at the last-seen spot for a moment.
    /// </summary>
    public sealed class EngageState : EnemyStateBase
    {
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            var m = ctx.Memory; var t = ctx.Tuning; var body = ctx.Body; var s = ctx.Sense;
            if (s.SeesPlayer)
            {
                body.Face(s.PlayerPosition);
                if (s.Distance <= t.AttackRange)
                {
                    body.Stop(); body.Animate(false);
                    ctx.Weapon.FireAtPlayer();
                }
                else if (m.HasAssignment && m.ReachedAssignment && t.Holds(m.AssignedRole))
                {
                    body.Stop(); body.Animate(false);
                }
                else
                {
                    body.MoveTo(s.PlayerPosition);
                    body.Animate(true);
                }
                return;
            }
            if (ctx.Tactics.TryGetAlternativeTarget(ctx, out var other))
            {
                body.Stop(); body.Animate(false);
                body.Face(other);
                ctx.Tactics.FireAtAlternative(ctx, other);
                return;
            }
            body.Stop(); body.Animate(false);
            body.Face(m.LastKnownPlayerPosition);
            // Past the suppression window only one who does not hunt is still here: he watches.
            if (ctx.Now < m.SuppressUntil) ctx.Weapon.FireSuppressive(m.LastKnownPlayerPosition);
        }
    }
}
