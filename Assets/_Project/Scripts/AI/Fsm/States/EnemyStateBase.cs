using ShadowVale.Core.Fsm;

namespace ShadowVale.AI.Fsm.States
{
    /// <summary>
    /// States are stateless and shared by every enemy: everything they remember is in the
    /// <see cref="EnemyBlackboard"/>, and <see cref="EnemyTransitions"/> decides when to switch.
    /// </summary>
    public abstract class EnemyStateBase : IState<EnemyContext>
    {
        public virtual void Enter(EnemyContext ctx) { }
        public abstract void Tick(EnemyContext ctx, float deltaTime);
        public virtual void Exit(EnemyContext ctx) { }
    }
}
