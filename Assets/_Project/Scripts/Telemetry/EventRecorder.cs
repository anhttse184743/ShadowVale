using System;
using System.Collections.Generic;
using ShadowVale.Data.Telemetry;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// The session's event list, kept small enough to upload in one batch: frequent types are
    /// rate-limited per key (a footstep a second, not every step), and past <see cref="MaxEvents"/>
    /// nothing more is kept. Every event is still counted, so the summary knows what was dropped.
    /// </summary>
    public sealed class EventRecorder
    {
        private readonly string _mapId;
        private readonly Dictionary<string, double> _lastAt = new Dictionary<string, double>();

        public int MaxEvents { get; }
        public List<TelemetryEventDto> Events { get; } = new List<TelemetryEventDto>();
        public Dictionary<string, int> Counts { get; } = new Dictionary<string, int>();
        public int Dropped { get; private set; }

        public EventRecorder(string mapId, int maxEvents)
        {
            _mapId = mapId; MaxEvents = Math.Max(1, maxEvents);
        }

        /// <summary>Counts the event and keeps it unless capped or rate-limited.</summary>
        /// <param name="minInterval">Seconds that must pass between two kept events with the same <paramref name="rateKey"/> (default: the type).</param>
        /// <returns>Whether the event was kept.</returns>
        public bool Add(string type, double t, Dictionary<string, object> payload, double minInterval = 0, string rateKey = null)
        {
            Counts[type] = Counts.TryGetValue(type, out int n) ? n + 1 : 1;
            if (minInterval > 0)
            {
                string key = rateKey ?? type;
                if (_lastAt.TryGetValue(key, out double last) && t - last < minInterval) return false;
                _lastAt[key] = t;
            }
            if (Events.Count >= MaxEvents) { Dropped++; return false; }
            Events.Add(new TelemetryEventDto { Type = type, T = Round(t), MapId = _mapId, Payload = payload ?? new Dictionary<string, object>() });
            return true;
        }

        /// <summary>Two decimals: centimetres and hundredths of a second are plenty, and halve the payload.</summary>
        public static double Round(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    }
}
