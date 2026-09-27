using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Foliage that guards cannot see through. Leaves and bushes have no solid colliders — Nam,
    /// bullets and the camera pass through them — so a guard's sight ray alone saw straight through
    /// the forest. This keeps a simple volume for every plant instead: low cover (bushes, tall grass,
    /// ferns, bamboo) from the ground up, and a tree's crown from partway up its height. A sight
    /// line crossing one is blocked (<see cref="Blocks"/>); tree trunks, terrain and buildings still
    /// block through physics as before.
    /// <para>
    /// Built once from the scene's LOD groups, sized by their near-LOD bounds, so no plant needs its
    /// own component. Kept in a coarse grid: a check only looks at the plants around the line.
    /// </para>
    /// </summary>
    public sealed class Map01SightCover : MonoBehaviour
    {
        [Tooltip("Plants lower than this (ground grass) hide nothing.")]
        [SerializeField] private float minHeight = .9f;
        [Tooltip("Taller than this is a tree: only its crown hides, the trunk blocks through physics.")]
        [SerializeField] private float treeHeight = 4f;
        [Tooltip("Share of a plant's footprint that is dense enough to hide behind.")]
        [SerializeField] private float density = .75f;
        [Tooltip("Up close a guard sees through or around any plant.")]
        [SerializeField] private float peerDistance = 3f;

        private struct Cover { public Vector2 center; public float radius, bottom, top; }
        private const float Cell = 4f;
        private readonly List<Cover> covers = new List<Cover>();
        private readonly Dictionary<Vector2Int, List<int>> grid = new Dictionary<Vector2Int, List<int>>();

        public int Count => covers.Count;

        private void Awake() => Build();

        /// <summary>Collects the foliage from the scene's LOD groups — again after plants are added or removed.</summary>
        public void Build()
        {
            covers.Clear(); grid.Clear();
            foreach (var group in FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
            {
                var lods = group.GetLODs();
                if (lods.Length == 0) continue;
                Bounds? bounds = null;
                foreach (var r in lods[0].renderers)
                    if (r != null) { if (bounds == null) bounds = r.bounds; else { var b = bounds.Value; b.Encapsulate(r.bounds); bounds = b; } }
                if (bounds == null) continue;
                var box = bounds.Value;
                float height = box.size.y;
                if (height < minHeight) continue;
                bool tree = height > treeHeight;
                Add(new Cover {
                    center = new Vector2(box.center.x, box.center.z),
                    radius = Mathf.Min(box.extents.x, box.extents.z) * density,
                    bottom = tree ? box.min.y + height * .35f : box.min.y,
                    top = box.max.y,
                });
            }
        }

        private void Add(Cover cover)
        {
            int index = covers.Count;
            covers.Add(cover);
            var min = CellOf(cover.center - Vector2.one * cover.radius);
            var max = CellOf(cover.center + Vector2.one * cover.radius);
            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                {
                    var key = new Vector2Int(x, y);
                    if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                    list.Add(index);
                }
        }

        private static Vector2Int CellOf(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell));

        /// <summary>
        /// True if foliage hides <paramref name="target"/> from an eye at <paramref name="eye"/>.
        /// Within <see cref="peerDistance"/> nothing does; a plant the eye itself stands in does not
        /// count, one around the target does — crouching in a bush is the best hiding place there is.
        /// </summary>
        public bool Blocks(Vector3 eye, Vector3 target)
        {
            if (Vector3.Distance(eye, target) <= peerDistance) return false;
            Vector2 a = new Vector2(eye.x, eye.z), d = new Vector2(target.x - eye.x, target.z - eye.z);
            var min = CellOf(Vector2.Min(a, a + d));
            var max = CellOf(Vector2.Max(a, a + d));
            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                {
                    if (!grid.TryGetValue(new Vector2Int(x, y), out var list)) continue;
                    foreach (int i in list)
                        if (Crosses(covers[i], eye, target, a, d)) return true;
                }
            return false;
        }

        /// <summary>The eye-to-target segment passes through the cover's upright cylinder.</summary>
        private static bool Crosses(Cover c, Vector3 eye, Vector3 target, Vector2 a, Vector2 d)
        {
            // Where on the segment (t in 0..1) its ground track lies inside the circle.
            Vector2 f = a - c.center;
            float qa = Vector2.Dot(d, d), qb = 2 * Vector2.Dot(f, d), qc = Vector2.Dot(f, f) - c.radius * c.radius;
            if (qa < 1e-6f) return false;
            float disc = qb * qb - 4 * qa * qc;
            if (disc <= 0) return false;
            float root = Mathf.Sqrt(disc);
            float t0 = Mathf.Max(0, (-qb - root) / (2 * qa)), t1 = Mathf.Min(1, (-qb + root) / (2 * qa));
            if (t1 <= t0) return false;
            // The guard standing in the plant sees out of it.
            if (qc < 0 && eye.y >= c.bottom && eye.y <= c.top) return false;
            float y0 = Mathf.Lerp(eye.y, target.y, t0), y1 = Mathf.Lerp(eye.y, target.y, t1);
            return Mathf.Min(y0, y1) <= c.top && Mathf.Max(y0, y1) >= c.bottom;
        }
    }
}
