using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// The pooled shot tracers: made once, reused, and owned by the actor so they follow scene transfers.
    /// </summary>
    public sealed partial class PlayerCombat
    {
        /// <summary>
        /// Tracers are pre-made and reused: shots come out fast enough that allocating one per
        /// bullet would churn. The root belongs to this actor so it follows scene transfers;
        /// world-space line positions keep already-fired streaks fixed when the actor moves.
        /// </summary>
        private void SpawnTracerPool()
        {
            if (tracerPrefab == null)
            {
                return;
            }

            // Adopt an existing pool after an editor script reload instead of duplicating it.
            if (_tracerRoot == null) _tracerRoot = transform.Find("ShotTracers");
            if (_tracerRoot == null && _tracers != null)
                foreach (var tracer in _tracers)
                    if (tracer != null && tracer.transform.parent != null)
                    {
                        _tracerRoot = tracer.transform.parent;
                        break;
                    }
            if (_tracerRoot == null) _tracerRoot = new GameObject("ShotTracers").transform;
            _tracerRoot.SetParent(transform, false);
            var existing = _tracerRoot.GetComponentsInChildren<ShotTracer>(true);
            _tracers = new ShotTracer[Mathf.Max(1, tracerPoolSize)];
            _tracerCursor = 0;
            for (int i = 0; i < _tracers.Length; i++)
            {
                _tracers[i] = i < existing.Length ? existing[i] : Instantiate(tracerPrefab, _tracerRoot);
            }
            for (int i = _tracers.Length; i < existing.Length; i++) Destroy(existing[i].gameObject);
        }

        private ShotTracer GetUsableTracer(int index)
        {
            var tracer = _tracers[index];
            if (tracer != null && tracer.IsUsable) return tracer;
            // Repair only a lost slot, never allocate a new streak for every shot.
            if (tracer != null) Destroy(tracer.gameObject);
            return _tracers[index] = Instantiate(tracerPrefab, _tracerRoot);
        }

        private void OnDestroy()
        {
            if (_tracerRoot != null) Destroy(_tracerRoot.gameObject);
        }

        private void ShowTracer(Vector3 from, Vector3 to) => PlayTracer(from,to);

        private ShotTracer PlayTracer(Vector3 from, Vector3 to)
        {
            if (tracerPrefab == null) return null;
            if (_tracerRoot == null || _tracers == null || _tracers.Length == 0) SpawnTracerPool();
            if (_tracers == null || _tracers.Length == 0)
            {
                return null;
            }

            // Prefer a free one; if every streak is still flashing, recycle at the cursor.
            for (int i = 0; i < _tracers.Length; i++)
            {
                int index = (_tracerCursor + i) % _tracers.Length;
                var tracer = GetUsableTracer(index);
                if (!tracer.IsFree)
                {
                    continue;
                }
                _tracerCursor = (index + 1) % _tracers.Length;
                tracer.TryPlay(from, to);
                return tracer;
            }

            var recycled=GetUsableTracer(_tracerCursor);
            recycled.TryPlay(from, to);
            _tracerCursor = (_tracerCursor + 1) % _tracers.Length;
            return recycled;
        }
    }
}
