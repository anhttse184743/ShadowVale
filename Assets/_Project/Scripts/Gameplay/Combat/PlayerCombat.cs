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

        /// <summary>
        /// Legacy per-shot ammo gate, kept for scenes that never configured a magazine. Once a
        /// magazine exists it owns the round count and this is not consulted: the reserve is
        /// drawn on reload through <see cref="DrawRounds"/> instead, so a shot costs one round
        /// from the weapon rather than one from the pack.
        /// </summary>
        public System.Func<bool> TryConsumeRound { get; set; }

        /// <summary>
        /// Asked for rounds when the magazine reloads; returns how many the reserve could give.
        /// Left unset, reloads draw on an unlimited supply, which is what a greybox scene wants.
        /// </summary>
        public System.Func<int, int> DrawRounds { get; set; }

        /// <summary>Rounds left in the reserve, for the HUD and for refusing a pointless reload.</summary>
        public System.Func<int> ReserveRounds { get; set; }
        /// <summary>The scene draws the weapon slots in its own HUD, so the stock hotbar row
        /// (<c>WeaponHotbar</c>) stays hidden. The number keys are 1/2/3 either way.</summary>
        public bool DrawsOwnWeaponHud { get; set; }
        public float AttackCooldownRemaining => Mathf.Max(0, _nextAttackTime - Time.time);

        /// <summary>Half-angle of the cone the next shot can land in, in degrees.</summary>
        public float CurrentSpreadDegrees => _spread.ConeHalfAngle(CurrentStance(), _aiming);

        /// <summary>0 to 1. How far sustained fire has opened the cone.</summary>
        public float SpreadBloom => _spread.Bloom;

        /// <summary>Shots into the current burst.</summary>
        public int BurstShotIndex => _spread.ShotIndex;

        /// <summary>Rounds in the weapon right now.</summary>
        public int RoundsInMagazine => _magazine?.Rounds ?? 0;

        public int MagazineCapacity => _magazine?.Capacity ?? 0;
        public bool IsReloading => _magazine is { IsReloading: true };

        /// <summary>0 to 1 across a reload, for a HUD bar. 0 when not reloading.</summary>
        public float ReloadProgress => _magazine?.ReloadProgress ?? 0f;

        /// <summary>Puts a saved round count back in the weapon and clears any reload.</summary>
        public void RestoreMagazine(int rounds) => _magazine?.Refill(rounds);

        /// <summary>Starts a reload if one is possible. Returns false when it is not.</summary>
        public bool TryReload()
        {
            if (_magazine == null || _equipped == null || !_equipped.IsGun) return false;
            bool wasEmpty = _magazine.IsEmpty;
            if (!_magazine.BeginReload(ReserveRounds != null ? ReserveRounds() : -1)) return false;
            AnimateReload(wasEmpty);
            return true;
        }

        /// <summary>
        /// Nam's own reload takes (Blender): swapping a part-used magazine, an empty one, or crouched.
        /// Played at whatever pace makes the take end as the magazine timer does.
        /// </summary>
        private void AnimateReload(bool wasEmpty)
        {
            if (animator == null || !_hasReloadParams) return;
            bool crouched = _controller != null && _controller.IsSneaking;
            float length = ClipLength(crouched ? "Nam_Crouch_Reload" : wasEmpty ? "Nam_Rifle_Reload" : "Nam_Rifle_ReloadTactical");
            animator.SetBool("ReloadEmpty", wasEmpty);
            animator.SetFloat("ReloadSpeed", length > 0 ? length / Mathf.Max(0.1f, _magazine.ReloadRemaining) : 1f);
            animator.SetTrigger("Reload");
        }

        private float ClipLength(string clipName)
        {
            if (animator.runtimeAnimatorController == null) return 0f;
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == clipName) return clip.length;
            return 0f;
        }
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

        /// <summary>
        /// Raised the moment an attack commits — after the round is spent, before the trace.
        /// Carries the weapon used and where the sound comes from, so audio and anything else
        /// that reacts to a shot can hang off one place instead of reaching into the internals.
        /// </summary>
        public event System.Action<WeaponKind, Vector3> Attacked;

        [Header("Magazine")]
        [SerializeField] private WeaponTuning tuning = WeaponTuning.Rifle;

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
        private CharacterController _characterController;
        private Weapon _equipped;
        private WeaponKind _equippedKind;
        private float _nextAttackTime;
        private Magazine _magazine;
        private readonly WeaponSpreadState _spread = new();
        private bool _aiming;
        private ShotTracer[] _tracers;
        private Transform _tracerRoot;
        private int _tracerCursor;
        private int _upperBodyLayer = -1;
        private float _aimPoseBlend;
        private float _upperBodyWeight;
        /// <summary>Base-layer state holding Nam's own AK carry, when the controller has one.</summary>
        public const string RifleLocomotionState = "Locomotion_Rifle";
        private bool _rifleBody, _hasReloadParams, _hasHitParam;
        private bool HasKnifeCarry => animator!=null&&animator.HasState(0,Animator.StringToHash("Locomotion_Knife"));
        private float _lastHealthSeen = -1f;
        private float _upperBodyHoldUntil;
        private float _faceCameraHoldUntil;

        public WeaponKind EquippedKind => _equippedKind;
        public WeaponKind StartingWeapon => startingWeapon;
        public bool IsAiming => _aiming;
        /// <summary>Optional mission-specific melee interaction, before ordinary damage.</summary>
        public System.Func<bool> TrySpecialMelee { get; set; }

        // Rebind clears Animator parameters even when the cached aiming flag is unchanged.
        // Restore gameplay explicitly rather than waiting for another input edge.
        /// <summary>Seat the equipped prop in its authored carry grip before a locomotion playable takes over.</summary>
        public void PrepareCinematicCarry()
        {
            _aiming=false;_aimPoseBlend=0;_upperBodyHoldUntil=_faceCameraHoldUntil=0;
            if(animator!=null){animator.SetBool(AnimatorParams.Aiming,false);if(_upperBodyLayer>0)animator.SetLayerWeight(_upperBodyLayer,0);}
            if(_equipped!=null&&gripConfig!=null)gripConfig.Apply(_equipped.transform,_equippedKind,false);
        }

        public void RestoreAfterCinematic()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.Rebind();
            _lastHealthSeen=health!=null?health.Current:-1f;
            _upperBodyHoldUntil=_faceCameraHoldUntil=0;
            _nextAttackTime=Time.time;
            _magazine?.CancelReload();_spread.Reset();
            if(_hasHitParam)animator.ResetTrigger("Hit");
            _rifleBody=animator.HasState(0,Animator.StringToHash(RifleLocomotionState));
            _upperBodyLayer = animator.GetLayerIndex(UpperBodyLayer);
            animator.SetInteger(AnimatorParams.Weapon, (int)_equippedKind);
            animator.SetFloat(AnimatorParams.Speed, 0);
            animator.SetBool(AnimatorParams.Grounded, true);
            animator.SetBool(AnimatorParams.Sneaking, _controller != null && _controller.IsSneaking);
            animator.ResetTrigger(AnimatorParams.Attack); animator.ResetTrigger(AnimatorParams.Die);
            _aiming = Mouse.current != null && Mouse.current.rightButton.isPressed && _equipped != null && _equipped.IsGun;
            animator.SetBool(AnimatorParams.Aiming, _aiming);
            bool rifleCarry=_rifleBody && _equippedKind==WeaponKind.Rifle && !_aiming;
            bool knifeCarry=HasKnifeCarry&&_equippedKind==WeaponKind.Knife;
            _upperBodyWeight = _equippedKind == WeaponKind.Unarmed || rifleCarry || knifeCarry ? 0 : 1;
            if (_upperBodyLayer > 0) {
                animator.SetLayerWeight(_upperBodyLayer, _upperBodyWeight);
                string state = _equippedKind == WeaponKind.Rifle ? "Hold_Rifle" : _equippedKind == WeaponKind.Knife ? "Hold_Knife" : "Empty";
                if (animator.HasState(_upperBodyLayer, Animator.StringToHash(state))) animator.Play(state, _upperBodyLayer, 0);
            }
            string locomotion=knifeCarry?(_controller!=null&&_controller.IsSneaking?"Sneak_Knife":"Locomotion_Knife"):rifleCarry?RifleLocomotionState:LocomotionState;
            if (animator.HasState(0, Animator.StringToHash(locomotion))) animator.Play(locomotion, 0, 0);
            foreach (var pair in _weapons) pair.Value.gameObject.SetActive(pair.Key == _equippedKind);
            // Cinematic NPC grips may have reparented/resized this existing gameplay rifle.
            if(_equipped!=null) {
                _equipped.transform.SetParent(handAnchor!=null?handAnchor:transform,false);
                if(gripConfig!=null)gripConfig.Apply(_equipped.transform,_equippedKind,_aiming);
            }
            _aimPoseBlend = _aiming ? 1 : 0;
            if (cameraRig != null) {cameraRig.ClearRecoil(); cameraRig.SetAiming(_aiming);}
            UpdateGripPose(); UpdateFacing(); animator.Update(0);
        }

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _characterController = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (health == null) health = GetComponent<Health>();
            if (cameraRig == null && Camera.main != null)
            {
                cameraRig = Camera.main.GetComponent<ThirdPersonCamera>();
            }

            if (animator != null)
            {
                _upperBodyLayer = animator.GetLayerIndex(UpperBodyLayer);
                _rifleBody = animator.runtimeAnimatorController != null
                    && animator.HasState(0, Animator.StringToHash(RifleLocomotionState));
                foreach (AnimatorControllerParameter p in animator.parameters)
                {
                    if (p.name == "ReloadSpeed") _hasReloadParams = true;
                    if (p.name == "Hit") _hasHitParam = true;
                }
            }

            tuning = tuning.OrDefault();
            _magazine = tuning.Create();

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
        /// bullet would churn. The root belongs to this actor so it follows scene transfers;
        /// world-space line positions keep already-fired streaks fixed when the actor moves.
        /// </summary>
        private void SpawnTracerPool()
        {
            if (tracerPrefab == null)
            {
                return;
            }

            // Adopt an existing pool after an editor script reload instead of duplicating it.
            if (_tracerRoot == null) _tracerRoot = transform.Find("ShotTracers");
            if (_tracerRoot == null && _tracers != null)
                foreach (var tracer in _tracers)
                    if (tracer != null && tracer.transform.parent != null)
                    {
                        _tracerRoot = tracer.transform.parent;
                        break;
                    }
            if (_tracerRoot == null) _tracerRoot = new GameObject("ShotTracers").transform;
            _tracerRoot.SetParent(transform, false);
            var existing = _tracerRoot.GetComponentsInChildren<ShotTracer>(true);
            _tracers = new ShotTracer[Mathf.Max(1, tracerPoolSize)];
            _tracerCursor = 0;
            for (int i = 0; i < _tracers.Length; i++)
            {
                _tracers[i] = i < existing.Length ? existing[i] : Instantiate(tracerPrefab, _tracerRoot);
            }
            for (int i = _tracers.Length; i < existing.Length; i++) Destroy(existing[i].gameObject);
        }

        private ShotTracer GetUsableTracer(int index)
        {
            var tracer = _tracers[index];
            if (tracer != null && tracer.IsUsable) return tracer;
            // Repair only a lost slot, never allocate a new streak for every shot.
            if (tracer != null) Destroy(tracer.gameObject);
            return _tracers[index] = Instantiate(tracerPrefab, _tracerRoot);
        }

        private void OnDestroy()
        {
            if (_tracerRoot != null) Destroy(_tracerRoot.gameObject);
        }

        private void ShowTracer(Vector3 from, Vector3 to)
        {
            if (tracerPrefab == null) return;
            if (_tracerRoot == null || _tracers == null || _tracers.Length == 0) SpawnTracerPool();
            if (_tracers == null || _tracers.Length == 0)
            {
                return;
            }

            // Prefer a free one; if every streak is still flashing, recycle at the cursor.
            for (int i = 0; i < _tracers.Length; i++)
            {
                int index = (_tracerCursor + i) % _tracers.Length;
                var tracer = GetUsableTracer(index);
                if (!tracer.IsFree)
                {
                    continue;
                }
                _tracerCursor = (index + 1) % _tracers.Length;
                tracer.TryPlay(from, to);
                return;
            }

            GetUsableTracer(_tracerCursor).TryPlay(from, to);
            _tracerCursor = (_tracerCursor + 1) % _tracers.Length;
        }

        private void Update()
        {
            bool dead = health != null && health.IsDead;

            // A round that lands and leaves him standing: Nam's own flinch (Blender hit-reaction take).
            if (health != null)
            {
                if (_hasHitParam && !dead && animator != null && _equippedKind == WeaponKind.Rifle
                    && _lastHealthSeen >= 0f && health.Current < _lastHealthSeen - 0.01f)
                {
                    animator.SetTrigger("Hit");
                    _upperBodyHoldUntil = Mathf.Max(_upperBodyHoldUntil, Time.time + 0.6f);
                }
                _lastHealthSeen = health.Current;
            }

            // The magazine is ticked before any early return. Dying halfway through a reload
            // would otherwise leave the timer frozen for good, and a magazine that believes it
            // is reloading refuses to fire — the weapon would come back from the respawn dead.
            if (dead)
            {
                _magazine?.CancelReload();
                _spread.Reset();
                if (cameraRig != null) cameraRig.ClearRecoil();
            }
            else
            {
                _magazine?.Tick(Time.deltaTime, DrawRounds);
                _spread.Tick(Time.deltaTime);
            }

            if (dead || (InputAllowed != null && !InputAllowed()))
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
            ReadReloadInput();
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
            // With Nam's own AK carry in the base layer (Locomotion_Rifle), his arms already hold the
            // rifle: the stance layer only comes up to aim, fire, reload or take a hit.
            bool carried = _rifleBody && _equippedKind == WeaponKind.Rifle && !_aiming && !attacking
                && (_magazine == null || !_magazine.IsReloading);
            carried|=HasKnifeCarry&&_equippedKind==WeaponKind.Knife&&!attacking
                &&(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion_Knife")||animator.GetCurrentAnimatorStateInfo(0).IsName("Sneak_Knife"));
            float target = (_equippedKind != WeaponKind.Unarmed || attacking) && !carried ? 1f : 0f;
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
                if (kb[SlotKeys[slot]].wasPressedThisFrame)
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

        /// <summary>
        /// Reads how steady the shooter is right now. Feet off the ground beats everything —
        /// a sneaking player who jumps is not sneaking any more.
        /// </summary>
        private ShooterStance CurrentStance()
        {
            if (_controller == null) return ShooterStance.Standing;
            if (_characterController != null && !_characterController.isGrounded) return ShooterStance.Airborne;
            if (_controller.IsSprinting) return ShooterStance.Running;
            if (_controller.IsSneaking) return ShooterStance.Sneak;
            return _controller.IsMoving ? ShooterStance.Walking : ShooterStance.Standing;
        }

        private void ReadReloadInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) TryReload();
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
            if (_equippedKind == WeaponKind.Knife && TrySpecialMelee != null && TrySpecialMelee()) return;
            // Everything that can refuse the shot is settled before a single side effect runs.
            // Pulling the trigger on an empty magazine used to burn the cooldown and play the
            // firing animation anyway, so the weapon mimed a shot it never took.
            bool firingGun = _equipped != null && _equipped.IsGun;
            if (firingGun)
            {
                if (_magazine != null)
                {
                    if (!_magazine.TryConsume())
                    {
                        // Out, or mid-reload. Reaching for a fresh magazine is what the player
                        // meant by pulling the trigger on an empty gun.
                        TryReload();
                        return;
                    }
                }
                else if (TryConsumeRound != null && !TryConsumeRound())
                {
                    return;
                }
            }

            float cooldown = _equipped != null ? _equipped.Cooldown : punchCooldown;

            // `Time.time + cooldown` rounds every gap up to a whole frame, quietly turning 600
            // rounds per minute into 500 at 50 fps. Carrying the remainder forward fixes that
            // during sustained fire, but only while the weapon is genuinely mid-burst: once the
            // schedule is more than a cooldown stale the burst is over, and resuming from it
            // would leave no delay at all before the next shot.
            _nextAttackTime = Time.time - _nextAttackTime > cooldown
                ? Time.time + cooldown
                : _nextAttackTime + cooldown;

            // The muzzle is where a gunshot is heard from; an empty hand has none, so fall back
            // to the character. Raised before the trace so a listener cannot miss a kill's shot.
            Transform sound = _equipped != null && _equipped.Muzzle != null ? _equipped.Muzzle : transform;
            Attacked?.Invoke(_equippedKind, sound.position);

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

            // How steady the shooter is decides the cone, and how long they have held the
            // trigger widens it. The weapon's own HipSpread is no longer consulted: one flat
            // number could not tell a crouched aimed tap from a shot fired mid-jump.
            ShooterStance stance = CurrentStance();
            direction = WeaponSpreadState.Scatter(direction, _spread.ConeHalfAngle(stance, _aiming));

            Vector2 kick = _spread.Fire(Time.time);
            if (cameraRig != null) cameraRig.AddRecoil(kick.x, kick.y);

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
