using System;
using ShadowVale.Core.Services;
using ShadowVale.Gameplay.Audio;
using UnityEngine;

namespace ShadowVale.Gameplay.Player
{
    /// <summary>
    /// Plays the character's footsteps and announces each one, so whatever owns the level can
    /// decide how far that step carried.
    /// <para>
    /// It measures the distance the character actually moved rather than the speed it asked for.
    /// Walking into a wall with the key held down produces no steps, which is the behaviour a
    /// speed-driven version always gets wrong.
    /// </para>
    /// <para>
    /// With a humanoid rig a step sounds as an animated foot comes down, so the sound keeps time
    /// with the legs whatever the pace (carrying, wading, a slowed walk). The distance count stays
    /// as a backstop for legs that do not lift, such as a test moving the body directly.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        [SerializeField] private FootstepBank bank;
        public FootstepBank Bank => bank;
        [SerializeField] private PlayerController controller;

        [Header("Metres between footfalls")]
        [SerializeField] private float sneakStride = 0.75f;
        [SerializeField] private float walkStride = 1.0f;
        [SerializeField] private float sprintStride = 1.35f;

        [Tooltip("Time each step to the animated foot coming down (humanoid rigs). Off: by distance only.")]
        [SerializeField] private bool followFeet = true;

        [Header("Volume")]
        [Tooltip("The player's own feet, heard from inside their head: flat stereo, not placed " +
                 "in the world, so the left/right split is audible.")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float landingVolumeBoost = 0.35f;

        /// <summary>
        /// Asked once per step for what is underfoot. Map 1 answers from the river it already
        /// tracks; a scene that does not set it gets grass.
        /// </summary>
        public Func<FootSurface> SurfaceProbe { get; set; }

        /// <summary>Raised on every footfall, including the thump of a landing.</summary>
        public event Action<Footstep> Stepped;

        public readonly struct Footstep
        {
            public readonly Vector3 Position;
            public readonly MoveStance Stance;
            public readonly FootSurface Surface;
            public readonly bool LeftFoot;
            /// <summary>True for the heavier sound made by landing from a jump or a fall.</summary>
            public readonly bool Landing;

            public Footstep(Vector3 position, MoveStance stance, FootSurface surface,
                bool leftFoot, bool landing)
            {
                Position = position;
                Stance = stance;
                Surface = surface;
                LeftFoot = leftFoot;
                Landing = landing;
            }
        }

        private readonly StrideAccumulator _strider = new();
        private CharacterController _characterController;
        private IAudioService _audio;
        private Vector3 _lastPosition;
        private bool _wasGrounded = true;
        private Animator _body;
        private Transform _leftFoot, _rightFoot;
        private readonly FootContact _leftContact = new(), _rightContact = new();
        /// <summary>Distance strides without a foot coming down before the backstop steps instead.</summary>
        private const float BackstopStrides = 1.6f;
        /// <summary>Below this the character is standing; a shifting idle foot is not a step.</summary>
        private const float MovingSpeed = 0.25f;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (controller == null) controller = GetComponent<PlayerController>();
            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            _strider.ResetFully();
            _leftContact.Reset(); _rightContact.Reset();
        }

        private void Update()
        {
            if (bank == null) return;

            Vector3 position = transform.position;
            Vector3 delta = position - _lastPosition;
            _lastPosition = position;
            delta.y = 0f;

            bool grounded = _characterController.isGrounded;
            MoveStance stance = CurrentStance();
            FootSurface surface = SurfaceProbe != null ? SurfaceProbe() : FootSurface.Grass;

            if (grounded && !_wasGrounded)
            {
                // Landing is its own footfall. Clearing the banked distance first stops the
                // jump's forward travel from firing a second step on the next frame, and taking
                // the foot keeps the alternation going into the first stride afterwards.
                _strider.Reset();
                Emit(position, stance, surface, _strider.TakeFoot(), landing: true);
                _wasGrounded = true;
                return;
            }
            _wasGrounded = grounded;

            if (!grounded)
            {
                // Nothing to step on. Keep the partial stride so a short hop does not restart
                // the rhythm from zero.
                return;
            }

            if (FeetFound())
            {
                float dt = Mathf.Max(Time.deltaTime, 0.0001f), ground = position.y;
                bool moving = delta.magnitude / dt > MovingSpeed;
                bool left = _leftContact.Planted(_leftFoot.position.y - ground, dt);
                bool right = _rightContact.Planted(_rightFoot.position.y - ground, dt);
                bool late = _strider.Advance(delta.magnitude, StrideFor(stance) * BackstopStrides, out bool lateFoot);
                if (moving && (left || right))
                {
                    _strider.Reset();
                    Emit(position, stance, surface, left, landing: false);
                }
                else if (late)
                {
                    Emit(position, stance, surface, lateFoot, landing: false);
                }
                return;
            }

            if (_strider.Advance(delta.magnitude, StrideFor(stance), out bool leftFoot))
            {
                Emit(position, stance, surface, leftFoot, landing: false);
            }
        }

        /// <summary>The humanoid's feet, found again if the visual was swapped.</summary>
        private bool FeetFound()
        {
            if (!followFeet) return false;
            if (_body == null || !_body.isActiveAndEnabled || _leftFoot == null || _rightFoot == null)
            {
                _body = null; _leftFoot = _rightFoot = null;
                foreach (var animator in GetComponentsInChildren<Animator>())
                {
                    if (!animator.isHuman || !animator.isActiveAndEnabled) continue;
                    _body = animator;
                    _leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    _rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    _leftContact.Reset(); _rightContact.Reset();
                    break;
                }
            }
            return _leftFoot != null && _rightFoot != null;
        }

        /// <summary>
        /// One foot's height above the character's base, watched for the moment it comes down:
        /// it must have lifted clear of its lowest recent height, then return close to it. Heights are
        /// taken from the character's base, so slopes and stairs move both; the lowest height only drifts
        /// up slowly (a foot held up through its swing must not become the new ground).
        /// </summary>
        private sealed class FootContact
        {
            private const float LiftHeight = 0.05f, PlantHeight = 0.02f, LowRecovery = 0.025f;   // slower than a swing lasts
            private float _low = float.PositiveInfinity;
            private bool _lifted;

            public bool Planted(float height, float dt)
            {
                _low = Mathf.Min(height, _low + LowRecovery * dt);
                if (height > _low + LiftHeight) { _lifted = true; return false; }
                if (!_lifted || height > _low + PlantHeight) return false;
                _lifted = false;
                return true;
            }

            public void Reset() { _low = float.PositiveInfinity; _lifted = false; }
        }

        private void Emit(Vector3 position, MoveStance stance, FootSurface surface,
            bool leftFoot, bool landing)
        {
            Play(surface, stance, leftFoot, landing);
            Stepped?.Invoke(new Footstep(position, stance, surface, leftFoot, landing));
        }

        private void Play(FootSurface surface, MoveStance stance, bool leftFoot, bool landing)
        {
            if (!bank.Resolve(surface, stance, leftFoot, out AudioSlice slice,
                    out float volume, out float pitch, out float pan))
            {
                return;
            }

            _audio ??= Audio.PooledAudioService.Resolve();
            volume *= masterVolume;
            if (landing)
            {
                volume = Mathf.Clamp01(volume + landingVolumeBoost);
                pitch *= 0.92f; // Heavier than a stride, so drop it slightly.
            }

            _audio.PlayOneShot(slice.clip, transform.position, volume, pitch, pan,
                spatialBlend: 0f, startTime: slice.startTime, duration: slice.duration);
        }

        private MoveStance CurrentStance()
        {
            if (controller == null) return MoveStance.Walk;
            if (controller.IsSneaking) return MoveStance.Sneak;
            return controller.IsSprinting ? MoveStance.Sprint : MoveStance.Walk;
        }

        private float StrideFor(MoveStance stance) => stance switch
        {
            MoveStance.Sneak => sneakStride,
            MoveStance.Sprint => sprintStride,
            _ => walkStride,
        };
    }
}
