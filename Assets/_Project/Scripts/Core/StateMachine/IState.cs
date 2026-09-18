namespace ShadowVale.Core.Fsm
{
    /// <summary>A state of a <see cref="StateMachine{TContext}"/>. Enemy FSM states and UI screens both implement this.</summary>
    public interface IState<in TContext>
    {
        void Enter(TContext ctx);
        void Tick(TContext ctx, float deltaTime);
        void Exit(TContext ctx);
    }
}
