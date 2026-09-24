using UnityEngine;

namespace ShadowVale.Core.Services
{
    /// <summary>
    /// Fire-and-forget sound effects. Implementations pool their <see cref="AudioSource"/>s;
    /// nothing in gameplay should ever call <c>AudioSource.PlayClipAtPoint</c>, which allocates
    /// a GameObject per sound and is the usual cause of a hitch during sustained fire.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// Plays part of <paramref name="clip"/> once.
        /// </summary>
        /// <param name="position">Where the sound comes from. Ignored when fully 2D.</param>
        /// <param name="volume">Linear gain, 0..1.</param>
        /// <param name="pitch">Playback rate. Also shortens or lengthens the slice in real time.</param>
        /// <param name="pan">Stereo placement, -1 left to +1 right. Only audible while 2D.</param>
        /// <param name="spatialBlend">0 = flat 2D, 1 = positioned in the world.</param>
        /// <param name="startTime">Seconds into the clip to begin. Skips silent lead-in.</param>
        /// <param name="duration">Seconds of clip to play; 0 plays to the end.</param>
        void PlayOneShot(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f,
            float pan = 0f, float spatialBlend = 1f, float startTime = 0f, float duration = 0f);
    }
}
