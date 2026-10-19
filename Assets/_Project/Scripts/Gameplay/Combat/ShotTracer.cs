using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// One reusable bullet streak. A hitscan shot arrives instantly, so the tracer is a line that
    /// flashes along the path and thins out rather than a projectile that travels.
    /// Width is what fades, not alpha, so the material can stay opaque.
    /// </summary>
    [RequireComponent(typeof(LineRenderer)),DefaultExecutionOrder(10020)]
    public sealed class ShotTracer : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.07f;
        [SerializeField] private float startWidth = 0.012f;

        private LineRenderer _line;
        private float _elapsed = -1f; // negative = idle
        private float _startedAt;
        private Transform _emissionMuzzle;

        /// <summary>True when this tracer is not mid-flash and can be handed out again.</summary>
        public bool IsFree => _elapsed < 0f;
        public bool IsUsable => EnsureLine();
        public int EmissionFrame { get; private set; } = -1;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            if (_line != null)
            {
                _line.useWorldSpace = true;
                _line.enabled = false;
            }
        }

        public void Play(Vector3 from, Vector3 to)
        {
            TryPlay(from, to);
        }

        public bool TryPlay(Vector3 from, Vector3 to)
        {
            if (!EnsureLine()) return false;
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.SetPosition(0, from);
            _line.SetPosition(1, to);
            _line.widthMultiplier = startWidth;
            _line.enabled = true;
            _elapsed = 0f;
            _startedAt=Time.time;
            EmissionFrame=Time.frameCount;
            _emissionMuzzle=null;
            return true;
        }

        public void AnchorEmission(Transform muzzle)=>_emissionMuzzle=muzzle;

        private void LateUpdate()
        {
            if(_emissionMuzzle!=null && EnsureLine() && !IsFree) {
                _line.SetPosition(0,_emissionMuzzle.position);
                _emissionMuzzle=null; // A bullet already fired stays in world space.
            }
        }

        private bool EnsureLine()
        {
            // A managed reference can survive after its native scene object is destroyed.
            if (this == null) return false;
            if (_line == null) _line = GetComponent<LineRenderer>();
            return _line != null;
        }

        private void OnDisable()
        {
            _elapsed = -1f;
            _emissionMuzzle=null;
            if (_line != null) _line.enabled = false;
        }

        private void Update()
        {
            if (_elapsed < 0f)
            {
                return;
            }

            if (!EnsureLine())
            {
                _elapsed = -1f;
                return;
            }

            _elapsed = Time.time-_startedAt;
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
