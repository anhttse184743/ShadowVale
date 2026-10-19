using ShadowVale.AI.Agents;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using ShadowVale.Core.Services;
using ShadowVale.Data.Content;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Gameplay.Enemies
{
    /// <summary>
    /// A ready-made enemy for any map: the shared six-state FSM, sight and hearing, squad membership
    /// for the coordinator, and a plain hitscan rifle — configured from an <see cref="EnemyArchetype"/>
    /// of the content bundle, nothing map-specific. Map 1 keeps its own <c>Map01EnemyController</c>
    /// (hostage, loot, save format); a new map can start from this one and add its rules through
    /// <see cref="Tactics"/> instead of copying a controller.
    /// Needs a <see cref="NavMeshAgent"/> and a <see cref="Health"/>; sees the player through the
    /// <see cref="IPlayerObservable"/> the map registers, hears through its <see cref="NoiseSystem"/>.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public sealed class EnemyAgent : MonoBehaviour, IEnemyAgent, IEnemyWeapon, INoiseListener
    {
        [SerializeField] private string squadId = "";
        [SerializeField] private float visionRange = 18f, visionAngle = 100f, hearingScale = 1f;
        [SerializeField] private float attackRange = 18f, damage = 4f, fireInterval = 1.2f;
        [SerializeField] private LayerMask obstructionMask = ~0;
        [SerializeField] private ShotAccuracyProfile accuracy = new ShotAccuracyProfile();

        private NavMeshAgent _navAgent;
        private Health _health;
        private NavMeshEnemyBody _body;
        private EnemyBrain _brain;
        private readonly EnemyTuning _tuning = new EnemyTuning();
        private PerceptionSchedule _sense;
        private bool _sawPlayer;
        private float _lastDistance = float.PositiveInfinity, _lastHealth, _nextShot;
        private NoiseSystem _noise;
        private EnemyFeedback _feedback;
        private readonly AimTracker _aim = new AimTracker();
        private System.Random _shotRng;

        /// <summary>How well he shoots; tests that isolate damage turn it off.</summary>
        public ShotAccuracyProfile Accuracy { get => accuracy; set => accuracy = value ?? new ShotAccuracyProfile(); }
        /// <summary>The map's rules on top of the shared FSM (alternative targets, cover, retreat).</summary>
        public IEnemyTactics Tactics { get; set; }
        /// <summary>Who this enemy shoots: by default the IDamageable on the registered player.</summary>
        public IDamageable Target { get; set; }

        public string Id => name;
        public string SquadId { get => squadId; set => squadId = value ?? ""; }
        public EnemyBrain Brain { get { EnsureBrain(); return _brain; } }
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public bool Alive => _health != null && !_health.IsDead;
        public float HealthFraction => _health != null && _health.Max > 0 ? _health.Current / _health.Max : 0f;
        public bool SeesPlayer => _sawPlayer;
        public float VisionRange => visionRange;
        public float VisionConeAngle => visionAngle;
        public EnemyStateId State => Brain.State;

        /// <summary>Numbers from the content bundle's archetype; the weapon's from its weapon definition if given.</summary>
        public EnemyAgent Configure(EnemyArchetype archetype, string squad, WeaponDefinition weapon = null)
        {
            EnsureBrain();
            SquadId = squad;
            if (archetype != null)
            {
                visionRange = archetype.VisionRange > 0 ? archetype.VisionRange : visionRange;
                visionAngle = archetype.VisionAngle > 0 ? archetype.VisionAngle : visionAngle;
                hearingScale = archetype.HearingRange > 0 ? archetype.HearingRange / 12f : hearingScale; // 12 m = the grunt's, the reference.
                _tuning.RetreatHealthFraction = Mathf.Clamp01(archetype.RetreatHpThreshold);
                _tuning.IsBoss = archetype.IsBoss;
                if (archetype.MaxHp > 0) _health.SetMaxHealth(archetype.MaxHp);
                if (archetype.MoveSpeed > 0) _navAgent.speed = archetype.MoveSpeed;
            }
            if (weapon != null)
            {
                if (weapon.Damage > 0) damage = weapon.Damage;
                if (weapon.FireRate > 0) fireInterval = 1f / weapon.FireRate;
                if (weapon.Range > 0) attackRange = weapon.Range;
            }
            _tuning.VisionRange = visionRange;
            _tuning.AttackRange = attackRange;
            _lastHealth = _health.Current;
            // Left alone for a while, a wounded enemy recovers; a commander's wounds stay.
            if (_tuning.IsBoss) EnemyRegen.Detach(gameObject);
            else EnemyRegen.Attach(gameObject, Id).Suspended = () => !enabled;
            return this;
        }

        private void Awake()
        {
            EnsureBrain();
            _feedback = EnemyFeedback.Attach(gameObject);
            _feedback.CanStep = () => enabled && Alive && _navAgent.enabled && _navAgent.isOnNavMesh && !_navAgent.isStopped;
        }

        private void EnsureBrain()
        {
            if (_brain != null) return;
            _navAgent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _body = new NavMeshEnemyBody(transform, _navAgent, GetComponentInChildren<Animator>(), _health);
            _tuning.VisionRange = visionRange;
            _tuning.AttackRange = attackRange;
            _brain = new EnemyBrain(name, _body, this, _tuning, new TacticsRelay(this));
            _lastHealth = _health.Current;
        }

        private void OnEnable()
        {
            AgentRegistry.Register(this);
            if (ServiceLocator.TryResolve(out _noise)) _noise.Register(this);
        }

        private void OnDisable()
        {
            AgentRegistry.Unregister(this);
            _noise?.Unregister(this);
        }

        private void Update()
        {
            if (_health.Current < _lastHealth) OnHurt();
            _lastHealth = _health.Current;
            if (_health.IsDead) { _body.Stop(); _body.Animate(false); _sawPlayer = false; return; }
            ServiceLocator.TryResolve<IPlayerObservable>(out var player);
            bool alert = Brain.Memory.Engaged(Time.time) || _sawPlayer || Brain.Memory.Suspicion > 0f;
            if (_sense.Due(Time.time, alert, Id))
                _sawPlayer = VisionSensor.CanSee(transform.position, transform.forward, Brain.Memory.Engaged(Time.time),
                    new VisionProfile(visionRange, visionAngle), player, null, obstructionMask, out _lastDistance);
            _brain.Tick(new PerceptionSample(_sawPlayer, _lastDistance, player != null ? player.Position : transform.position),
                Time.time, Time.deltaTime);
        }

        private void OnHurt()
        {
            if (_health.IsDead)
            {
                EventBus.Publish(new EnemyKilledEvent(Id, "weapon", "", transform.position));
                return;
            }
            if (ServiceLocator.TryResolve<IPlayerObservable>(out var player)) _brain.OnHurtByPlayer(player.Position, Time.time);
        }

        // ---- INoiseListener -----------------------------------------------------------------------

        public bool OnNoise(Vector3 position, float radius, NoiseKind kind)
        {
            if (!Alive || !HearingSensor.Hears(transform.position, position, radius, hearingScale)) return false;
            Brain.BeginInvestigation(position, Time.time);
            return true;
        }

        // ---- IEnemyWeapon: a plain hitscan rifle --------------------------------------------------

        public void FireAtPlayer()
        {
            if (Time.time < _nextShot) return;
            if (_feedback == null) _feedback = EnemyFeedback.Attach(gameObject);
            _nextShot = Time.time + fireInterval;
            var target = Target ?? ResolvePlayerTarget();
            if (target != null && !target.IsDead && ServiceLocator.TryResolve<IPlayerObservable>(out var player))
            {
                var victim = target as Component;
                Vector3 aim = player.Position + Vector3.up;
                var controller = player is Component pc ? pc.GetComponentInParent<ShadowVale.Gameplay.Player.PlayerController>() : null;
                var shot = ShotAccuracy.For(transform.position, _navAgent != null ? _navAgent.velocity : Vector3.zero, attackRange,
                    _aim.Shot(Time.time), _tuning.IsBoss, player.Position, player, controller);
                _shotRng ??= ShotAccuracy.RngFor(Id);
                if (_shotRng.NextDouble() >= ShotAccuracy.HitChance(accuracy, shot))
                {
                    _feedback.Fire(ShotAccuracy.MissPoint(transform.position + Vector3.up * 1.3f, aim, _shotRng), null, obstructionMask, out _);
                    return;
                }
                if (_feedback.Fire(aim, victim != null ? victim.transform : null,
                    obstructionMask, out var impact))
                {
                    if (target is Health health) health.TakeBulletDamage(damage, impact, gameObject, impact - transform.position);
                    else target.TakeDamage(damage, impact, gameObject);
                }
            }
        }

        public void FireSuppressive(Vector3 point)
        {
            if (Time.time < _nextShot) return;
            if (_feedback == null) _feedback = EnemyFeedback.Attach(gameObject);
            _nextShot = Time.time + fireInterval;
            _feedback.Fire(point + Vector3.up, null, obstructionMask, out _);
        }

        private static IDamageable ResolvePlayerTarget() =>
            ServiceLocator.TryResolve<IPlayerObservable>(out var player) && player is Component c ? c.GetComponentInParent<IDamageable>() : null;

        // ---- IEnemyAgent debug hooks ----------------------------------------------------------------

        public void DebugEngage(float seconds) => Brain.Memory.EngagedUntil = Time.time + seconds;
        public void DebugDamage(float fractionOfMax) => _health.TakeDamage(_health.Max * fractionOfMax, transform.position + Vector3.up, null);

        /// <summary>Forwards to <see cref="Tactics"/>, which may be set after the brain exists.</summary>
        private sealed class TacticsRelay : IEnemyTactics
        {
            private readonly EnemyAgent _owner;
            public TacticsRelay(EnemyAgent owner) { _owner = owner; }
            private IEnemyTactics T => _owner.Tactics ?? DefaultEnemyTactics.Instance;
            public bool TryGetAlternativeTarget(EnemyContext ctx, out Vector3 target) => T.TryGetAlternativeTarget(ctx, out target);
            public void FireAtAlternative(EnemyContext ctx, Vector3 target) => T.FireAtAlternative(ctx, target);
            public bool TryFindCover(EnemyContext ctx, Vector3 threat, out Vector3 cover) => T.TryFindCover(ctx, threat, out cover);
            public bool TryFindRetreatPoint(EnemyContext ctx, Vector3 threat, out Vector3 point) => T.TryFindRetreatPoint(ctx, threat, out point);
        }
    }
}
