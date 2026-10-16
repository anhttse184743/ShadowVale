using System.Collections.Generic;
using ShadowVale.Core.Pooling;
using ShadowVale.Core.Services;
using UnityEngine;

namespace ShadowVale.Gameplay.Audio
{
    /// <summary>
    /// The project's <see cref="IAudioService"/>: a pool of <see cref="AudioSource"/>s handed out
    /// per sound and returned when the slice ends.
    /// <para>
    /// It registers itself rather than waiting for <c>GameBootstrap</c>. Bootstrap only runs in
    /// <c>00_Boot</c>, and Map 1 is entered straight from the main menu, so anything that waited
    /// for the locator to be populated would be silent in the only level that ships.
    /// </para>
    /// </summary>
    public sealed class PooledAudioService : MonoBehaviour, IAudioService
    {
        [Tooltip("Sources created up front. Sustained fire plus footsteps is the busy case.")]
        [SerializeField] private int prewarm = 12;

        [Tooltip("Metres at which a positioned sound has faded out.")]
        [SerializeField] private float maxDistance = 45f;

        private ObjectPool<AudioSource> _pool;
        private readonly List<Playing> _active = new();

        private struct Playing
        {
            public AudioSource Source;
            public float EndTime;
        }

        /// <summary>
        /// Returns the live service, creating it on first use. Callers are gameplay components
        /// that may wake up in any scene, so "resolve or build one" is the only reliable order.
        /// </summary>
        public static IAudioService Resolve()
        {
            if (ServiceLocator.TryResolve(out IAudioService existing) && existing != null)
            {
                // A destroyed MonoBehaviour still answers TryResolve; Unity's overloaded null
                // check is the only way to notice it went away with the previous scene.
                if (existing is Object unityObject && unityObject == null)
                {
                    ServiceLocator.Unregister<IAudioService>();
                }
                else
                {
                    return existing;
                }
            }

            var host = new GameObject("Audio service");
            DontDestroyOnLoad(host);
            return host.AddComponent<PooledAudioService>();
        }

        private void Awake()
        {
            var template = new GameObject("Pooled audio source");
            template.transform.SetParent(transform, false);
            var source = template.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = maxDistance;
            template.SetActive(false);

            _pool = new ObjectPool<AudioSource>(source, prewarm, transform);
            ServiceLocator.Register<IAudioService>(this);
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryResolve(out IAudioService registered) && ReferenceEquals(registered, this))
            {
                ServiceLocator.Unregister<IAudioService>();
            }
        }

        public void PlayOneShot(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f,
            float pan = 0f, float spatialBlend = 1f, float startTime = 0f, float duration = 0f)
        {
            if (clip == null || volume <= 0f) return;

            AudioSource source = _pool.Get(position, Quaternion.identity);
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.panStereo = Mathf.Clamp(pan, -1f, 1f);
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.minDistance = 2f;
            source.maxDistance = maxDistance;
            source.loop = false;

            // Clamp rather than reject: a slice authored against a clip that was later
            // re-exported shorter should still make a sound.
            float from = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.01f));
            source.time = from;
            source.Play();

            float remaining = clip.length - from;
            float played = duration > 0f ? Mathf.Min(duration, remaining) : remaining;
            // Pitch is a playback rate, so it stretches wall-clock time as well as sound.
            float wallClock = played / Mathf.Max(0.01f, Mathf.Abs(pitch));

            _active.Add(new Playing { Source = source, EndTime = Time.unscaledTime + wallClock });
        }

        private void Update()
        {
            // Unscaled: sounds already playing when the pause menu freezes time should finish
            // and hand their source back instead of holding the pool hostage.
            float now = Time.unscaledTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Playing playing = _active[i];
                if (playing.Source == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                if (now < playing.EndTime) continue;

                playing.Source.Stop();
                playing.Source.clip = null;
                _pool.Release(playing.Source);
                _active.RemoveAt(i);
            }
        }
    }
}
