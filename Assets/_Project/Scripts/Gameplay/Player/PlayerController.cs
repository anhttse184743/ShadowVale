using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Gameplay.Player
{
    /// <summary>
    /// Greybox test player. Camera-relative WASD movement on the XZ plane, sprint, sneak and jump,
    /// driven by the new Input System. Works with any camera whose yaw defines "forward":
    /// the isometric rig (<see cref="IsoFollowCamera"/>) or the Minecraft-style
    /// <see cref="ThirdPersonCamera"/>.
    /// Uses a CharacterController so it collides with greybox walls/blocks and walks up
    /// ramps under manual gravity. Animation is driven through parameters only — root motion
    /// stays off so the controller remains the single source of truth for movement.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement (metric, 1 unit = 1 m)")]
        [SerializeField] private float walkSpeed = 2.5f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float sneakSpeed = 1.2f;
        [SerializeField] private float rotationSpeed = 720f; // deg/sec toward travel direction
        [SerializeField] private float acceleration = 14f;   // m/s^2 toward the target velocity
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float jumpHeight = 1.4f;

        [Header("Camera basis")]
        [Tooltip("Transform whose yaw defines 'forward' for input. Defaults to Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Animation")]
        [Tooltip("Animator on the character model. Optional — movement works without one.")]
        [SerializeField] private Animator animator;

        private CharacterController _controller;
        private Health _health;
        private Vector3 _planarVelocity;
        private float _verticalVelocity;
        private bool _faceCameraYaw;
        private float _faceYawOffset;
        private bool _sprintToggled;
        private bool _sneakToggled;

        public bool IsSneaking { get; private set; }
        public bool IsSprinting { get; private set; }
        public System.Func<bool> InputAllowed { get; set; }
        public System.Func<bool> SprintAllowed { get; set; }
        public float SurfaceSpeedMultiplier { get; set; } = 1;
        public void RestoreMotion(bool sneaking)
        {
            _planarVelocity = Vector3.zero;
            _verticalVelocity = 0;
            _sprintToggled = false;
            _sneakToggled = IsSneaking = sneaking;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

        /// <summary>
        /// While aiming, the character turns to face the camera instead of its travel direction,
        /// so strafing reads correctly down the sights.
        /// <para>
        /// <paramref name="yawOffset"/> stands the body that many degrees round from the camera's
        /// yaw. A shooting take whose arms aim off to one side needs it: the body turns past the
        /// target so the rifle itself ends up on it. Zero faces the camera squarely.
        /// </para>
        /// </summary>
        public void SetFaceCameraYaw(bool value, float yawOffset = 0f)
        {
            _faceCameraYaw = value;
            _faceYawOffset = yawOffset;
        }

        private void Update()
        {
            if (InputAllowed != null && !InputAllowed())
            {
                _planarVelocity = Vector3.zero;
                if (animator != null) animator.SetFloat(PlayerCombat.AnimatorParams.Speed, 0);
                return;
            }
            bool dead = _health != null && _health.IsDead;
            Keyboard kb = dead ? null : Keyboard.current;

            Vector2 input = ReadMoveInput(kb);
            UpdateStanceToggles(kb);

            Vector3 move = ToCameraSpace(input);

            ApplyRotation(move);

            float targetSpeed = IsSneaking ? sneakSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            Vector3 targetVelocity = move * targetSpeed * SurfaceSpeedMultiplier;
            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity, targetVelocity, acceleration * Time.deltaTime);

            bool grounded = _controller.isGrounded;
            if (grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f; // Stick to slopes instead of ratcheting downhill.
            }

            if (grounded && kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                // Jumping stands you up instead of being swallowed by the crouch.
                _sneakToggled = false;
                IsSneaking = false;
                _verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
                if (animator != null)
                {
                    animator.SetTrigger(PlayerCombat.AnimatorParams.Jump);
                }
            }

            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = _planarVelocity;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            if (animator != null)
            {
                animator.SetFloat(PlayerCombat.AnimatorParams.Speed, _planarVelocity.magnitude);
                animator.SetBool(PlayerCombat.AnimatorParams.Grounded, grounded);
                animator.SetBool(PlayerCombat.AnimatorParams.Sneaking, IsSneaking);
                // Drives the sneak clip's playback speed: standing still freezes the crouch
                // instead of shuffling on the spot. There is no crouch-idle clip to blend to.
                animator.SetFloat(PlayerCombat.AnimatorParams.SneakCycle,
                    sneakSpeed > 0.01f ? Mathf.Clamp01(_planarVelocity.magnitude / sneakSpeed) : 0f);
            }
        }

        /// <summary>
        /// Sprint and sneak are toggles, not hold-to-use: tap once to turn on, tap again to turn
        /// off. They are mutually exclusive, so turning one on clears the other. Dying clears both.
        /// </summary>
        private void UpdateStanceToggles(Keyboard kb)
        {
            if (kb == null)
            {
                _sprintToggled = false;
                _sneakToggled = false;
            }
            else
            {
                if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
                {
                    _sprintToggled = !_sprintToggled;
                    if (_sprintToggled) _sneakToggled = false;
                }

                if (kb.leftCtrlKey.wasPressedThisFrame || kb.rightCtrlKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame)
                {
                    _sneakToggled = !_sneakToggled;
                    if (_sneakToggled) _sprintToggled = false;
                }
            }

            if (SprintAllowed != null && !SprintAllowed()) _sprintToggled = false;
            IsSneaking = _sneakToggled;
            IsSprinting = _sprintToggled && !_sneakToggled;
        }

        private void ApplyRotation(Vector3 move)
        {
            Vector3 facing;
            if (_faceCameraYaw && cameraTransform != null)
            {
                facing = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
                if (Mathf.Abs(_faceYawOffset) > 0.01f)
                {
                    facing = Quaternion.Euler(0f, _faceYawOffset, 0f) * facing;
                }
            }
            else
            {
                facing = move;
            }

            if (facing.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(facing.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, rotationSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Builds a camera-relative basis flattened onto the XZ plane, so "up" on screen
        /// moves the player away from the camera.
        /// </summary>
        private Vector3 ToCameraSpace(Vector2 input)
        {
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.001f)
                {
                    // Camera looking near-straight-down: fall back to world axes.
                    forward = Vector3.forward;
                    right = Vector3.right;
                }
            }

            Vector3 move = right * input.x + forward * input.y;
            return move.sqrMagnitude > 1f ? move.normalized : move;
        }

        private static Vector2 ReadMoveInput(Keyboard kb)
        {
            if (kb == null)
            {
                return Vector2.zero; // No keyboard device, or the player is dead.
            }

            float x = 0f;
            float y = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }
    }
}
