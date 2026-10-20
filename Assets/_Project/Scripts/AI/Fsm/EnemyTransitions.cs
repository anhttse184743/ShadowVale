namespace ShadowVale.AI.Fsm
{
    /// <summary>
    /// Which of the six states an enemy should be in, from its memory and this tick's senses. All
    /// transition rules live here so the states only act. Priority, highest first:
    /// <list type="number">
    /// <item>Retreat — badly hurt while fighting (started by the brain).</item>
    /// <item>SpotPlayer — something in view, suspicion not yet full.</item>
    /// <item>TakeCover — fighting, with a cover/plan position not yet reached (and, by default, the
    /// player not in sight and range: a guard who can fire fights first).</item>
    /// <item>Engage — fighting and in sight; or just lost sight (suppressing) or another target in reach;
    /// or, for one who does not hunt (<see cref="EnemyTuning.HuntWhenLost"/>), still fighting at all.</item>
    /// <item>Investigate — checking a noise / hunting the last-seen spot, or walking back.</item>
    /// <item>Patrol.</item>
    /// </list>
    /// </summary>
    public static class EnemyTransitions
    {
        public static EnemyStateId Next(EnemyContext ctx)
        {
            var m = ctx.Memory;
            bool sees = ctx.Sense.SeesPlayer, engaged = ctx.Engaged;
            if (m.Retreating) return EnemyStateId.Retreat;
            if (sees && !engaged) return EnemyStateId.SpotPlayer;
            if (engaged)
            {
                // Covering his commander's move: he fires from where he is, whatever his assignment.
                if (ctx.Now < m.CoverFireUntil) return EnemyStateId.Engage;
                bool inRange = sees && ctx.Sense.Distance <= ctx.Tuning.AttackRange;
                if (m.HasAssignment && !m.ReachedAssignment && ctx.Now < m.TakeCoverUntil
                    && !(inRange && ctx.Tuning.EngageInRangeFirst)) return EnemyStateId.TakeCover;
                if (sees) return EnemyStateId.Engage;
                if (ctx.Tactics.TryGetAlternativeTarget(ctx, out _)) return EnemyStateId.Engage;
                if (ctx.Now < m.SuppressUntil || !ctx.Tuning.HuntWhenLost) return EnemyStateId.Engage;
            }
            if (ctx.Alerted || m.Returning) return EnemyStateId.Investigate;
            return EnemyStateId.Patrol;
        }
    }
}
