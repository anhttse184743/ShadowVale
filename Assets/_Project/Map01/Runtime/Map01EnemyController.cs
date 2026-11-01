using System.Linq;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Map 1's guard. The behaviour itself — the six-state FSM (<see cref="EnemyBrain"/>) and the
    /// sensors — is the shared AI module every map uses; this component only adds Map 1's pieces:
    /// its balance numbers, Hùng as a second target, the map's cover points, loot, save/restore,
    /// the reset after a failed scouting run, and the rescue set-piece's rules on top of the FSM
    /// (jetty posts, the overseer and his stone lure, the footstep reaction, cinematic holds,
    /// pursuers, the knife takedown). Its public API is what the rest of Map 1 and its tests rely on.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public sealed partial class Map01EnemyController : MonoBehaviour, INoiseListener, IEnemyWeapon, IEnemyTactics, IEnemyAgent
    {
        [SerializeField] private Vector3[] patrolPoints = System.Array.Empty<Vector3>();
        [SerializeField] private float visionRange = 24f;
        [Tooltip("Field of view while unaware; once engaged a guard keeps track of Nam all around.")]
        [SerializeField] private float visionAngle = 110f;
        [SerializeField] private float attackRange = 18f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float fireInterval = 0.65f;
        [SerializeField] private LayerMask obstructionMask = ~0;
        [Header("Map 1 operation membership; 0 leaves other missions unchanged")]
        public int operationIndex;
        public bool carriesSupplyKey, suppressOperationLoot;

        /// <summary>A guard on first contact runs to a Map 1 cover point only if one is this close.</summary>
        private const float CoverSearchRadius = 10f;

        private NavMeshAgent _agent;
        private Animator _animator;
        private Health _health;
        private Transform _player;
        private Health _playerHealth;
        private EnemyFeedback _feedback;
        private IPlayerObservable _playerView;
        private PlayerController _playerController;
        private ShotAccuracyProfile _accuracy = new ShotAccuracyProfile();
        /// <summary>How well he shoots (Map01Balance.json guardAccuracy); tests that isolate damage turn it off.</summary>
        public ShotAccuracyProfile Accuracy { get => _accuracy; set => _accuracy = value ?? new ShotAccuracyProfile(); }
        private readonly AimTracker _aim = new AimTracker();
        private System.Random _shotRng;
        private NavMeshEnemyBody _body;
        private Map01ConfinedBody _brainBody;
        private System.Func<Vector3, Vector3> _confineMoves;
        /// <summary>Where the FSM may walk him (the C3 commander: inside the fence); null = anywhere.</summary>
        public System.Func<Vector3, Vector3> ConfineMoves
        {
            get => _confineMoves;
            set { _confineMoves = value; if (_brainBody != null) _brainBody.Confine = value; }
        }
        private EnemyBrain _brain;
        private readonly EnemyTuning _tuning = new EnemyTuning();
        private float _nextShot;
        private readonly Map01GuardFireCycle _guardFireCycle = new Map01GuardFireCycle();
        private float _guardBurstLeaseUntil;
        private static readonly Map01GuardFireTuning DefaultGuardFire = new Map01GuardFireTuning();
        private Map01GuardFireTuning GuardFire => _mission?.Settings?.guardM16 ?? DefaultGuardFire;
        private bool UsesGuardM16Cycle => !IsBoss && RifleItemId == Map01M16.Id;
        private float GuardFireStagger => Fnv(LegacyId) % 101 / 100f * Mathf.Max(0, GuardFire.staggerSeconds);
        // Bursts (the commander): rounds per burst, the gap inside one, the pause between them; 1 round = single shots.
        private int _burstRounds = 1, _burstLeft;
        private float _burstGap, _burstInterval;
        private Map01Mission _mission;
        private bool _lootCreated;
        private float _hiddenScale = .32f, _crouchScale = .6f;
        private Map01Rescue _rescue;
        private int _shots;
        private Vector3 _postPosition;
        private Quaternion _postRotation;
        private float _lastHealth;
        private bool _sawPlayer;
        private float _lastDistance = float.PositiveInfinity;
        private PerceptionSchedule _sense;
        private float _baseDetectionSeconds = 1.5f, _baseSearchSeconds = 8f, _baseInvestigateArrive = 1.5f;

        // The rescue set-piece (Map01Rescue): the jetty squad's posts, the overseer beside Hùng, pursuers.
        private bool _rescuePost, _overseer, _pursuer;
        private Map01RescueLayout _rescueTuning;
        private float _hearReactionAt;
        private Vector3 _pendingFootstep;
        private bool _stoneLure;
        private Quaternion _stoneFacing = Quaternion.identity;
        private bool _startled;
        private bool _pendingDamage, _pendingOnlyMosin;
        private Vector3 _heardShotPosition;
        private float _heardShotUntil = float.NegativeInfinity;

        private void RecordDamage(DamageNotice notice)
        {
            bool mosin = notice.IsBullet && notice.Weapon == WeaponKind.Sniper && _mission != null && _mission.player != null
                && notice.Source == _mission.player.gameObject;
            _pendingOnlyMosin = _pendingDamage ? _pendingOnlyMosin && mosin : mosin;
            _pendingDamage = true;
        }
        private void ClearDamageEvidence() { _pendingDamage = false; _pendingOnlyMosin = false; }

        // The bed proxy holds this controller disabled, so it must settle a lethal bullet
        // before taking ownership of the corpse. The normal Update must not credit it twice.
        internal void ResolveSleepingMosinDeath()
        {
            if (Alive || !_pendingDamage || !_pendingOnlyMosin || _health.Current >= _lastHealth) return;
            OnHurt(); _lastHealth = _health.Current; ClearDamageEvidence(); CreateLoot();
        }

        /// <summary>Information a reinforcement may receive; never looks up Nam's current position.</summary>
        public bool TryGetKnownThreat(out Vector3 at)
        {
            at = default;
            if (!Alive) return false;
            if (Engaged) { at = Memory.LastKnownPlayerPosition; return true; }
            if (Time.time < _heardShotUntil) { at = _heardShotPosition; return true; }
            return false;
        }

        // Held still for a cinematic or a stopped mission; every timer resumes where it was.
        private bool _holdingAI;
        private float _pausedAt, _animatorSpeedBeforeHold;
        private bool _agentUpdatesPosition, _agentUpdatesRotation;
        private Quaternion _heldRotation;
        private Vector3 _heldPosition;
        private readonly System.Collections.Generic.HashSet<string> _animatorParams = new System.Collections.Generic.HashSet<string>();

        public EnemyBrain Brain { get { EnsureBrain(); return _brain; } }
        private EnemyBlackboard Memory => Brain.Memory;

        public bool Alive => !(_health != null ? _health : GetComponent<Health>()).IsDead; // Asked every frame, all over.
        /// <summary>Which of the six FSM states he is in.</summary>
        public EnemyStateId State => Brain.State;
        /// <summary>Checking out a noise or the last place he saw Nam — there, or looking around.</summary>
        public bool Alerted => Memory.Alerted(Time.time);
        /// <summary>At the spot, looking around.</summary>
        public bool Searching => Memory.Searching(Time.time);
        /// <summary>Done looking and walking back to where he was.</summary>
        public bool Returning => Memory.Returning;
        public Vector3 ReturnPoint => Memory.ReturnPoint;
        /// <summary>Goes up every time he is hurt (lethally or not) — for whoever needs to notice attacks.</summary>
        public int HurtCount { get; private set; }
        /// <summary>He went down to a silent knife takedown (<see cref="TrySilentTakedown"/>).</summary>
        public bool TakenDownSilently { get; private set; }
        public float LastStoneHeardAt { get; private set; } = float.NegativeInfinity;
        /// <summary>A sentry: a sound makes him look, not walk over to check; only a thrown stone draws him off.</summary>
        public bool HoldsPost { get; set; }
        /// <summary>0..1 — how sure this guard is that he has seen Nam; 1 means spotted.</summary>
        public float Suspicion => Memory.Suspicion;
        /// <summary>Spotted Nam and fighting him — refreshed while he stays in sight.</summary>
        public bool Engaged => Memory.Engaged(Time.time);
        /// <summary>Set by whoever groups Map 1's guards: the camps (C1–C3), the jetty squad (B), the boss.</summary>
        public string SquadId { get; internal set; } = "";
        public string LegacyId => name.StartsWith("Outpost guard ", System.StringComparison.Ordinal)
            ? "expansion_guard_" + name.Substring("Outpost guard ".Length) : name;
        public string SaveId {
            get {
                string path = name;
                for (var parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
                return path;
            }
        }

        /// <summary>He saw Nam (fully spotted) or heard a shot — the rescue's alarm listens.</summary>
        public event System.Action<Map01Detection> Detected;
        public bool IsPursuer => _pursuer;
        /// <summary>The overseer, alive and standing by Hùng.</summary>
        public bool AtHostagePost => _overseer && Alive && Vector3.Distance(transform.position, _postPosition)
            <= (_rescueTuning != null ? _rescueTuning.overseerHostageRadius : 1.25f);

        /// <summary>The jetty squad while Hùng is still held: quieter steps, slower to spot, closer to touch.</summary>

        private bool RescueStealth => _rescuePost && _rescue != null && _rescue.CurrentPhase == Map01Rescue.Phase.Captive;

        /// <summary>Treat him as fighting Nam for <paramref name="seconds"/> (debug console, tests).</summary>
        public void EngageFor(float seconds) => Memory.EngagedUntil = Time.time + seconds;

        public string RifleItemId { get; private set; } = "rifle_standard";

        public void RefreshRifleLoadout(string savedItemId = null)
        {
            if (_mission == null) return;
            string previous = RifleItemId;
            // Old saves have no loadout field; new saves keep the guard's actual gun
            // even if waking reserves has changed his index in the roster.
            RifleItemId = savedItemId == Map01M16.Id || savedItemId == "rifle_standard"
                ? savedItemId : Map01M16.EnemyRifle(_mission, this);
            if (previous != RifleItemId) { _guardFireCycle.Reset(); _guardBurstLeaseUntil = 0; }
            var held = GetComponentInChildren<Map01Rifle>(true);
            if (held != null) Map01RifleVisual.Apply(held.weapon, RifleItemId);
        }
        public void BindMission(Map01Mission mission)
        {
            _mission = mission;
            Map01EnemyGrip.Bind(this);
            _feedback = EnemyFeedback.Attach(gameObject, mission.player.GetComponent<PlayerController>());
            _feedback.GunshotClip = RifleReport;
            _feedback.CanStep = () => enabled && !_holdingAI && _mission != null && !_mission.Stopped;
            GetComponent<Health>().KeepCheckpointCorpse();
            // Map 1's guard tuning lives in Map01Balance.json and Map01Content.json, not in each
            // guard's serialized fields.
            damage = mission.Weapon.damage * mission.Settings.guardDamageScale;
            fireInterval = mission.Settings.guardShotInterval;
            RefreshRifleLoadout();
            _accuracy = mission.Settings.guardAccuracy ?? new ShotAccuracyProfile();
            _baseDetectionSeconds = _tuning.DetectionSeconds = Mathf.Max(.1f, mission.Settings.detectionSeconds);
            _hiddenScale = mission.Settings.hiddenVisionScale;
            _crouchScale = mission.Settings.crouchVisionScale;
            _baseSearchSeconds = _tuning.SearchSeconds = mission.Settings.searchSeconds > 0 ? mission.Settings.searchSeconds : 8f;
            _tuning.SuppressSeconds = Mathf.Max(0f, mission.Settings.suppressSeconds);
            _tuning.RetreatHealthFraction = Mathf.Clamp01(mission.GuardData.retreat_hp_threshold);
            _rescue = mission.GetComponent<Map01Rescue>(); // Added by Map01Quest.Awake, before this runs.
            mission.Noise.Register(this);
            // Left alone for a while, a wounded guard recovers (not the commander: ConfigureAsBoss takes it off).
            if (!IsBoss) EnemyRegen.Attach(gameObject, LegacyId).Suspended = () => _holdingAI || _mission == null || _mission.Stopped;
            SyncTuning();
        }

        private void OnDestroy()
        {
            if (_mission != null) _mission.Noise.Unregister(this);
            if (_health != null) _health.Damaged -= RecordDamage;
            bushCorpse?.Dispose();
        }

        /// <summary>
        /// Back to the post at full health and calm — a failed scouting run means the camp was
        /// reinforced, so even a guard Nam took down is replaced, loot and all. Calm for a few
        /// seconds so Nam gets to slip away instead of being re-spotted on the spot.
        /// </summary>
        public void ReturnToPost()
        {
            _guardFireCycle.Reset(); _guardBurstLeaseUntil = 0; _nextShot = 0;
            ClearDamageEvidence(); _heardShotUntil = float.NegativeInfinity;
            LastStoneHeardAt = float.NegativeInfinity;
            bushCorpse?.Dispose(); bushCorpse = null; silentKind = 0; grappleOwned = false;
            TakenDownSilently = false; SetSilent(false); ShowRifle(true); _startled = false;
            _bodyState = BodyUnseen; Wary = false;
            if (_health.IsDead) _health.Revive(); else _health.RestoreHealth(_health.Max);
            if (_lootCreated)
            {
                _lootCreated = false;
                var loot = GetComponent<ForestPoint>();
                if (loot != null) { _mission.UnregisterPoint(loot); Destroy(loot); } // _lootCreated implies a bound mission.
                Map01Drops.For(_mission).Unlay(GunPoint(0), GunPoint(1));
            }
            _lastHealth = _health.Current;
            _body.Warp(_postPosition);
            transform.rotation = _postRotation;
            _hearReactionAt = 0; _stoneLure = false;
            Brain.ResetToPost(Time.time + 4f);
            _sense.Reset();
            _body.SetPace(1f);
            if (patrolPoints.Length > 0) _body.MoveTo(patrolPoints[0]);
            RebaseHold();
        }

        /// <summary>
        /// A reset sets his timers from now. If he is still held (a stopped mission), the time he stood
        /// still before it must not be added on resume, or he comes back engaged for that long.
        /// </summary>
        private void RebaseHold() { if (_holdingAI) _pausedAt = Time.time; }
        /// <summary>How far he sees Nam standing in the open.</summary>
        public float VisionRange => visionRange;
        /// <summary>His patrol loop; empty for a guard who holds his post.</summary>
        public System.Collections.Generic.IReadOnlyList<Vector3> PatrolPoints => patrolPoints;
        public float DamagePerShot => damage;
        public float FireInterval => fireInterval;

        /// <summary>A hit in the extraction film: real health and death, but no extra loot or takedown credit.</summary>
        public void ReceiveExtractionHit(float amount, Vector3 point)
        {
            if (!Alive) return;
            if (amount >= _health.Current) _lootCreated = true;
            _health.TakeDamage(amount, point, null); _lastHealth = _health.Current;
            if (_health.IsDead && _agent.isOnNavMesh) { _agent.ResetPath(); _agent.isStopped = true; }
        }

        // ---- IEnemyAgent: what the coordinator and the debug tools read -------------------------

        string IEnemyAgent.Id => LegacyId;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public float HealthFraction => _health != null && _health.Max > 0 ? _health.Current / _health.Max : 0f;
        public bool SeesPlayer => _sawPlayer;
        float IEnemyAgent.VisionConeAngle => visionAngle;
        public void DebugEngage(float seconds) => EngageFor(seconds);
        public void DebugDamage(float fractionOfMax) => _health.TakeDamage(_health.Max * fractionOfMax, transform.position + Vector3.up, null);

        private void OnEnable() => AgentRegistry.Register(this);
        private void OnDisable() => AgentRegistry.Unregister(this);

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _health = GetComponent<Health>();
            _postPosition = transform.position; _postRotation = transform.rotation;
            _health.Damaged += RecordDamage;
            _lastHealth = _health.Current;
            var controller = FindFirstObjectByType<PlayerController>();
            if (controller != null)
            {
                _player = controller.transform;
                _playerController = controller;
                _playerHealth = controller.GetComponent<Health>();
                _playerView = PlayerStealthAdapter.For(controller.gameObject);
            }
            if (_animator != null)
            {
                int upperBody = _animator.GetLayerIndex(PlayerCombat.UpperBodyLayer);
                if (upperBody > 0) _animator.SetLayerWeight(upperBody, 1f);
                foreach (AnimatorControllerParameter p in _animator.parameters) _animatorParams.Add(p.name);
                // One of the guards' five deaths, fixed per guard (by name) so a reload replays the same fall.
                if (_animatorParams.Contains("DeathIndex"))
                    _animator.SetInteger("DeathIndex", (int)((uint)Fnv(name) % 5u));
            }
            EnsureBrain();
            if (patrolPoints.Length > 0) _body.MoveTo(patrolPoints[0]);
        }

        private void EnsureBrain()
        {
            if (_brain != null) return;
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_health == null) _health = GetComponent<Health>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            _body = new NavMeshEnemyBody(transform, _agent, _animator, _health);
            _brainBody = new Map01ConfinedBody(_body) { Confine = _confineMoves };
            _baseInvestigateArrive = _tuning.InvestigateArriveDistance;
            _brain = new EnemyBrain(LegacyId, _brainBody, this, _tuning, this);
            _brain.Memory.PatrolPoints = patrolPoints;
            SyncTuning();
        }

        /// <summary>Copies the serialized/boss-adjusted numbers into the brain's tuning.</summary>
        private void SyncTuning()
        {
            _tuning.VisionRange = visionRange;
            _tuning.AttackRange = attackRange;
            _tuning.IsBoss = IsBoss;
        }

        private void Update()
        {
            if (grappleOwned) return; // Map01BushTakedown moves him.
            if (_rescuePost && _rescue != null) _rescue.TryApproachIntro();
            // A stopped mission or a cinematic holds him exactly where he is; his timers resume after.
            bool holding = Alive && _mission != null && (_mission.Stopped || (_rescue != null && _rescue.HoldEnemy(this)));
            if (holding)
            {
                if (!_holdingAI) PauseForCinematic();
                transform.position = _heldPosition;
                transform.rotation = _heldRotation;
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                if (_animator != null)
                {
                    var cinematic = _mission.GetComponent<Map01RescueCinematic>();
                    bool owned = cinematic != null && cinematic.ControlsEnemy(this);
                    _animator.speed = !_mission.Stopped || owned ? 1 : 0;
                    if (!_mission.Stopped) _body.Animate(false);
                }
                return;
            }
            if (_holdingAI) ResumeAfterHold();
            if (_mission != null && _mission.Stopped) { _body.Stop(); _body.Animate(false); return; } // Dead guards in a stopped mission.

            // Before the dead check: a hit that killed him outright is still an attack to account for.
            if (_health.Current < _lastHealth) OnHurt();
            _lastHealth = _health.Current;
            ClearDamageEvidence();
            if (_health.IsDead)
            {
                _sawPlayer = false;
                CreateLoot();
                _body.Stop();
                _body.Animate(false);
                return;
            }

            if (_hearReactionAt > 0 && Time.time >= _hearReactionAt)
            {
                _hearReactionAt = 0;
                BeginInvestigation(_pendingFootstep);
            }

            // The jetty squad while Hùng is held is slower to be sure of what it saw.
            bool stealth = RescueStealth;
            _tuning.DetectionSeconds = stealth
                ? Mathf.Max(_baseDetectionSeconds, _rescueTuning != null ? _rescueTuning.closeDetectionSeconds : 1.6f)
                : _baseDetectionSeconds;
            if (Wary) _tuning.DetectionSeconds *= _mission != null && _mission.Settings.waryDetectionScale > 0f
                ? _mission.Settings.waryDetectionScale : WaryDetectionScale;
            _tuning.SuspicionCloseGain = stealth ? 1.4f : 3f;

            // Calm guards look 10 times a second, staggered; alert ones every frame (PerceptionSchedule).
            bool alert = Engaged || _sawPlayer || Memory.Suspicion > 0f;
            if (_sense.Due(Time.time, alert, LegacyId)) _sawPlayer = CanSeePlayer(out _lastDistance);
            bool sees = _sawPlayer;
            float distance = _lastDistance;
            _brain.Tick(new PerceptionSample(sees, distance, _player != null ? _player.position : transform.position),
                Time.time, Time.deltaTime);
            ApplyPatrolStops();

            if (sees && Engaged)
            {
                ReportDetection(Map01DetectionCause.Sight, _player.position);
                if (_mission != null) _mission.Alarmed = true;
            }
            // The first moment he catches something: a start, then the stare.
            if (!_startled && !Engaged && Memory.Suspicion > .2f) { _startled = true; Trigger("Startled"); }
            if (Memory.Suspicion <= 0f && !Engaged && !Alerted) _startled = false;

            if (_stoneLure)
            {
                if (!Alerted) { _stoneLure = false; ApplyLureTuning(); }
                else if (Searching)
                {
                    // Eyes on where the stone fell, a slow small sweep — not the usual look around.
                    float seconds = _rescueTuning != null ? _rescueTuning.overseerStoneFocusSeconds : 11f;
                    float sweep = _rescueTuning != null ? _rescueTuning.overseerStoneSweepDegrees : 8f;
                    var focused = _stoneFacing * Quaternion.Euler(0, Mathf.Sin((Time.time - Memory.SearchUntil + seconds) * .8f) * sweep, 0);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, focused, 120 * Time.deltaTime);
                }
            }

            // A pursuer with Nam out of sight goes for Hùng while Hùng is out in the open.
            if (_pursuer && _rescue != null && _mission.hung != null && _rescue.Exposed && !sees && !(Engaged && HostageInReach(out _)))
            {
                _body.MoveTo(_mission.hung.position);
                _body.Animate(true);
            }

            // The guards' own clips (StoryAnimationSetup): rifle up while fighting, a look round at the spot.
            if (_animator != null)
            {
                if (_animatorParams.Contains("Engaged")) _animator.SetBool("Engaged", Engaged);
                if (_animatorParams.Contains("Searching")) _animator.SetBool("Searching", Searching);
            }
        }

        /// <summary>After a hold: every timer moves on by the time he stood still.</summary>
        private void ResumeAfterHold()
        {
            var m = Memory;
            float delay = Time.time - _pausedAt;
            m.AlertUntil += delay; m.EngagedUntil += delay; m.SuppressUntil += delay; m.TakeCoverUntil += delay;
            m.CalmUntil += delay; m.PatrolPauseUntil += delay; _nextShot += delay;
            if (_guardBurstLeaseUntil > _pausedAt) _guardBurstLeaseUntil += delay;
            if (_hearReactionAt > 0) _hearReactionAt += delay;
            if (_heardShotUntil > _pausedAt) _heardShotUntil += delay;
            if (m.SearchUntil > 0) m.SearchUntil += delay;
            if (_animator != null) _animator.speed = _animatorSpeedBeforeHold;
            _agent.updatePosition = _agentUpdatesPosition;
            _agent.updateRotation = _agentUpdatesRotation;
            if (_agent.isOnNavMesh) { _agent.nextPosition = transform.position; _agent.isStopped = !Alive; }
            _holdingAI = false;
        }

        /// <summary>
        /// Hurt — by a weapon: the knife takedown is its own action (<see cref="TrySilentTakedown"/>).
        /// Mosin damage is evidence of an attack, not evidence that the shooter was seen.
        /// </summary>
        private void OnHurt()
        {
            HurtCount++;
            if (TakenDownSilently || _player == null) return;
            if (_pendingDamage && _pendingOnlyMosin) { OnMosinHurt(); return; }
            ReportDetection(Map01DetectionCause.Sight, _player.position);
            if (_health.IsDead) // Killed outright — by a gun, say: loud, and nothing left to react.
            {
                string weapon = _mission != null && _mission.ModernCombat != null ? _mission.WeaponCode(_mission.ModernCombat.EquippedKind.ToString().ToLowerInvariant()) : "";
                EventBus.Publish(new EnemyKilledEvent(LegacyId, "weapon", weapon, transform.position));
                return;
            }
            Trigger("Hit");
            _brain.OnHurtByPlayer(_player.position, Time.time);
        }

        private void OnMosinHurt()
        {
            if (_health.IsDead)
            {
                EventBus.Publish(new EnemyKilledEvent(LegacyId, "weapon", "sniper", transform.position));
                return;
            }
            Trigger("Hit");
            _sawPlayer = CanSeePlayer(out _lastDistance);
            if (_sawPlayer)
            {
                ReportDetection(Map01DetectionCause.Sight, _player.position);
                Brain.OnHurtByPlayer(_player.position, Time.time);
                return;
            }
            MakeWary();
            // Existing confirmed contact survives, but this hit does not update it to Nam's live position.
            if (Engaged) return;
            if (TryGetKnownThreat(out var heard)) { BeginInvestigation(heard); return; }
            // Unknown source: search locally, optionally reaching an existing nearby cover point.
            // Keep sentries/hostage overseers in their roles and leave the commander's combat policy alone.
            var nearby = transform.position;
            if (!IsBoss && !_overseer && !HoldsPost && CanMove && TryFindCover(Brain.Context, nearby, out var cover))
            {
                var path = new NavMeshPath();
                if (_agent.CalculatePath(cover, path) && path.status == NavMeshPathStatus.PathComplete) nearby = cover;
            }
            BeginInvestigation(nearby);
        }

        /// <summary>How this guard sees; rebuilt on demand so the boss's longer sight applies at once.
        /// The jetty squad notices Nam within arm's reach only when he is right on them.</summary>
        private VisionProfile Vision => _rescuePost
            ? new VisionProfile(visionRange, visionAngle, _crouchScale, _hiddenScale, touchRadiusStanding: .9f, touchRadiusCrouched: .65f)
            : new VisionProfile(visionRange, visionAngle, _crouchScale, _hiddenScale);

        private bool CanSeePlayer(out float distance)
        {
            distance = float.PositiveInfinity;
            if (_player == null || _playerHealth == null || Time.time < Memory.CalmUntil) return false;
            // Bushes and tree crowns hide Nam — even from a guard already hunting him, who then
            // goes to where he last saw him.
            return VisionSensor.CanSee(transform.position, transform.forward, Engaged, Vision, _playerView,
                _mission != null ? _mission.SightCover : null, obstructionMask, out distance);
        }

        // ---- Animator ----------------------------------------------------------------------------

        private void Trigger(string parameter)
        {
            if (_animator != null && _animatorParams.Contains(parameter)) _animator.SetTrigger(parameter);
        }

        private void SetSilent(bool value)
        {
            if (_animator != null && _animatorParams.Contains("Silent")) _animator.SetBool("Silent", value);
        }

        private void ShowRifle(bool on)
        {
            var rifle = GetComponentInChildren<Map01Rifle>(true);
            if (rifle != null && rifle.weapon != null) rifle.weapon.gameObject.SetActive(on);
        }

        private static uint Fnv(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text) { hash ^= c; hash *= 16777619; }
            return hash;
        }
    }
}
