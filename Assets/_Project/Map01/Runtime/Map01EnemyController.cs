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
    public sealed class Map01EnemyController : MonoBehaviour, INoiseListener, IEnemyWeapon, IEnemyTactics, IEnemyAgent
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
        // ---- The bush pull (Map01BushTakedown) --------------------------------------------------
        private bool grappleOwned;
        public bool BedPoseOwned { get; set; }
        public bool WeaponPoseAvailable => !BedPoseOwned && !grappleOwned && !TakenDownSilently && (_mission == null || !_mission.Cinematic);
        /// <summary>Rounds fired with their sound (tests count them).</summary>
        public int AudibleShots { get; private set; }
        private int silentKind;
        private Map01CinematicPose bushCorpse;
        public void SetGrappleWeaponReady(bool ready) { if (grappleOwned && Alive) ShowRifle(ready); }
        public void BeginGrapple() { grappleOwned = true; ShowRifle(false); }
        /// <summary>Approved bedside execution: silent credit, visible discoverable corpse (not a bush hide).</summary>
        public void CommitBedKill(string weaponId="pistol")
        {
            if (!Alive) return;
            TakenDownSilently = true; silentKind = 2;
            _health.TakeDamage(_health.Current, transform.position + Vector3.up, _mission.player.gameObject);
            _lastHealth = _health.Current;
            if (_health.IsDead) _mission.Skills?.Award(ShadowVale.Gameplay.Skills.SkillKind.Stealth);
            CreateLoot(); FillLoot();
            EventBus.Publish(new TakedownEvent(LegacyId, transform.position));
            EventBus.Publish(new EnemyKilledEvent(LegacyId, "takedown", weaponId, transform.position));
        }
        public void CommitBushKill()
        {
            if (!Alive) return; TakenDownSilently = true; silentKind = 1;
            _health.TakeDamage(_health.Current, transform.position + Vector3.up, _mission.player.gameObject); _lastHealth = _health.Current;
            if (_health.IsDead) _mission.Skills?.Award(ShadowVale.Gameplay.Skills.SkillKind.Stealth);
        }
        public void EndGrapple(bool won)
        {
            grappleOwned = false; _heldPosition = transform.position; _heldRotation = transform.rotation;
            if (_agent != null && _agent.isOnNavMesh) _agent.nextPosition = transform.position;
            if (won) HoldBushCorpse(); else { TakenDownSilently = false; silentKind = 0; SetSilent(false); ShowRifle(true); }
        }
        private void HoldBushCorpse()
        {
            bushCorpse?.Dispose(); bushCorpse = new Map01CinematicPose(_animator);
            var clip = Map01CinematicPose.Clip("Enemy_Bush_Finish"); bushCorpse.Sample(clip, clip.length); bushCorpse.Ground(transform.position.y);
        }
        private void LateUpdate()
        {
            if (_muzzleFlash != null && Time.time >= _flashUntil) _muzzleFlash.enabled = false;
            if (bushCorpse == null) return;
            var clip = Map01CinematicPose.Clip("Enemy_Bush_Finish"); bushCorpse.Sample(clip, clip.length); bushCorpse.Ground(transform.position.y);
        }
        /// <summary>He broke free of the bush pull: straight into the fight, where Nam is.</summary>
        public void ReportGrappleEscape()
        {
            if (!Alive || _mission == null) return;
            var m = Memory; float now = Time.time; var at = _mission.player.position;
            m.Suspicion = 1f; m.EngagedUntil = now + 10f; m.LastKnownPlayerPosition = at;
            Brain.BeginInvestigation(at, now); _mission.Alarmed = true;
            ReportDetection(Map01DetectionCause.Sight, at);
        }

        // ---- A comrade's body (Map01BodyDiscovery) -----------------------------------------------
        private const int BodyUnseen = 1, BodySeen = 2;
        /// <summary>Unseen (1) or seen by another guard (2); a save older than the rule brings 0 (see RestoreSnapshot).</summary>
        private int _bodyState = BodyUnseen;
        /// <summary>Dead, and another guard has found him — a body is found only once.</summary>
        public bool BodyFound => !Alive && _bodyState == BodySeen;
        /// <summary>Pulled into a bush (Map01BushTakedown): hidden for good, nobody finds him.</summary>
        public bool BodyHidden => silentKind == 1;
        public void MarkBodyFound() => _bodyState = BodySeen;
        /// <summary>He has seen a comrade's body: from then on he is quicker to be sure of what he sees, and stays so.</summary>
        public bool Wary { get; private set; }
        /// <summary>A wary guard needs this share of the usual time to be sure he has seen Nam.</summary>
        public const float WaryDetectionScale = .75f;
        public void MakeWary() { if (Alive) Wary = true; }
        /// <summary>Walks on the NavMesh (not the watchtower's sentry, whose agent is off).</summary>
        public bool CanMove => _agent != null && _agent.enabled && _agent.isOnNavMesh;

        /// <summary>A comrade's body at <paramref name="at"/>: a start, then over to look at it and around it. The overseer
        /// only looks from beside Hùng (<see cref="BeginInvestigation"/>).</summary>
        public void InspectBody(Vector3 at)
        {
            if (!Alive || Engaged) return;
            if (!_startled) { _startled = true; Trigger("Startled"); }
            _hearReactionAt = 0;
            BeginInvestigation(at);
            Wary = true;
        }

        /// <summary>Off to <paramref name="to"/> at the investigating hurry, without a start (the C3 kẻng runner).</summary>
        public void Dispatch(Vector3 to)
        {
            if (!Alive || Engaged) return;
            _hearReactionAt = 0; _stoneLure = false; ApplyLureTuning();
            Brain.BeginInvestigation(to, Time.time);
            Wary = true;
        }

        private bool RescueStealth => _rescuePost && _rescue != null && _rescue.CurrentPhase == Map01Rescue.Phase.Captive;

        /// <summary>Treat him as fighting Nam for <paramref name="seconds"/> (debug console, tests).</summary>
        public void EngageFor(float seconds) => Memory.EngagedUntil = Time.time + seconds;

        public string RifleItemId { get; private set; } = "rifle_standard";

        public void RefreshRifleLoadout(string savedItemId = null)
        {
            if (_mission == null) return;
            // Old saves have no loadout field; new saves keep the guard's actual gun
            // even if waking reserves has changed his index in the roster.
            RifleItemId = savedItemId == Map01M16.Id || savedItemId == "rifle_standard"
                ? savedItemId : Map01M16.EnemyRifle(_mission, this);
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

        /// <summary>A noise at <paramref name="position"/> that carries <paramref name="radius"/>
        /// metres: within it, the guard goes to see what it was. True if he heard it.</summary>
        public bool Hear(Vector3 position, float radius, NoiseKind kind = NoiseKind.Other)
        {
            if (BedPoseOwned) return false;
            bool stealth = RescueStealth;
            if (stealth && kind == NoiseKind.Footstep && _mission.Crouched)
                radius *= _rescueTuning != null ? _rescueTuning.crouchedFootstepScale : .55f;
            if (!Alive || !HearingSensor.Hears(transform.position, position, radius)) return false;
            if (kind == NoiseKind.Stone) LastStoneHeardAt = Time.time;
            if (_mission != null && _mission.Cinematic) return false;
            if (kind == NoiseKind.Gunshot)
            {
                _heardShotPosition = position; _heardShotUntil = Time.time + _tuning.InvestigateTimeoutSeconds;
                ReportDetection(Map01DetectionCause.Gunshot, position);
            }
            // Finish checking a thrown stone instead of cancelling the lure for a quiet step behind
            // him. Sight and gunshots still apply.
            if (stealth && kind == NoiseKind.Footstep && _mission.Crouched && _stoneLure && !Engaged) return true;
            if (stealth && kind == NoiseKind.Footstep && !Engaged)
            {
                // The first audible step starts the reaction clock; later steps move its source
                // without extending it. Stones and gunshots stay immediate.
                _pendingFootstep = position;
                if (_hearReactionAt <= 0) _hearReactionAt = Time.time + (_rescueTuning != null ? _rescueTuning.footstepReactionSeconds : 1f);
                return true;
            }
            _hearReactionAt = 0;
            BeginInvestigation(position, kind == NoiseKind.Stone);
            return true;
        }

        bool INoiseListener.OnNoise(Vector3 position, float radius, NoiseKind kind) => Hear(position, radius, kind);

        /// <summary>
        /// Off to check <paramref name="position"/> (the FSM's Investigate). The overseer does not
        /// leave Hùng for a sound: he looks from beside him, and for a stone he goes a few steps
        /// towards it and keeps his eyes on that spot (the stone lure) — never into the river.
        /// </summary>
        private void BeginInvestigation(Vector3 position, bool stone = false)
        {
            if (_overseer && RescueStealth && !Engaged)
            {
                var delta = Vector3.ProjectOnPlane(position - _postPosition, Vector3.up);
                position = _postPosition + Vector3.ClampMagnitude(delta,
                    stone ? (_rescueTuning != null ? _rescueTuning.overseerStoneRadius : 12f) : 1.25f);
                if (stone)
                {
                    _stoneFacing = delta.sqrMagnitude > .01f ? Quaternion.LookRotation(delta) : transform.rotation;
                    var path = new NavMeshPath();
                    if (NavMesh.SamplePosition(position, out var hit, 1.5f, NavMesh.AllAreas)
                        && _agent.isOnNavMesh && _agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                        position = hit.position;
                    else position = _postPosition; // A stone in the river must not draw him into water.
                }
            }
            else if (HoldsPost && !stone && !Engaged)
            {
                // A sentry turns to look at a sound but stays put; only a stone draws him off.
                var delta = Vector3.ProjectOnPlane(position - _postPosition, Vector3.up);
                position = _postPosition + Vector3.ClampMagnitude(delta, .3f);
            }
            _stoneLure = stone && _overseer;
            ApplyLureTuning();
            if (!_overseer && !IsBoss && CanMove) position = ReachableInvestigation(position);
            Brain.BeginInvestigation(position, Time.time);
        }

        // A sound on a roof or prop is not itself a walking destination. Prefer the original
        // point; otherwise search the reachable floor around it, without inventing a player position.
        private Vector3 ReachableInvestigation(Vector3 source)
        {
            var path = new NavMeshPath();
            bool Reach(Vector3 at, out Vector3 result)
            {
                result = default;
                if (!NavMesh.SamplePosition(at, out var hit, 2f, NavMesh.AllAreas)
                    || !_agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
                result = hit.position; return true;
            }
            if (Reach(source, out var direct)) return direct;
            var floor = new Vector3(source.x, transform.position.y, source.z);
            Vector3 best = transform.position;
            float closest = float.PositiveInfinity;
            for (float radius = 0; radius <= 8; radius += 2)
                for (int angle = 0; angle < 360; angle += 45)
                {
                    if (!Reach(floor + Quaternion.Euler(0, angle, 0) * Vector3.forward * radius, out var candidate)) continue;
                    float distance = Vector3.ProjectOnPlane(candidate - source, Vector3.up).sqrMagnitude;
                    if (distance < closest) { closest = distance; best = candidate; }
                }
            return best;
        }

        /// <summary>On a stone lure the overseer walks right up to the spot and stares at it longer.</summary>
        private void ApplyLureTuning()
        {
            _tuning.InvestigateArriveDistance = _stoneLure ? .55f : _baseInvestigateArrive;
            _tuning.SearchSeconds = _stoneLure ? (_rescueTuning != null ? _rescueTuning.overseerStoneFocusSeconds : 11f) : _baseSearchSeconds;
        }

        [System.Serializable] public sealed class Snapshot
        {
            public string id;
            public string rifleItemId;
            public Vector3 position, destination, investigate, returnPoint;
            public Quaternion rotation, returnRotation;
            public float hp, shotRemaining, alertRemaining, searchRemaining;
            public int patrolIndex;
            public bool stopped, returning, silent, pursuer;
            public int silentKind;
            public float suspicion, engagedRemaining, pauseRemaining;
            public float footstepReactionRemaining;
            public Vector3 pendingFootstep;
            public bool stoneLure;
            public Quaternion stoneFacing;
            /// <summary>1 unseen, 2 found by another guard; 0 in saves from before bodies could be found.</summary>
            public int bodyState;
            public bool wary;
            public bool hasKnownPlayer;
            public Vector3 knownPlayer, heardShotPosition;
            public float heardShotRemaining;
        }
        public Snapshot Capture()
        {
            var m = Memory; float now = Time.time;
            return new Snapshot {
                id = SaveId, rifleItemId = RifleItemId, position = transform.position, rotation = transform.rotation, hp = _health.Current,
                silent = TakenDownSilently, silentKind = silentKind, pursuer = _pursuer, suspicion = m.Suspicion,
                engagedRemaining = Mathf.Max(0, m.EngagedUntil - now), pauseRemaining = Mathf.Max(0, m.PatrolPauseUntil - now),
                footstepReactionRemaining = _hearReactionAt > 0 ? Mathf.Max(0, _hearReactionAt - now) : 0,
                pendingFootstep = _pendingFootstep, stoneLure = _stoneLure, stoneFacing = _stoneFacing,
                patrolIndex = m.PatrolIndex, shotRemaining = Mathf.Max(0, _nextShot - now),
                alertRemaining = Mathf.Max(0, m.AlertUntil - now), investigate = m.InvestigatePoint,
                searchRemaining = m.SearchUntil > 0 ? Mathf.Max(.01f, m.SearchUntil - now) : 0,
                returning = m.Returning, returnPoint = m.ReturnPoint, returnRotation = m.ReturnRotation,
                destination = _agent.isOnNavMesh && _agent.hasPath ? _agent.destination : transform.position,
                stopped = _agent.isOnNavMesh && _agent.isStopped,
                bodyState = _bodyState, wary = Wary,
                hasKnownPlayer = Engaged, knownPlayer = m.LastKnownPlayerPosition,
                heardShotPosition = _heardShotPosition, heardShotRemaining = Mathf.Max(0, _heardShotUntil - now)
            };
        }
        /// <summary>This guard as he would be fresh at his post: alive, calm, starting his patrol.</summary>
        public Snapshot CaptureAtPost() => new Snapshot {
            id = SaveId, rifleItemId = RifleItemId, position = _postPosition, rotation = _postRotation, hp = _health.Max,
            destination = _postPosition, patrolIndex = 0, bodyState = BodyUnseen
        };
        public void RestoreSnapshot(Snapshot saved)
        {
            RefreshRifleLoadout(saved.rifleItemId);
            var m = Memory; float now = Time.time;
            _body.Warp(saved.position);
            transform.rotation = saved.rotation;
            bushCorpse?.Dispose(); bushCorpse = null; silentKind = saved.silentKind;
            TakenDownSilently = saved.silent; SetSilent(saved.silent); ShowRifle(!saved.silent);
            _health.RestoreHealth(saved.hp);
            _lastHealth = _health.Current;
            // A body in a save from before bodies could be found counts as found: loading it must not set anyone off.
            ClearDamageEvidence();
            _heardShotPosition = saved.heardShotPosition; _heardShotUntil = now + saved.heardShotRemaining;
            m.LastKnownPlayerPosition = saved.hasKnownPlayer ? saved.knownPlayer : saved.investigate;
            m.LastSeenTime = float.NegativeInfinity;
            _bodyState = saved.bodyState != 0 ? saved.bodyState : Alive ? BodyUnseen : BodySeen;
            Wary = saved.wary;
            _sense.Reset();
            m.Suspicion = saved.suspicion; m.EngagedUntil = now + saved.engagedRemaining;
            m.PatrolPauseUntil = now + saved.pauseRemaining; _pursuer = saved.pursuer;
            _hearReactionAt = saved.footstepReactionRemaining > 0 ? now + saved.footstepReactionRemaining : 0;
            _pendingFootstep = saved.pendingFootstep;
            _stoneLure = saved.stoneLure;
            var look = Vector3.ProjectOnPlane(saved.investigate - _postPosition, Vector3.up);
            _stoneFacing = Quaternion.Dot(saved.stoneFacing, saved.stoneFacing) > .1f ? saved.stoneFacing
                : Quaternion.LookRotation(look.sqrMagnitude > .01f ? look : transform.forward);
            ApplyLureTuning();
            m.PatrolIndex = Mathf.Clamp(saved.patrolIndex, 0, Mathf.Max(0, patrolPoints.Length - 1));
            _nextShot = now + saved.shotRemaining;
            m.AlertUntil = now + saved.alertRemaining; m.InvestigatePoint = saved.investigate;
            m.SearchUntil = saved.searchRemaining > 0 ? now + saved.searchRemaining : 0;
            m.Returning = saved.returning; m.ReturnPoint = saved.returnPoint; m.ReturnRotation = saved.returnRotation;
            _body.MoveTo(saved.destination);
            if (_agent.isOnNavMesh) _agent.isStopped = saved.stopped || !Alive;
            if (!Alive) { CreateLoot(); FillLoot(); if (saved.silent && saved.silentKind == 1) HoldBushCorpse(); }
            RebaseHold();
        }

        /// <summary>A hit in the extraction film: real health and death, but no extra loot or takedown credit.</summary>
        public void ReceiveExtractionHit(float amount, Vector3 point)
        {
            if (!Alive) return;
            if (amount >= _health.Current) _lootCreated = true;
            _health.TakeDamage(amount, point, null); _lastHealth = _health.Current;
            if (_health.IsDead && _agent.isOnNavMesh) { _agent.ResetPath(); _agent.isStopped = true; }
        }

        public bool IsSupplyKeyKeeper => carriesSupplyKey && operationIndex == 1 && _mission != null &&
            _mission.GetComponent<Map01Story>()?.Layout?.keyKeeper == this;

        private void CreateLoot()
        {
            if (_lootCreated || _mission == null) return;
            _lootCreated = true;
            if (suppressOperationLoot) return;
            var loot = gameObject.AddComponent<ForestPoint>();
            loot.id = "loot_" + LegacyId; loot.kind = ForestPointKind.Loot;
            _mission.RegisterPoint(loot);
            FillLoot();
        }

        /// <summary>Gun piles of this guard: his rifle, and the commander's K-54.</summary>
        private string GunPoint(int index) => Map01Logistics.GunPointPrefix + LegacyId + (index == 0 ? "" : "_sidearm");

        /// <summary>
        /// His pack, rolled from his kind's loot table (Map01Logistics.GuardPack), and his guns, worn, laid on the ground
        /// beside him; the rifle in his hands goes with them. Called again after a load, which may bring back the rolls
        /// of another run.
        /// </summary>
        public void FillLoot()
        {
            var loot = _lootCreated && !Alive && _mission != null && !suppressOperationLoot ? GetComponent<ForestPoint>() : null;
            if (loot == null) return;
            var logistics = Map01Logistics.For(_mission);
            var pack = new System.Collections.Generic.List<ForestIngredient>(logistics.GuardPack(this, loot.id));
            if (IsSupplyKeyKeeper) pack.Add(new ForestIngredient { item_id = Map01Story.KeyItem, count = 1 });
            loot.items = pack.ToArray();
            loot.label = IsSupplyKeyKeeper ? "Lính gác kho — chìa khóa kho tiếp tế" : logistics.PackName(this);
            int index = 0;
            foreach (var gun in logistics.GuardGuns(this, loot.id))
            {
                // The rifle drops at his right side, the pistol at his left, lying across his line.
                float side = index == 0 ? 1f : -1f;
                var at = transform.position + transform.right * (.75f * side) + transform.forward * .2f;
                Map01Drops.For(_mission).Lay(GunPoint(index), gun, at, transform.eulerAngles.y + 90f + 25f * index);
                index++;
            }
            if (index > 0) ShowRifle(false);
        }

        // ---- Map 1's operations (Map01Story, Map01C3Mission), on the shared FSM's memory ---------

        /// <summary>Into the fight at once, towards <paramref name="lastSeen"/>, holding fire for <paramref name="readySeconds"/>.</summary>
        public void BeginOperationCombat(Vector3 lastSeen, float readySeconds = 1.5f)
        {
            if (!Alive) return;
            var m = Memory; float now = Time.time;
            _hearReactionAt = 0; _stoneLure = false; ApplyLureTuning();
            m.Suspicion = 1f; m.EngagedUntil = now + 15f; m.LastKnownPlayerPosition = lastSeen;
            Brain.BeginInvestigation(lastSeen, now);
            _nextShot = Mathf.Max(_nextShot, now + readySeconds);
        }
        /// <summary>Starts the fight at once from wherever he is (the camp's alarm, a commander's call).</summary>
        public void Alert(Vector3 lastSeen) => BeginOperationCombat(lastSeen, .8f);
        /// <summary>Gives up the fight and walks back to his post (Nam is too far: the camp does not chase deep into the forest).</summary>
        public void StandDown()
        {
            if (!Alive) return;
            var m = Memory;
            m.EngagedUntil = 0; m.AlertUntil = 0; m.SearchUntil = 0; m.SuppressUntil = 0; _hearReactionAt = 0; _stoneLure = false;
            m.Suspicion = Mathf.Min(m.Suspicion, .5f); m.ClearAssignment();
            m.Returning = true; m.ReturnPoint = _postPosition; m.ReturnRotation = _postRotation;
            _body.SetPace(1f);
        }

        // Per point of the beat (Map01C3Mission): how long he stays there and which way he faces meanwhile (NaN: as he came).
        private float[] _patrolStops, _patrolYaws;
        private float _stopYaw = float.NaN;
        private int _lastPatrolIndex = -1;
        /// <summary>
        /// A new post and beat for this guard, set by a mission's layout (Map 1's C3 command post). A stationary guard
        /// (a sentry on the watchtower's platform, off the NavMesh) has his agent switched off and only turns and shoots.
        /// <paramref name="stops"/> and <paramref name="yaws"/> follow <paramref name="points"/>: seconds he waits at each,
        /// and the yaw he holds there.
        /// </summary>
        /// <param name="walk">Walk there instead of being set down on the spot (a change of post in front of Nam).</param>
        public void Repost(Vector3 position, float yaw, Vector3[] points = null, float[] stops = null, float[] yaws = null,
            float vision = 0f, bool stationary = false, bool walk = false)
        {
            var rotation = Quaternion.Euler(0, yaw, 0);
            _postPosition = position; _postRotation = rotation;
            var m = Memory; m.ReturnPoint = position; m.ReturnRotation = rotation; m.PatrolIndex = 0; m.PatrolPauseUntil = 0; _lastPatrolIndex = 0;
            Configure(points); _patrolStops = stops; _patrolYaws = yaws; _stopYaw = float.NaN;
            if (vision > 0) { visionRange = vision; SyncTuning(); }
            if (stationary && _agent != null) _agent.enabled = false;
            if (!walk || stationary)
            {
                _body.Warp(position);
                transform.rotation = rotation;
            }
            if (!stationary && patrolPoints.Length > 0) _body.MoveTo(patrolPoints[0]);
            else if (walk && !stationary && !Alerted) _body.MoveTo(position);
        }

        /// <summary>The per-point stops of a Repost beat: the shared patrol only knows one pause for every point.</summary>
        private void ApplyPatrolStops()
        {
            var m = Memory;
            if (_patrolStops == null || patrolPoints.Length == 0) return;
            if (m.PatrolIndex != _lastPatrolIndex)
            {
                int reached = (m.PatrolIndex - 1 + patrolPoints.Length) % patrolPoints.Length;
                _lastPatrolIndex = m.PatrolIndex;
                if (reached < _patrolStops.Length && _patrolStops[reached] > 0)
                {
                    m.PatrolPauseUntil = Time.time + _patrolStops[reached];
                    _stopYaw = _patrolYaws != null && reached < _patrolYaws.Length ? _patrolYaws[reached] : float.NaN;
                }
                else _stopYaw = float.NaN;
            }
            if (Time.time < m.PatrolPauseUntil && !Engaged && !Alerted && !float.IsNaN(_stopYaw))
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, _stopYaw, 0), 150 * Time.deltaTime);
        }

        public void Configure(Vector3[] points)
        {
            patrolPoints = points ?? System.Array.Empty<Vector3>();
            if (_brain != null) _brain.Memory.PatrolPoints = patrolPoints;
        }

        /// <summary>
        /// A beat round his area (<see cref="Map01PatrolBeats"/>): the loop starting at <paramref name="start"/>, the
        /// seconds he stands and the yaw he faces at each point, the walking pace. Unlike <see cref="Repost"/> he is
        /// not moved: he walks to his first point from where he stands. His post becomes that point, so a reset
        /// after a failed scouting run puts him back on his beat. A checkpoint restored afterwards keeps its own
        /// position and point index.
        /// </summary>
        public void AssignBeat(Vector3[] points, float[] stops, float[] yaws, int start, float pace)
        {
            if (points == null || points.Length == 0) return;
            EnsureBrain();
            int n = points.Length;
            start = ((start % n) + n) % n;
            T[] Turn<T>(T[] a) => a == null ? null : Enumerable.Range(0, n).Select(i => a[(start + i) % n]).ToArray();
            var loop = Turn(points);
            Configure(loop); _patrolStops = Turn(stops); _patrolYaws = Turn(yaws); _stopYaw = float.NaN;
            _postPosition = loop[0];
            if (_patrolYaws != null && !float.IsNaN(_patrolYaws[0])) _postRotation = Quaternion.Euler(0, _patrolYaws[0], 0);
            _tuning.PatrolPace = pace;
            var m = Memory; m.PatrolIndex = 0; _lastPatrolIndex = 0; m.PatrolPauseUntil = 0;
            if (Alive && !Engaged && !Alerted && _agent != null && _agent.isOnNavMesh) _body.MoveTo(loop[0]);
        }

        /// <summary>The walking pace of his beat (1 = full speed; 0.55 for a calm beat).</summary>
        public float PatrolPace => _tuning.PatrolPace;

        // ---- The rescue set-piece ----------------------------------------------------------------

        /// <summary>One of the four jetty guards (Map01Rescue): his post, his loop, the layout's tuning.</summary>
        public void ConfigureRescuePost(Transform post, Vector3[] points, bool overseer, Map01RescueLayout layout)
        {
            EnsureBrain();
            _rescuePost = true; _overseer = overseer; _pursuer = false;
            _rescueTuning = layout;
            if (overseer)
            {
                var grip = _animator.GetComponent<Map01OverseerGrip>();
                if (grip == null) grip = _animator.gameObject.AddComponent<Map01OverseerGrip>();
                grip.Bind(this);
            }
            _postPosition = post.position; _postRotation = post.rotation;
            _agent.speed = _body.BaseSpeed = layout.patrolSpeed;
            _tuning.PatrolPauseSeconds = layout.patrolPause;
            visionRange = layout.patrolVision; visionAngle = layout.patrolAngle;
            SyncTuning();
            Configure(points);
            _body.Warp(_postPosition);
            transform.rotation = _postRotation;
            if (points.Length > 0) _body.MoveTo(points[0]); else if (_agent.isOnNavMesh) _agent.ResetPath();
        }

        /// <summary>A pursuer spawned after Hùng is freed: alive, armed, heading for Hùng if <paramref name="pursue"/>.</summary>
        public void InitializePursuer(Map01Mission owner, Vector3 post, bool pursue)
        {
            enabled = true;
            EnsureBrain();
            _rescuePost = _overseer = false; _pursuer = true; _lootCreated = false;
            carriesSupplyKey = false; operationIndex = 0;
            _holdingAI = false; _agent.updatePosition = true; _agent.updateRotation = true;
            _postPosition = post; _postRotation = Quaternion.identity; Configure(System.Array.Empty<Vector3>()); BindMission(owner);
            _health.CinematicInvulnerable = false; _health.Revive(); _lastHealth = _health.Current;
            TakenDownSilently = false; SetSilent(false); ShowRifle(true); _bodyState = BodyUnseen; Wary = false;
            _hearReactionAt = 0; _stoneLure = false; ApplyLureTuning();
            Brain.ResetToPost(0f);
            if (_animator != null) _animator.speed = 1;
            if (_agent.isOnNavMesh) { _agent.Warp(post); _agent.isStopped = false; }
            if (pursue && owner.hung != null) BeginInvestigation(owner.hung.position);
        }

        /// <summary>Hùng is home: a pursuer stops where he is.</summary>
        public void CeasePursuit()
        {
            var m = Memory;
            m.Suspicion = 0; m.EngagedUntil = 0; m.AlertUntil = 0; m.SearchUntil = 0;
            if (_agent.isOnNavMesh) { _agent.ResetPath(); _agent.isStopped = true; }
            _body.Animate(false);
        }

        /// <summary>The rescue's alarm: a jetty guard who has not seen Nam joins the fight at <paramref name="lastSeen"/>.</summary>
        public void JoinRescueCombat(Vector3 lastSeen)
        {
            if (!Alive || !_rescuePost) return;
            var m = Memory; float now = Time.time;
            _hearReactionAt = 0; _stoneLure = false; ApplyLureTuning();
            m.Suspicion = 1f; m.EngagedUntil = now + 10f; m.LastKnownPlayerPosition = lastSeen;
            Brain.BeginInvestigation(lastSeen, now);
        }

        private void ReportDetection(Map01DetectionCause cause, Vector3 position)
        {
            var signal = new Map01Detection(this, cause, position);
            Detected?.Invoke(signal);
            _rescue?.ReportDetection(signal);
        }

        /// <summary>Nam, knife in hand, right behind him, unseen, nothing in between.</summary>
        public bool CanSilentTakedown()
        {
            if (!Alive || Engaged || IsBoss || _mission == null || _mission.Stopped || _mission.ModernCombat == null
                || _mission.ModernCombat.EquippedKind != WeaponKind.Knife) return false;
            var delta = Vector3.ProjectOnPlane(_mission.player.position - transform.position, Vector3.up);
            var layout = _rescue != null ? _rescue.Layout : null;
            bool overseerApproach = _overseer && _rescue != null && _rescue.CurrentPhase == Map01Rescue.Phase.Captive;
            float range = layout != null ? (overseerApproach ? layout.overseerBackstabRange : layout.backstabRange) : 1.6f;
            if (!_overseer && _mission.Skills != null) range = _mission.Skills.TakedownRange(range);
            float rear = layout != null ? (overseerApproach ? layout.overseerBackstabAngle : layout.backstabAngle) : 130f;
            if (Vector3.Distance(_mission.player.position, transform.position) > range) return false;
            if (delta.magnitude > range || Vector3.Angle(transform.forward, delta) < rear) return false;
            var origin = _mission.player.position + Vector3.up;
            return !Physics.Linecast(origin, transform.position + Vector3.up, out var hit, _mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                || hit.transform == transform || hit.transform.IsChildOf(transform);
        }

        /// <summary>The knife takedown: Nam's animation, the guard down without a sound, the cinematic.</summary>
        public bool TrySilentTakedown()
        {
            if (!CanSilentTakedown()) return false;
            if (!Map01NamActions.For(_mission).PlayTakedown(transform)) return false;
            TakenDownSilently = true; SetSilent(true); ShowRifle(false);
            if (_agent.isOnNavMesh) { _agent.ResetPath(); _agent.isStopped = true; }
            _health.TakeDamage(_health.Current, transform.position + Vector3.up, _mission.player.gameObject);
            if (_health.IsDead) _mission.Skills?.Award(ShadowVale.Gameplay.Skills.SkillKind.Stealth);
            EventBus.Publish(new TakedownEvent(LegacyId, transform.position));
            EventBus.Publish(new EnemyKilledEvent(LegacyId, "takedown", "knife", transform.position));
            var cinematic = _mission.GetComponent<Map01RescueCinematic>();
            if (cinematic == null) cinematic = _mission.gameObject.AddComponent<Map01RescueCinematic>();
            cinematic.ShowTakedown(_rescue, this);
            return true;
        }

        // A guard with the hostage in reach checks his line of fire every frame: the rifle is looked up once.
        private Map01Rifle _rifle;
        private Map01Rifle Rifle => _rifle != null && _rifle.gameObject.activeInHierarchy ? _rifle : _rifle = GetComponentInChildren<Map01Rifle>();
        /// <summary>Whether a round at <paramref name="target"/> would reach Hùng and nobody else.</summary>
        public bool SafeShot(Vector3 target)
        {
            var rifle = Rifle;
            Vector3 origin = rifle != null && rifle.weapon != null && rifle.weapon.Muzzle != null ? rifle.weapon.Muzzle.position : transform.position + Vector3.up * 1.3f;
            var end = target + Vector3.up * 1.1f;
            if (Physics.Linecast(origin, end, out var hit, ~0, QueryTriggerInteraction.Ignore))
                return hit.transform == _mission.hung || hit.transform.IsChildOf(_mission.hung);
            return true;
        }

        /// <param name="bodyPoint">Aim at <paramref name="target"/> itself (a bone, in the execution film) rather than his chest above it.</param>
        public void FireAtHostage(Vector3 target, float amount, bool bodyPoint = false)
        {
            FaceImmediate(target);
            if (_animator != null && _animator.runtimeAnimatorController != null) _animator.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            if (_rescue == null || _mission == null || _mission.hung == null) return;
            AudibleShots++;
            if (!FireRound(target + (bodyPoint ? Vector3.zero : Vector3.up * .9f), _mission.hung, out var impact)) return;
            float before = _rescue.HungHealth;
            _rescue.HitHung(amount);
            if (_rescue.HungHealth < before) BloodImpact.Play(impact, impact - transform.position);
        }

        public void FaceImmediate(Vector3 point)
        {
            var flat = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (flat.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(flat);
        }

        /// <summary>Freeze for a cinematic (or a stopped mission); <paramref name="animate"/> keeps his animator running.</summary>
        public void PauseForCinematic(bool animate = false)
        {
            if (!Alive) return;
            if (!_holdingAI)
            {
                _holdingAI = true; _pausedAt = Time.time; _animatorSpeedBeforeHold = _animator != null ? _animator.speed : 1;
                _heldPosition = transform.position; _heldRotation = transform.rotation;
                _agentUpdatesPosition = _agent != null && _agent.updatePosition; _agentUpdatesRotation = _agent != null && _agent.updateRotation;
            }
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            if (_agent != null) { _agent.updatePosition = false; _agent.updateRotation = false; }
            if (_animator != null) _animator.speed = animate ? 1 : 0;
        }

        public bool IsBoss { get; private set; }
        /// <summary>
        /// Turns a regular outpost guard clone into the map's commander: no different model yet
        /// (Map 1's briefing doesn't have one), just a tougher, longer-sighted stand its ground.
        /// </summary>
        public void ConfigureAsBoss(float healthMultiplier, float damageMultiplier, float visionMultiplier = 1.2f)
        {
            IsBoss = true;
            damage *= damageMultiplier;
            visionRange *= visionMultiplier; attackRange *= visionMultiplier;
            var health = GetComponent<Health>();
            if (health != null) health.SetMaxHealth(health.Max * healthMultiplier);
            SquadId = "boss";
            EnemyRegen.Detach(gameObject); // His wounds stay: the fight against him wears him down.
            SyncTuning();
        }

        /// <summary>The commander from his archetype's boss profile (Map01Content.json): his own damage, rate of
        /// fire, reach and eye, where the profile gives them, over a guard's times the multipliers.</summary>
        public void ConfigureAsBoss(ShadowVale.Data.Content.BossProfile profile)
        {
            ConfigureAsBoss(profile.HealthMultiplier, profile.DamageMultiplier, profile.VisionMultiplier);
            if (profile.DamagePerRound > 0) damage = profile.DamagePerRound;
            if (profile.FireIntervalSeconds > 0) fireInterval = profile.FireIntervalSeconds;
            if (profile.AttackRange > 0) attackRange = profile.AttackRange;
            if (profile.DetectionSeconds > 0) _baseDetectionSeconds = _tuning.DetectionSeconds = profile.DetectionSeconds;
            ConfigureBursts(profile.BurstRounds, profile.BurstGapSeconds, profile.BurstIntervalSeconds);
            SyncTuning();
        }

        /// <summary>The commander's body from his archetype (boss_commander): walking speed and sight. 0 keeps the guard's.</summary>
        public void ApplyBossArchetype(float moveSpeed, float vision, float visionAngleDegrees)
        {
            EnsureBrain();
            if (moveSpeed > 0 && _agent != null) _agent.speed = _body.BaseSpeed = moveSpeed;
            if (vision > 0) visionRange = vision;
            if (visionAngleDegrees > 0) visionAngle = visionAngleDegrees;
            SyncTuning();
        }

        public float VisionAngle => visionAngle;
        public float AttackRange => attackRange;
        public float DetectionSeconds => _tuning.DetectionSeconds;
        public float MoveSpeed => _body != null ? _body.BaseSpeed : 0f;

        /// <summary>Fires <paramref name="rounds"/> at a time, <paramref name="gap"/> apart, <paramref name="interval"/> from one burst to the next.</summary>
        public void ConfigureBursts(int rounds, float gap, float interval)
        {
            _burstRounds = Mathf.Max(1, rounds); _burstLeft = _burstRounds;
            _burstGap = Mathf.Max(.02f, gap); _burstInterval = interval;
        }

        public int BurstRounds => _burstRounds;

        /// <summary>Until the next round may go: inside a burst the gap, after its last round the pause.</summary>
        private float NextShotDelay()
        {
            if (_burstRounds <= 1) return RifleItemId == Map01M16.Id ? Map01M16.ShotSeconds : fireInterval;
            if (--_burstLeft > 0) return RifleItemId == Map01M16.Id ? Map01M16.ShotSeconds : _burstGap;
            _burstLeft = _burstRounds;
            return _burstInterval > 0 ? _burstInterval : fireInterval;
        }

        private void ScheduleNextShot()
        {
            float delay = NextShotDelay();
            _nextShot = RifleItemId == Map01M16.Id && Time.time - _nextShot <= delay
                ? _nextShot + delay : Time.time + delay;
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
                string weapon = _mission != null && _mission.ModernCombat != null ? _mission.ModernCombat.EquippedKind.ToString().ToLowerInvariant() : "";
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

        // ---- IEnemyWeapon: how a Map 1 guard fires ----------------------------------------------

        /// <summary>The old name of <see cref="FireAtPlayer"/>, kept for tests that still call it.</summary>
        private void Shoot() => FireAtPlayer();

        public void FireAtPlayer()
        {
            if (Time.time < _nextShot || _player == null) return;
            ScheduleNextShot();
            AudibleShots++;
            _animator?.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            // Hùng is in the line of fire while he is being held or walked home: every third
            // round goes his way when he is in reach.
            if (++_shots % 3 == 0 && HostageInReach(out var hostage) && SafeShot(hostage)) { FireAtHostage(hostage, damage * (RifleItemId == Map01M16.Id ? .9f : 1f)); return; }
            Vector3 aim = _player.position + Vector3.up;
            // Not every round finds him (ShotAccuracy): a miss flies past him, tracer and all.
            var shot = ShotAccuracy.For(transform.position, _agent != null ? _agent.velocity : Vector3.zero, attackRange,
                _aim.Shot(Time.time), IsBoss, _player.position, _playerView, _playerController);
            _shotRng ??= ShotAccuracy.RngFor(LegacyId);
            if (_shotRng.NextDouble() >= ShotAccuracy.HitChance(_accuracy, shot))
            {
                Vector3 chest = transform.position + Vector3.up * 1.3f;
                FireRound(ShotAccuracy.MissPoint(chest, aim, _shotRng), null, out _);
                return;
            }
            if (FireRound(aim, _player, out var impact))
                _playerHealth.TakeBulletDamage(damage * (RifleItemId == Map01M16.Id ? .9f : 1f), impact, gameObject, impact - transform.position);
        }

        /// <summary>Rounds at the spot Nam was last seen: noise and muzzle flash, no hit.</summary>
        public void FireSuppressive(Vector3 point)
        {
            if (Time.time < _nextShot) return;
            ScheduleNextShot();
            AudibleShots++;
            _animator?.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            FireRound(point + Vector3.up, null, out _);
        }

        private static AudioClip rifleReport;
        /// <summary>The guards' rifle report: the AK of the extraction film, not Nam's own gunshot.</summary>
        private static AudioClip RifleReport => rifleReport != null ? rifleReport
            : rifleReport = Resources.Load<Map01Extraction>("Cutscenes/Map01Extraction")?.rifleAudio;
        private Light _muzzleFlash;
        private float _flashUntil;

        /// <summary>One round through the shared feedback (tracer, report heard within 65 m), with his rifle's muzzle flash.</summary>
        private bool FireRound(Vector3 at, Transform victim, out Vector3 impact)
        {
            _feedback ??= EnemyFeedback.Attach(gameObject);
            _feedback.GunshotClip ??= RifleReport;
            bool reached = _feedback.Fire(at, victim, obstructionMask, out impact);
            var muzzle = GetComponentInChildren<Map01Rifle>()?.weapon?.Muzzle;
            if (muzzle != null)
            {
                if (_muzzleFlash == null)
                {
                    var go = new GameObject("Rifle muzzle flash"); go.transform.SetParent(muzzle, false);
                    _muzzleFlash = go.AddComponent<Light>(); _muzzleFlash.color = new Color(1f, .7f, .25f); _muzzleFlash.range = 1.5f; _muzzleFlash.intensity = 1.6f;
                }
                _muzzleFlash.enabled = true; _flashUntil = Time.time + .045f;
            }
            return reached;
        }

        // ---- IEnemyTactics: Map 1's rules on top of the shared FSM ------------------------------

        /// <summary>
        /// Engaged but Nam out of sight: with Hùng in reach and a clear line, the guard turns on him
        /// instead, at half the rate — Nam hiding does not keep Hùng safe.
        /// </summary>
        public bool TryGetAlternativeTarget(EnemyContext ctx, out Vector3 target) => HostageInReach(out target) && SafeShot(target);

        public void FireAtAlternative(EnemyContext ctx, Vector3 target)
        {
            if (Time.time < _nextShot) return;
            _nextShot = Time.time + (RifleItemId == Map01M16.Id ? Map01M16.ShotSeconds : fireInterval) * 2f;
            FireAtHostage(target, damage * (RifleItemId == Map01M16.Id ? .9f : 1f));
        }

        /// <summary>The nearest of Map 1's placed cover points, if one is close enough to reach under fire.</summary>
        public bool TryFindCover(EnemyContext ctx, Vector3 threat, out Vector3 cover)
        {
            cover = default;
            if (_mission == null) return false;
            float best = CoverSearchRadius * CoverSearchRadius;
            bool found = false;
            foreach (var point in _mission.CoverPoints)
            {
                if (point == null) continue;
                Vector3 p = point.transform.position;
                float d = (p - transform.position).sqrMagnitude;
                if (d >= best || (p - threat).sqrMagnitude < 16f) continue; // Not into Nam's arms.
                best = d; cover = p; found = true;
            }
            return found;
        }

        public bool TryFindRetreatPoint(EnemyContext ctx, Vector3 threat, out Vector3 point)
            => DefaultEnemyTactics.Instance.TryFindRetreatPoint(ctx, threat, out point);

        private bool HostageInReach(out Vector3 hostage)
        {
            hostage = default;
            if (_rescue == null || !_rescue.Exposed || _mission.hung == null) return false;
            hostage = _mission.hung.position;
            Vector3 origin = transform.position + Vector3.up * 1.3f, target = hostage + Vector3.up * 1.1f;
            if (Vector3.Distance(origin, target) > attackRange) return false;
            return !Physics.Linecast(origin, target, out var hit, _mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                || hit.transform == _mission.hung || hit.transform.IsChildOf(_mission.hung);
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
