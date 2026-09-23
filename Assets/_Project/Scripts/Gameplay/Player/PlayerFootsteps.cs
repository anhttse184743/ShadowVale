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
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        [SerializeField] private FootstepBank bank;
        [SerializeField] private PlayerController controller;

        [Header("Metres between footfalls")]
        [SerializeField] private float sneakStride = 0.75f;
        [SerializeField] private float walkStride = 1.0f;
        [SerializeField] private float sprintStride = 1.35f;

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

            if (_strider.Advance(delta.magnitude, StrideFor(stance), out bool leftFoot))
            {
                Emit(position, stance, surface, leftFoot, landing: false);
            }
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
