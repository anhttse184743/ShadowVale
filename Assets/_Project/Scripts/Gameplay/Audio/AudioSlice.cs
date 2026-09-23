using System;
using UnityEngine;

namespace ShadowVale.Gameplay.Audio
{
    /// <summary>
    /// A usable sound carved out of a source file: where it starts, how long to play, and how
    /// loud relative to the others.
    /// <para>
    /// The project's recordings are not trimmed. <c>step_water_1.mp3</c> holds a quarter second
    /// of near-silence before the splash, which would delay every footstep audibly, and the
    /// grass step peaks 30 dB below the water ones, which would make it vanish underneath them.
    /// Both are fixed here rather than by re-exporting audio nobody has the session files for.
    /// </para>
    /// </summary>
    [Serializable]
    public struct AudioSlice
    {
        public AudioClip clip;

        [Tooltip("Seconds of lead-in to skip so the sound lands on the frame it is triggered.")]
        [Min(0f)] public float startTime;

        [Tooltip("Seconds to play. 0 plays to the end of the clip.")]
        [Min(0f)] public float duration;

        [Tooltip("Linear gain. Levels the mix between recordings made at different volumes.")]
        [Range(0f, 1f)] public float gain;

        public bool IsValid => clip != null;

        /// <summary>
        /// A slice deserialised from a struct that was never filled in has <c>gain == 0</c>,
        /// which is silence. Treat that as "unset" and play at full volume instead, so a
        /// half-configured bank is audible and obvious rather than mysteriously mute.
        /// </summary>
        public float EffectiveGain => gain <= 0.0001f ? 1f : gain;

        public AudioSlice(AudioClip clip, float startTime, float duration, float gain)
        {
            this.clip = clip;
            this.startTime = startTime;
            this.duration = duration;
            this.gain = gain;
        }
    }
}
