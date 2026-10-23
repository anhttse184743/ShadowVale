using ShadowVale.Core.Events;
using UnityEngine;

namespace ShadowVale.AI.Perception
{
    /// <summary>A silent takedown happened (telemetry: stealth vs combat; debug console).</summary>
    public readonly struct TakedownEvent : IEvent
    {
        public readonly string VictimId;
        public readonly Vector3 Position;

        public TakedownEvent(string victimId, Vector3 position) { VictimId = victimId; Position = position; }
    }
}
