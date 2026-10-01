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
    /// its balance numbers, Hùng as a second target, the map's cover points, loot, the knife
    /// takedown, save/restore and the reset after a failed scouting run. Its public API is what
    /// the rest of Map 1 and its tests rely on.
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
        private IPlayerObservable _playerView;
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
        /// <summary>The last hurt was a silent knife takedown from behind.</summary>
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

        /// <summary>Treat him as fighting Nam for <paramref name="seconds"/> (debug console, tests).</summary>
        public void EngageFor(float seconds) => Memory.EngagedUntil = Time.time + seconds;

        public void BindMission(Map01Mission mission)
        {
            _mission = mission;
            GetComponent<Health>().KeepCheckpointCorpse();
            // Map 1's guard tuning lives in Map01Balance.json and Map01Content.json, not in each
            // guard's serialized fields.
            damage = mission.Weapon.damage * mission.Settings.guardDamageScale;
            fireInterval = mission.Settings.guardShotInterval;
            _tuning.DetectionSeconds = Mathf.Max(.1f, mission.Settings.detectionSeconds);
            _hiddenScale = mission.Settings.hiddenVisionScale;
            _crouchScale = mission.Settings.crouchVisionScale;
            _tuning.SearchSeconds = mission.Settings.searchSeconds > 0 ? mission.Settings.searchSeconds : 8f;
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
        public bool Hear(Vector3 position, float radius)
        {
            if (!Alive || !HearingSensor.Hears(transform.position, position, radius)) return false;
            Brain.BeginInvestigation(position, Time.time);
            return true;
        }

        bool INoiseListener.OnNoise(Vector3 position, float radius, NoiseKind kind) => Hear(position, radius);

        [System.Serializable] public sealed class Snapshot
        {
            public string id;
            public Vector3 position, destination, investigate, returnPoint;
            public Quaternion rotation, returnRotation;
            public float hp, shotRemaining, alertRemaining, searchRemaining;
            public int patrolIndex;
            public bool stopped, returning;
        }
        public Snapshot Capture()
        {
            var m = Memory; float now = Time.time;
            return new Snapshot {
                id = SaveId, position = transform.position, rotation = transform.rotation, hp = _health.Current,
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
            _health.RestoreHealth(saved.hp);
            _lastHealth = _health.Current;
            _sense.Reset();
            m.PatrolIndex = Mathf.Clamp(saved.patrolIndex, 0, Mathf.Max(0, patrolPoints.Length - 1));
            _nextShot = now + saved.shotRemaining;
            m.AlertUntil = now + saved.alertRemaining; m.InvestigatePoint = saved.investigate;
            m.SearchUntil = saved.searchRemaining > 0 ? now + saved.searchRemaining : 0;
            m.Returning = saved.returning; m.ReturnPoint = saved.returnPoint; m.ReturnRotation = saved.returnRotation;
            _body.MoveTo(saved.destination);
            if (_agent.isOnNavMesh) _agent.isStopped = saved.stopped || !Alive;
            if (!Alive) CreateLoot();
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
                _playerHealth = controller.GetComponent<Health>();
                _playerView = PlayerStealthAdapter.For(controller.gameObject);
            }
            if (_animator != null)
            {
                int upperBody = _animator.GetLayerIndex(PlayerCombat.UpperBodyLayer);
                if (upperBody > 0) _animator.SetLayerWeight(upperBody, 1f);
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
            if (_mission != null && _mission.Stopped) {
                _body.Stop();
                _body.Animate(false); return;
            }
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

            // Calm guards look 10 times a second, staggered; alert ones every frame (PerceptionSchedule).
            bool alert = Engaged || _sawPlayer || Memory.Suspicion > 0f;
            if (_sense.Due(Time.time, alert, LegacyId)) _sawPlayer = CanSeePlayer(out _lastDistance);
            bool sees = _sawPlayer;
            float distance = _lastDistance;
            _brain.Tick(new PerceptionSample(sees, distance, _player != null ? _player.position : transform.position),
                Time.time, Time.deltaTime);
            if (sees && Engaged && _mission != null) _mission.Alarmed = true;
        }

        /// <summary>
        /// Hurt. A knife from behind before he has spotted Nam is a silent takedown — even while
        /// he is off checking a noise, which is what a thrown stone is for. Anything else, and he
        /// knows where it came from.
        /// </summary>
        private void OnHurt()
        {
            HurtCount++;
            TakenDownSilently = false;
            if (_player == null) return;
            bool knife = _mission != null && _mission.ModernCombat != null && _mission.ModernCombat.EquippedKind == WeaponKind.Knife;
            if (TakedownRule.IsSilent(transform.position, transform.forward, _player.position, knife, Engaged, IsBoss))
            {
                TakenDownSilently = true;
                if (!_health.IsDead) _health.TakeDamage(_health.Current, transform.position + Vector3.up, _player.gameObject);
                EventBus.Publish(new TakedownEvent(LegacyId, transform.position));
                EventBus.Publish(new EnemyKilledEvent(LegacyId, "takedown", "knife", transform.position));
                return;
            }
            if (_health.IsDead) // Killed outright — by a gun, say: loud, and nothing left to react.
            {
                string weapon = _mission != null && _mission.ModernCombat != null ? _mission.ModernCombat.EquippedKind.ToString().ToLowerInvariant() : "";
                EventBus.Publish(new EnemyKilledEvent(LegacyId, "weapon", weapon, transform.position));
                return;
            }
            _brain.OnHurtByPlayer(_player.position, Time.time);
        }

        /// <summary>How this guard sees; rebuilt on demand so the boss's longer sight applies at once.</summary>
        private VisionProfile Vision => new VisionProfile(visionRange, visionAngle, _crouchScale, _hiddenScale);

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
            if (++_shots % 3 == 0 && HostageInReach(out _)) { _rescue.HitHung(damage); return; }
            _playerHealth.TakeDamage(damage, _player.position + Vector3.up, gameObject);
        }

        /// <summary>Rounds at the spot Nam was last seen: noise and muzzle flash, no hit.</summary>
        public void FireSuppressive(Vector3 point)
        {
            if (Time.time < _nextShot) return;
            _nextShot = Time.time + fireInterval;
            _animator?.SetTrigger(PlayerCombat.AnimatorParams.Attack);
        }

        // ---- IEnemyTactics: Map 1's rules on top of the shared FSM ------------------------------

        /// <summary>
        /// Engaged but Nam out of sight: with Hùng in reach, the guard turns on him instead, at
        /// half the rate — Nam hiding does not keep Hùng safe, it hands the squad an easier target.
        /// </summary>
        public bool TryGetAlternativeTarget(EnemyContext ctx, out Vector3 target) => HostageInReach(out target);

        public void FireAtAlternative(EnemyContext ctx, Vector3 target)
        {
            if (Time.time < _nextShot) return;
            _nextShot = Time.time + fireInterval * 2f;
            _animator?.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            _rescue.HitHung(damage);
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
    }
}
