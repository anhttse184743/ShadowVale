using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// Works out where throwing stones lie on Map 1 and writes them to Resources/Stones/Map01StoneSites
    /// (spawned at run time by Map01StoneSites; Map 1.unity is not changed). Run it again after moving
    /// camps, boulders, bridges or the river.
    /// <para>The rules, in the order they are applied:</para>
    /// <list type="number">
    /// <item>Gravel bars at the water's edge, about every 38 m along each bank and by each bridge:
    /// three stones, and the river brings more. Nobody can run out for good.</item>
    /// <item>Round each camp and the jetty, at least three sites outside the guards' sight: two stones on
    /// the far side of a boulder from the camp (up to two boulders each), or beside a bush where there is none.</item>
    /// <item>Along the ways between base, jetty and camps, never more than 45 m without stones in reach.</item>
    /// <item>Wherever else the mission is played (near a camp, the jetty or one of those ways) and a walker
    /// would be more than 30 m from the nearest stones, a few more. The far corners of the map get none.</item>
    /// </list>
    /// No site is within a guard's sight (plus 2 m) of his post or beat, or within 12 m of another site or of the
    /// rescue's own stone piles round the jetty (Map01RescueStones), which also count towards the jetty's three.
    /// </summary>
    public static class Map01StoneSitesBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Maps/Map 1.unity";
        public const string AssetPath = "Assets/_Project/Map01/Resources/Stones/Map01StoneSites.asset";
        private const string PebbleFolder = "Assets/_Project/Art/Environment/AnGiangRocks/Prefabs";

        public const float Spacing = 12f, VisionMargin = 2f;
        public const float RiverbankEvery = 38f, TrailGap = 45f, Coverage = 30f;
        public const float CampRingIn = 18f, CampRingOut = 40f, JettyRingIn = 28f, JettyRingOut = 45f;
        public const int PerEncounter = 3, BouldersPerEncounter = 3, MostSites = 34;
        /// <summary>Rule 4 covers the ground a mission is played on: this near a camp or the jetty, or this near a way between them.</summary>
        public const float PlayedRoundEncounter = 55f, PlayedBesideWay = 30f;
        private const int RiverbankStones = 3, BoulderStones = 2, TrailStones = 2;

        private sealed class Plan
        {
            public readonly List<Map01StoneSiteData.Site> sites = new List<Map01StoneSiteData.Site>();
            public readonly List<(Vector3 centre, float radius)> watched = new List<(Vector3, float)>();
            public readonly List<(Vector3 at, float radius)> boulders = new List<(Vector3, float)>();
            public readonly List<Vector3> bushes = new List<Vector3>();
            /// <summary>The rescue approaches' own stone piles (Map01RescueStones): stones are already there.</summary>
            public readonly List<Vector3> piles = new List<Vector3>();
            public Vector3[] river = new Vector3[0];
            public float water;
            public Vector3 start;
        }

        [MenuItem("ShadowVale/Map 1/Rải đá nhặt được")]
        public static void Build()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            Physics.SyncTransforms();
            var plan = Survey();
            var report = new StringBuilder();
            var encounters = Encounters(plan);

            Riverbanks(plan);
            foreach (var encounter in encounters) Ring(plan, encounter.at, encounter.inner, encounter.outer);
            var legs = Legs(plan, encounters);
            foreach (var leg in legs) Trail(plan, leg.corners);
            var samples = WalkableSamples(plan);
            // The far corners of the map are left out: nothing happens there, and stones nobody passes are only clutter.
            var played = samples.Where(p => encounters.Any(e => Flat(e.at, p) <= PlayedRoundEncounter) || legs.Any(l => NearWay(l.corners, p, PlayedBesideWay))).ToList();
            Fill(plan, played);

            // Riverbank first (south to north), then boulders, then trails: an id is the place in this list.
            var ordered = plan.sites.OrderBy(s => (int)s.source).ThenBy(s => s.position.z).ThenBy(s => s.position.x).ToArray();

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var data = AssetDatabase.LoadAssetAtPath<Map01StoneSiteData>(AssetPath);
            if (data == null) { data = ScriptableObject.CreateInstance<Map01StoneSiteData>(); AssetDatabase.CreateAsset(data, AssetPath); }
            data.sites = ordered;
            data.pebbles = Enumerable.Range(0, 8).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>($"{PebbleFolder}/Pebble_{i}.prefab")).Where(p => p != null).ToArray();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            report.AppendLine($"[Stones] {ordered.Length} sites: {ordered.Count(s => s.source == Map01StoneSiteData.Source.Riverbank)} riverbank (refill), " +
                $"{ordered.Count(s => s.source == Map01StoneSiteData.Source.Boulder)} boulder, {ordered.Count(s => s.source == Map01StoneSiteData.Source.Trail)} trail; " +
                $"{ordered.Where(s => s.source != Map01StoneSiteData.Source.Riverbank).Sum(s => s.stones)} stones picked up once, {data.pebbles.Length} pebble models.");
            report.AppendLine($"[Stones] {plan.piles.Count} rescue piles round the jetty kept clear of by {Spacing:0} m.");
            foreach (var encounter in encounters)
                report.AppendLine($"[Stones] {encounter.name}: {ordered.Count(s => Between(Flat(s.position, encounter.at), encounter.inner, encounter.outer))} sites {encounter.inner:0}-{encounter.outer:0} m out.");
            foreach (var leg in legs) report.AppendLine($"[Stones] {leg.name}: {Length(leg.corners):0} m, longest stretch out of sight of the guards without stones within 8 m: {LongestGap(plan, leg.corners):0} m.");
            foreach (var area in new[] { (name: "where the mission is played", points: played), (name: "anywhere walkable", points: samples) })
            {
                var nearest = area.points.Select(p => plan.sites.Min(s => Flat(s.position, p))).OrderBy(d => d).ToArray();
                if (nearest.Length > 0)
                    report.AppendLine($"[Stones] nearest stones from {area.name}: median {nearest[nearest.Length / 2]:0} m, 95% within {nearest[(int)(nearest.Length * .95f)]:0} m, furthest {nearest[nearest.Length - 1]:0} m ({nearest.Length} samples, about {nearest.Length * 36} m2).");
            }
            foreach (var site in ordered) report.AppendLine($"[Stones]   {site.source} x{site.stones} at ({site.position.x:0.0}, {site.position.y:0.0}, {site.position.z:0.0})");
            Debug.Log(report.ToString());
        }

        // ---- The map as the rules see it ----------------------------------------------------

        private static Plan Survey()
        {
            var plan = new Plan();
            var player = Object.FindFirstObjectByType<CharacterController>();
            plan.start = player != null ? player.transform.position : Vector3.zero;

            var river = GameObject.Find("Winding river");
            if (river != null && river.TryGetComponent<MeshFilter>(out var surface) && surface.sharedMesh != null)
            {
                plan.river = surface.sharedMesh.vertices.Select(v => river.transform.TransformPoint(v)).ToArray();
                plan.water = plan.river.Average(v => v.y);
            }

            // Everything a guard can see from his post or anywhere on his beat.
            foreach (var guard in Object.FindObjectsByType<Map01EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                plan.watched.Add((guard.transform.position, guard.VisionRange + VisionMargin));
                foreach (var point in guard.PatrolPoints) plan.watched.Add((point, guard.VisionRange + VisionMargin));
            }
            // The jetty squad is stood on the rescue layout's posts and routes when the mission starts.
            var layout = Resources.Load<Map01RescueLayout>("Rescue/Map01RescueLayout");
            if (layout != null)
            {
                float sight = Mathf.Max(layout.patrolVision, Map01Rescue.SquadNames.Select(GameObject.Find).Where(g => g != null)
                    .Select(g => g.GetComponent<Map01EnemyController>()).Where(g => g != null).Select(g => g.VisionRange).DefaultIfEmpty(0).Max()) + VisionMargin;
                foreach (var post in layout.guardPosts) if (post != null) plan.watched.Add((post.position, sight));
                foreach (var route in layout.patrolRoutes) if (route != null) foreach (Transform point in route) plan.watched.Add((point.position, sight));
                // The rescue lays its own piles out round Hùng (Map01RescueStones, on the same offsets): none of ours near them.
                var mission = Object.FindFirstObjectByType<Map01Mission>();
                if (mission != null && mission.hung != null)
                    foreach (var offset in layout.stoneOffsets)
                        if (NavMesh.SamplePosition(mission.hung.position + offset, out var pile, 4, NavMesh.AllAreas)) plan.piles.Add(pile.position);
            }

            foreach (var group in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var mesh = group.GetComponentsInChildren<MeshFilter>().FirstOrDefault(m => m.sharedMesh != null && m.sharedMesh.name.Contains("Boulder"));
                if (mesh == null || !mesh.TryGetComponent<Renderer>(out var renderer)) continue;
                plan.boulders.Add((group.transform.position, Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.z)));
            }
            plan.bushes.AddRange(Object.FindObjectsByType<ForestPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(p => p.kind == ForestPointKind.Hide).Select(p => p.transform.position));
            return plan;
        }

        private static List<(string name, Vector3 at, float inner, float outer)> Encounters(Plan plan)
        {
            var list = new List<(string, Vector3, float, float)>();
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var jetty = mission != null && mission.hung != null ? mission.hung : GameObject.Find("B_HungCaptive")?.transform;
            if (jetty != null) list.Add(("Jetty B", jetty.position, JettyRingIn, JettyRingOut));
            for (int camp = 1; camp <= 3; camp++)
            {
                var outpost = GameObject.Find("Enemy outpost " + camp);
                if (outpost != null) list.Add(("Camp C" + camp, outpost.transform.position, CampRingIn, CampRingOut));
            }
            return list;
        }

        private static List<(string name, Vector3[] corners)> Legs(Plan plan, List<(string name, Vector3 at, float inner, float outer)> encounters)
        {
            var legs = new List<(string, Vector3[])>();
            void Add(string name, Vector3 from, Vector3 to) { var corners = Route(from, to); if (corners.Length > 1) legs.Add((name, corners)); }
            var jetty = encounters.FirstOrDefault(e => e.name.StartsWith("Jetty"));
            var camps = encounters.Where(e => e.name.StartsWith("Camp")).ToArray();
            if (jetty.name != null) Add("Base A to jetty B", plan.start, jetty.at);
            if (camps.Length > 0) Add("Base A to " + camps[0].name, plan.start, camps[0].at);
            for (int i = 1; i < camps.Length; i++) Add(camps[i - 1].name + " to " + camps[i].name, camps[i - 1].at, camps[i].at);
            if (camps.Length > 1) Add(camps[camps.Length - 1].name + " to base A", camps[camps.Length - 1].at, plan.start);
            return legs;
        }

        // ---- The rules -----------------------------------------------------------------------

        private static void Riverbanks(Plan plan)
        {
            if (plan.river.Length == 0) return;
            float south = plan.river.Min(v => v.z) + 8, north = plan.river.Max(v => v.z) - 8;
            foreach (int bank in new[] { -1, 1 })
            {
                // Staggered, so the two banks do not face each other with stones at the same places.
                float next = south + (bank > 0 ? RiverbankEvery / 2 : 0);
                for (float z = south; z <= north; z += 2)
                    if (z >= next && Bar(plan, bank, z)) next = z + RiverbankEvery;
            }
            // A crossing is where a walker meets the river: stones by every bridge.
            foreach (float bridge in Bridges(plan))
            {
                if (plan.sites.Any(s => s.source == Map01StoneSiteData.Source.Riverbank && Mathf.Abs(s.position.z - bridge) <= 15)) continue;
                bool placed = false;
                for (float away = 4; away <= 14 && !placed; away += 2)
                    foreach (float z in new[] { bridge - away, bridge + away })
                        foreach (int bank in new[] { -1, 1 })
                            if (!placed && Bar(plan, bank, z)) placed = true;
            }
        }

        /// <summary>A gravel bar on one bank at <paramref name="z"/>: dry ground a step or two from the water, not a bridge deck.</summary>
        private static bool Bar(Plan plan, int bank, float z)
        {
            if (!Banks(plan, z, out float west, out float east)) return false;
            float edge = bank < 0 ? west : east;
            foreach (float back in new[] { 1.2f, 2f, 3f })
            {
                var wanted = new Vector3(edge + bank * back, plan.water + 1f, z);
                if (!NavMesh.SamplePosition(wanted, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                float above = hit.position.y - plan.water;
                if (above < .2f || above > 1.6f || Mathf.Abs(hit.position.x - edge) > 4.5f) continue;
                if (TryAdd(plan, hit.position, Map01StoneSiteData.Source.Riverbank, RiverbankStones)) return true;
            }
            return false;
        }

        private static void Ring(Plan plan, Vector3 centre, float inner, float outer)
        {
            int Have() => plan.sites.Count(s => Between(Flat(s.position, centre), inner, outer)) + plan.piles.Count(p => Between(Flat(p, centre), inner, outer));
            // Boulders first: the stones go on the side away from the camp, so the rock covers whoever stoops for them.
            var rocks = plan.boulders.Where(b => Between(Flat(b.at, centre), inner + 1, outer - 1))
                .OrderByDescending(b => Score(plan, b.at, centre)).ToArray();
            int used = 0;
            foreach (var rock in rocks)
            {
                if (used >= BouldersPerEncounter) break;
                if (TryFoot(plan, rock.at, rock.radius, 15f, centre)) used++;
            }
            // No boulder where one is needed: a few stones beside a bush.
            var spots = new List<Vector3>();
            foreach (float radius in new[] { (inner + outer) / 2, inner + 4, outer - 4 })
                for (int angle = 0; angle < 360; angle += 10)
                    spots.Add(centre + Quaternion.Euler(0, angle, 0) * Vector3.forward * radius);
            foreach (var spot in spots.OrderByDescending(p => Score(plan, p, centre)))
            {
                if (Have() >= PerEncounter) return;
                TryAdd(plan, BesideBush(plan, spot), Map01StoneSiteData.Source.Trail, TrailStones, 15f, centre);
            }
        }

        /// <summary>Higher for a place beside a bush and on the side of the camp a walker from the base arrives at.</summary>
        private static float Score(Plan plan, Vector3 point, Vector3 centre)
        {
            float approach = Vector3.Dot(Flat(point - centre).normalized, Flat(plan.start - centre).normalized);
            return approach + (plan.bushes.Any(b => Flat(b, point) <= 5f) ? 1f : 0f);
        }

        private static void Trail(Plan plan, Vector3[] corners)
        {
            float since = 0;
            for (int i = 1; i < corners.Length; i++)
            {
                float length = Vector3.Distance(corners[i - 1], corners[i]);
                Vector3 side = Vector3.Cross(Vector3.up, (corners[i] - corners[i - 1]).normalized);
                for (float d = 0; d < length; d += 2)
                {
                    Vector3 at = Vector3.Lerp(corners[i - 1], corners[i], d / length);
                    since = plan.sites.Any(s => Flat(s.position, at) <= 8f) ? 0 : since + 2;
                    if (since < TrailGap) continue;
                    // A boulder close to the way is where a walker would look first; else beside the path, by a bush if there is one.
                    var rock = plan.boulders.Where(b => Flat(b.at, at) <= 7f).OrderBy(b => Flat(b.at, at)).FirstOrDefault();
                    if (rock.radius > 0 && TryFoot(plan, rock.at, rock.radius, Spacing, rock.at * 2 - at)) { since = 0; continue; }
                    foreach (var beside in new[] { at + side * 3, at - side * 3, at + side * 5, at - side * 5 }.OrderByDescending(p => plan.bushes.Any(b => Flat(b, p) <= 6f)))
                        if (TryAdd(plan, beside, Map01StoneSiteData.Source.Trail, TrailStones)) { since = 0; break; }
                }
            }
        }

        private static void Fill(Plan plan, List<Vector3> samples)
        {
            var skip = new HashSet<int>();
            while (plan.sites.Count < MostSites)
            {
                int furthest = -1; float gap = Coverage;
                for (int i = 0; i < samples.Count; i++)
                {
                    if (skip.Contains(i)) continue;
                    float nearest = float.PositiveInfinity;
                    foreach (var site in plan.sites) nearest = Mathf.Min(nearest, Flat(site.position, samples[i]));
                    if (nearest > gap) { gap = nearest; furthest = i; }
                }
                if (furthest < 0) return;
                Vector3 at = samples[furthest];
                var rock = plan.boulders.Where(b => Flat(b.at, at) <= 18f).OrderBy(b => Flat(b.at, at)).FirstOrDefault();
                bool placed = rock.radius > 0 && TryFoot(plan, rock.at, rock.radius, Spacing, rock.at * 2 - at);
                if (!placed) placed = TryAdd(plan, BesideBush(plan, at), Map01StoneSiteData.Source.Trail, TrailStones) || TryAdd(plan, at, Map01StoneSiteData.Source.Trail, TrailStones);
                if (!placed) skip.Add(furthest);
            }
        }

        // ---- Placing one site ------------------------------------------------------------------

        /// <summary>At the foot of a boulder, trying round it from the side facing away from <paramref name="awayFrom"/>.</summary>
        private static bool TryFoot(Plan plan, Vector3 boulder, float radius, float apart, Vector3 awayFrom)
        {
            Vector3 first = Flat(boulder - awayFrom).normalized;
            if (first.sqrMagnitude < .5f) first = Vector3.forward;
            foreach (int turn in new[] { 0, 30, -30, 60, -60, 90, -90, 120, -120, 150, -150, 180 })
                if (TryAdd(plan, boulder + Quaternion.Euler(0, turn, 0) * first * (radius + .45f), Map01StoneSiteData.Source.Boulder, BoulderStones, apart, awayFrom)) return true;
            return false;
        }

        /// <summary>The point moved to a pace from the nearest bush within 6 m, if there is one.</summary>
        private static Vector3 BesideBush(Plan plan, Vector3 point)
        {
            Vector3 best = point; float nearest = 6f;
            foreach (var bush in plan.bushes)
            {
                float d = Flat(bush, point);
                if (d < nearest) { nearest = d; best = bush + Flat(point - bush).normalized * 1.2f; }
            }
            return best;
        }

        private static bool TryAdd(Plan plan, Vector3 wanted, Map01StoneSiteData.Source source, int stones, float apart = Spacing, Vector3? ring = null)
        {
            if (!NavMesh.SamplePosition(wanted, out var hit, 1.5f, NavMesh.AllAreas)) return false;
            Vector3 at = hit.position;
            if (plan.river.Length > 0 && at.y < plan.water + .2f) return false;
            if (Watched(plan, at)) return false;
            foreach (var pile in plan.piles) if (Flat(pile, at) < Spacing) return false;
            foreach (var site in plan.sites)
            {
                float d = Flat(site.position, at);
                // Sites round one camp keep further apart, so they are not all on the same side of it.
                if (d < Spacing || (ring != null && d < apart && Flat(site.position, ring.Value) < 60f)) return false;
            }
            if (Route(plan.start, at).Length < 2) return false;
            plan.sites.Add(new Map01StoneSiteData.Site { position = at, source = source, stones = stones });
            return true;
        }

        private static bool Watched(Plan plan, Vector3 point)
        {
            foreach (var eye in plan.watched) if (Flat(eye.centre, point) <= eye.radius) return true;
            return false;
        }

        // ---- Measuring ---------------------------------------------------------------------------

        private static bool Banks(Plan plan, float z, out float west, out float east)
        {
            west = float.PositiveInfinity; east = float.NegativeInfinity;
            foreach (var v in plan.river) if (Mathf.Abs(v.z - z) < 6f) { west = Mathf.Min(west, v.x); east = Mathf.Max(east, v.x); }
            return west < east;
        }

        /// <summary>Where the walkable ground crosses the river above the water: the Z of each bridge.</summary>
        private static List<float> Bridges(Plan plan)
        {
            var decks = new List<float>();
            for (float z = plan.river.Min(v => v.z); z <= plan.river.Max(v => v.z); z += 2)
            {
                if (!Banks(plan, z, out float west, out float east)) continue;
                float middle = (west + east) / 2;
                if (NavMesh.SamplePosition(new Vector3(middle, plan.water + 1.5f, z), out var hit, 1.2f, NavMesh.AllAreas)
                    && Mathf.Abs(hit.position.x - middle) < 2.5f && hit.position.y > plan.water + .5f) decks.Add(z);
            }
            var bridges = new List<float>();
            for (int i = 0; i < decks.Count; i++)
            {
                int end = i;
                while (end + 1 < decks.Count && decks[end + 1] - decks[end] <= 4) end++;
                bridges.Add((decks[i] + decks[end]) / 2);
                i = end;
            }
            return bridges;
        }

        private static Vector3[] Route(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(from, out var a, 6, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var b, 6, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return new Vector3[0];
            return path.corners;
        }

        private static bool NearWay(Vector3[] corners, Vector3 point, float within)
        {
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 a = Flat(corners[i - 1]), b = Flat(corners[i]), p = Flat(point);
                float t = Mathf.Clamp01(Vector3.Dot(p - a, b - a) / Mathf.Max(.001f, (b - a).sqrMagnitude));
                if (Vector3.Distance(p, a + (b - a) * t) <= within) return true;
            }
            return false;
        }

        private static float Length(Vector3[] corners)
        {
            float length = 0;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        private static float LongestGap(Plan plan, Vector3[] corners)
        {
            float since = 0, longest = 0;
            for (int i = 1; i < corners.Length; i++)
            {
                float length = Vector3.Distance(corners[i - 1], corners[i]);
                for (float d = 0; d < length; d += 2)
                {
                    Vector3 at = Vector3.Lerp(corners[i - 1], corners[i], d / length);
                    since = Watched(plan, at) || plan.sites.Any(s => Flat(s.position, at) <= 8f) ? 0 : since + 2;
                    longest = Mathf.Max(longest, since);
                }
            }
            return longest;
        }

        /// <summary>Points about 6 m apart over everything walkable and above the water.</summary>
        private static List<Vector3> WalkableSamples(Plan plan)
        {
            var samples = new List<Vector3>();
            var mesh = NavMesh.CalculateTriangulation();
            var taken = new HashSet<(int, int)>();
            for (int i = 0; i < mesh.indices.Length; i += 3)
            {
                Vector3 a = mesh.vertices[mesh.indices[i]], b = mesh.vertices[mesh.indices[i + 1]], c = mesh.vertices[mesh.indices[i + 2]];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Vector3.Distance(a, b), Vector3.Distance(b, c), Vector3.Distance(c, a)) / 6f));
                for (int u = 0; u <= steps; u++)
                    for (int v = 0; u + v <= steps; v++)
                    {
                        Vector3 p = a + (b - a) * (u / (float)steps) + (c - a) * (v / (float)steps);
                        if (plan.river.Length > 0 && p.y < plan.water + .2f) continue;
                        if (taken.Add((Mathf.RoundToInt(p.x / 6f), Mathf.RoundToInt(p.z / 6f)))) samples.Add(p);
                    }
            }
            return samples;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        private static bool Between(float value, float low, float high) => value >= low && value <= high;
    }
}
