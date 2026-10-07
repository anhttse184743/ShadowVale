using UnityEngine;
using UnityEngine.InputSystem;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Nam's story moves, from the clips made in Blender (chuyen_dong): the knife takedown (played with
    /// the guard's matching clip), the stone throw, the binoculars, looting, bandaging and untying Hùng.
    /// Full-body moves are base-layer states of AC_Player (only the base layer places a humanoid's body):
    /// while one plays his own controls wait, the stance layer is lowered and the take's ground travel is
    /// applied to his model as root motion, then handed to his controller. The moves he can walk through
    /// (stone, binoculars) play on the masked "ActionsUpper" layer, raised only while they play.
    /// </summary>
    public sealed class Map01NamActions : MonoBehaviour
    {
        public const int Takedown = 1, ThrowAim = 2, Throw = 3, Binoculars = 4, BinocularsCrouch = 5,
            PickUp = 6, OpenChest = 7, Bandage = 8, Untie = 9;
        public const string UpperLayer = "ActionsUpper";

        /// <summary>Where the guard stands as the takedown starts, in Nam's frame (right, forward), from the Blender scene.</summary>
        public static readonly Vector2 TakedownGuardOffset = new Vector2(.028f, 1.038f);
        /// <summary>Grab, cover the mouth, the cut, let go: 141 frames at 30 fps.</summary>
        public const float TakedownSeconds = 141f / 30f;
        /// <summary>Kneeling at Hùng's back working the knot loose (141 frames).</summary>
        public const float UntieSeconds = 141f / 30f;
        private const float Blend = .15f;

        private Map01Mission mission;
        private Animator actor;
        private PlayerController controller;
        private PlayerCombat combat;
        private CharacterController body;
        private int upperLayer = -1, stanceLayer = -1;
        private bool hasFullMoves;
        private float upperWeight, fullUntil, upperUntil;
        private bool locked, cancelable, holding, rootMotionWas;
        private Vector3 modelPosition; private Quaternion modelRotation;

        /// <summary>A full-body move is playing; Nam's own controls are waiting.</summary>
        public bool Busy => locked;
        public int ActiveAction { get; private set; }

        public static Map01NamActions For(Map01Mission mission)
        {
            if (mission == null || mission.player == null) return null;
            var actions = mission.player.GetComponent<Map01NamActions>();
            if (actions == null) actions = mission.player.gameObject.AddComponent<Map01NamActions>();
            if (actions.mission == null) actions.Bind(mission);
            return actions;
        }

        private void Bind(Map01Mission owner)
        {
            mission = owner;
            actor = owner.player.GetComponentInChildren<Animator>();
            controller = owner.ModernPlayer; combat = owner.ModernCombat;
            body = owner.player.GetComponent<CharacterController>();
            if (actor == null || actor.runtimeAnimatorController == null) return;
            upperLayer = actor.GetLayerIndex(UpperLayer); stanceLayer = actor.GetLayerIndex(PlayerCombat.UpperBodyLayer);
            hasFullMoves = actor.HasState(0, Animator.StringToHash("Act_Takedown"));
        }

        private bool Ready(int layer) => actor != null && layer > 0 && actor.isActiveAndEnabled;
        private bool ReadyFull => actor != null && hasFullMoves && actor.isActiveAndEnabled;

        private void Fire(int id) { actor.SetInteger("Action", id); actor.SetTrigger("DoAction"); }

        /// <summary>A full-body move. <paramref name="canCancel"/>: walking off ends it early.</summary>
        public bool Play(int id, float seconds, bool canCancel = false)
        {
            if (!ReadyFull || locked) return false;
            locked = true; ActiveAction=id; cancelable = canCancel; fullUntil = Time.time + seconds;
            modelPosition = actor.transform.localPosition; modelRotation = actor.transform.localRotation;
            rootMotionWas = actor.applyRootMotion; actor.applyRootMotion = true;   // the take's own travel moves his model
            SetControls(false);
            Fire(id);
            return true;
        }

        /// <summary>An upper-body pose held for as long as <paramref name="on"/> (the stone drawn back, the glasses up).</summary>
        public void Hold(int id, bool on)
        {
            if (!Ready(upperLayer)) return;
            if (on && !holding) { holding = true; actor.SetBool("ActionHold", true); Fire(id); }
            else if (!on && holding) { holding = false; actor.SetBool("ActionHold", false); }
        }

        /// <summary>The stone leaves the hand: the throw plays on from the wind-up.</summary>
        public void ThrowNow()
        {
            if (!Ready(upperLayer)) return;
            holding = false; actor.SetBool("ActionHold", false);
            Fire(Throw); upperUntil = Time.time + 1.1f;
        }

        /// <summary>
        /// The silent knife takedown: Nam steps in behind <paramref name="guard"/> exactly where the
        /// Blender scene has him, then the two clips run together — he pulls the guard back, covers his
        /// mouth and cuts, the guard drops where he stands.
        /// </summary>
        public bool PlayTakedown(Transform guard)
        {
            if (!ReadyFull || locked || guard == null) return false;
            Vector3 forward = Vector3.ProjectOnPlane(guard.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 1e-4f) forward = transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 spot = guard.position - forward * TakedownGuardOffset.y - right * TakedownGuardOffset.x;
            spot.y = GroundAt(spot, guard.position.y);
            // Both halves were baked from one origin (Nam's): the guard's take holds his body 1.04 m ahead of
            // it. His transform moves back onto Nam's spot, so his body stays exactly where he stood.
            var agent = guard.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Vector3 guardSpot = new Vector3(spot.x, guard.position.y, spot.z);
            if (agent != null && agent.isOnNavMesh) agent.Warp(guardSpot); else guard.position = guardSpot;
            guard.rotation = Quaternion.LookRotation(forward);
            Teleport(spot, Quaternion.LookRotation(forward));
            if (!Play(Takedown, TakedownSeconds)) return false;
            // hands kept on his mouth and neck as in Blender, whatever the retargets did to the two bodies
            var ik = actor.GetComponent<Map01TakedownIK>();
            if (ik == null) ik = actor.gameObject.AddComponent<Map01TakedownIK>();
            ik.Begin(guard.GetComponentInChildren<Animator>(), guard);
            return true;
        }

        /// <summary>Kneels at Hùng's back and works the rope off his wrists.</summary>
        public bool PlayUntie(Transform hung)
        {
            if (!ReadyFull || locked || hung == null) return false;
            Vector3 forward = Vector3.ProjectOnPlane(hung.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 1e-4f) forward = transform.forward;
            Vector3 spot = hung.position - forward * .65f;   // the knot 0.52 m ahead of his hips, at Hùng's back
            spot.y = GroundAt(spot, hung.position.y);
            Teleport(spot, Quaternion.LookRotation(forward));
            return Play(Untie, UntieSeconds);
        }

        /// <summary>
        /// The ground under <paramref name="spot"/>: his own controls are off while a move plays, so nothing
        /// would settle him onto it afterwards. Falls back to the other actor's height.
        /// </summary>
        private float GroundAt(Vector3 spot, float fallback)
        {
            float best = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(spot + Vector3.up * 1.5f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.transform.GetComponentInParent<Map01EnemyController>() == null
                    && hit.transform.GetComponentInParent<Map01HungVisual>() == null && hit.normal.y > .6f && hit.point.y > best) best = hit.point.y;
            return float.IsNegativeInfinity(best) ? fallback : best;
        }

        private void Teleport(Vector3 position, Quaternion rotation)
        {
            bool was = body != null && body.enabled;
            if (was) body.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            if (was) body.enabled = true;
        }

        private void SetControls(bool on)
        {
            if (controller != null) controller.enabled = on;
            if (combat != null) combat.enabled = on;
            if (!on && actor != null) actor.SetFloat(PlayerCombat.AnimatorParams.Speed, 0);
        }

        /// <summary>Walking off, or going for the gun (fire / aim), cuts a cancelable move short.</summary>
        private static bool CancelPressed()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            return (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed))
                || (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed));
        }

        /// <summary>
        /// The move is over: the controller takes over where the take's travel left his model (the takedown
        /// alone carries him about half a metre) — same frame, so the body does not move — and the
        /// controls come back.
        /// </summary>
        private void Finish()
        {
            if (actor != null) {
                Vector3 travel = actor.transform.position - transform.TransformPoint(modelPosition); travel.y = 0;
                actor.applyRootMotion = rootMotionWas;
                actor.transform.SetLocalPositionAndRotation(modelPosition, modelRotation);
                if (travel.sqrMagnitude > 1e-6f) Teleport(transform.position + travel, transform.rotation);
                actor.SetInteger("Action", 0);   // a move cut short leaves its state (StoryAnimationSetup)
            }
            if (actor != null && actor.TryGetComponent(out Map01TakedownIK ik)) ik.End();
            locked = false; ActiveAction=0; cancelable = false;
            SetControls(true);
            combat?.RestoreAfterCinematic();
        }

        private void Update()
        {
            if (actor == null) return;
            bool dead = mission != null && mission.ModernHealth != null && mission.ModernHealth.IsDead;
            if (locked && (Time.time >= fullUntil || (cancelable && CancelPressed()) || dead))
                Finish();
            // the stance layer (weapon arms) would otherwise sit on top of a full-body move; PlayerCombat,
            // which drives it, is waiting meanwhile and raises it again afterwards
            if (locked && stanceLayer > 0) actor.SetLayerWeight(stanceLayer, Mathf.MoveTowards(actor.GetLayerWeight(stanceLayer), 0, Time.deltaTime / Blend));
            if (Ready(upperLayer)) {
                upperWeight = Mathf.MoveTowards(upperWeight, holding || Time.time < upperUntil ? 1 : 0, Time.deltaTime / Blend);
                actor.SetLayerWeight(upperLayer, upperWeight);
            }
        }

        private void OnDisable() { if (locked) Finish(); holding = false; }
    }
}
