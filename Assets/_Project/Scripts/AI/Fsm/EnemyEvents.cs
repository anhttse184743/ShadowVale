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

    /// <summary>
    /// An enemy joined a fight already knowing where the player is (a wave sent at a held lane, a squad-mate's
    /// radio), without spotting him himself. Telemetry opens or joins the encounter on it; unlike
    /// <see cref="PlayerContactEvent"/> the coordinator ignores it, so it never radios anyone else in.
    /// </summary>
    public readonly struct EnemyJoinedFightEvent : IEvent
    {
        public readonly string AgentId;
        public readonly Vector3 PlayerPosition;

        public EnemyJoinedFightEvent(string agentId, Vector3 playerPosition)
        {
            AgentId = agentId; PlayerPosition = playerPosition;
        }
    }
}
