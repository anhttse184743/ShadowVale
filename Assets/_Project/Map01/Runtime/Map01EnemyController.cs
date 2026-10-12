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
        private EnemyBrain _brain;
        private readonly EnemyTuning _tuning = new EnemyTuning();
        private float _nextShot;
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

        public void BindMission(Map01Mission mission)
        {
            _mission = mission;
            Map01Rifle.Attach(GetComponentInChildren<Animator>());
            _feedback = EnemyFeedback.Attach(gameObject, mission.player.GetComponent<PlayerController>());
            _feedback.CanStep = () => enabled && !_holdingAI && _mission != null && !_mission.Stopped;
            GetComponent<Health>().KeepCheckpointCorpse();
            // Map 1's guard tuning lives in Map01Balance.json and Map01Content.json, not in each
            // guard's serialized fields.
            damage = mission.Weapon.damage * mission.Settings.guardDamageScale;
            fireInterval = mission.Settings.guardShotInterval;
            _accuracy = mission.Settings.guardAccuracy ?? new ShotAccuracyProfile();
            _baseDetectionSeconds = _tuning.DetectionSeconds = Mathf.Max(.1f, mission.Settings.detectionSeconds);
            _hiddenScale = mission.Settings.hiddenVisionScale;
            _crouchScale = mission.Settings.crouchVisionScale;
            _baseSearchSeconds = _tuning.SearchSeconds = mission.Settings.searchSeconds > 0 ? mission.Settings.searchSeconds : 8f;
            _tuning.SuppressSeconds = Mathf.Max(0f, mission.Settings.suppressSeconds);
            _tuning.RetreatHealthFraction = Mathf.Clamp01(mission.GuardData.retreat_hp_threshold);
            _rescue = mission.GetComponent<Map01Rescue>(); // Added by Map01Quest.Awake, before this runs.
            mission.Noise.Register(this);
            SyncTuning();
        }

        private void OnDestroy()
        {
            if (_mission != null) _mission.Noise.Unregister(this);
        }

        /// <summary>
        /// Back to the post at full health and calm — a failed scouting run means the camp was
        /// reinforced, so even a guard Nam took down is replaced, loot and all. Calm for a few
        /// seconds so Nam gets to slip away instead of being re-spotted on the spot.
        /// </summary>
        public void ReturnToPost()
        {
            TakenDownSilently = false; SetSilent(false); ShowRifle(true); _startled = false;
            if (_health.IsDead) _health.Revive(); else _health.RestoreHealth(_health.Max);
            if (_lootCreated)
            {
                _lootCreated = false;
                var loot = GetComponent<ForestPoint>();
                if (loot != null) { _mission.UnregisterPoint(loot); Destroy(loot); } // _lootCreated implies a bound mission.
            }
            _lastHealth = _health.Current;
            _body.Warp(_postPosition);
            transform.rotation = _postRotation;
            _hearReactionAt = 0; _stoneLure = false;
            Brain.ResetToPost(Time.time + 4f);
            _sense.Reset();
            _body.SetPace(1f);
            if (patrolPoints.Length > 0) _body.MoveTo(patrolPoints[0]);
        }
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
            bool stealth = RescueStealth;
            if (stealth && kind == NoiseKind.Footstep && _mission.Crouched)
                radius *= _rescueTuning != null ? _rescueTuning.crouchedFootstepScale : .55f;
            if (!Alive || !HearingSensor.Hears(transform.position, position, radius)) return false;
            if (_mission != null && _mission.Cinematic) return false;
            if (kind == NoiseKind.Gunshot) ReportDetection(Map01DetectionCause.Gunshot, position);
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
            _stoneLure = stone && _overseer;
            ApplyLureTuning();
            Brain.BeginInvestigation(position, Time.time);
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
            public Vector3 position, destination, investigate, returnPoint;
            public Quaternion rotation, returnRotation;
            public float hp, shotRemaining, alertRemaining, searchRemaining;
            public int patrolIndex;
            public bool stopped, returning, silent, pursuer;
            public float suspicion, engagedRemaining, pauseRemaining;
            public float footstepReactionRemaining;
            public Vector3 pendingFootstep;
            public bool stoneLure;
            public Quaternion stoneFacing;
        }
        public Snapshot Capture()
        {
            var m = Memory; float now = Time.time;
            return new Snapshot {
                id = SaveId, position = transform.position, rotation = transform.rotation, hp = _health.Current,
                silent = TakenDownSilently, pursuer = _pursuer, suspicion = m.Suspicion,
                engagedRemaining = Mathf.Max(0, m.EngagedUntil - now), pauseRemaining = Mathf.Max(0, m.PatrolPauseUntil - now),
                footstepReactionRemaining = _hearReactionAt > 0 ? Mathf.Max(0, _hearReactionAt - now) : 0,
                pendingFootstep = _pendingFootstep, stoneLure = _stoneLure, stoneFacing = _stoneFacing,
                patrolIndex = m.PatrolIndex, shotRemaining = Mathf.Max(0, _nextShot - now),
                alertRemaining = Mathf.Max(0, m.AlertUntil - now), investigate = m.InvestigatePoint,
                searchRemaining = m.SearchUntil > 0 ? Mathf.Max(.01f, m.SearchUntil - now) : 0,
                returning = m.Returning, returnPoint = m.ReturnPoint, returnRotation = m.ReturnRotation,
                destination = _agent.isOnNavMesh && _agent.hasPath ? _agent.destination : transform.position,
                stopped = _agent.isOnNavMesh && _agent.isStopped
            };
        }
        /// <summary>This guard as he would be fresh at his post: alive, calm, starting his patrol.</summary>
        public Snapshot CaptureAtPost() => new Snapshot {
            id = SaveId, position = _postPosition, rotation = _postRotation, hp = _health.Max,
            destination = _postPosition, patrolIndex = 0
        };
        public void RestoreSnapshot(Snapshot saved)
        {
            var m = Memory; float now = Time.time;
            _body.Warp(saved.position);
            transform.rotation = saved.rotation;
            TakenDownSilently = saved.silent; SetSilent(saved.silent); ShowRifle(!saved.silent);
            _health.RestoreHealth(saved.hp);
            _lastHealth = _health.Current;
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
            if (!Alive) CreateLoot();
        }

        /// <summary>A hit in the extraction film: real health and death, but no extra loot or takedown credit.</summary>
        public void ReceiveExtractionHit(float amount, Vector3 point)
        {
            if (!Alive) return;
            if (amount >= _health.Current) _lootCreated = true;
            _health.TakeDamage(amount, point, null); _lastHealth = _health.Current;
            if (_health.IsDead && _agent.isOnNavMesh) { _agent.ResetPath(); _agent.isStopped = true; }
        }

        private void CreateLoot()
        {
            if (_lootCreated || _mission == null) return;
            _lootCreated = true;
            var loot = gameObject.AddComponent<ForestPoint>();
            loot.id = "loot_" + LegacyId; loot.label = "Túi lính tuần tra"; loot.kind = ForestPointKind.Loot;
            loot.items = new[] {
                new ForestIngredient { item_id = "ammo_rifle", count = _mission.Settings.guardDropAmmo },
                new ForestIngredient { item_id = "medkit_small", count = _mission.Settings.guardDropMedkits }
            };
            _mission.RegisterPoint(loot);
        }

        public void Configure(Vector3[] points)
        {
            patrolPoints = points ?? System.Array.Empty<Vector3>();
            if (_brain != null) _brain.Memory.PatrolPoints = patrolPoints;
        }

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
            _holdingAI = false; _agent.updatePosition = true; _agent.updateRotation = true;
            _postPosition = post; _postRotation = Quaternion.identity; Configure(System.Array.Empty<Vector3>()); BindMission(owner);
            _health.CinematicInvulnerable = false; _health.Revive(); _lastHealth = _health.Current;
            TakenDownSilently = false; SetSilent(false); ShowRifle(true);
            _hearReactionAt = 0; _stoneLure = false; ApplyLureTuning();
            Brain.ResetToPost(0f);
            if (_animator != null) _animator.speed = 1;
            if (_agent.isOnNavMesh) { _agent.Warp(post); _agent.isStopped = false; }
            if (pursue) BeginInvestigation(owner.hung.position);
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
            EventBus.Publish(new TakedownEvent(LegacyId, transform.position));
            EventBus.Publish(new EnemyKilledEvent(LegacyId, "takedown", "knife", transform.position));
            var cinematic = _mission.GetComponent<Map01RescueCinematic>();
            if (cinematic == null) cinematic = _mission.gameObject.AddComponent<Map01RescueCinematic>();
            cinematic.ShowTakedown(_rescue, this);
            return true;
        }

        /// <summary>Whether a round at <paramref name="target"/> would reach Hùng and nobody else.</summary>
        public bool SafeShot(Vector3 target)
        {
            var rifle = GetComponentInChildren<Map01Rifle>();
            Vector3 origin = rifle != null && rifle.weapon != null && rifle.weapon.Muzzle != null ? rifle.weapon.Muzzle.position : transform.position + Vector3.up * 1.3f;
            var end = target + Vector3.up * 1.1f;
            if (Physics.Linecast(origin, end, out var hit, ~0, QueryTriggerInteraction.Ignore))
                return hit.transform == _mission.hung || hit.transform.IsChildOf(_mission.hung);
            return true;
        }

        public void FireAtHostage(Vector3 target, float amount)
        {
            FaceImmediate(target); _animator?.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            if (_rescue == null || _mission == null) return;
            _feedback ??= EnemyFeedback.Attach(gameObject);
            if (!_feedback.Fire(target + Vector3.up * .9f, _mission.hung, obstructionMask, out var impact)) return;
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
            SyncTuning();
        }

        /// <summary>The commander from his archetype's boss profile (Map01Content.json).</summary>
        public void ConfigureAsBoss(ShadowVale.Data.Content.BossProfile profile)
            => ConfigureAsBoss(profile.HealthMultiplier, profile.DamageMultiplier, profile.VisionMultiplier);

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
            _baseInvestigateArrive = _tuning.InvestigateArriveDistance;
            _brain = new EnemyBrain(LegacyId, _body, this, _tuning, this);
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
            _tuning.SuspicionCloseGain = stealth ? 1.4f : 3f;

            // Calm guards look 10 times a second, staggered; alert ones every frame (PerceptionSchedule).
            bool alert = Engaged || _sawPlayer || Memory.Suspicion > 0f;
            if (_sense.Due(Time.time, alert, LegacyId)) _sawPlayer = CanSeePlayer(out _lastDistance);
            bool sees = _sawPlayer;
            float distance = _lastDistance;
            _brain.Tick(new PerceptionSample(sees, distance, _player != null ? _player.position : transform.position),
                Time.time, Time.deltaTime);

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
            if (_pursuer && _rescue != null && _rescue.Exposed && !sees && !(Engaged && HostageInReach(out _)))
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
            if (m.SearchUntil > 0) m.SearchUntil += delay;
            if (_animator != null) _animator.speed = _animatorSpeedBeforeHold;
            _agent.updatePosition = _agentUpdatesPosition;
            _agent.updateRotation = _agentUpdatesRotation;
            if (_agent.isOnNavMesh) { _agent.nextPosition = transform.position; _agent.isStopped = !Alive; }
            _holdingAI = false;
        }

        /// <summary>
        /// Hurt — by a weapon: the knife takedown is its own action (<see cref="TrySilentTakedown"/>).
        /// A hit he survives tells him where it came from, and raises the rescue's alarm.
        /// </summary>
        private void OnHurt()
        {
            HurtCount++;
            if (TakenDownSilently || _player == null) return;
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

        public void FireAtPlayer()
        {
            if (Time.time < _nextShot || _player == null) return;
            _nextShot = Time.time + fireInterval;
            _animator?.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            // Hùng is in the line of fire while he is being held or walked home: every third
            // round goes his way when he is in reach.
            if (++_shots % 3 == 0 && HostageInReach(out var hostage) && SafeShot(hostage)) { FireAtHostage(hostage, damage); return; }
            _feedback ??= EnemyFeedback.Attach(gameObject);
            Vector3 aim = _player.position + Vector3.up;
            // Not every round finds him (ShotAccuracy): a miss flies past him, tracer and all.
            var shot = ShotAccuracy.For(transform.position, _agent != null ? _agent.velocity : Vector3.zero, attackRange,
                _aim.Shot(Time.time), IsBoss, _player.position, _playerView, _playerController);
            _shotRng ??= ShotAccuracy.RngFor(LegacyId);
            if (_shotRng.NextDouble() >= ShotAccuracy.HitChance(_accuracy, shot))
            {
                Vector3 chest = transform.position + Vector3.up * 1.3f;
                _feedback.Fire(ShotAccuracy.MissPoint(chest, aim, _shotRng), null, obstructionMask, out _);
                return;
            }
            if (_feedback.Fire(aim, _player, obstructionMask, out var impact))
                _playerHealth.TakeBulletDamage(damage, impact, gameObject, impact - transform.position);
        }

        /// <summary>Rounds at the spot Nam was last seen: noise and muzzle flash, no hit.</summary>
        public void FireSuppressive(Vector3 point)
        {
            if (Time.time < _nextShot) return;
            _nextShot = Time.time + fireInterval;
            _animator?.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            _feedback ??= EnemyFeedback.Attach(gameObject);
            _feedback.Fire(point + Vector3.up, null, obstructionMask, out _);
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
            _nextShot = Time.time + fireInterval * 2f;
            FireAtHostage(target, damage);
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
