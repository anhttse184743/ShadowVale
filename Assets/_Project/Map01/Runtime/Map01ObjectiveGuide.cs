using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Works out where the current objective is and how to walk there, for the HUD's compass,
    /// marker and objective line and the minimap. The target follows the quest — the supply crate
    /// while Nam still needs a herb, Hùng, the base, the rough area of the nearest camp still to
    /// scout, Hùng again whenever a task must be reported, the nearest manned outpost, the commander.
    /// </summary>
    public sealed class Map01ObjectiveGuide : MonoBehaviour
    {
        [SerializeField] private float repathInterval = .5f;
        [Tooltip("A partial route (target off the NavMesh, e.g. a guard up a watchtower) is still used if it ends this close.")]
        [SerializeField] private float partialTolerance = 10f;

        public bool HasTarget { get; private set; }
        public string Label { get; private set; }
        public Vector3 Target { get; private set; }
        /// <summary>Walking distance along the route (straight line when no route was found).</summary>
        public float Distance { get; private set; }
        /// <summary>A few metres along the route — the compass points here, not through walls.</summary>
        public Vector3 SteerPoint { get; private set; }
        /// <summary>The route as points about a metre apart.</summary>
        public IReadOnlyList<Vector3> Route => route;

        private Map01Mission mission;
        private Map01Quest quest;
        private Map01Inventory inventory;
        private Map01Scouting scouting;
        private readonly List<Vector3> route = new List<Vector3>();
        private readonly List<float> routeDistance = new List<float>();
        private NavMeshPath path;
        private float nextRepath;

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            quest = GetComponent<Map01Quest>();
            inventory = GetComponent<Map01Inventory>();
            path = new NavMeshPath();
        }

        private void Start() => scouting = GetComponent<Map01Scouting>(); // Added by Map01Quest.Awake.

        private bool PickTarget(out Vector3 target, out string label)
        {
            target = default; label = null;
            if (quest.AwaitingReport) { target = mission.hung.position; label = "BÁO CÁO HÙNG"; return true; }
            switch (quest.Stage)
            {
                case Map01Quest.RescueStage:
                    var crate = mission.Points.FirstOrDefault(p => p.id == "tutorial_loot");
                    if (inventory.Count("herb") <= 0 && crate != null) { target = crate.transform.position; label = "THÙNG VẬT TƯ"; }
                    else { target = mission.hung.position; label = "HÙNG"; }
                    return true;
                case Map01Quest.ScoutStage:
                    // Only the rough area of the nearest camp not yet logged — never the camp itself.
                    var zone = scouting.Camps.Where(c => !c.Found)
                        .OrderBy(c => (c.ZoneCenter - mission.player.position).sqrMagnitude).FirstOrDefault();
                    if (zone == null) return false;
                    target = zone.ZoneCenter; label = "KHU VỰC NGHI NGỜ";
                    return true;
                case Map01Quest.EscortStage:
                    var supplies = mission.Points.FirstOrDefault(p => p.kind == ForestPointKind.Supplies);
                    if (supplies == null) return false;
                    target = supplies.transform.position; label = "CĂN CỨ CHỈ HUY";
                    return true;
                case Map01Quest.CampsStage:
                    var guard = mission.Enemies.Where(e => e.Alive && e.name.StartsWith("Outpost guard "))
                        .OrderBy(e => (e.transform.position - mission.player.position).sqrMagnitude).FirstOrDefault();
                    if (guard == null) return false;
                    target = guard.transform.position; label = "DOANH TRẠI ĐỊCH";
                    return true;
                case Map01Quest.BossStage:
                    var boss = mission.Enemies.FirstOrDefault(e => e.IsBoss && e.Alive);
                    if (boss == null) return false;
                    target = boss.transform.position; label = "CHỈ HUY ĐỊCH";
                    return true;
                default:
                    return false; // The complete stage has nowhere left to go.
            }
        }

        private void Update()
        {
            if (!mission.IsInitialized || Time.time < nextRepath) return;
            nextRepath = Time.time + repathInterval;
            bool found = PickTarget(out var target, out var label);
            HasTarget = found && !mission.Stopped;
            if (HasTarget) { Target = target; Label = label; Repath(); }
        }

        /// <summary>
        /// Rebuilds the route on the NavMesh, densified to ~1 m so its length follows slopes.
        /// Falls back to a straight line when there is no usable route.
        /// </summary>
        private void Repath()
        {
            route.Clear(); routeDistance.Clear();
            Vector3 from = mission.player.position;
            SteerPoint = Target;
            Distance = Vector3.Distance(from, Target);
            if (!NavMesh.SamplePosition(from, out var start, 3f, NavMesh.AllAreas)
                || !SampleNear(Target, out var end)
                || !NavMesh.CalculatePath(start.position, end, NavMesh.AllAreas, path)
                || path.status == NavMeshPathStatus.PathInvalid || path.corners.Length < 2)
                return;
            var corners = path.corners;
            // A partial path stops at the closest reachable point — worth following only if that
            // is right at the objective, not a dead end somewhere across the map.
            if (path.status == NavMeshPathStatus.PathPartial
                && Vector3.ProjectOnPlane(corners[corners.Length - 1] - Target, Vector3.up).magnitude > partialTolerance)
                return;
            AddRoutePoint(corners[0]);
            for (int i = 1; i < corners.Length; i++)
            {
                float length = Vector3.Distance(corners[i - 1], corners[i]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length));
                for (int s = 1; s <= steps; s++)
                {
                    var p = Vector3.Lerp(corners[i - 1], corners[i], s / (float)steps);
                    if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) p = hit.position;
                    AddRoutePoint(p);
                }
            }
            float walked = routeDistance[routeDistance.Count - 1];
            Distance = walked + Vector3.Distance(route[route.Count - 1], Target); // The last steps off the mesh count too.
            SteerPoint = PointAt(Mathf.Min(6f, walked));
        }

        /// <summary>
        /// The nearest NavMesh point in 3D can be a roof right above the objective — the base's
        /// supply point stands where the floor is carved out under furniture, and the closest mesh
        /// is its roof, which no route reaches. Prefer mesh at the objective's own height, probing
        /// rings around it, and only then settle for whatever is closest.
        /// </summary>
        private static bool SampleNear(Vector3 target, out Vector3 point)
        {
            point = default;
            if (NavMesh.SamplePosition(target, out var hit, 1.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - target.y) < 1.5f)
            {
                point = hit.position; return true;
            }
            for (float ring = 1.5f; ring <= 6f; ring += 1.5f)
            {
                float best = float.MaxValue;
                for (int i = 0; i < 12; i++)
                {
                    var probe = target + Quaternion.Euler(0, i * 30f, 0) * Vector3.forward * ring;
                    if (!NavMesh.SamplePosition(probe, out hit, 1f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - target.y) > 1.5f) continue;
                    float d = (hit.position - target).sqrMagnitude;
                    if (d < best) { best = d; point = hit.position; }
                }
                if (best < float.MaxValue) return true;
            }
            if (!NavMesh.SamplePosition(target, out hit, 6f, NavMesh.AllAreas)) return false;
            point = hit.position; return true;
        }

        private void AddRoutePoint(Vector3 p)
        {
            routeDistance.Add(route.Count == 0 ? 0 : routeDistance[routeDistance.Count - 1] + Vector3.Distance(route[route.Count - 1], p));
            route.Add(p);
        }

        private Vector3 PointAt(float distance)
        {
            int i = routeDistance.BinarySearch(distance);
            if (i >= 0) return route[i];
            i = ~i;
            if (i <= 0) return route[0];
            if (i >= route.Count) return route[route.Count - 1];
            float t = Mathf.InverseLerp(routeDistance[i - 1], routeDistance[i], distance);
            return Vector3.Lerp(route[i - 1], route[i], t);
        }
    }
}
