namespace ShadowVale.AI.Fsm.States
{
    /// <summary>Something's there — stop and stare at it rather than walk on, while suspicion builds.</summary>
    public sealed class SpotPlayerState : EnemyStateBase
    {
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            ctx.Body.Stop();
            ctx.Body.Animate(false);
            ctx.Body.Face(ctx.Sense.PlayerPosition);
        }
    }
}
