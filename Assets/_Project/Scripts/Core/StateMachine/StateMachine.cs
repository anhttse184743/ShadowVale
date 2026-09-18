using System;

namespace ShadowVale.Core.Fsm
{
    /// <summary>
    /// Generic finite state machine. The six-state enemy FSM (Patrol → Investigate → SpotPlayer →
    /// TakeCover → SuppressEngage → Retreat) is built on this; so are UI screen flows.
    /// </summary>
    public sealed class StateMachine<TContext>
    {
        private readonly TContext _ctx;

        public IState<TContext> Current { get; private set; }
        public IState<TContext> Previous { get; private set; }
        public float TimeInState { get; private set; }

        public event Action<IState<TContext>, IState<TContext>> StateChanged;

        public StateMachine(TContext ctx) { _ctx = ctx; }

        public void ChangeState(IState<TContext> next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (ReferenceEquals(next, Current)) return;
            Current?.Exit(_ctx);
            Previous = Current;
            Current = next;
            TimeInState = 0f;
            Current.Enter(_ctx);
            StateChanged?.Invoke(Previous, Current);
        }

        public void Tick(float deltaTime)
        {
            if (Current == null) return;
            TimeInState += deltaTime;
            Current.Tick(_ctx, deltaTime);
        }

        public bool IsIn<TState>() where TState : IState<TContext> => Current is TState;
    }
}
