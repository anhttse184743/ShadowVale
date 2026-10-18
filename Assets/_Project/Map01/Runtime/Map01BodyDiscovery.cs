using System;
using System.Collections.Generic;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Perception;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Guards who come upon a comrade's body (the design the user locked on 18/10/2026, prompt 8 of
    /// Docs/Handoff/SauGopDev2_ViecConLai_Prompts.md). A body is seen by the rules a guard sees Nam with — his view
    /// cone, then foliage and smoke (<see cref="Map01SightCover"/>), then walls — but from 0.6 of his range, a body lying
    /// low and still, and only once it has stayed in sight for a second. Each body is found once (saved with the dead
    /// guard); a body pulled into a bush is never found. What happens then depends on where the finder serves:
    /// <list type="bullet">
    /// <item>jetty B (<see cref="Map01Rescue.OnBodyFound"/>): the squad searches, never the hostage's execution;</item>
    /// <item>points 1–2 (<see cref="Map01Story.OnBodyFound"/>): the point's men search; point 2 radios for the response squad;</item>
    /// <item>the C3 command post (<see cref="Map01C3Mission.OnBodyFound"/>): the finder runs to strike the kẻng;</item>
    /// <item>anywhere else he and his squad-mates close by search round the body.</item>
    /// </list>
    /// A guard who has seen a body stays wary (<see cref="Map01EnemyController.Wary"/>). Map 1 only: Map01Quest adds it,
    /// so the tutorial (dev2's) keeps its guards as they were.
    /// </summary>
    public sealed class Map01BodyDiscovery : MonoBehaviour
    {
        /// <summary>A body is seen from this share of the guard's own vision range (the crouched-Nam scale, Map01Balance).</summary>
        public const float RangeScale = .6f;
        /// <summary>Seconds a body must stay in sight: a glance does not count.</summary>
        public const float NoticeSeconds = 1f;
        /// <summary>This close a guard notices a body from any side (he all but steps on it).</summary>
        public const float TouchRadius = 1.5f;
        /// <summary>Nam hears (reads) a guard's shout within this distance.</summary>
        public const float ShoutMeters = 40f;
        /// <summary>Squad-mates within this distance of a finder outside the named posts come to search with him.</summary>
        public const float SquadCallMeters = 15f;
        private const float Interval = .1f, EyeHeight = 1.3f, SearchRing = 3.5f, BesideMeters = 1.1f;

        /// <summary>Bodies found so far in this run.</summary>
        public int Found { get; private set; }
        /// <summary>A guard (first) found a body (second).</summary>
        public event Action<Map01EnemyController, Map01EnemyController> BodyFound;

        private Map01Mission mission;
        private Map01Rescue rescue;
        private Map01Story story;
        private Map01C3Mission c3;
        private float next;
        private readonly Dictionary<Map01EnemyController, (Map01EnemyController body, float since)> watching =
            new Dictionary<Map01EnemyController, (Map01EnemyController, float)>();
        private readonly List<Map01EnemyController> bodies = new List<Map01EnemyController>();
        private readonly List<Vector3> bodyPoints = new List<Vector3>();

        public static Map01BodyDiscovery For(Map01Mission mission) => mission != null ? mission.GetComponent<Map01BodyDiscovery>() : null;

        private void Awake() => mission = GetComponent<Map01Mission>();

        private void Update()
        {
            if (mission == null || !mission.IsInitialized) return;
            if (mission.Stopped) { watching.Clear(); return; }
            if (Time.time < next) return;
            next = Time.time + Interval;
            rescue ??= GetComponent<Map01Rescue>(); story ??= GetComponent<Map01Story>(); c3 ??= GetComponent<Map01C3Mission>();

            bodies.Clear(); bodyPoints.Clear();
            foreach (var e in mission.Enemies)
                if (e != null && !e.Alive && !e.BodyFound && !e.BodyHidden && e.gameObject.activeInHierarchy) { bodies.Add(e); bodyPoints.Add(SightPoint(e)); }
            if (bodies.Count == 0) { watching.Clear(); return; }

            foreach (var guard in mission.Enemies)
            {
                if (guard == null) continue;
                var seen = Watches(guard) ? NearestSeen(guard) : null;
                if (seen == null) { watching.Remove(guard); continue; }
                if (!watching.TryGetValue(guard, out var w) || w.body != seen) { watching[guard] = (seen, Time.time); continue; }
                if (Time.time - w.since < NoticeSeconds) continue;
                watching.Remove(guard);
                Discover(guard, seen);
            }
        }

        /// <summary>Alive, on the field, calm enough to notice: not fighting, not held in the bush pull, not the commander.</summary>
        private static bool Watches(Map01EnemyController guard) =>
            guard.Alive && guard.isActiveAndEnabled && !guard.Engaged && !guard.IsBoss && guard.WeaponPoseAvailable;

        private Map01EnemyController NearestSeen(Map01EnemyController guard)
        {
            Map01EnemyController best = null; float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (body == guard || body.BodyFound) continue;
                if (Sees(mission, guard, bodyPoints[i], body.transform, out float d) && d < bestDistance) { best = body; bestDistance = d; }
            }
            return best;
        }

        /// <summary>Whether <paramref name="guard"/> sees <paramref name="body"/> right now (no notice time), by the rule above.</summary>
        public static bool CanSee(Map01Mission mission, Map01EnemyController guard, Map01EnemyController body, out float distance)
            => Sees(mission, guard, SightPoint(body), body.transform, out distance);

        private static bool Sees(Map01Mission mission, Map01EnemyController guard, Vector3 target, Transform body, out float distance)
        {
            distance = float.PositiveInfinity;
            float range = guard.VisionRange * RangeScale;
            Vector3 eye = guard.Position + Vector3.up * EyeHeight;
            if ((target - eye).sqrMagnitude > (range + 1f) * (range + 1f)) return false;
            var profile = new VisionProfile(range, ((IEnemyAgent)guard).VisionConeAngle, 1f, 1f, TouchRadius, TouchRadius, EyeHeight);
            if (!VisionSensor.InView(eye, guard.Forward, target, false, false, false, profile, out distance)) return false;
            if (mission.SightCover != null ? mission.SightCover.Blocks(eye, target) : Map01Smoke.Blocks(eye, target)) return false;
            if (Physics.Linecast(eye, target, out var hit, mission.ObstructionMask, QueryTriggerInteraction.Ignore))
                return hit.transform == body || hit.transform.IsChildOf(body);
            return true;
        }

        /// <summary>Where a guard has to see: the body's hips as it lies (a fall carries them off the guard's root), a hand above the ground.</summary>
        public static Vector3 SightPoint(Map01EnemyController body)
        {
            var animator = body.GetComponentInChildren<Animator>();
            var hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            return hips != null ? hips.position + Vector3.up * .1f : body.transform.position + Vector3.up * .3f;
        }

        /// <summary>The walkable spot by the body.</summary>
        public static Vector3 BodySpot(Map01EnemyController body)
        {
            var p = SightPoint(body);
            return NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas) ? hit.position : body.transform.position;
        }

        private void Discover(Map01EnemyController finder, Map01EnemyController body)
        {
            body.MarkBodyFound();
            Found++;
            var at = BodySpot(body);
            BodyFound?.Invoke(finder, body);
            if (rescue != null && rescue.CurrentPhase == Map01Rescue.Phase.Captive && rescue.IsRescueGuard(finder)) rescue.OnBodyFound(finder, at);
            else if (c3 != null && c3.IsGarrison(finder)) c3.OnBodyFound(finder, at);
            else if (story != null && story.PointOf(finder) > 0) story.OnBodyFound(story.PointOf(finder), finder, at);
            else Search(mission, finder, at, Squad(finder), "Địch phát hiện xác đồng đội — chúng đang lục soát quanh đó.");
        }

        /// <summary>His squad-mates within call.</summary>
        private IEnumerable<Map01EnemyController> Squad(Map01EnemyController finder)
        {
            if (string.IsNullOrEmpty(finder.SquadId)) yield break;
            foreach (var g in mission.Enemies)
                if (g != null && g != finder && g.SquadId == finder.SquadId && Vector3.Distance(g.Position, finder.Position) <= SquadCallMeters) yield return g;
        }

        /// <summary>
        /// The finder goes over to the body, each of <paramref name="helpers"/> to his own spot round it, and all of them
        /// stay wary; the finder's shout shows as <paramref name="line"/> when Nam is within earshot.
        /// </summary>
        public static void Search(Map01Mission mission, Map01EnemyController finder, Vector3 at, IEnumerable<Map01EnemyController> helpers, string line)
        {
            finder.InspectBody(Beside(at, finder.Position));
            int k = 0;
            foreach (var g in helpers)
                if (g != null && g != finder && g.Alive && g.isActiveAndEnabled && !g.Engaged) g.InspectBody(Around(at, k++));
            Shout(mission, finder, line);
        }

        /// <summary>Where the finder stops: beside the body on his side of it, not on top of it.</summary>
        public static Vector3 Beside(Vector3 at, Vector3 from)
        {
            var side = Vector3.ProjectOnPlane(from - at, Vector3.up);
            var p = at + (side.sqrMagnitude > .01f ? side.normalized : Vector3.forward) * BesideMeters;
            return NavMesh.SamplePosition(p, out var hit, 1f, NavMesh.AllAreas) ? hit.position : at;
        }

        /// <summary>The <paramref name="k"/>-th search spot round <paramref name="at"/>, on the NavMesh.</summary>
        public static Vector3 Around(Vector3 at, int k)
        {
            float a = (45f + 90f * k) * Mathf.Deg2Rad;
            var p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * SearchRing;
            return NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas) ? hit.position : at;
        }

        /// <summary>
        /// What Nam makes of a guard's call, while he is within earshot. A narrator's notice, not the guard's own words:
        /// character speech is audio only (Map01Mission.GameplayCaption hides "Lính…" lines) and these calls have no
        /// recorded voice yet.
        /// </summary>
        public static void Shout(Map01Mission mission, Map01EnemyController from, string line)
        {
            if (string.IsNullOrEmpty(line) || mission == null || mission.player == null || from == null) return;
            if (Vector3.Distance(mission.player.position, from.Position) <= ShoutMeters) mission.Say(line, 3.5f);
        }
    }
}
