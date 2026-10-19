using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Fixed beats for the guards of Map 1's supply and radio areas, the way the jetty squad
    /// (Map01RescueLayout) and the C3 garrison (Map01C3Mission) already walk theirs. Until 18/10/2026 these
    /// guards paced 3 m triangles round their post at full speed (Map01OperationsSetup), which read as
    /// wandering about. Now each area has one loop round its perimeter: the first guard of the area stands
    /// sentry (the supply key keeper, the radio's first guard), the others walk the loop at a calm pace,
    /// each starting at a different point so they keep apart, stopping at every point and looking out.
    /// <para>The loop comes from the layout if a designer placed one (<see cref="Map01OperationsLayout.supplyBeat"/>,
    /// <see cref="Map01OperationsLayout.radioBeat"/>: a route whose children are the points, each facing where
    /// the guard should look, with an optional <see cref="Map01PatrolWaypoint"/> for the stop); otherwise it is
    /// laid out here: a ring round the area, every point on the NavMesh and reachable from the one before.
    /// The commander's camp is left alone: C3 posts its garrison itself.</para>
    /// </summary>
    public static class Map01PatrolBeats
    {
        public const float SupplyRadius = 9f, RadioRadius = 10f;
        public const int RingPoints = 6;
        /// <summary>Share of a guard's base speed on his beat: about 1.9 m/s, the jetty squad's walk.</summary>
        public const float Pace = .55f;
        /// <summary>Seconds at a point when the route does not say: 1.5, 2.25, 3 in turn, so no two stops match.</summary>
        public static float DefaultStop(int index) => 1.5f + index % 3 * .75f;

        public readonly struct Beat
        {
            public readonly Vector3[] Points;
            public readonly float[] Stops, Yaws;
            public Beat(Vector3[] points, float[] stops, float[] yaws) { Points = points; Stops = stops; Yaws = yaws; }
            public int Count => Points?.Length ?? 0;
            /// <summary>The longest distance between two of its points: how much ground the beat covers.</summary>
            public float Span
            {
                get
                {
                    var points = Points;
                    return Count < 2 ? 0f : points.SelectMany(a => points, (a, b) => Flat(a - b)).Max();
                }
            }
        }

        /// <summary>Beats every supply and radio guard who is not a sentry or one of C3's garrison. Returns the guards beaten.</summary>
        public static int Apply(Map01OperationsLayout layout)
        {
            if (layout == null) return 0;
            var garrison = C3Garrison();
            int n = 0;
            if (layout.supply != null)
                n += Assign(Walkers(layout.supplyGuards, layout.keyKeeper, garrison), SupplyBeat(layout));
            if (layout.radio != null)
                n += Assign(Walkers(layout.radioGuards, layout.radioGuards?.FirstOrDefault(), garrison), RadioBeat(layout));
            return n;
        }

        /// <summary>The supply area's beat.</summary>
        public static Beat SupplyBeat(Map01OperationsLayout layout)
            => layout == null || layout.supply == null ? default : BeatFor(layout.supplyBeat, layout.supply.position, SupplyRadius, 15f);

        /// <summary>The radio area's beat: where the radio responders walk once they have checked the station.</summary>
        public static Beat RadioBeat(Map01OperationsLayout layout)
            => layout == null || layout.radio == null ? default : BeatFor(layout.radioBeat, layout.radio.position, RadioRadius, 45f);

        /// <summary>Puts <paramref name="guards"/> on <paramref name="beat"/>, each starting a fair share of the loop further on.</summary>
        public static int Assign(IReadOnlyList<Map01EnemyController> guards, Beat beat)
        {
            if (beat.Count < 3 || guards == null || guards.Count == 0) return 0;
            for (int k = 0; k < guards.Count; k++)
                guards[k].AssignBeat(beat.Points, beat.Stops, beat.Yaws, k * beat.Count / guards.Count, Pace);
            return guards.Count;
        }

        private static List<Map01EnemyController> Walkers(Map01EnemyController[] area, Map01EnemyController sentry, HashSet<string> garrison)
            => (area ?? Array.Empty<Map01EnemyController>())
                .Where(g => g != null && g != sentry && g.Alive && !garrison.Contains(g.name))
                .OrderBy(g => g.SaveId, StringComparer.Ordinal).ToList();

        private static Beat BeatFor(Transform authored, Vector3 centre, float radius, float phase)
        {
            var route = FromRoute(authored);
            return route.Count >= 3 ? route : Ring(centre, radius, RingPoints, phase);
        }

        /// <summary>A designer's route: its children in order are the points, each one's forward the way to look.</summary>
        public static Beat FromRoute(Transform route)
        {
            if (route == null || route.childCount < 3) return default;
            var points = new Vector3[route.childCount];
            var stops = new float[route.childCount];
            var yaws = new float[route.childCount];
            for (int i = 0; i < route.childCount; i++)
            {
                var c = route.GetChild(i);
                points[i] = NavMesh.SamplePosition(c.position, out var hit, 2f, NavMesh.AllAreas) ? hit.position : c.position;
                var w = c.GetComponent<Map01PatrolWaypoint>();
                stops[i] = w != null ? w.waitSeconds : DefaultStop(i);
                yaws[i] = c.eulerAngles.y;
            }
            return new Beat(points, stops, yaws);
        }

        /// <summary>
        /// A ring of up to <paramref name="count"/> points round <paramref name="centre"/>, starting at <paramref name="phase"/>°:
        /// each on the NavMesh, at least 2 m from the last, reachable from it, the last reachable back to the first.
        /// Every stop faces out, away from the centre. Fewer than 3 points: no beat.
        /// </summary>
        public static Beat Ring(Vector3 centre, float radius, int count, float phase)
        {
            var points = new List<Vector3>();
            for (int k = 0; k < count; k++)
            {
                float angle = (phase + k * 360f / count) * Mathf.Deg2Rad;
                var want = centre + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
                if (!NavMesh.SamplePosition(want, out var hit, 3f, NavMesh.AllAreas)) continue;
                if (points.Count > 0 && (Flat(hit.position - points[points.Count - 1]) < 2f || !Reachable(points[points.Count - 1], hit.position))) continue;
                points.Add(hit.position);
            }
            while (points.Count >= 3 && !Reachable(points[points.Count - 1], points[0])) points.RemoveAt(points.Count - 1);
            if (points.Count < 3) return default;
            var stops = points.Select((_, i) => DefaultStop(i)).ToArray();
            var yaws = points.Select(p => Mathf.Atan2(p.x - centre.x, p.z - centre.z) * Mathf.Rad2Deg).ToArray();
            return new Beat(points.ToArray(), stops, yaws);
        }

        /// <summary>The guards C3 posts itself (Resources/Missions/Map01C3Layout.json): never given a beat here.</summary>
        private static HashSet<string> C3Garrison()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>(Map01C3Mission.ResourcePath);
            var data = asset != null ? JsonUtility.FromJson<Map01C3Mission.Layout>(asset.text) : null;
            foreach (var role in data?.roles ?? Array.Empty<Map01C3Mission.Role>())
                if (!string.IsNullOrEmpty(role.guard)) names.Add(role.guard);
            return names;
        }

        private static bool Reachable(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
