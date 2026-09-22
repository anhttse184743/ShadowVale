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

        private Camera _camera;
        private float _yaw;
        private float _pitch = 15f;
        private float _currentDistance;
        private float _aimBlend; // 0 = hip, 1 = aiming
        private bool _aiming;

        public bool IsAiming => _aiming;
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
                _yaw += delta.x * sensitivity;
                _pitch = Mathf.Clamp(_pitch - delta.y * sensitivity, minPitch, maxPitch);
            }

            // Wheel zoom only applies to the hip-fire arm; aiming has its own fixed distance.
            float scroll = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0f) && !_aiming)
            {
                distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
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
