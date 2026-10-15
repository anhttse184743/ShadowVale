using System;
using UnityEngine;

namespace ShadowVale.Map01
{
    [Serializable]
    public sealed class MapExplorationState
    {
        public int version = 1, resolution;
        public Rect bounds;
        public string cells;
    }

    /// <summary>Compact per-checkpoint discovery grid; coordinates are world X/Z, north up.</summary>
    public sealed class MapExploration
    {
        public const int Resolution = 128;
        public Rect Bounds { get; }
        private readonly byte[] bits = new byte[(Resolution * Resolution + 7) / 8];
        public MapExploration(Rect bounds) => Bounds = bounds;

        public bool IsExplored(Vector3 position)
        {
            if (!Contains(position)) return false;
            int x = Mathf.Clamp((int)((position.x - Bounds.x) / Bounds.width * Resolution), 0, Resolution - 1);
            int y = Mathf.Clamp((int)((position.z - Bounds.y) / Bounds.height * Resolution), 0, Resolution - 1);
            return Cell(x, y);
        }
        public bool Contains(Vector3 point) => point.x >= Bounds.xMin && point.x <= Bounds.xMax
            && point.z >= Bounds.yMin && point.z <= Bounds.yMax;
        public bool Cell(int x, int y)
        {
            int index = y * Resolution + x;
            return (bits[index >> 3] & (1 << (index & 7))) != 0;
        }
        public bool Reveal(Vector3 position, float radius)
        {
            if (!Contains(position) || radius <= 0 || float.IsNaN(radius) || float.IsInfinity(radius)) return false;
            float dx = Bounds.width / Resolution, dz = Bounds.height / Resolution;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((position.x - radius - Bounds.x) / dx), 0, Resolution - 1);
            int x1 = Mathf.Clamp(Mathf.FloorToInt((position.x + radius - Bounds.x) / dx), 0, Resolution - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((position.z - radius - Bounds.y) / dz), 0, Resolution - 1);
            int y1 = Mathf.Clamp(Mathf.FloorToInt((position.z + radius - Bounds.y) / dz), 0, Resolution - 1);
            bool changed = false;
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                var delta = new Vector2(Bounds.x + (x + .5f) * dx - position.x, Bounds.y + (y + .5f) * dz - position.z);
                if (delta.sqrMagnitude > radius * radius) continue;
                int index = y * Resolution + x; byte mask = (byte)(1 << (index & 7));
                if ((bits[index >> 3] & mask) != 0) continue;
                bits[index >> 3] |= mask; changed = true;
            }
            return changed;
        }
        public MapExplorationState Capture() => new MapExplorationState
        { resolution = Resolution, bounds = Bounds, cells = Convert.ToBase64String(bits) };

        public void Restore(MapExplorationState state)
        {
            Array.Clear(bits, 0, bits.Length);
            if (state == null || state.version != 1 || state.resolution != Resolution || state.bounds != Bounds || string.IsNullOrEmpty(state.cells)) return;
            try
            {
                var saved = Convert.FromBase64String(state.cells);
                if (saved.Length == bits.Length) Array.Copy(saved, bits, bits.Length);
            }
            catch (FormatException) { /* Invalid exploration data must not prevent loading the checkpoint. */ }
        }
    }
}
