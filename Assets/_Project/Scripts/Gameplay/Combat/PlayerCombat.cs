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
    public sealed partial class PlayerCombat : MonoBehaviour
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
        /// Default hotbar order, left to right: 1 rifle, 2 knife. There is no bare-hands slot any
        /// more: something is always in hand. The HUD reads this, so the slots on screen and the
        /// number keys can never drift apart. A scene with its own loadout takes the keys over
        /// through <see cref="SlotPressed"/> (Map 1: 1 long gun, 2 pistol, 3 knife, 4 throwable).
        /// </summary>
        public static readonly WeaponKind[] SlotOrder =
        {
            WeaponKind.Rifle,
            WeaponKind.Knife,
        };

        private static readonly Key[] SlotKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
        public System.Func<bool> InputAllowed { get; set; }

        /// <summary>
        /// A number key (0 for "1") goes to the scene first; returning true means the scene handled
        /// it and the default <see cref="SlotOrder"/> is not consulted.
        /// </summary>
        public System.Func<int, bool> SlotPressed { get; set; }

        /// <summary>
        /// Legacy per-shot ammo gate, kept for scenes that never configured a magazine. Once a
        /// magazine exists it owns the round count and this is not consulted: the reserve is
        /// drawn on reload through <see cref="DrawRounds"/> instead, so a shot costs one round
        /// from the weapon rather than one from the pack.
        /// </summary>
        public System.Func<bool> TryConsumeRound { get; set; }

        /// <summary>
        /// Asked before a gun fires: false when the gun in hand cannot (worn to nothing). Settled before the
        /// magazine, so a broken gun keeps its rounds and mimes no shot. Left unset, every gun fires.
        /// </summary>
        public System.Func<bool> GunUsable { get; set; }
        public System.Func<WeaponKind,bool> EquipAllowed { get; set; }

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
        public float CurrentSpreadDegrees => _spread.ConeHalfAngle(CurrentStance(), _aiming, _equippedKind, _equipped != null && _equipped.UsesM16Handling);

        /// <summary>0 to 1. How far sustained fire has opened the cone.</summary>
        public float SpreadBloom => _spread.Bloom;

        /// <summary>Shots into the current burst.</summary>
        public int BurstShotIndex => _spread.ShotIndex;

        /// <summary>Rounds in the weapon right now.</summary>
        public int RoundsInMagazine => _magazine?.Rounds ?? 0;

        public int MagazineCapacity => _magazine?.Capacity ?? 0;
        public bool IsReloading => _magazine is { IsReloading: true };
        public float ReloadDurationMultiplier {get;set;}=1f;
        public float BaseReloadSeconds => _activeTuning.OrDefault().reloadSeconds;
        public bool EquippedIsGun => _equipped != null && _equipped.IsGun;
        public float StanceBlendSeconds => Mathf.Max(.01f, stanceBlendTime);
        public int ReloadPlannedRounds => _magazine?.ReloadPlannedRounds??0;
        public float FullReloadProgress => _magazine?.FullReloadProgress??0;

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
        public ShotTracer TracerPrefab => tracerPrefab;

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
        private readonly System.Collections.Generic.Dictionary<WeaponKind, Magazine> _magazines = new();
        private readonly System.Collections.Generic.Dictionary<WeaponKind, WeaponTuning> _tunings = new();
        private WeaponTuning _activeTuning;
        private readonly WeaponSpreadState _spread = new();
        private bool _aiming;
        private bool _firingHeld;
        private bool _automaticBurst;
        private float _stanceYaw;
        private ShotTracer[] _tracers;
        private Transform _tracerRoot;
        private int _tracerCursor;
        private int _upperBodyLayer = -1;
        private float _aimPoseBlend;
        private float _upperBodyWeight;
        /// <summary>Base-layer state holding Nam's own AK carry, when the controller has one.</summary>
        public const string RifleLocomotionState = "Locomotion_Rifle";
        private bool _rifleBody, _hasReloadParams;
        private static readonly int KnifeLocomotionHash = Animator.StringToHash("Locomotion_Knife");
        private bool HasKnifeCarry => animator!=null&&animator.HasState(0,KnifeLocomotionHash);
        private float _upperBodyHoldUntil;
        private float _faceCameraHoldUntil;
        private float _fireHoldUntil;
        private BarrelAim _barrelAim;

        /// <summary>The rifle in hand, or null with anything else out.</summary>
        internal Weapon Gun => !_handsOccupied && _equipped != null && _equipped.IsGun ? _equipped : null;
        private bool _handsOccupied;
        /// <summary>The hands hold something other than the weapon (a load, a tool); the weapon cannot be used.</summary>
        public bool HandsOccupied => _handsOccupied;

        /// <summary>Free the hands for a held item while preserving the equipped weapon and ammunition.</summary>
        public void SetHandsOccupied(bool occupied)
        {
            if(!occupied && !_handsOccupied)return;
            _handsOccupied=occupied;
            _firingHeld=false;
            _automaticBurst=false;
            SetAiming(false);_upperBodyHoldUntil=_faceCameraHoldUntil=_fireHoldUntil=0;
            _aimPoseBlend=0;_upperBodyWeight=0;_magazine?.CancelReload();_pendingGunfire.Clear();
            _controller?.SetFaceCameraYaw(false);
            if(occupied)cameraRig?.ClearRecoil();
            if(animator==null || animator.runtimeAnimatorController==null)return;
            animator.SetBool(AnimatorParams.Aiming,false);
            animator.SetInteger(AnimatorParams.Weapon,occupied?0:(int)_equippedKind);
            animator.ResetTrigger(AnimatorParams.Attack);
            if(_hasReloadParams)animator.ResetTrigger("Reload");
            if(_upperBodyLayer>0) {
                animator.SetLayerWeight(_upperBodyLayer,0);
                string upper=occupied?"Empty":_equippedKind==WeaponKind.Rifle?"Hold_Rifle":"Hold_Knife";
                if(animator.HasState(_upperBodyLayer,Animator.StringToHash(upper)))animator.Play(upper,_upperBodyLayer,0);
            }
            bool crouched=_controller!=null && _controller.IsSneaking;
            string state=occupied?(crouched?"Sneak":LocomotionState):_equippedKind==WeaponKind.Rifle
                ?(crouched?"Sneak_Rifle":RifleLocomotionState):_equippedKind==WeaponKind.Knife && HasKnifeCarry
                ?(crouched?"Sneak_Knife":"Locomotion_Knife"):(crouched?"Sneak":LocomotionState);
            if(animator.HasState(0,Animator.StringToHash(state)))animator.CrossFadeInFixedTime(state,.1f,0);
        }

        /// <summary>
        /// True while the takes hold the rifle up to shoot — sights raised, or a round just fired from the hip —
        /// which is when <see cref="BarrelAim"/> lays the barrel on the crosshair. A flinch raises the stance
        /// layer too, but leaves the rifle in its carry: that does not count.
        /// </summary>
        public bool GunPresentationReady { get; set; } = true;
        internal bool RifleLevelled => Gun != null && GunPresentationReady && (_aiming || _firingHeld || Time.time < _fireHoldUntil);

        internal bool RecoilPosePlaying => animator != null && _upperBodyLayer > 0
            && (animator.GetCurrentAnimatorStateInfo(_upperBodyLayer).IsName("Attack_Rifle")
                || (animator.IsInTransition(_upperBodyLayer)
                    && animator.GetNextAnimatorStateInfo(_upperBodyLayer).IsName("Attack_Rifle")));

        internal int HitMask => hitMask;

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
            _aiming=false;_aimPoseBlend=0;_upperBodyHoldUntil=_faceCameraHoldUntil=_fireHoldUntil=0;
            if(animator!=null){animator.SetBool(AnimatorParams.Aiming,false);if(_upperBodyLayer>0)animator.SetLayerWeight(_upperBodyLayer,0);}
            if(_equipped!=null&&gripConfig!=null)gripConfig.Apply(_equipped.transform,_equippedKind,false);
        }

        public void RestoreAfterCinematic()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.Rebind();
            _upperBodyHoldUntil=_faceCameraHoldUntil=_fireHoldUntil=0;
            _nextAttackTime=Time.time;
            _magazine?.CancelReload();_spread.Reset();
            _rifleBody=animator.HasState(0,Animator.StringToHash(RifleLocomotionState));
            _upperBodyLayer = animator.GetLayerIndex(UpperBodyLayer);
            animator.SetInteger(AnimatorParams.Weapon, (int)_equippedKind);
            animator.SetFloat(AnimatorParams.Speed, 0);
            animator.SetBool(AnimatorParams.Grounded, true);
            animator.SetBool(AnimatorParams.Sneaking, _controller != null && _controller.IsSneaking);
            animator.ResetTrigger(AnimatorParams.Attack); animator.ResetTrigger(AnimatorParams.Die);
            // Clear the cinematic pose; gameplay polls the held aim button on its next update.
            _aiming = false;
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
            if(_handsOccupied)SetHandsOccupied(true);
        }

        private void Awake()
        {
            if(GetComponent<GunfireFrame>()==null)gameObject.AddComponent<GunfireFrame>();
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
                }
            }

            // Added here rather than on the prefab: every character that shoots through this needs it.
            if (!TryGetComponent(out _barrelAim)) _barrelAim = gameObject.AddComponent<BarrelAim>();
            _barrelAim.Bind(this, animator);

            tuning = tuning.OrDefault();
            _magazine = tuning.Create();
            _activeTuning = tuning;
            _magazines[WeaponKind.Rifle] = _magazine;
            _tunings[WeaponKind.Rifle] = tuning;

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

        private void Update()
        {
            bool dead = health != null && health.IsDead;

            // No flinch when a round lands: Nam (and Hùng in the tutorial) keeps his aim. The controller's
            // "Hit" state (Nam_HitReaction) is no longer triggered.

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
                _spread.Tick(Time.deltaTime, _equipped != null && _equipped.UsesM16Handling);
            }

            if (dead || _handsOccupied || (InputAllowed != null && !InputAllowed()))
            {
                _firingHeld = false;
                _automaticBurst = false;
                SetAiming(false);
                if(animator != null) animator.SetBool(AnimatorParams.Aiming, false);
                _faceCameraHoldUntil = 0f;
                _fireHoldUntil = 0f;
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
            // Holding the trigger keeps the shooting stance up between rounds without enabling camera ADS.
            if(animator != null) animator.SetBool(AnimatorParams.Aiming, _aiming || _firingHeld);
            UpdateFacing();
            UpdateUpperBodyWeight();
            UpdateGripPose();
        }

        // ---- Actions --------------------------------------------------------

        /// <summary>
        /// Puts <paramref name="kind"/> in hand. There are no bare hands to fall back to: asking for a
        /// weapon there is no prefab for keeps the one in hand and returns false.
        /// </summary>
        public bool Equip(WeaponKind kind)
        {
            if(EquipAllowed!=null&&!EquipAllowed(kind))return false;
            _weapons.TryGetValue(kind, out Weapon next);
            if (next == null)
            {
                // Only a scene with no weapons at all ends up empty-handed (the Unarmed default).
                if (kind != WeaponKind.Unarmed) Debug.LogWarning($"[Combat] No prefab for {kind}: keeping {_equippedKind} in hand.", this);
                return false;
            }
            if (next == _equipped) return true;

            CancelReload();

            if (_equipped != null)
            {
                _equipped.gameObject.SetActive(false);
            }

            _equipped = next;
            _equippedKind = next.Kind;
            GunPresentationReady=true;
            if (_magazines.TryGetValue(_equippedKind, out var magazine))
            {
                _magazine = magazine; _activeTuning = _tunings[_equippedKind];
            }
            _equipped.gameObject.SetActive(true);

            if (animator != null)
            {
                animator.SetInteger(AnimatorParams.Weapon, (int)_equippedKind);
            }

            // Sights belong to the gun that was holstered.
            if (!_equipped.IsGun)
            {
                SetAiming(false);
            }

            WeaponChanged?.Invoke(_equippedKind);
            return true;
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
    }
}
