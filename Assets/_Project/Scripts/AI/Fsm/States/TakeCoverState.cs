namespace ShadowVale.AI.Fsm.States
{
    /// <summary>
    /// Run to the assigned position (squad plan or nearby cover), firing on the way when the
    /// player is in sight and range. Arriving hands over to Engage, which then holds the position.
    /// </summary>
    public sealed class TakeCoverState : EnemyStateBase
    {
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            var m = ctx.Memory; var t = ctx.Tuning; var body = ctx.Body; var s = ctx.Sense;
            body.SetPace(t.InvestigatePace);
            body.MoveToIfNew(m.AssignedPosition);
            if (s.SeesPlayer && s.Distance <= t.AttackRange) ctx.Weapon.FireAtPlayer();
            if (body.HasArrived(t.CoverArriveDistance))
            {
                m.ReachedAssignment = true;
                body.Stop();
                body.SetPace(1f);
            }
            body.Animate(true);
        }
    }
}
