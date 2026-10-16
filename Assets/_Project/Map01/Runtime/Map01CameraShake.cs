using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// A short shake of the game camera after a blast: strongest close to it, nothing past its range, dying away over
    /// a fraction of a second. Runs after every camera script (ThirdPersonCamera and the cinematics place the camera in
    /// LateUpdate); the offset of the frame before is taken off again if nothing else moved the camera, so it never
    /// builds up.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class Map01CameraShake : MonoBehaviour
    {
        private float amplitude, seconds, until;
        private Vector3 lastOffset; private Quaternion lastTurn = Quaternion.identity;
        private Vector3 lastPosition; private Quaternion lastRotation;
        private bool applied;

        /// <summary>Shakes <paramref name="cam"/> (the main camera when null) for a blast at <paramref name="at"/>: <paramref name="amplitude"/> metres at
        /// the blast, falling off to nothing at <paramref name="range"/> metres, over <paramref name="duration"/> s.</summary>
        public static void Kick(Camera cam, Vector3 at, float amplitude, float range, float duration)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            float near = Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, at) / range);
            if (near <= 0f) return;
            if (!cam.TryGetComponent<Map01CameraShake>(out var shake)) shake = cam.gameObject.AddComponent<Map01CameraShake>();
            shake.amplitude = Mathf.Max(shake.Strength(), amplitude * near * near);
            shake.seconds = duration; shake.until = Time.time + duration;
        }

        /// <summary>How hard it is shaking now, in metres.</summary>
        public float Strength() => Time.time >= until || seconds <= 0f ? 0f : amplitude * Mathf.Pow((until - Time.time) / seconds, 2f);

        private void LateUpdate()
        {
            var t = transform;
            if (applied && t.position == lastPosition && t.rotation == lastRotation)
            {
                t.position -= lastOffset; t.rotation = Quaternion.Inverse(lastTurn) * t.rotation;   // nobody placed it since: undo ours
            }
            applied = false;
            float s = Strength();
            if (s <= 0f) return;
            float time = Time.time * 23f;
            lastOffset = new Vector3(Mathf.PerlinNoise(time, 1.3f) - .5f, Mathf.PerlinNoise(2.7f, time) - .5f, Mathf.PerlinNoise(time, 5.1f) - .5f) * 2f * s;
            lastTurn = Quaternion.Euler((Mathf.PerlinNoise(time, 9.2f) - .5f) * 14f * s, 0f, (Mathf.PerlinNoise(7.4f, time) - .5f) * 10f * s);
            t.position += lastOffset; t.rotation = lastTurn * t.rotation;
            lastPosition = t.position; lastRotation = t.rotation; applied = true;
        }
    }
}
