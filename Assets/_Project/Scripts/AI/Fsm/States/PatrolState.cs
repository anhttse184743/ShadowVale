namespace ShadowVale.AI.Fsm.States
{
    /// <summary>Walks his patrol loop point to point; a guard with no loop holds his post.</summary>
    public sealed class PatrolState : EnemyStateBase
    {
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            var m = ctx.Memory; var body = ctx.Body;
            body.Resume();
            var points = m.PatrolPoints;
            if (points != null && points.Length > 0 && body.HasArrived(ctx.Tuning.PatrolArriveDistance))
            {
                m.PatrolIndex = (m.PatrolIndex + 1) % points.Length;
                body.MoveTo(points[m.PatrolIndex]);
            }
            body.Animate(true);
        }
    }
}
