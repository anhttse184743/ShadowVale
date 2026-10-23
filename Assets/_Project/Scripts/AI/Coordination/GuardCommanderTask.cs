using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Tactical;
using UnityEngine;

namespace ShadowVale.AI.Coordination
{
    /// <summary>
    /// A group fighting under its commander: besides what the map's task asks (by default, the ways out
    /// round the player), the squad keeps watch on the way from the player to the commander — up to
    /// <see cref="MaxGuardNodes"/> nodes of the subgraph in a corridor <see cref="CorridorWidth"/> wide
    /// between them, on the commander's half, plus those right round the commander. The QUBO is the same;
    /// only the nodes it rewards covering change, so the solver itself shares the men between closing the
    /// ring on the player and standing between him and their commander.
    /// </summary>
    public sealed class GuardCommanderTask : ICoordinationTask
    {
        public const float CorridorWidth = 4f, AroundRadius = 5f;
        public const int MaxGuardNodes = 4;

        private readonly ICoordinationTask _inner;
        private readonly Vector3 _commander;

        public GuardCommanderTask(ICoordinationTask inner, Vector3 commander)
        {
            _inner = inner ?? ContainPlayerTask.Instance;
            _commander = commander;
        }

        public string Id => _inner.Id + "+guard_commander";

        public IReadOnlyList<int> EscapeNodes(LocalSubgraph sub, IWorldQuery graph)
        {
            var result = new SortedSet<int>(_inner.EscapeNodes(sub, graph));
            foreach (int i in GuardNodes(sub, graph, _commander)) result.Add(i);
            return new List<int>(result);
        }

        /// <summary>The local nodes on the way from the player to <paramref name="commander"/>, nearest the line first.</summary>
        public static IReadOnlyList<int> GuardNodes(LocalSubgraph sub, IWorldQuery graph, Vector3 commander)
        {
            if (sub == null || sub.Count == 0 || sub.PlayerLocal < 0) return new int[0];
            Vector3 player = graph.NodePosition(sub.Nodes[sub.PlayerLocal]);
            var picked = new List<(float off, float toCommander, int local)>();
            for (int i = 0; i < sub.Count; i++)
            {
                if (i == sub.PlayerLocal) continue;
                Vector3 p = graph.NodePosition(sub.Nodes[i]);
                float toCommander = Flat(p - commander), toPlayer = Flat(p - player);
                float off = OffLine(p, player, commander);
                bool corridor = off <= CorridorWidth && toCommander < toPlayer;
                if (corridor || toCommander <= AroundRadius) picked.Add((Mathf.Min(off, toCommander), toCommander, i));
            }
            return picked.OrderBy(x => x.off).ThenBy(x => x.toCommander).ThenBy(x => x.local)
                .Take(MaxGuardNodes).Select(x => x.local).ToList();
        }

        /// <summary>How far <paramref name="p"/> is from the segment <paramref name="a"/>–<paramref name="b"/>, on the ground.</summary>
        public static float OffLine(Vector3 p, Vector3 a, Vector3 b)
        {
            var p2 = new Vector2(p.x, p.z); var a2 = new Vector2(a.x, a.z); var b2 = new Vector2(b.x, b.z);
            Vector2 ab = b2 - a2;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / ab.sqrMagnitude);
            return (p2 - (a2 + ab * t)).magnitude;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
