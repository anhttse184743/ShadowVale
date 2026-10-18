using ShadowVale.Core.Events;
using UnityEngine;

namespace ShadowVale.AI.Fsm
{
    /// <summary>An enemy changed FSM state (debug console, telemetry <c>enemy_state_changed</c>).</summary>
    public readonly struct EnemyStateChangedEvent : IEvent
    {
        public readonly string AgentId;
        public readonly EnemyStateId From, To;

        public EnemyStateChangedEvent(string agentId, EnemyStateId from, EnemyStateId to)
        {
            AgentId = agentId; From = from; To = to;
        }
    }

    /// <summary>
    /// An enemy went from unaware to fighting: he spotted the player or was shot by him. The squad
    /// coordinator re-plans on it; telemetry records <c>player_spotted</c>.
    /// </summary>
    public readonly struct PlayerContactEvent : IEvent
    {
        public readonly string AgentId;
        public readonly Vector3 PlayerPosition;
        /// <summary>False when the contact came from being hurt, not from seeing the player.</summary>
        public readonly bool Seen;

        public PlayerContactEvent(string agentId, Vector3 playerPosition, bool seen)
        {
            AgentId = agentId; PlayerPosition = playerPosition; Seen = seen;
        }
    }
}
