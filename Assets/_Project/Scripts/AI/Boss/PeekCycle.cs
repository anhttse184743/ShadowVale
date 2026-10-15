using UnityEngine;

namespace ShadowVale.AI.Boss
{
    /// <summary>
    /// Fighting from cover in bursts rather than standing in the open at it: out at the firing point
    /// (the cover node, which sees the player) for <see cref="OutSeconds"/>, then back to a spot behind
    /// the cover that does not, for <see cref="HideSeconds"/>, and out again. Each phase varies by ±30%
    /// so the player cannot time his shots to it. Pure and clock-driven; the body does the walking.
    /// </summary>
    public sealed class PeekCycle
    {
        public enum Step { None, Out, Back }

        public const float Jitter = .3f;

        public float OutSeconds { get; }
        public float HideSeconds { get; }
        public bool Active { get; private set; }
        /// <summary>At (or heading to) the firing point rather than behind the cover.</summary>
        public bool IsOut { get; private set; }
        public Vector3 FiringPoint { get; private set; }
        public Vector3 HidePoint { get; private set; }
        public int Node { get; private set; }

        private readonly System.Random _rng;
        private float _until;

        public PeekCycle(float outSeconds, float hideSeconds, System.Random rng = null)
        {
            OutSeconds = Mathf.Max(.1f, outSeconds);
            HideSeconds = Mathf.Max(.1f, hideSeconds);
            _rng = rng;
        }

        /// <summary>He has just reached his cover node: fire from it first, then duck behind.</summary>
        public void Begin(Vector3 firingPoint, Vector3 hidePoint, int node, float now)
        {
            Active = true; IsOut = true;
            FiringPoint = firingPoint; HidePoint = hidePoint; Node = node;
            _until = now + Vary(OutSeconds);
        }

        public void Stop() => Active = false;

        /// <summary>Where to go now, if anywhere.</summary>
        public Step Tick(float now)
        {
            if (!Active || now < _until) return Step.None;
            IsOut = !IsOut;
            _until = now + Vary(IsOut ? OutSeconds : HideSeconds);
            return IsOut ? Step.Out : Step.Back;
        }

        private float Vary(float seconds)
            => _rng == null ? seconds : seconds * (1f + Jitter * (float)(_rng.NextDouble() * 2 - 1));
    }
}
