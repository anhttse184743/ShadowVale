using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Gameplay.Player
{
    /// <summary>
    /// Minecraft-style third-person camera: orbits behind the target on a spring arm,
    /// yaw/pitch driven by the mouse, wheel to zoom. Pulls in when the arm would clip
    /// through geometry so the player never ends up inside a wall.
    /// Aiming pulls the arm in over the shoulder and narrows the FOV — iron-sight zoom,
    /// no scope overlay. The yaw here is the movement basis PlayerController reads, so
    /// keep them consistent.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Arm")]
        [Tooltip("Pivot height above the target's feet — roughly head height.")]
        [SerializeField] private float pivotHeight = 1.6f;

        [Tooltip("Resting distance behind the pivot.")]
        [SerializeField] private float distance = 2.0f;

        [SerializeField] private float minDistance = 1.2f;
        [SerializeField] private float maxDistance = 12f;

        [Tooltip("Sideways offset of the pivot, so the character does not sit dead centre.")]
        [SerializeField] private float shoulderOffset = 0.4f;

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float zoomSpeed = 0.01f;

        [Header("Aim (iron sights — no scope)")]
        [Tooltip("Field of view when hip firing.")]
        [SerializeField] private float hipFov = 65f;

        [Tooltip("Narrowed field of view while aiming. Lower = more zoom.")]
        [SerializeField] private float aimFov = 38f;

        [Tooltip("Arm length while aiming — close over the shoulder.")]
        [SerializeField] private float aimDistance = 1.8f;

        [Tooltip("Sideways offset while aiming, so the weapon clears the character.")]
        [SerializeField] private float aimShoulderOffset = 0.65f;

        [Tooltip("How fast the camera eases between hip and aim.")]
        [SerializeField] private float aimBlendSpeed = 12f;

        [Tooltip("Mouse sensitivity multiplier while aiming — steadier at high zoom.")]
        [SerializeField] private float aimSensitivityScale = 0.55f;

        [Header("Collision")]
        [Tooltip("Layers the arm collides with. Player layer should be excluded.")]
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Tooltip("Keeps the camera off the surface it hits.")]
        [SerializeField] private float collisionPadding = 0.3f;

        [Header("Recoil")]
        [Tooltip("Degrees per second the view settles back down after a burst. Fast enough to " +
                 "recover between bursts, slow enough that the climb has to be fought during one.")]
        [SerializeField] private float recoilRecoverySpeed = 9f;

        private Camera _camera;
        private float _yaw;
        private float _pitch = 15f;

        // Recoil applied to the view and not yet paid back, stored as the correction still to
        // be made: recovery adds these straight onto pitch and yaw. Same convention on both
        // axes, so one absorb rule covers them.
        private float _recoilPitchOwed;
        private float _recoilYawOwed;
        private float _currentDistance;
        private float _aimBlend; // 0 = hip, 1 = aiming
        private bool _aiming;

        public bool IsAiming => _aiming;

        /// <summary>Degrees of recoil still sitting in the view, for tests and for the HUD.</summary>
        public Vector2 RecoilOwed => new(_recoilPitchOwed, _recoilYawOwed);

        /// <summary>
        /// Kicks the view. <paramref name="up"/> raises the aim, <paramref name="right"/> drags
        /// it sideways, both in degrees.
        /// <para>
        /// The kick goes into the same pitch and yaw the player steers with, not a decorative
        /// layer on top, so the barrel really does move and a shot fired mid-climb really does
        /// miss. A cosmetic shake that leaves the aim untouched teaches the player to ignore it.
        /// </para>
        /// </summary>
        public void AddRecoil(float up, float right)
        {
            _pitch = Mathf.Clamp(_pitch - up, minPitch, maxPitch);
            _yaw += right;
            _recoilPitchOwed += up;    // adding this back brings the view down again
            _recoilYawOwed -= right;   // and this brings it back left
        }

        /// <summary>Drops any outstanding recoil without moving the view. For respawns.</summary>
        public void ClearRecoil()
        {
            _recoilPitchOwed = 0f;
            _recoilYawOwed = 0f;
        }
        public System.Func<bool> InputAllowed { get; set; }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Start()
        {
            _currentDistance = distance;
            _camera.fieldOfView = hipFov;
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
            }
            SetCursorLocked(true);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (InputAllowed != null && !InputAllowed()) SetCursorLocked(false);
            else ReadLookInput();

            // After the player's input, so a pull that cancels the climb is credited before any
            // of it is handed back.
            RecoverRecoil(Time.deltaTime);

            _aimBlend = Mathf.MoveTowards(_aimBlend, _aiming ? 1f : 0f, aimBlendSpeed * Time.deltaTime);
            _camera.fieldOfView = Mathf.Lerp(hipFov, aimFov, _aimBlend);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = target.position
                            + Vector3.up * pivotHeight
                            + rotation * Vector3.right * Mathf.Lerp(shoulderOffset, aimShoulderOffset, _aimBlend);

            // Spring arm: shorten instantly on a hit, ease back out when clear.
            float desired = Mathf.Lerp(distance, aimDistance, _aimBlend);
            if (Physics.SphereCast(pivot, 0.2f, -(rotation * Vector3.forward), out RaycastHit hit,
                    desired, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                desired = Mathf.Max(minDistance, hit.distance - collisionPadding);
            }

            _currentDistance = desired < _currentDistance
                ? desired
                : Mathf.Lerp(_currentDistance, desired, 1f - Mathf.Exp(-8f * Time.deltaTime));

            transform.SetPositionAndRotation(
                pivot - rotation * Vector3.forward * _currentDistance, rotation);
        }

        private void ReadLookInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            // Escape releases the cursor so you can leave Play mode; click re-captures it.
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }
            else if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                SetCursorLocked(true);
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float sensitivity = mouseSensitivity * Mathf.Lerp(1f, aimSensitivityScale, _aimBlend);
                Vector2 delta = mouse.delta.ReadValue();

                float yawDelta = delta.x * sensitivity;
                float pitchDelta = -delta.y * sensitivity; // positive looks down

                // Pulling against the climb pays off the debt before anything else. Without
                // this step the recovery below keeps running underneath a player who is already
                // compensating correctly, and drags their aim below the target the moment they
                // stop firing — the usual way this mechanic is got wrong.
                Absorb(pitchDelta, ref _recoilPitchOwed);
                Absorb(yawDelta, ref _recoilYawOwed);

                _yaw += yawDelta;
                _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);
            }

            // Wheel zoom only applies to the hip-fire arm; aiming has its own fixed distance.
            float scroll = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0f) && !_aiming)
            {
                distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
            }
        }

        /// <summary>
        /// Cancels as much outstanding recoil as this input has already made up for. The input
        /// itself is untouched — the player still sees their own movement — only the debt
        /// shrinks, so the recovery below has that much less to hand back.
        /// </summary>
        private static void Absorb(float input, ref float owed)
        {
            if (owed > 0f && input > 0f) owed = Mathf.Max(0f, owed - input);
            else if (owed < 0f && input < 0f) owed = Mathf.Min(0f, owed - input);
        }

        /// <summary>
        /// Eases the view back down to where it was pointing, but only by as much recoil as the
        /// player has not already cancelled themselves.
        /// </summary>
        private void RecoverRecoil(float deltaTime)
        {
            float step = recoilRecoverySpeed * deltaTime;

            if (!Mathf.Approximately(_recoilPitchOwed, 0f))
            {
                float give = Mathf.Clamp(_recoilPitchOwed, -step, step);
                _pitch = Mathf.Clamp(_pitch + give, minPitch, maxPitch);
                _recoilPitchOwed -= give;
            }

            if (!Mathf.Approximately(_recoilYawOwed, 0f))
            {
                float give = Mathf.Clamp(_recoilYawOwed, -step, step);
                _yaw += give;
                _recoilYawOwed -= give;
            }
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetAiming(bool value)
        {
            _aiming = value;
        }
    }
}
