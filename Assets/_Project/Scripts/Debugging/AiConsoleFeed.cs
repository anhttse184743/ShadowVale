using System;
using System.Globalization;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using UnityEngine;

namespace ShadowVale.Debugging
{
    /// <summary>
    /// Turns AI events from the <see cref="EventBus"/> into console lines. The AI never knows the
    /// console exists; telemetry listens to the same events.
    /// </summary>
    public sealed class AiConsoleFeed : IDisposable
    {
        private readonly AiConsoleLog _log;
        private readonly Action<EnemyStateChangedEvent> _onState;
        private readonly Action<PlayerContactEvent> _onContact;
        private readonly Action<NoiseEvent> _onNoise;
        private readonly Action<TakedownEvent> _onTakedown;

        /// <summary>Also log footsteps nobody heard (very chatty).</summary>
        public bool VerboseNoise { get; set; }

        public AiConsoleFeed(AiConsoleLog log)
        {
            _log = log;
            _onState = e => Add(AiLogCategory.State, e.AgentId, $"{e.AgentId}  {e.From} → {e.To}");
            _onContact = e => Add(AiLogCategory.Contact, e.AgentId,
                $"{e.AgentId} {(e.Seen ? "thấy Nam" : "bị Nam tấn công")} tại {Fmt(e.PlayerPosition)}");
            _onNoise = e =>
            {
                if (!VerboseNoise && e.HeardBy == 0 && e.Kind == NoiseKind.Footstep) return;
                Add(AiLogCategory.Noise, null, $"{e.Kind} r={e.Radius:0.#} m tại {Fmt(e.Position)} — {e.HeardBy} lính nghe");
            };
            _onTakedown = e => Add(AiLogCategory.Combat, e.VictimId, $"{e.VictimId} bị hạ im lặng tại {Fmt(e.Position)}");
            EventBus.Subscribe(_onState);
            EventBus.Subscribe(_onContact);
            EventBus.Subscribe(_onNoise);
            EventBus.Subscribe(_onTakedown);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe(_onState);
            EventBus.Unsubscribe(_onContact);
            EventBus.Unsubscribe(_onNoise);
            EventBus.Unsubscribe(_onTakedown);
        }

        public void Add(AiLogCategory category, string agentId, string text)
        {
            string squad = agentId != null ? AgentRegistry.Find(agentId)?.SquadId : null;
            _log.Add(new AiLogEntry(Time.time, category, squad, text));
        }

        private static string Fmt(Vector3 p) => string.Format(CultureInfo.InvariantCulture, "({0:0.#}, {1:0.#})", p.x, p.z);
    }
}
