using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ShadowVale.Gameplay.Player
{
    /// <summary>
    /// Minecraft-style third-person camera: orbits behind the target on a spring arm,
    /// yaw/pitch driven by the mouse, wheel to zoom. Pulls in when the arm would clip
    /// through geometry so the player never ends up inside a wall.
    /// Aiming pulls the arm in over the shoulder and narrows the FOV — iron-sight zoom,
    /// no scope overlay. Binoculars instead go first person from the eyes (see SetBinoculars).
    /// The yaw here is the movement basis PlayerController reads, so
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

        [Header("Binoculars (first person)")]
        [Tooltip("Field of view through the binoculars — far narrower than aiming.")]
        [SerializeField] private float binocularFov = 16f;

        [Tooltip("How quickly the view zooms in once the binoculars are up.")]
        [SerializeField] private float binocularZoomSpeed = 14f;

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
        private readonly RaycastHit[] _obstructionHits = new RaycastHit[64];
        private float _aimBlend; // 0 = hip, 1 = aiming
        private bool _aiming;
        private bool _binoculars;
        private bool _scope, _firstPersonHidden;
        private float _scopeEyeHeight, _scopeFov, _scopePreviousNearClip;
        private float _activeBinocularFov;
        private float _eyeHeight;
        private Renderer[] _hidden = System.Array.Empty<Renderer>();
        private ShadowCastingMode[] _hiddenModes = System.Array.Empty<ShadowCastingMode>();

        public bool IsAiming => _aiming;
        public bool IsBinoculars => _binoculars;
        public bool IsWeaponScope => _scope;
        public float WeaponScopeFieldOfView => _scopeFov;
        public void SetWeaponScope(bool value,float magnification,float eyeHeight)
        {
            _scopeEyeHeight=eyeHeight;
            _scopeFov=2*Mathf.Atan(Mathf.Tan(hipFov*Mathf.Deg2Rad*.5f)/Mathf.Max(1,magnification))*Mathf.Rad2Deg;
            if(value!=_scope&&_camera!=null){if(value){_scopePreviousNearClip=_camera.nearClipPlane;_camera.nearClipPlane=Mathf.Min(.05f,_scopePreviousNearClip);}else _camera.nearClipPlane=_scopePreviousNearClip;}
            _scope=value;RefreshFirstPersonVisuals();
        }
        public void RestoreLook(Quaternion rotation)
        {
            _yaw = rotation.eulerAngles.y;
            _pitch = Mathf.Clamp(Mathf.DeltaAngle(0, rotation.eulerAngles.x), minPitch, maxPitch);
            ClearRecoil(); transform.rotation = rotation;
        }

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

        public System.Func<float?> SurfaceCameraFloor { get; set; }
        private float cinematicWeight, cinematicFov;
        private Vector3 cinematicPosition;
        private Quaternion cinematicRotation;
        public void SetCinematicView(Vector3 position, Quaternion rotation, float fov, float weight)
        {
            cinematicPosition = position; cinematicRotation = rotation;
            cinematicFov = fov; cinematicWeight = Mathf.Clamp01(weight);
        }
        public void ClearCinematicView() => cinematicWeight = 0;
        private float _shakeAmplitude, _shakeUntil, _shakeLength;
        /// <summary>
        /// A decaying shake on top of the final view, for blasts and hits in scripted scenes.
        /// Nothing shakes unless something asks; the strongest pending shake wins.
        /// </summary>
        public void Shake(float amplitude, float seconds)
        {
            if (amplitude <= 0f || seconds <= 0f) return;
            if (amplitude < ShakeAmplitude) return;
            _shakeAmplitude = amplitude; _shakeLength = seconds; _shakeUntil = Time.time + seconds;
        }
        /// <summary>Current shake in metres, fading to zero over its length.</summary>
        public float ShakeAmplitude => Time.time < _shakeUntil ? _shakeAmplitude * ((_shakeUntil - Time.time) / _shakeLength) : 0f;
        private void ApplyShake()
        {
            float amount = ShakeAmplitude;
            if (amount <= 0f) return;
            float t = Time.time * 25f;
            transform.position += new Vector3(Mathf.PerlinNoise(t, 0.3f) - .5f, Mathf.PerlinNoise(0.7f, t) - .5f, Mathf.PerlinNoise(t, t) - .5f) * (2f * amount);
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
            // of it is handed back. Before the binoculars too: a burst fired just before raising
            // them still settles.
            RecoverRecoil(Time.deltaTime);

            if (_binoculars || _scope)
            {
                // Straight from the eyes: no arm, no shoulder, so nothing of the character in view.
                Quaternion look = Quaternion.Euler(_pitch, _yaw, 0f);
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _binoculars?_activeBinocularFov:_scopeFov, 1f - Mathf.Exp(-binocularZoomSpeed * Time.deltaTime));
                transform.SetPositionAndRotation(target.position + Vector3.up * (_binoculars?_eyeHeight:_scopeEyeHeight) + look * Vector3.forward * 0.15f, look);
                return;
            }

            _aimBlend = Mathf.MoveTowards(_aimBlend, _aiming ? 1f : 0f, aimBlendSpeed * Time.deltaTime);
            _camera.fieldOfView = Mathf.Lerp(hipFov, aimFov, _aimBlend);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 head = target.position + Vector3.up * pivotHeight;
            Vector3 shoulder = rotation * Vector3.right * Mathf.Lerp(shoulderOffset, aimShoulderOffset, _aimBlend);
            float radius = CameraCollisionRadius();
            Vector3 pivot = head + shoulder.normalized * ClearDistance(head, shoulder.normalized, shoulder.magnitude, radius);

            // Spring arm: shorten instantly on a hit, ease back out when clear.
            float desired = Mathf.Lerp(distance, aimDistance, _aimBlend);
            // minDistance limits player-controlled zoom only. A nearby wall must be able
            // to retract the arm all the way to the pivot, including when aiming.
            desired = ClearDistance(pivot, -(rotation * Vector3.forward), desired, radius);

            _currentDistance = desired < _currentDistance
                ? desired
                : Mathf.Lerp(_currentDistance, desired, 1f - Mathf.Exp(-8f * Time.deltaTime));

            Vector3 cameraPosition = pivot - rotation * Vector3.forward * _currentDistance;
            float? surfaceFloor = SurfaceCameraFloor?.Invoke();
            if (surfaceFloor.HasValue && cameraPosition.y < surfaceFloor.Value) {
                cameraPosition.y = surfaceFloor.Value;
                var raised = cameraPosition - pivot;
                cameraPosition = pivot + raised.normalized * ClearDistance(pivot, raised.normalized, raised.magnitude, radius);
                rotation = Quaternion.LookRotation(pivot - cameraPosition);
            }
            transform.SetPositionAndRotation(cameraPosition, rotation);
            if (cinematicWeight > 0) {
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, cinematicPosition, cinematicWeight),
                    Quaternion.Slerp(transform.rotation, cinematicRotation, cinematicWeight));
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, cinematicFov, cinematicWeight);
            }
            ApplyShake();
        }

        private float CameraCollisionRadius()
        {
            // Enclose the near plane as well as the camera centre, so screen corners
            // cannot look through a wall at wide FOV/aspect ratios.
            float halfHeight = _camera.nearClipPlane * Mathf.Tan(_camera.fieldOfView * .5f * Mathf.Deg2Rad);
            return Mathf.Max(.2f, Mathf.Sqrt(halfHeight * halfHeight * (1f + _camera.aspect * _camera.aspect)
                                           + _camera.nearClipPlane * _camera.nearClipPlane));
        }

        private float ClearDistance(Vector3 origin, Vector3 direction, float length, float radius)
        {
            if (length <= 0f) return 0f;
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, _obstructionHits,
                length, obstructionMask, QueryTriggerInteraction.Ignore);
            // Dense foliage/compound buildings must not hide a wall behind a full buffer.
            var hits = count == _obstructionHits.Length
                ? Physics.SphereCastAll(origin, radius, direction, length, obstructionMask, QueryTriggerInteraction.Ignore)
                : _obstructionHits;
            if (hits != _obstructionHits) count = hits.Length;
            float clear = length;
            for (int i = 0; i < count; i++)
            {
                var obstacle = hits[i].collider;
                if (obstacle == null || obstacle.transform.IsChildOf(target) || obstacle.transform == transform
                    || target.IsChildOf(obstacle.transform)) continue;
                clear = Mathf.Min(clear, Mathf.Max(0f, hits[i].distance - collisionPadding));
            }
            return clear;
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
                float sensitivity = mouseSensitivity * (_binoculars ? _activeBinocularFov / hipFov : _scope ? _scopeFov/hipFov : Mathf.Lerp(1f, aimSensitivityScale, _aimBlend));
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
            if (!Mathf.Approximately(scroll, 0f) && !_aiming && !_binoculars && !_scope)
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

        /// <summary>
        /// Raise or lower binoculars: a first-person view from <paramref name="eyeHeight"/> above
        /// the target's feet with a narrow field of view. The target's own model would fill the
        /// screen from there, so it only casts its shadow until they come down. Safe to call
        /// every frame (the eye height follows crouching).
        /// </summary>
        public void SetBinoculars(bool value, float eyeHeight, float? fieldOfView = null)
        {
            _eyeHeight = eyeHeight;
            if (value) _activeBinocularFov = Mathf.Clamp(fieldOfView ?? binocularFov, 5, 90);
            _binoculars = value;
            RefreshFirstPersonVisuals();
        }
        private void RefreshFirstPersonVisuals()
        {
            bool value=_binoculars||_scope;
            if(value==_firstPersonHidden||target==null)return;
            _firstPersonHidden=value;
            if (value)
            {
                _hidden = target.GetComponentsInChildren<Renderer>();
                _hiddenModes = new ShadowCastingMode[_hidden.Length];
                for (int i = 0; i < _hidden.Length; i++)
                {
                    _hiddenModes[i] = _hidden[i].shadowCastingMode;
                    _hidden[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
                return;
            }
            for (int i = 0; i < _hidden.Length; i++)
                if (_hidden[i] != null) _hidden[i].shadowCastingMode = _hiddenModes[i];
            _hidden = System.Array.Empty<Renderer>();
            _camera.fieldOfView = Mathf.Lerp(hipFov, aimFov, _aimBlend);
        }
    }
}
