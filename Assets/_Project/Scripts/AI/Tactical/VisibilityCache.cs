using System;
using System.Collections;
using ShadowVale.AI.Perception;
using UnityEngine;

namespace ShadowVale.AI.Tactical
{
    /// <summary>
    /// Node-to-node visibility for the QUBO coverage terms: an agent standing at node a (eye 1.3 m)
    /// sees a person at node b (1.1 m) within <c>maxRange</c> when neither the physics obstruction
    /// layers nor the map's occluders (foliage) are in the way. ~n²/2 linecasts, so it is built
    /// once per map load, spread over frames.
    /// </summary>
    public static class VisibilityCache
    {
        public const float EyeHeight = 1.3f, TargetHeight = 1.1f;

        public static bool Sees(TacticalGraph g, int a, int b, float maxRange, ISightOccluder occluder)
        {
            if (a == b) return true;
            Vector3 eye = g.NodePosition(a) + Vector3.up * EyeHeight, target = g.NodePosition(b) + Vector3.up * TargetHeight;
            if ((target - eye).sqrMagnitude > maxRange * maxRange) return false;
            if (occluder != null && occluder.Blocks(eye, target)) return false;
            return g.HasLineOfSight(eye, target);
        }

        /// <summary>Everything at once (tests, small graphs).</summary>
        public static bool[] BuildNow(TacticalGraph g, float maxRange, ISightOccluder occluder)
        {
            bool[] result = null;
            var outer = Build(g, maxRange, occluder, int.MaxValue, v => result = v);
            while (outer.MoveNext()) { }
            return result;
        }

        /// <summary>
        /// Coroutine: <paramref name="pairsPerFrame"/> pairs per step. Symmetric (the eye/target
        /// height difference is ignored for the pair's mirror), which halves the work.
        /// </summary>
        public static IEnumerator Build(TacticalGraph g, float maxRange, ISightOccluder occluder, int pairsPerFrame,
            Action<bool[]> done)
        {
            int n = g.NodeCount;
            var visible = new bool[n * n];
            int budget = pairsPerFrame;
            for (int a = 0; a < n; a++)
            {
                visible[a * n + a] = true;
                for (int b = a + 1; b < n; b++)
                {
                    bool v = Sees(g, a, b, maxRange, occluder);
                    visible[a * n + b] = v; visible[b * n + a] = v;
                    if (--budget <= 0) { budget = pairsPerFrame; yield return null; }
                }
            }
            done?.Invoke(visible);
        }
    }
}
