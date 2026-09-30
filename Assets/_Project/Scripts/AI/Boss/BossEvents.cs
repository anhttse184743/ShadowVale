using ShadowVale.Core.Events;
using UnityEngine;

namespace ShadowVale.AI.Boss
{
    /// <summary>The boss left his position for another cover node (telemetry, debug console).</summary>
    public readonly struct BossRelocatedEvent : IEvent
    {
        public readonly string BossId;
        public readonly int Node;
        public readonly Vector3 From, To;
        /// <summary>interval | hurt.</summary>
        public readonly string Reason;

        public BossRelocatedEvent(string bossId, int node, Vector3 from, Vector3 to, string reason)
        {
            BossId = bossId; Node = node; From = from; To = to; Reason = reason;
        }
    }

    /// <summary>A grenade left the thrower's hand.</summary>
    public readonly struct GrenadeThrownEvent : IEvent
    {
        public readonly string ThrowerId;
        public readonly Vector3 From, Target;
        public GrenadeThrownEvent(string throwerId, Vector3 from, Vector3 target) { ThrowerId = throwerId; From = from; Target = target; }
    }

    /// <summary>A grenade went off; <see cref="PlayerDamage"/> is 0 when the player got clear.</summary>
    public readonly struct GrenadeExplodedEvent : IEvent
    {
        public readonly string ThrowerId;
        public readonly Vector3 Position;
        public readonly float PlayerDamage;
        public GrenadeExplodedEvent(string throwerId, Vector3 position, float playerDamage)
        {
            ThrowerId = throwerId; Position = position; PlayerDamage = playerDamage;
        }
    }

    /// <summary>The boss called for help (once, below his threshold).</summary>
    public readonly struct BossReinforcementsEvent : IEvent
    {
        public readonly string BossId;
        public readonly int Requested, Arrived;
        /// <summary>Agent ids that answered, "+"-joined.</summary>
        public readonly string Agents;

        public BossReinforcementsEvent(string bossId, int requested, int arrived, string agents)
        {
            BossId = bossId; Requested = requested; Arrived = arrived; Agents = agents;
        }
    }
}
