using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// Greybox combat for the test map: carries every weapon at once and swaps which one is
    /// visible, fires on left mouse, aims down sights on right mouse (guns only).
    /// Guns trace from the camera centre so the shot lands where the crosshair is; melee sweeps
    /// a sphere forward from the character.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        /// <summary>
        /// Every animator parameter the player rig uses — locomotion and combat both.
        /// Single source of truth, mirrored by PlayerAnimatorBuilder.
        /// </summary>
        public static class AnimatorParams
        {
            public const string Speed = "Speed";
            public const string Grounded = "Grounded";
            public const string Jump = "Jump";
            public const string Sneaking = "Sneaking";
            public const string Weapon = "Weapon";
            public const string Attack = "Attack";
            public const string Aiming = "Aiming";
            public const string Die = "Die";
            public const string SneakCycle = "SneakCycle";
        }

        /// <summary>Animator layer holding the weapon stance. Muted when empty-handed.</summary>
        public const string UpperBodyLayer = "UpperBody";

        /// <summary>
        /// Base-layer state the character stands in while alive. Reviving jumps straight back to
        /// it, and the animator builder creates it under this name — one constant, so renaming the
        /// state cannot silently break coming back from the dead.
        /// </summary>
        public const string LocomotionState = "Locomotion";

        /// <summary>
        /// Hotbar order, left to right: 1 rifle, 2 knife, 3 fists. The HUD reads this, so the
        /// slots on screen and the number keys can never drift apart.
        /// </summary>
        public static readonly WeaponKind[] SlotOrder =
        {
            WeaponKind.Rifle,
            WeaponKind.Knife,
            WeaponKind.Unarmed,
        };

        private static readonly Key[] SlotKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };
        public System.Func<bool> InputAllowed { get; set; }
        public System.Func<bool> TryConsumeRound { get; set; }
        public bool UsesInventoryHotkeys { get; set; }
        public float AttackCooldownRemaining => Mathf.Max(0, _nextAttackTime - Time.time);
        public void RestoreAttackCooldown(float remaining) => _nextAttackTime = Time.time + Mathf.Max(0, remaining);

        [Header("Loadout")]
        [Tooltip("Weapon prefabs, spawned once and parented to the hand anchor.")]
        [SerializeField] private Weapon[] weaponPrefabs = System.Array.Empty<Weapon>();

        [Tooltip("Usually the right hand bone. Weapons are parented here at their own local zero.")]
        [SerializeField] private Transform handAnchor;

        [SerializeField] private WeaponKind startingWeapon = WeaponKind.Rifle;

        [Tooltip("Hand-tuned grip offsets. Without one, weapons sit at the anchor's origin.")]
        [SerializeField] private WeaponGripConfig gripConfig;

        [Tooltip("Seconds to fade the weapon-stance layer in and out.")]
        [SerializeField] private float stanceBlendTime = 0.15f;

        [Tooltip("Seconds to swing the weapon between its carry pose and its levelled aim pose.")]
        [SerializeField] private float gripBlendTime = 0.12f;

        [Tooltip("Extra seconds the stance layer stays up after an unarmed attack, so the punch " +
                 "animation is not cut off by the layer fading.")]
        [SerializeField] private float attackLayerTail = 0.3f;

        [Tooltip("Extra seconds the body stays turned toward the camera after the last shot, so " +
                 "firing single shots does not leave the character swivelling back and forth.")]
        [SerializeField] private float faceCameraTail = 0.6f;

        /// <summary>Raised on equip so the HUD can follow without polling.</summary>
        public event System.Action<WeaponKind> WeaponChanged;

        [Header("Unarmed")]
        [SerializeField] private float punchDamage = 12f;
        [SerializeField] private float punchRange = 1.5f;
        [SerializeField] private float punchCooldown = 0.45f;
        [SerializeField] private float punchSweepRadius = 0.3f;

        [Header("Targeting")]
        [Tooltip("What shots and swings can hit. Exclude the player's own layer.")]
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Tracers")]
        [Tooltip("Bullet streak spawned per shot. Optional — shooting works without it.")]
        [SerializeField] private ShotTracer tracerPrefab;

        [Tooltip("How many streaks can overlap. A full pool reuses the oldest.")]
        [SerializeField] private int tracerPoolSize = 12;

        [Header("Wiring")]
        [SerializeField] private Animator animator;
        [SerializeField] private ThirdPersonCamera cameraRig;
        [SerializeField] private Health health;

        private readonly System.Collections.Generic.Dictionary<WeaponKind, Weapon> _weapons = new();
        private PlayerController _controller;
        private Weapon _equipped;
        private WeaponKind _equippedKind;
        private float _nextAttackTime;
        private bool _aiming;
        private ShotTracer[] _tracers;
        private int _tracerCursor;
        private int _upperBodyLayer = -1;
        private float _aimPoseBlend;
        private float _upperBodyWeight;
        private float _upperBodyHoldUntil;
        private float _faceCameraHoldUntil;

        public WeaponKind EquippedKind => _equippedKind;
        public WeaponKind StartingWeapon => startingWeapon;
        public bool IsAiming => _aiming;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (health == null) health = GetComponent<Health>();
            if (cameraRig == null && Camera.main != null)
            {
                cameraRig = Camera.main.GetComponent<ThirdPersonCamera>();
            }

            if (animator != null)
            {
                _upperBodyLayer = animator.GetLayerIndex(UpperBodyLayer);
            }

            SpawnWeapons();
            SpawnTracerPool();
            Equip(startingWeapon);

            // Snap the stance layer to where it belongs instead of fading in from zero: the
            // controller starts it at full weight, so a cold start would pop.
            if (animator != null && _upperBodyLayer > 0)
            {
                _upperBodyWeight = _equippedKind != WeaponKind.Unarmed ? 1f : 0f;
                animator.SetLayerWeight(_upperBodyLayer, _upperBodyWeight);
            }
        }

        private void SpawnWeapons()
        {
            Transform parent = handAnchor != null ? handAnchor : transform;

            foreach (Weapon prefab in weaponPrefabs)
            {
                if (prefab == null || _weapons.ContainsKey(prefab.Kind))
                {
                    continue;
                }

                Weapon instance = Instantiate(prefab, parent);
                // The prefab's own transform is meaningless here — the grip config is what says
                // where this weapon sits in the fist.
                if (gripConfig != null)
                {
                    gripConfig.Apply(instance.transform, prefab.Kind);
                }
                else
                {
                    instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    instance.transform.localScale = Vector3.one;
                }
                instance.gameObject.SetActive(false);
                _weapons[prefab.Kind] = instance;
            }
        }

        /// <summary>
        /// Tracers are pre-made and reused: shots come out fast enough that allocating one per
        /// bullet would churn. They live under their own root so they are not dragged along by
        /// the player's movement.
        /// </summary>
        private void SpawnTracerPool()
        {
            if (tracerPrefab == null)
            {
                return;
            }

            var root = new GameObject("ShotTracers");
            _tracers = new ShotTracer[Mathf.Max(1, tracerPoolSize)];
            for (int i = 0; i < _tracers.Length; i++)
            {
                _tracers[i] = Instantiate(tracerPrefab, root.transform);
            }
        }

        private void ShowTracer(Vector3 from, Vector3 to)
        {
            if (_tracers == null)
            {
                return;
            }

            // Prefer a free one; if every streak is still flashing, recycle at the cursor.
            for (int i = 0; i < _tracers.Length; i++)
            {
                int index = (_tracerCursor + i) % _tracers.Length;
                if (!_tracers[index].IsFree)
                {
                    continue;
                }
                _tracerCursor = (index + 1) % _tracers.Length;
                _tracers[index].Play(from, to);
                return;
            }

            _tracers[_tracerCursor].Play(from, to);
            _tracerCursor = (_tracerCursor + 1) % _tracers.Length;
        }

        private void Update()
        {
            if ((health != null && health.IsDead) || (InputAllowed != null && !InputAllowed()))
            {
                SetAiming(false);
                _faceCameraHoldUntil = 0f;
                if (_controller != null)
                {
                    _controller.SetFaceCameraYaw(false);
                }
                return;
            }

            ReadEquipInput();
            ReadAimInput();
            ReadAttackInput();
            UpdateFacing();
            UpdateUpperBodyWeight();
            UpdateGripPose();
        }

        /// <summary>
        /// Swings the weapon between the carry pose it rides in while moving and the levelled pose
        /// it takes to shoot. The arms come from the animation; only the weapon's seat in the hand
        /// changes, which is what separates "slung over the shoulder" from "aimed".
        /// </summary>
        private void UpdateGripPose()
        {
            if (_equipped == null || gripConfig == null
                || !gripConfig.TryGetOffset(_equippedKind, out WeaponGripConfig.GripOffset offset)
                || !offset.hasAimPose)
            {
                return;
            }

            bool levelled = _aiming || Time.time < _upperBodyHoldUntil;
            _aimPoseBlend = Mathf.MoveTowards(
                _aimPoseBlend, levelled ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, gripBlendTime));

            WeaponGripConfig.GetPose(offset, false, out Vector3 carryPos, out Quaternion carryRot);
            WeaponGripConfig.GetPose(offset, true, out Vector3 aimPos, out Quaternion aimRot);
            _equipped.transform.SetLocalPositionAndRotation(
                Vector3.Lerp(carryPos, aimPos, _aimPoseBlend),
                Quaternion.Slerp(carryRot, aimRot, _aimPoseBlend));
        }

        /// <summary>
        /// Who the body faces. Walking, it follows the legs. Aiming or shooting, it locks to the
        /// camera's yaw so the barrel points where the player is looking — that turn is what puts
        /// the character into the bladed stance the Gunplay take was authored around.
        /// Pushed every frame rather than from the aim toggle alone, because firing turns the body
        /// too and that turn has to time out on its own.
        /// </summary>
        private void UpdateFacing()
        {
            if (_controller == null)
            {
                return;
            }

            bool locked = _aiming || Time.time < _faceCameraHoldUntil;
            bool holdingGun = _equipped != null && _equipped.IsGun;
            _controller.SetFaceCameraYaw(locked, locked && holdingGun ? -PoseAimYaw() : 0f);
        }

        /// <summary>
        /// How far round from the character's own forward the pose is pointing the rifle. Read
        /// straight off the barrel, because that is the thing that has to end up on the crosshair.
        /// The Mixamo shooting take aims about 87 degrees to the character's left, which is why
        /// firing has to stand the body side-on to the target.
        /// <para>
        /// Measured live rather than stored as a constant: re-seat the weapon in the hand or swap
        /// the shooting clip for one that aims straight ahead, and this follows on its own instead
        /// of leaving a stale number turning the body for no reason.
        /// </para>
        /// </summary>
        private float PoseAimYaw()
        {
            Transform muzzle = _equipped != null ? _equipped.Muzzle : null;
            if (muzzle == null)
            {
                return 0f;
            }

            Vector3 barrel = Vector3.ProjectOnPlane(muzzle.forward, Vector3.up);
            // Barrel straight up or down carries no yaw; leave the body facing the camera.
            return barrel.sqrMagnitude < 0.0004f
                ? 0f
                : Vector3.SignedAngle(transform.forward, barrel.normalized, Vector3.up);
        }

        /// <summary>
        /// The stance layer is faded out when empty-handed. An override layer whose state has no
        /// motion writes nothing, so it would otherwise hold the last armed pose forever — which
        /// is exactly what made putting the knife away leave the character still gripping it.
        /// The layer is forced back up while an attack plays, so the punch still reaches the arms.
        /// </summary>
        private void UpdateUpperBodyWeight()
        {
            if (animator == null || _upperBodyLayer <= 0)
            {
                return;
            }

            bool attacking = Time.time < _upperBodyHoldUntil;
            float target = _equippedKind != WeaponKind.Unarmed || attacking ? 1f : 0f;
            _upperBodyWeight = Mathf.MoveTowards(
                _upperBodyWeight, target, Time.deltaTime / Mathf.Max(0.01f, stanceBlendTime));
            animator.SetLayerWeight(_upperBodyLayer, _upperBodyWeight);
        }

        // ---- Input ----------------------------------------------------------

        private void ReadEquipInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            // Guard the shorter of the two: a weapon added to SlotOrder without a key would
            // otherwise index past the end.
            int slots = Mathf.Min(SlotOrder.Length, SlotKeys.Length);
            for (int slot = 0; slot < slots; slot++)
            {
                var key = UsesInventoryHotkeys ? (Key)((int)Key.Digit6 + slot) : SlotKeys[slot];
                if (kb[key].wasPressedThisFrame)
                {
                    Equip(SlotOrder[slot]);
                    return;
                }
            }
        }

        private void ReadAimInput()
        {
            Mouse mouse = Mouse.current;
            // Only guns aim; swapping to a blade while aiming drops the sights.
            bool wants = mouse != null
                         && mouse.rightButton.isPressed
                         && _equipped != null
                         && _equipped.IsGun;
            SetAiming(wants);
        }

        private void ReadAttackInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            bool automatic = _equipped != null && _equipped.Automatic;
            bool pressed = automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
            if (!pressed || Time.time < _nextAttackTime)
            {
                return;
            }

            Attack();
        }

        // ---- Actions --------------------------------------------------------

        public void Equip(WeaponKind kind)
        {
            if (_equipped != null)
            {
                _equipped.gameObject.SetActive(false);
            }

            _weapons.TryGetValue(kind, out Weapon next);
            _equipped = next; // null for Unarmed, which has no prefab.
            _equippedKind = next != null ? next.Kind : WeaponKind.Unarmed;

            if (_equipped != null)
            {
                _equipped.gameObject.SetActive(true);
            }
            else if (kind != WeaponKind.Unarmed)
            {
                Debug.LogWarning($"[Combat] No prefab for {kind} — falling back to unarmed.", this);
            }

            if (animator != null)
            {
                animator.SetInteger(AnimatorParams.Weapon, (int)_equippedKind);
            }

            // Sights belong to the gun that was holstered.
            if (_equipped == null || !_equipped.IsGun)
            {
                SetAiming(false);
            }

            WeaponChanged?.Invoke(_equippedKind);
        }

        private void SetAiming(bool value)
        {
            if (_aiming == value)
            {
                return;
            }

            _aiming = value;
            if (animator != null)
            {
                animator.SetBool(AnimatorParams.Aiming, value);
            }
            if (cameraRig != null)
            {
                cameraRig.SetAiming(value);
            }
        }

        private void Attack()
        {
            if (_equipped != null && _equipped.IsGun && TryConsumeRound != null && !TryConsumeRound()) return;
            float cooldown = _equipped != null ? _equipped.Cooldown : punchCooldown;
            _nextAttackTime = Time.time + cooldown;

            _upperBodyHoldUntil = Time.time + cooldown + attackLayerTail;
            if (animator != null)
            {
                animator.SetTrigger(AnimatorParams.Attack);
            }

            if (_equipped != null && _equipped.IsGun)
            {
                _faceCameraHoldUntil = Time.time + cooldown + faceCameraTail;
                FireGun(_equipped);
            }
            else
            {
                SwingMelee();
            }
        }

        /// <summary>Hitscan from the camera centre, so the shot goes where the crosshair points.</summary>
        private void FireGun(Weapon gun)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 origin = cam.transform.position;
            Vector3 direction = cam.transform.forward;

            // Aiming halves the cone; hip fire keeps the full spread.
            float spread = _aiming ? gun.HipSpread * 0.5f : gun.HipSpread;
            if (spread > 0f)
            {
                direction = Quaternion.Euler(
                    Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * direction;
            }

            // The trace starts at the camera so the shot lands on the crosshair, but the streak
            // has to come out of the barrel or it looks like the player is firing from their eyes.
            Vector3 endPoint;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, gun.Range,
                    hitMask, QueryTriggerInteraction.Ignore))
            {
                ApplyDamage(hit.collider, gun.Damage, hit.point);
                endPoint = hit.point;
            }
            else
            {
                endPoint = origin + direction * gun.Range;
            }

            ShowTracer(gun.Muzzle.position, endPoint);
        }

        /// <summary>Sphere sweep forward from chest height — forgiving enough for a greybox test.</summary>
        private void SwingMelee()
        {
            float range = _equipped != null ? _equipped.Range : punchRange;
            float radius = _equipped != null ? _equipped.SweepRadius : punchSweepRadius;
            float damage = _equipped != null ? _equipped.Damage : punchDamage;

            Vector3 origin = transform.position + Vector3.up * 1.2f;
            if (Physics.SphereCast(origin, radius, transform.forward, out RaycastHit hit, range,
                    hitMask, QueryTriggerInteraction.Ignore))
            {
                ApplyDamage(hit.collider, damage, hit.point);
            }
        }

        private void ApplyDamage(Collider target, float amount, Vector3 point)
        {
            var damageable = target.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(amount, point, gameObject);
        }
    }
}
