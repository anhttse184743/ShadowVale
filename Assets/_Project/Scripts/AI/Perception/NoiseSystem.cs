using System.Collections.Generic;
using ShadowVale.Core.Events;
using UnityEngine;

namespace ShadowVale.AI.Perception
{
    /// <summary>
    /// One per loaded map: every noise source calls <see cref="Emit"/>, every enemy registers as a
    /// listener. Map code no longer loops over its own enemy list, so any map gets the same
    /// hearing by owning one of these. Main thread only.
    /// </summary>
    public sealed class NoiseSystem
    {
        private readonly List<INoiseListener> _listeners = new List<INoiseListener>();

        public int ListenerCount => _listeners.Count;

        public void Register(INoiseListener listener)
        {
            if (listener != null && !_listeners.Contains(listener)) _listeners.Add(listener);
        }

        public void Unregister(INoiseListener listener) => _listeners.Remove(listener);

        /// <summary>Dispatches the noise to every listener, publishes a <see cref="NoiseEvent"/>, returns how many heard it.</summary>
        public int Emit(Vector3 position, float radius, NoiseKind kind = NoiseKind.Other)
        {
            int heard = 0;
            for (int i = 0; i < _listeners.Count; i++)
            {
                var listener = _listeners[i];
                // A destroyed MonoBehaviour that missed its Unregister compares equal to null.
                if (listener is Object unityObject && unityObject == null) continue;
                if (listener.OnNoise(position, radius, kind)) heard++;
            }
            EventBus.Publish(new NoiseEvent(position, radius, kind, heard));
            return heard;
        }
    }
}
