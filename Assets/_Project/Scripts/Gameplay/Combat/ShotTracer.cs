using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// One reusable bullet streak. A hitscan shot arrives instantly, so the tracer is a line that
    /// flashes along the path and thins out rather than a projectile that travels.
    /// Width is what fades, not alpha, so the material can stay opaque.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ShotTracer : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.07f;
        [SerializeField] private float startWidth = 0.035f;

        private LineRenderer _line;
        private float _elapsed = -1f; // negative = idle

        /// <summary>True when this tracer is not mid-flash and can be handed out again.</summary>
        public bool IsFree => _elapsed < 0f;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.enabled = false;
        }

        public void Play(Vector3 from, Vector3 to)
        {
            _line.positionCount = 2;
            _line.SetPosition(0, from);
            _line.SetPosition(1, to);
            _line.widthMultiplier = startWidth;
            _line.enabled = true;
            _elapsed = 0f;
        }

        private void Update()
        {
            if (_elapsed < 0f)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float remaining = 1f - _elapsed / lifetime;
            if (remaining <= 0f)
            {
                _line.enabled = false;
                _elapsed = -1f;
                return;
            }

            _line.widthMultiplier = startWidth * remaining;
        }
    }
}
