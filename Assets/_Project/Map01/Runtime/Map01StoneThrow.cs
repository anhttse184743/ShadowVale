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
    /// <item>F1: goes off the moment it lands (3.5 s after it until 2026-10-22); anyone within <see cref="KillRadius"/> m is
    /// killed, within <see cref="WoundRadius"/> m wounded, and the blast is heard <see cref="GrenadeNoise"/> m off
    /// like a shot. Nam is not spared. (Real F1: 3.2-4.2 s fuse, fragments to 200 m; the radii are scaled down
    /// for a stealth map, starting values from the approved design; no wait on the ground, by the user's call.)</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed partial class Map01StoneThrow : MonoBehaviour
    {
        [SerializeField] private float throwSpeed = 14f;
        [Tooltip("Guards within this distance of Nam (and in his sight) show their hearing ring while he aims.")]
        [SerializeField] private float ringsWithin = 40f;
        private const int ArcPoints = 32, RingPoints = 40, MaxRings = 10;
        public const float SmokeRadius = 4f, SmokeSeconds = 15f, KillRadius = 6f, WoundRadius = 14f, GrenadeNoise = 40f;
        private static readonly Color ArcColour = new Color(1f, .92f, .6f, .9f);
        private static readonly Color RingIdle = new Color(1f, 1f, 1f, .35f);
        private static readonly Color RingHears = new Color(1f, .62f, .15f, .95f);
        private static readonly Color RingKill = new Color(.95f, .2f, .12f, .95f);
        private static readonly Color RingSmoke = new Color(.85f, .87f, .82f, .9f);

        public bool Aiming { get; private set; }
        public bool PresentingThrow => Aiming || (mission != null && mission.player != null &&
            mission.player.GetComponent<Map01NamActions>() is { StoneActionPlaying: true });
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
        private CharacterHudSession mission;
        private Map01Inventory inventory;
        private IBinoculars scouting;
        private LineRenderer arcLine, landingRing, killRing;
        private Vector3 throwFacing;
        private float faceThrowUntil;

        private void Awake()
        {
            mission = CharacterHudSession.For(gameObject);
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
            if (ready == null)
            {
                mission.Say(inventory.Throwable == "stone"
                    ? "Hết đá ném. Nhặt thêm ở bờ sông hoặc các bãi đá bên đường."
                    : $"Hết {ForestInventory.Name(inventory.Throwable).ToLowerInvariant()}. Nhấn 4 để chọn vật ném khác.", 4);
                return;
            }
            Item = ready;
            mission.ModernCombat?.CancelReload();
            Aiming = true;
            Map01NamActions.For(mission.MapOne)?.Hold(Map01NamActions.ThrowAim, true);   // the stone drawn back
            Plan(AimTarget());
        }

        /// <summary>Right mouse while aiming: the stone goes back in the pocket.</summary>
        public void Cancel()
        {
            Aiming = false;
            faceThrowUntil = 0;
            Map01NamActions.For(mission.MapOne)?.Hold(Map01NamActions.ThrowAim, false);
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
            var nam = Map01NamActions.For(mission.MapOne);
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
            mission.ModernCombat?.CancelReload();
            if (!Launch()) return false;
            Map01NamActions.For(mission.MapOne)?.ThrowNow();
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
            if(mission.MapTwo!=null)foreach(var guard in mission.MapTwo.Chapter.Enemies)
            {if(guard==null||guard.Dead||!guard.gameObject.activeInHierarchy)continue;float d=Vector3.Distance(guard.transform.position,Landing);if(d<=HearingRadius)hears++;if(d<=WoundRadius)inBlast++;}
            WouldHear = hears;
            WouldHit = Item == "grenade_f1" ? inBlast : 0;
        }

        private bool Launch()
        {
            if (arc.Count < 2 || !inventory.UseItem(Item, Landing)) return false;
            StartCoroutine(Fly(arc.ToArray(), Landing, Item));
            return true;
        }
    }
}
