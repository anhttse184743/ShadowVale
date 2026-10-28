using ShadowVale.AI.Contracts;

namespace ShadowVale.AI.Fsm
{
    /// <summary>Everything a state needs: body, weapon, memory, tuning, map tactics, this tick's senses and clock.</summary>
    public sealed class EnemyContext
    {
        public string AgentId;
        public IEnemyBody Body;
        public IEnemyWeapon Weapon;
        public EnemyBlackboard Memory;
        public EnemyTuning Tuning;
        public IEnemyTactics Tactics;

        public PerceptionSample Sense;
        public float Now;
        public float DeltaTime;

        public bool Engaged => Memory.Engaged(Now);
        public bool Alerted => Memory.Alerted(Now);
    }
}
