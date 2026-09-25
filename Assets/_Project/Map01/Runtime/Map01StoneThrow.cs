using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Stones as a distraction. Hold [Q] to aim: an arc shows where the stone will land, and each
    /// guard Nam can see shows the ring he would hear a stone fall in — lit when the aim is inside
    /// it. Release [Q] to throw, right mouse to put the stone away. The stone flies the arc, and
    /// where it lands every guard in earshot leaves his post to check it out and look around for
    /// a while before going back (Map01EnemyController) — time to slip past, log a camp through
    /// the binoculars, or take him down from behind with the knife.
    /// </summary>
    public sealed class Map01StoneThrow : MonoBehaviour
    {
        [SerializeField] private float throwSpeed = 14f;
        [Tooltip("Guards within this distance of Nam (and in his sight) show their hearing ring while he aims.")]
        [SerializeField] private float ringsWithin = 40f;
        private const int ArcPoints = 32, RingPoints = 40, MaxRings = 10;
        private static readonly Color ArcColour = new Color(1f, .92f, .6f, .9f);
        private static readonly Color RingIdle = new Color(1f, 1f, 1f, .35f);
        private static readonly Color RingHears = new Color(1f, .62f, .15f, .95f);

        public bool Aiming { get; private set; }
        /// <summary>Where the aimed stone would come down.</summary>
        public Vector3 Landing { get; private set; }
        public IReadOnlyList<Vector3> Arc => arc;
        /// <summary>How many guards the aimed stone would bring over.</summary>
        public int WouldHear { get; private set; }
        /// <summary>How many guards Nam can see, each showing his hearing ring.</summary>
        public int RingsShown { get; private set; }
        public float Range => mission.Settings.stoneRange;
        /// <summary>A guard hears a stone land within this many metres of him.</summary>
        public float HearingRadius => mission.Settings.stoneNoise;

        private readonly List<Vector3> arc = new List<Vector3>();
        private readonly List<LineRenderer> rings = new List<LineRenderer>();
        private Map01Mission mission;
        private Map01Inventory inventory;
        private Map01Scouting scouting;
        private LineRenderer arcLine, landingRing;

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            inventory = GetComponent<Map01Inventory>();
        }

        private void Start() => scouting = GetComponent<Map01Scouting>(); // Added by Map01Quest.Awake.

        private bool CanAim() => mission.IsInitialized && !mission.Stopped && !mission.InventoryOpen && !mission.MapOpen
            && !ForestMenu.Visible && (scouting == null || !scouting.Binoculars);

        /// <summary>[Q] down.</summary>
        public void BeginAim()
        {
            if (Aiming || !CanAim()) return;
            if (inventory.Count("stone") <= 0) { mission.Say("Hết đá để ném.", 3); return; }
            Aiming = true;
            Plan(AimTarget());
        }

        /// <summary>Right mouse while aiming: the stone goes back in the pocket.</summary>
        public void Cancel()
        {
            Aiming = false;
            Show(false);
        }

        /// <summary>[Q] up: throw along the arc shown.</summary>
        public void Release()
        {
            if (!Aiming) return;
            Aiming = false;
            Show(false);
            Launch();
        }

        /// <summary>Throw straight at <paramref name="target"/> (as if aimed there). False without a stone to throw.</summary>
        public bool ThrowAt(Vector3 target)
        {
            if (!CanAim()) return false;
            Plan(target);
            return Launch();
        }

        private void Update()
        {
            if (!Aiming) return;
            if (!CanAim()) { Cancel(); return; }
            Plan(AimTarget());
            Draw();
        }

        /// <summary>Where the crosshair points, no further than a throw, brought down to the ground.</summary>
        private Vector3 AimTarget()
        {
            var ray = mission.gameCamera.ViewportPointToRay(new Vector3(.5f, .5f));
            Vector3 point = Physics.Raycast(ray, out var hit, 200f, mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                ? hit.point : ray.GetPoint(Range);
            Vector3 flat = Vector3.ProjectOnPlane(point - mission.player.position, Vector3.up);
            if (flat.magnitude > Range) point = mission.player.position + flat.normalized * Range + Vector3.up * (point.y - mission.player.position.y);
            return Ground(point);
        }

        private Vector3 Ground(Vector3 point)
        {
            var from = point + Vector3.up * 4f;
            return Physics.Raycast(from, Vector3.down, out var hit, 30f, mission.ObstructionMask, QueryTriggerInteraction.Ignore) ? hit.point : point;
        }

        /// <summary>A lobbed arc from Nam's hand to <paramref name="target"/>, cut short where it hits something.</summary>
        private void Plan(Vector3 target)
        {
            Vector3 hand = mission.player.position + Vector3.up * 1.5f + mission.player.right * .25f;
            float distance = Vector3.Distance(hand, target);
            float apex = Mathf.Clamp(distance * .18f, 1f, 3.2f);
            arc.Clear();
            arc.Add(hand);
            Landing = target;
            for (int i = 1; i <= ArcPoints; i++)
            {
                float t = i / (float)ArcPoints;
                Vector3 previous = arc[arc.Count - 1];
                Vector3 next = Vector3.Lerp(hand, target, t) + Vector3.up * (4f * apex * t * (1f - t));
                if (Physics.Linecast(previous, next, out var hit, mission.ObstructionMask, QueryTriggerInteraction.Ignore))
                {
                    // Glances off whatever it hit and drops to the ground below.
                    Vector3 off = hit.point - (next - previous).normalized * .25f;
                    arc.Add(off);
                    Landing = Physics.Raycast(off, Vector3.down, out var ground, 30f, mission.ObstructionMask, QueryTriggerInteraction.Ignore) ? ground.point : off;
                    arc.Add(Landing);
                    break;
                }
                arc.Add(next);
            }
            int hears = 0;
            foreach (var guard in mission.Enemies)
                if (guard != null && guard.Alive && Vector3.Distance(guard.transform.position, Landing) <= HearingRadius) hears++;
            WouldHear = hears;
        }

        private bool Launch()
        {
            if (arc.Count < 2 || !inventory.UseItem("stone", Landing)) return false;
            StartCoroutine(Fly(arc.ToArray(), Landing));
            return true;
        }

        private IEnumerator Fly(Vector3[] path, Vector3 landing)
        {
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = "Thrown stone";
            Destroy(stone.GetComponent<Collider>());
            stone.transform.localScale = Vector3.one * .16f;
            stone.GetComponent<Renderer>().material.color = new Color(.46f, .44f, .39f);
            float length = 0;
            for (int i = 1; i < path.Length; i++) length += Vector3.Distance(path[i - 1], path[i]);
            float duration = Mathf.Max(.2f, length / throwSpeed), elapsed = 0;
            while (elapsed < duration)
            {
                float along = elapsed / duration * (path.Length - 1);
                int i = Mathf.Min(path.Length - 2, (int)along);
                stone.transform.position = Vector3.Lerp(path[i], path[i + 1], along - i);
                yield return null;
                if (mission.Stopped) continue; // A pause holds the stone in the air rather than dropping the noise early.
                elapsed += Time.deltaTime;
            }
            stone.transform.position = landing + Vector3.up * .06f;
            int heard = mission.EmitNoise(landing, HearingRadius);
            mission.Say(heard > 0
                ? $"Cạch! {heard} lính nghe thấy tiếng đá và bỏ vị trí đi kiểm tra — tranh thủ lúc này."
                : "Cạch! ...Không lính nào ở đủ gần để nghe thấy.", 4);
            Destroy(stone, 8f);
        }

        // ---- Drawing -----------------------------------------------------------------------

        private LineRenderer NewLine(string name, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = mission.trailMaterial;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = width;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void Show(bool on)
        {
            if (arcLine != null) arcLine.enabled = on;
            if (landingRing != null) landingRing.enabled = on;
            foreach (var ring in rings) ring.enabled = on && rings.IndexOf(ring) < RingsShown;
            if (!on) RingsShown = 0;
        }

        private void Draw()
        {
            if (arcLine == null) { arcLine = NewLine("Stone arc", .05f); landingRing = NewLine("Stone landing", .1f); landingRing.loop = true; }
            arcLine.positionCount = arc.Count;
            for (int i = 0; i < arc.Count; i++) arcLine.SetPosition(i, arc[i]);
            arcLine.startColor = arcLine.endColor = ArcColour;
            Circle(landingRing, Landing, .75f, false);
            landingRing.startColor = landingRing.endColor = WouldHear > 0 ? RingHears : ArcColour;

            // Each guard in sight shows the ring he would hear the stone land in.
            int shown = 0;
            Vector3 eye = mission.player.position + Vector3.up * 1.5f;
            foreach (var guard in mission.Enemies)
            {
                if (shown >= MaxRings) break;
                if (guard == null || !guard.Alive) continue;
                Vector3 at = guard.transform.position;
                if (Vector3.Distance(at, mission.player.position) > ringsWithin) continue;
                if (Physics.Linecast(eye, at + Vector3.up * 1.2f, mission.ObstructionMask, QueryTriggerInteraction.Ignore)) continue;
                if (rings.Count <= shown) { var ring = NewLine("Guard hearing ring", .07f); ring.loop = true; rings.Add(ring); }
                Circle(rings[shown], at, HearingRadius, true);
                rings[shown].startColor = rings[shown].endColor = Vector3.Distance(at, Landing) <= HearingRadius ? RingHears : RingIdle;
                shown++;
            }
            RingsShown = shown;
            Show(true);
        }

        private void Circle(LineRenderer line, Vector3 centre, float radius, bool followGround)
        {
            line.positionCount = RingPoints;
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i * Mathf.PI * 2f / RingPoints;
                var p = centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                if (followGround && Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, mission.ObstructionMask, QueryTriggerInteraction.Ignore))
                    p.y = hit.point.y;
                line.SetPosition(i, p + Vector3.up * .08f);
            }
        }
    }
}
