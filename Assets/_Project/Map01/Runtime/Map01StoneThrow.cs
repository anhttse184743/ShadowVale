using System.Linq;
using System.Collections;
using System.Collections.Generic;
using ShadowVale.AI.Perception;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Slot 4 equips and cycles available stones, smoke bombs and F1 grenades.
    /// Hold left mouse to aim along the arc, release to throw, right mouse to cancel.
    /// <list type="bullet">
    /// <item>Stone: each guard Nam can see shows the ring he would hear it fall in, lit when the aim is inside it;
    /// where it lands every guard in earshot leaves his post to check it out (Map01EnemyController).</item>
    /// <item>Smoke bomb: a cloud <see cref="SmokeRadius"/> m round for <see cref="SmokeSeconds"/> s that no guard
    /// sees through (Map01Smoke).</item>
    /// <item>F1: goes off <see cref="FuseSeconds"/> s after it lands; anyone within <see cref="KillRadius"/> m is
    /// killed, within <see cref="WoundRadius"/> m wounded, and the blast is heard <see cref="GrenadeNoise"/> m off
    /// like a shot. Nam is not spared. (Real F1: 3.2-4.2 s fuse, fragments to 200 m; the radii are scaled down
    /// for a stealth map, starting values from the approved design.)</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class Map01StoneThrow : MonoBehaviour
    {
        [SerializeField] private float throwSpeed = 14f;
        [Tooltip("Guards within this distance of Nam (and in his sight) show their hearing ring while he aims.")]
        [SerializeField] private float ringsWithin = 40f;
        private const int ArcPoints = 32, RingPoints = 40, MaxRings = 10;
        public const float SmokeRadius = 4f, SmokeSeconds = 15f, FuseSeconds = 3.5f, KillRadius = 3f, WoundRadius = 7f, GrenadeNoise = 40f;
        private static readonly Color ArcColour = new Color(1f, .92f, .6f, .9f);
        private static readonly Color RingIdle = new Color(1f, 1f, 1f, .35f);
        private static readonly Color RingHears = new Color(1f, .62f, .15f, .95f);
        private static readonly Color RingKill = new Color(.95f, .2f, .12f, .95f);
        private static readonly Color RingSmoke = new Color(.85f, .87f, .82f, .9f);

        public bool Aiming { get; private set; }
        /// <summary>What is being aimed (or was last thrown): "stone", "smoke_bomb" or "grenade_f1".</summary>
        public string Item { get; private set; } = "stone";
        /// <summary>Where the aimed throwable would come down.</summary>
        public Vector3 Landing { get; private set; }
        public IReadOnlyList<Vector3> Arc => arc;
        /// <summary>How many guards the aimed throw would bring over (stone) or alarm (F1).</summary>
        public int WouldHear { get; private set; }
        /// <summary>F1: how many guards stand inside the wound ring of the aimed landing.</summary>
        public int WouldHit { get; private set; }
        /// <summary>How many guards Nam can see, each showing his hearing ring.</summary>
        public int RingsShown { get; private set; }
        public float Range => mission.Settings.stoneRange;
        /// <summary>A guard hears the throwable land (or go off) within this many metres of him. Smoke makes no noise to speak of.</summary>
        public float HearingRadius => Item == "grenade_f1" ? GrenadeNoise : Item == "smoke_bomb" ? 0f : mission.Settings.stoneNoise;

        private readonly List<Vector3> arc = new List<Vector3>();
        private readonly List<LineRenderer> rings = new List<LineRenderer>();
        private Map01Mission mission;
        private Map01Inventory inventory;
        private IBinoculars scouting;
        private LineRenderer arcLine, landingRing, killRing;
        private Vector3 throwFacing;
        private float faceThrowUntil;

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            inventory = GetComponent<Map01Inventory>();
        }

        private void Start() => scouting = GetComponents<MonoBehaviour>().OfType<IBinoculars>().FirstOrDefault(); // Added by Map01Quest.Awake.

        private bool CanAim() => mission.IsInitialized && !mission.Stopped && !mission.InventoryOpen && !mission.MapOpen
            && !ForestMenu.Visible && !ShadowVale.Core.Services.InputFocus.OverlayOpen && (scouting == null || !scouting.Binoculars);

        /// <summary>Left mouse down with slot 4 equipped.</summary>
        public void BeginAim()
        {
            if (Aiming || !CanAim()) return;
            string ready = inventory.ReadyThrowable();
            if (ready == null) return;
            Item = ready;
            Aiming = true;
            Map01NamActions.For(mission)?.Hold(Map01NamActions.ThrowAim, true);   // the stone drawn back
            Plan(AimTarget());
        }

        /// <summary>Right mouse while aiming: the stone goes back in the pocket.</summary>
        public void Cancel()
        {
            Aiming = false;
            faceThrowUntil = 0;
            Map01NamActions.For(mission)?.Hold(Map01NamActions.ThrowAim, false);
            Show(false);
        }

        /// <summary>Left mouse up: throw along the arc shown.</summary>
        public void Release()
        {
            if (!Aiming) return;
            if (!CanAim()) { Cancel(); return; }
            var target = AimTarget();
            FaceTarget(target);
            Plan(target);
            Aiming = false;
            Show(false);
            var nam = Map01NamActions.For(mission);
            if (Launch()) { faceThrowUntil=Time.time+1.1f; nam?.ThrowNow(); } else nam?.Hold(Map01NamActions.ThrowAim, false);
        }

        /// <summary>Throw straight at <paramref name="target"/> (as if aimed there). False without a stone to throw.</summary>
        public bool ThrowAt(Vector3 target)
        {
            if (!CanAim()) return false;
            string ready = inventory.ReadyThrowable();
            if (ready == null) return false;
            Item = ready;
            FaceTarget(target);
            Plan(target);
            if (!Launch()) return false;
            Map01NamActions.For(mission)?.ThrowNow();
            faceThrowUntil=Time.time+1.1f;
            return true;
        }

        private void LateUpdate()
        {
            if (!Aiming) {
                if (Time.time<faceThrowUntil && CanAim()) FaceDirection(throwFacing);
                return;
            }
            if (!CanAim()) { Cancel(); return; }
            var target=AimTarget();
            FaceTarget(target);
            Plan(target);
            Draw();
        }

        private void FaceTarget(Vector3 target)
        {
            throwFacing=Vector3.ProjectOnPlane(target-mission.player.position,Vector3.up);
            FaceDirection(throwFacing);
        }

        private void FaceDirection(Vector3 direction)
        {
            if(direction.sqrMagnitude>.0001f)mission.player.rotation=Quaternion.LookRotation(direction,Vector3.up);
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
            var actor=mission.player.GetComponentInChildren<Animator>();
            var wrist=actor!=null&&actor.isHuman?actor.GetBoneTransform(HumanBodyBones.RightHand):null;
            Vector3 hand=wrist!=null?wrist.TransformPoint(Vector3.up*.06f):mission.player.position+Vector3.up*(mission.Crouched?1:1.5f)+mission.player.right*.25f;
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
            int hears = 0, inBlast = 0;
            foreach (var guard in mission.Enemies)
            {
                if (guard == null || !guard.Alive) continue;
                float d = Vector3.Distance(guard.transform.position, Landing);
                if (d <= HearingRadius) hears++;
                if (d <= WoundRadius) inBlast++;
            }
            WouldHear = hears;
            WouldHit = Item == "grenade_f1" ? inBlast : 0;
        }

        private bool Launch()
        {
            if (arc.Count < 2 || !inventory.UseItem(Item, Landing)) return false;
            StartCoroutine(Fly(arc.ToArray(), Landing, Item));
            return true;
        }

        /// <summary>What flies: the item's own model where the catalog has one, else a grey pebble.</summary>
        private static GameObject Projectile(string item)
        {
            if (item == "stone") return CreateStoneVisual("Thrown stone");
            var catalog = Map01ItemCatalog.Load();
            var model = item != "stone" && catalog != null ? catalog.Model(item) : null;
            if (model != null)
            {
                var go = Instantiate(model);
                go.name = "Thrown " + item;
                go.transform.localScale = Vector3.one * catalog.Scale(item);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                return go;
            }
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = "Thrown " + item;
            Destroy(stone.GetComponent<Collider>());
            stone.transform.localScale = Vector3.one * .16f;
            stone.GetComponent<Renderer>().material.color = item == "stone" ? new Color(.46f, .44f, .39f) : new Color(.28f, .31f, .2f);
            return stone;
        }

        // The same palm-sized pebble is used in the hand and in flight.
        internal static GameObject CreateStoneVisual(string name)
            => CreateHeldVisual("stone",name);

        internal static GameObject CreateHeldVisual(string item,string name)
        {
            var root = new GameObject(name);
            var model = Map01ItemCatalog.Load()?.Model(item);
            var visual = model != null ? Instantiate(model, root.transform, false) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.transform.SetParent(root.transform, false);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) {
                collider.enabled = false;
                Destroy(collider);
            }
            foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(0);
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0) {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float size = bounds.size.magnitude;
                float scale = (item=="stone"?.15f:item=="smoke_bomb"?.21f:.18f) / Mathf.Max(.001f, size);
                visual.transform.localScale *= scale;
                visual.transform.localPosition -= bounds.center * scale;
            }
            return root;
        }

        private IEnumerator Fly(Vector3[] path, Vector3 landing, string item)
        {
            // Inventory is spent once on release; flight begins at the authored
            // hand-opening beat rather than while the arm is still drawn back.
            float release=0;
            while(release<Map01NamActions.StoneReleaseDelay) {
                yield return null;if(!mission.Stopped)release+=Time.deltaTime;
            }
            var actor=mission.player.GetComponentInChildren<Animator>();
            var hand=actor!=null&&actor.isHuman?actor.GetBoneTransform(HumanBodyBones.RightHand):null;
            if(hand!=null) {
                var offset=hand.TransformPoint(Vector3.up*.06f)-path[0];
                for(int i=0;i<path.Length;i++)path[i]+=offset*(1-i/(float)(path.Length-1));
            }
            var stone = Projectile(item);
            stone.transform.position = path[0];
            // The pin and its ring (the smoke bomb's pull cord) stayed in Nam's other hand; the F1's lever springs off
            // as it leaves his, and its fuse smokes from then on - the three-and-a-half seconds can be watched.
            var ring = FindPart(stone.transform, "ring");
            if (ring != null) Destroy(ring.gameObject);
            if (item == "grenade_f1")
            {
                var lever = FindPart(stone.transform, "lever");
                if (lever != null) StartCoroutine(LeverOff(lever, path[Mathf.Min(1, path.Length - 1)] - path[0]));
                FuseSmoke(stone.transform);
            }
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
            if (item == "smoke_bomb")
            {
                Map01Smoke.Spawn(landing, SmokeRadius, SmokeSeconds);
                mission.Say($"Xì... Bom khói phủ kín {SmokeRadius:0} m quanh chỗ rơi trong {SmokeSeconds:0} giây — lính không nhìn xuyên qua được.", 4);
                Destroy(stone, SmokeSeconds);
                yield break;
            }
            if (item == "grenade_f1")
            {
                mission.Say($"Lựu đạn đã rút chốt — nổ sau {FuseSeconds:0.#} giây!", FuseSeconds);
                float fuse = 0;
                while (fuse < FuseSeconds) { yield return null; if (!mission.Stopped) fuse += Time.deltaTime; }
                Destroy(stone);
                Explode(landing);
                yield break;
            }
            int heard = mission.EmitNoise(landing, mission.Settings.stoneNoise, NoiseKind.Stone);
            mission.Say(heard > 0
                ? $"Cạch! {heard} lính nghe thấy tiếng đá và bỏ vị trí đi kiểm tra — tranh thủ lúc này."
                : "Cạch! ...Không lính nào ở đủ gần để nghe thấy.", 4);
            Destroy(stone, 8f);
        }

        /// <summary>The F1 goes off at <paramref name="at"/>: killed within <see cref="KillRadius"/>, wounded (60% of
        /// their health) out to <see cref="WoundRadius"/>, Nam included; the blast is a gunshot to every guard within
        /// <see cref="GrenadeNoise"/> m. Returns (killed, wounded) guards.</summary>
        public (int killed, int wounded) Explode(Vector3 at)
        {
            int killed = 0, wounded = 0;
            foreach (var guard in mission.Enemies)
            {
                if (guard == null || !guard.Alive) continue;
                float d = Vector3.Distance(guard.transform.position, at);
                if (d > WoundRadius) continue;
                var health = guard.GetComponent<ShadowVale.Gameplay.Combat.Health>();
                if (health == null) continue;
                if (d <= KillRadius) { health.TakeDamage(health.Max * 4f, at, mission.player.gameObject); killed++; }
                else { health.TakeDamage(health.Max * .6f, at, mission.player.gameObject); wounded++; }
            }
            float self = Vector3.Distance(mission.player.position, at);
            if (self <= KillRadius) mission.Damage(mission.Settings.playerHP * 4f);
            else if (self <= WoundRadius) mission.Damage(mission.Settings.playerHP * .45f);
            mission.EmitNoise(at, GrenadeNoise, NoiseKind.Gunshot);
            Vector3 up = Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out var ground, 4f, mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                ? ground.normal : Vector3.up;
            Map01Blast.Spawn(at, up, mission.gameCamera);
            Map01Smoke.Spawn(at, 1.6f, 4f, visible: false);     // the blast's dust hides a man behind it for a few seconds
            mission.Say(killed + wounded > 0
                ? $"ẦM! Lựu đạn nổ: {killed} lính chết, {wounded} lính bị thương. Tiếng nổ vang xa {GrenadeNoise:0} m."
                : $"ẦM! Lựu đạn nổ, không trúng ai. Tiếng nổ vang xa {GrenadeNoise:0} m.", 5);
            return (killed, wounded);
        }

        private static Transform FindPart(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == name) return t;
            return null;
        }

        /// <summary>The F1's lever (the spoon) flicked off by its spring as the grenade leaves the hand: up and back,
        /// turning over, down onto the ground near Nam, where it lies a while.</summary>
        private IEnumerator LeverOff(Transform lever, Vector3 along)
        {
            lever.SetParent(null, true);
            Vector3 back = -Vector3.ProjectOnPlane(along, Vector3.up).normalized;
            Vector3 velocity = back * 1.6f + Vector3.up * 3.2f + Vector3.Cross(Vector3.up, back) * Random.Range(-.8f, .8f);
            Vector3 spin = Random.onUnitSphere * 1400f;
            for (float t = 0; t < 2f && lever != null; )
            {
                yield return null;
                if (mission.Stopped) continue;
                float dt = Time.deltaTime; t += dt;
                velocity += Physics.gravity * dt;
                Vector3 next = lever.position + velocity * dt;
                if (velocity.y < 0 && Physics.Linecast(lever.position, next, out var hit, mission.ObstructionMask, QueryTriggerInteraction.Ignore))
                {
                    lever.position = hit.point + hit.normal * .01f;
                    lever.rotation = Quaternion.FromToRotation(lever.up, hit.normal) * lever.rotation;
                    break;
                }
                lever.position = next;
                lever.Rotate(spin * dt, Space.World);
            }
            if (lever != null) Destroy(lever.gameObject, 20f);
        }

        /// <summary>The fuse's thin smoke, trailing in flight and rising from the grenade where it lies until it goes off.</summary>
        private static void FuseSmoke(Transform grenade)
        {
            var wisp = Map01Fx.System(grenade, "Fuse smoke", grenade.position, Map01Fx.Lit, 30f, 60);
            var main = wisp.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.9f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.04f, .08f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .5f);
            main.startColor = new Color(.78f, .78f, .76f, .55f);
            var emission = wisp.emission; emission.rateOverTime = 16f;
            var shape = wisp.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 10f; shape.radius = .01f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Map01Fx.Fade(wisp, .1f, .8f, .4f); Map01Fx.Grow(wisp, 1f, 5f); Map01Fx.Billow(wisp, .15f, 1.2f); Map01Fx.Wind(wisp, Map01Fx.Breeze);
            wisp.Play();
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
            if (killRing != null) killRing.enabled = on && Item == "grenade_f1";
            foreach (var ring in rings) ring.enabled = on && rings.IndexOf(ring) < RingsShown;
            if (!on) RingsShown = 0;
        }

        private void Draw()
        {
            if (arcLine == null) { arcLine = NewLine("Stone arc", .05f); landingRing = NewLine("Stone landing", .1f); landingRing.loop = true; }
            if (killRing == null) { killRing = NewLine("Grenade kill ring", .1f); killRing.loop = true; }
            arcLine.positionCount = arc.Count;
            for (int i = 0; i < arc.Count; i++) arcLine.SetPosition(i, arc[i]);
            Tint(arcLine, ArcColour);
            // Where it comes down: a stone's spot, the smoke cloud's extent, the F1's wound ring (and kill ring inside).
            bool grenade = Item == "grenade_f1", smoke = Item == "smoke_bomb";
            Circle(landingRing, Landing, grenade ? WoundRadius : smoke ? SmokeRadius : .75f, grenade || smoke);
            Tint(landingRing, smoke ? RingSmoke : grenade || WouldHear > 0 ? RingHears : ArcColour);
            killRing.enabled = grenade;
            if (grenade) { Circle(killRing, Landing, KillRadius, true); Tint(killRing, RingKill); }

            // A stone: each guard in sight shows the ring he would hear it land in.
            int shown = 0;
            Vector3 eye = mission.player.position + Vector3.up * 1.5f;
            foreach (var guard in mission.Enemies)
            {
                if (Item != "stone") break;
                if (shown >= MaxRings) break;
                if (guard == null || !guard.Alive) continue;
                Vector3 at = guard.transform.position;
                if (Vector3.Distance(at, mission.player.position) > ringsWithin) continue;
                if (Physics.Linecast(eye, at + Vector3.up * 1.2f, mission.ObstructionMask, QueryTriggerInteraction.Ignore)) continue;
                if (rings.Count <= shown) { var ring = NewLine("Guard hearing ring", .07f); ring.loop = true; rings.Add(ring); }
                Circle(rings[shown], at, HearingRadius, true);
                Tint(rings[shown], Vector3.Distance(at, Landing) <= HearingRadius ? RingHears : RingIdle);
                shown++;
            }
            RingsShown = shown;
            Show(true);
        }

        /// <summary>The line's colour. The shared tracer material is URP Unlit, which ignores vertex colours: each line gets
        /// its own instance (made once, on first use) with the colour in it, so rings can differ (the F1's red kill ring).</summary>
        private static void Tint(LineRenderer line, Color colour)
        {
            line.startColor = line.endColor = colour;
            var m = line.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
            if (m.HasProperty("_Color")) m.SetColor("_Color", colour);
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
