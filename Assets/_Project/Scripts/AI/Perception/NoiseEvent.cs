using ShadowVale.Core.Events;
using UnityEngine;

namespace ShadowVale.AI.Perception
{
    /// <summary>
    /// Published on the <see cref="EventBus"/> after a noise has been dispatched to every listener,
    /// so <see cref="HeardBy"/> is final. Telemetry (<c>noise_emitted</c>) and the debug console read it.
    /// </summary>
    public readonly struct NoiseEvent : IEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly NoiseKind Kind;
        public readonly int HeardBy;

        public NoiseEvent(Vector3 position, float radius, NoiseKind kind, int heardBy)
        {
            Position = position; Radius = radius; Kind = kind; HeardBy = heardBy;
        }
    }
}
