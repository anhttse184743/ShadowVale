using UnityEngine;

namespace ShadowVale.AI.Fsm.States
{
    /// <summary>
    /// Hurry to the noise (or the last-seen spot while hunting), stand and look around for a while,
    /// then walk back to where he came from — his post, or the spot on his patrol.
    /// </summary>
    public sealed class InvestigateState : EnemyStateBase
    {
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            var m = ctx.Memory; var t = ctx.Tuning; var body = ctx.Body;
            if (ctx.Alerted)
            {
                if (m.SearchUntil <= 0)
                {
                    body.SetPace(t.InvestigatePace);
                    body.MoveToIfNew(m.InvestigatePoint);
                    if (body.HasArrived(t.InvestigateArriveDistance)) m.SearchUntil = ctx.Now + t.SearchSeconds;
                    body.Animate(true);
                    return;
                }
                body.Stop();
                body.Turn(Mathf.Sin(ctx.Now * 1.2f) * t.LookAroundDegreesPerSecond * deltaTime);
                body.Animate(true);
                if (ctx.Now < m.SearchUntil) return;
                // Nothing there: back to where he came from.
                m.AlertUntil = 0; m.SearchUntil = 0; m.Returning = true;
                body.SetPace(1f);
                return;
            }
            // Walking back.
            body.MoveToIfNew(m.ReturnPoint);
            if (body.HasArrived(t.ReturnArriveDistance))
            {
                m.Returning = false;
                body.Rotation = m.ReturnRotation;
                if (m.PatrolPoints != null && m.PatrolPoints.Length > 0) body.MoveTo(m.PatrolPoints[m.PatrolIndex]);
            }
            body.Animate(true);
        }
    }
}
