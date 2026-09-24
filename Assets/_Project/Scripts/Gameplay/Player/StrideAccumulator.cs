using UnityEngine;

namespace ShadowVale.Gameplay.Player
{
    /// <summary>
    /// Turns distance travelled into footsteps, alternating feet.
    /// <para>
    /// Counting metres rather than seconds is what makes the cadence follow the character for
    /// free: walk and the steps space out, sprint and they close up, wade through the river and
    /// the slowdown is audible without anyone tuning a second timer. A timer would have to be
    /// re-tuned for every speed the player can move at, and would keep ticking while the
    /// character is pinned against a wall with the stick held forward.
    /// </para>
    /// </summary>
    public sealed class StrideAccumulator
    {
        /// <summary>
        /// A teleport, a checkpoint restore or a long frame hitch can hand us metres the legs
        /// never walked. Without a ceiling that arrives as a burst of footsteps in one frame,
        /// so the backlog is capped at this many strides.
        /// </summary>
        private const float MaxBacklogStrides = 2f;

        private float _pending;

        /// <summary>Metres banked toward the next step. Exposed for tests and debugging.</summary>
        public float Pending => _pending;

        /// <summary>True when the next step to be taken is the left foot.</summary>
        public bool NextIsLeft { get; private set; } = true;

        /// <summary>
        /// Banks <paramref name="distance"/> metres and reports whether that completed a step.
        /// One call yields at most one step, so a caller that moves several strides in a single
        /// frame hears a step now and the rest on following frames instead of all at once.
        /// </summary>
        /// <param name="strideLength">Metres between footfalls. Zero or less disables stepping.</param>
        /// <param name="leftFoot">Which foot took the step, when one was taken.</param>
        public bool Advance(float distance, float strideLength, out bool leftFoot)
        {
            leftFoot = NextIsLeft;
            if (strideLength <= 0.0001f)
            {
                // No usable stride length: bank nothing rather than divide by zero below.
                return false;
            }

            if (distance > 0f)
            {
                _pending = Mathf.Min(_pending + distance, strideLength * MaxBacklogStrides);
            }

            if (_pending < strideLength)
            {
                return false;
            }

            _pending -= strideLength;
            NextIsLeft = !NextIsLeft;
            return true;
        }

        /// <summary>
        /// Consumes the next foot without walking a stride for it, and reports which one it was.
        /// A landing is a real footfall — you come down on one foot and push off with the other —
        /// so it has to advance the alternation or the first stride afterwards repeats the same
        /// foot, which reads as a limp.
        /// </summary>
        public bool TakeFoot()
        {
            bool foot = NextIsLeft;
            NextIsLeft = !NextIsLeft;
            return foot;
        }

        /// <summary>
        /// Drops the partial stride without disturbing which foot is next. Used when the
        /// character stops, lands, or is teleported: the half-step they had banked before the
        /// interruption should not carry over into the first step afterwards.
        /// </summary>
        public void Reset() => _pending = 0f;

        /// <summary>Clears the partial stride and starts the next stride on the left foot.</summary>
        public void ResetFully()
        {
            _pending = 0f;
            NextIsLeft = true;
        }
    }
}
