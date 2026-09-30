namespace ShadowVale.AI.Fsm.States
{
    /// <summary>
    /// Badly hurt: fall back to a point away from the player, then hold there facing the threat,
    /// firing only if cornered in sight and range. After the hold he rejoins the fight (once).
    /// </summary>
    public sealed class RetreatState : EnemyStateBase
    {
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            var m = ctx.Memory; var t = ctx.Tuning; var body = ctx.Body; var s = ctx.Sense;
            if (m.RetreatHoldUntil <= 0)
            {
                body.SetPace(t.RetreatPace);
                body.MoveToIfNew(m.RetreatPoint);
                if (body.HasArrived(1f) || ctx.Now >= m.RetreatGiveUpAt)
                {
                    m.RetreatHoldUntil = ctx.Now + t.RetreatHoldSeconds;
                    body.Stop();
                    body.SetPace(1f);
                }
                body.Animate(true);
                return;
            }
            body.Stop(); body.Animate(false);
            body.Face(s.SeesPlayer ? s.PlayerPosition : m.LastKnownPlayerPosition);
            if (s.SeesPlayer && s.Distance <= t.AttackRange) ctx.Weapon.FireAtPlayer();
            if (ctx.Now >= m.RetreatHoldUntil) { m.Retreating = false; m.RetreatDone = true; }
        }
    }
}
