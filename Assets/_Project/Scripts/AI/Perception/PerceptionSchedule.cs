namespace ShadowVale.AI.Perception
{
    /// <summary>
    /// When an enemy looks for the player (NFR: 60 FPS with a map full of guards). A calm guard
    /// looks <see cref="CalmHz"/> times a second, and guards are spread over the interval by their
    /// id so their raycasts do not all land on the same frame; a guard who is fighting, or already
    /// sees the player or is growing suspicious, looks every frame — reaction time is never worse
    /// than one interval (0.1 s), well under the 1.5 s it takes to spot someone.
    /// Deterministic: the phase comes from the id, not from a random number.
    /// </summary>
    public struct PerceptionSchedule
    {
        public const float CalmHz = 10f;

        private float _nextAt;
        private bool _started;

        public static float Interval => 1f / CalmHz;

        /// <summary>True if the sensors should run this frame.</summary>
        public bool Due(float now, bool alert, string agentId)
        {
            if (!_started)
            {
                _started = true;
                _nextAt = now + Interval * Phase(agentId);
            }
            if (alert) { _nextAt = now + Interval; return true; }
            // A fixed cadence, not "now + interval": frame times never divide the interval exactly,
            // and pushing from now would drop a look every few intervals.
            if (now < _nextAt - 1e-4f) return false;
            _nextAt = _nextAt + Interval > now ? _nextAt + Interval : now + Interval;
            return true;
        }

        /// <summary>Forget the timing (after a reset or a checkpoint load): look on the next frame.</summary>
        public void Reset() { _started = true; _nextAt = 0; }

        /// <summary>A stable 0..1 offset from the id (FNV-1a), the same on every run and machine.</summary>
        public static float Phase(string id)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in id ?? "") { h ^= c; h *= 16777619; }
                return (h % 1000) / 1000f;
            }
        }
    }
}
