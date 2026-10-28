using System;
using UnityEngine;

namespace ShadowVale.Gameplay.Audio
{
    /// <summary>What the character is walking on. Drives both the sound and how far it carries.</summary>
    public enum FootSurface
    {
        Grass = 0,
        Water = 1,
    }

    /// <summary>How the character is moving. Quiet to loud.</summary>
    public enum MoveStance
    {
        Sneak = 0,
        Walk = 1,
        Sprint = 2,
    }

    /// <summary>
    /// The footstep sounds for one surface, split into a left and a right foot.
    /// </summary>
    [Serializable]
    public sealed class SurfaceFootsteps
    {
        public FootSurface surface;
        public AudioSlice left;
        public AudioSlice right;

        public AudioSlice Foot(bool leftFoot) => leftFoot ? left : right;
    }

    /// <summary>
    /// Footstep audio, keyed by surface.
    /// <para>
    /// There is only one grass recording, and the two water recordings are close cousins, so the
    /// left/right difference is produced here instead of in the asset: each foot gets its own
    /// stereo position and a small pitch offset. That is enough for a listener to hear two feet
    /// rather than one sound repeating, and it costs no extra audio files — which matters when
    /// the surfaces still to come (mud, planks, gravel) would each need a matched pair.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowVale/Audio/Footstep Bank", fileName = "FootstepBank")]
    public sealed class FootstepBank : ScriptableObject
    {
        [SerializeField] private SurfaceFootsteps[] surfaces = Array.Empty<SurfaceFootsteps>();

        [Header("Left / right, computed rather than recorded")]
        [Tooltip("How far each foot sits off centre in the stereo field.")]
        [Range(0f, 1f)] [SerializeField] private float footPan = 0.12f;

        [Tooltip("Pitch multiplier applied one way for the left foot and the other for the right.")]
        [Range(0f, 0.25f)] [SerializeField] private float footPitchOffset = 0.04f;

        [Header("Per-stance variation")]
        [Tooltip("Random pitch wobble, so a long run does not sound like a metronome.")]
        [Range(0f, 0.2f)] [SerializeField] private float pitchJitter = 0.05f;

        [Tooltip("Volume for a sneak, a walk and a sprint respectively.")]
        [SerializeField] private float[] stanceVolume = { 0.35f, 0.7f, 1f };

        [Tooltip("Sneaking drags rather than strikes, so its steps play slightly slower.")]
        [SerializeField] private float[] stancePitch = { 0.9f, 1f, 1.08f };

        public float FootPan => footPan;

        public SurfaceFootsteps For(FootSurface surface)
        {
            foreach (SurfaceFootsteps entry in surfaces)
            {
                if (entry != null && entry.surface == surface) return entry;
            }
            return null;
        }

        /// <summary>
        /// Everything one footfall needs: which slice, how loud, how it is pitched and where it
        /// sits in the stereo field. Returns false when the surface has no usable recording.
        /// </summary>
        public bool Resolve(FootSurface surface, MoveStance stance, bool leftFoot,
            out AudioSlice slice, out float volume, out float pitch, out float pan)
        {
            slice = default;
            volume = 0f;
            pitch = 1f;
            pan = 0f;

            SurfaceFootsteps entry = For(surface);
            if (entry == null) return false;

            slice = entry.Foot(leftFoot);
            if (!slice.IsValid)
            {
                // Only one foot recorded: use it for both rather than limping in silence.
                slice = entry.Foot(!leftFoot);
                if (!slice.IsValid) return false;
            }

            int index = Mathf.Clamp((int)stance, 0, 2);
            volume = slice.EffectiveGain * Sample(stanceVolume, index, 1f);
            pitch = Sample(stancePitch, index, 1f)
                    * (1f + (leftFoot ? -footPitchOffset : footPitchOffset))
                    * (1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter));
            pan = leftFoot ? -footPan : footPan;
            return true;
        }

        private static float Sample(float[] values, int index, float fallback)
            => values != null && index < values.Length && values[index] > 0f ? values[index] : fallback;
    }
}
