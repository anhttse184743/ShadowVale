using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// The player's own clock for firing, aiming, recoil and the camera. It is game time, except while a scene slows the
    /// world for a moment of focus (<see cref="IgnoreSlowMotion"/>): then the player's gun and view keep real pace while
    /// everything else crawls. A pause (time scale 0) still stops it.
    /// </summary>
    public static class PlayerTime
    {
        /// <summary>Set by a scene while its slow motion should not slow the player.</summary>
        public static bool IgnoreSlowMotion { get; set; }
        private static int frame = -1;
        private static float now, delta;

        private static void Step()
        {
            if (Time.frameCount == frame) return;
            frame = Time.frameCount;
            delta = Time.timeScale <= 0f ? 0f : IgnoreSlowMotion ? Mathf.Min(Time.unscaledDeltaTime, .1f) : Time.deltaTime;
            now = IgnoreSlowMotion || now > Time.time ? now + delta : Time.time;   // follows game time, runs ahead of it only while unscaled
        }
        /// <summary>The player's time: Time.time, or ahead of it after unscaled moments (never goes back).</summary>
        public static float Now { get { Step(); return now; } }
        public static float DeltaTime { get { Step(); return delta; } }
    }
}
