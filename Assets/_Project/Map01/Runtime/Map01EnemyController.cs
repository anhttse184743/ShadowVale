using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public sealed class Map01EnemyController : MonoBehaviour
    {
        [SerializeField] private Vector3[] patrolPoints = System.Array.Empty<Vector3>();
        [SerializeField] private float visionRange = 24f;
        [Tooltip("Field of view while unaware; once engaged a guard keeps track of Nam all around.")]
        [SerializeField] private float visionAngle = 110f;
        [SerializeField] private float attackRange = 18f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float fireInterval = 0.65f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        private NavMeshAgent _agent;
        private Animator _animator;
        private Health _health;
        private Transform _player;
        private Health _playerHealth;
        private int _patrolIndex;
        private float _nextShot;
        private Map01Mission _mission;
        private float _alertUntil;
        private Vector3 _investigate;
        private bool _lootCreated;
        private float _suspicion, _engagedUntil, _calmUntil;
        private float _detectionSeconds = 1.5f, _hiddenScale = .32f, _crouchScale = .6f;
        private Vector3 _postPosition;
        private Quaternion _postRotation;
        public bool Alive => !GetComponent<Health>().IsDead;
        public bool Alerted => Time.time < _alertUntil;
        /// <summary>0..1 — how sure this guard is that he has seen Nam; 1 means spotted.</summary>
        public float Suspicion => _suspicion;
        /// <summary>Spotted Nam and fighting him — refreshed while he stays in sight.</summary>
        public bool Engaged => Time.time < _engagedUntil;
        public string LegacyId => name.StartsWith("Outpost guard ", System.StringComparison.Ordinal)
            ? "expansion_guard_" + name.Substring("Outpost guard ".Length) : name;
        public string SaveId {
            get {
                string path = name;
                for (var parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
                return path;
            }
        }
        public void BindMission(Map01Mission mission)
        {
            _mission = mission;
            GetComponent<Health>().KeepCheckpointCorpse();
            // Map 1's guard tuning lives in Map01Balance.json, not in each guard's serialized fields.
            damage = mission.Weapon.damage * mission.Settings.guardDamageScale;
            fireInterval = mission.Settings.guardShotInterval;
            _detectionSeconds = Mathf.Max(.1f, mission.Settings.detectionSeconds);
            _hiddenScale = mission.Settings.hiddenVisionScale;
            _crouchScale = mission.Settings.crouchVisionScale;
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
            if (_agent.isOnNavMesh) _agent.Warp(_postPosition); else transform.position = _postPosition;
            transform.rotation = _postRotation;
            _suspicion = 0; _alertUntil = 0; _engagedUntil = 0;
            _calmUntil = Time.time + 4f;
            _patrolIndex = 0;
            if (patrolPoints.Length > 0) Go(patrolPoints[0]);
        }
        public float DamagePerShot => damage;
        public float FireInterval => fireInterval;
        public void Hear(Vector3 position, float radius)
        {
            if (!Alive || Vector3.Distance(position, transform.position) > radius) return;
            _investigate = position; _alertUntil = Time.time + 6;
        }
        [System.Serializable] public sealed class Snapshot
        {
            public string id;
            public Vector3 position, destination, investigate;
            public Quaternion rotation;
            public float hp, shotRemaining, alertRemaining;
            public int patrolIndex;
            public bool stopped;
        }
        public Snapshot Capture() => new Snapshot {
            id = SaveId, position = transform.position, rotation = transform.rotation, hp = _health.Current,
            patrolIndex = _patrolIndex, shotRemaining = Mathf.Max(0, _nextShot - Time.time),
            alertRemaining = Mathf.Max(0, _alertUntil - Time.time), investigate = _investigate,
            destination = _agent.isOnNavMesh && _agent.hasPath ? _agent.destination : transform.position,
            stopped = _agent.isOnNavMesh && _agent.isStopped
        };
        public void RestoreSnapshot(Snapshot saved)
        {
            if (_agent.isOnNavMesh) _agent.Warp(saved.position); else transform.position = saved.position;
            transform.rotation = saved.rotation;
            _health.RestoreHealth(saved.hp);
            _patrolIndex = Mathf.Clamp(saved.patrolIndex, 0, Mathf.Max(0, patrolPoints.Length - 1));
            _nextShot = Time.time + saved.shotRemaining;
            _alertUntil = Time.time + saved.alertRemaining; _investigate = saved.investigate;
            Go(saved.destination);
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

        public void Configure(Vector3[] points) => patrolPoints = points ?? System.Array.Empty<Vector3>();

        public bool IsBoss { get; private set; }
        /// <summary>
        /// Turns a regular outpost guard clone into the map's commander: no different model yet
        /// (Map 1's briefing doesn't have one), just a tougher, longer-sighted stand its ground.
        /// </summary>
        public void ConfigureAsBoss(float healthMultiplier, float damageMultiplier)
        {
            IsBoss = true;
            damage *= damageMultiplier;
            visionRange *= 1.2f; attackRange *= 1.2f;
            var health = GetComponent<Health>();
            if (health != null) health.SetMaxHealth(health.Max * healthMultiplier);
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _health = GetComponent<Health>();
            _postPosition = transform.position; _postRotation = transform.rotation;
            var controller = FindFirstObjectByType<ShadowVale.Gameplay.Player.PlayerController>();
            if (controller != null)
            {
                _player = controller.transform;
                _playerHealth = controller.GetComponent<Health>();
            }
            if (_animator != null)
            {
                int upperBody = _animator.GetLayerIndex(PlayerCombat.UpperBodyLayer);
                if (upperBody > 0) _animator.SetLayerWeight(upperBody, 1f);
            }
            if (patrolPoints.Length > 0) Go(patrolPoints[0]);
        }

        private void Update()
        {
            if (_mission != null && _mission.Stopped) {
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                SetSpeed(0); return;
            }
            if (_health.IsDead)
            {
                CreateLoot();
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                SetSpeed(0f);
                return;
            }

            bool seesPlayer = CanSeePlayer(out float distance);
            if (seesPlayer && !Engaged)
            {
                // Not an instant spot: suspicion builds while Nam stays in view, faster up close.
                float closeness = 1f - Mathf.Clamp01(distance / visionRange);
                _suspicion = Mathf.Min(1f, _suspicion + Time.deltaTime / _detectionSeconds * Mathf.Lerp(.6f, 3f, closeness));
                if (_suspicion < 1f)
                {
                    // Something's there — stop and stare at it rather than walk on.
                    if (_agent.isOnNavMesh) _agent.isStopped = true;
                    SetSpeed(0f);
                    Face(_player.position);
                    return;
                }
            }
            if (seesPlayer)
            {
                _engagedUntil = _alertUntil = Time.time + 6; _investigate = _player.position;
                if (_mission != null) _mission.Alarmed = true;
                Face(_player.position);
                if (distance <= attackRange)
                {
                    if (_agent.isOnNavMesh) _agent.isStopped = true;
                    SetSpeed(0f);
                    Shoot();
                }
                else
                {
                    Go(_player.position);
                    SetSpeed(_agent.velocity.magnitude);
                }
                return;
            }

            if (!Engaged) _suspicion = Mathf.Max(0f, _suspicion - Time.deltaTime * .35f);
            if (Alerted) Go(_investigate);
            else { if (_agent.isOnNavMesh) _agent.isStopped = false; Patrol(); }
            SetSpeed(_agent.velocity.magnitude);
        }

        private bool CanSeePlayer(out float distance)
        {
            distance = float.PositiveInfinity;
            if (_player == null || _playerHealth == null || _playerHealth.IsDead || Time.time < _calmUntil) return false;
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            Vector3 target = _player.position + Vector3.up * 1.1f;
            Vector3 delta = target - origin;
            distance = delta.magnitude;
            if (Engaged)
            {
                if (distance > visionRange) return false;
            }
            else
            {
                // Unaware: a forward cone, shorter while Nam crouches and far shorter while he
                // hides in cover; only brushing right past a guard is noticed from any side.
                bool hidden = _mission != null && _mission.Hidden, crouched = _mission != null && _mission.Crouched;
                if (distance > visionRange * (hidden ? _hiddenScale : crouched ? _crouchScale : 1f)) return false;
                if (distance > 2.5f && Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(delta, Vector3.up)) > visionAngle * .5f) return false;
            }
            if (Physics.Raycast(origin, delta.normalized, out var hit, distance, obstructionMask,
                    QueryTriggerInteraction.Ignore))
                return hit.transform == _player || hit.transform.IsChildOf(_player);
            return true;
        }

        private void Shoot()
        {
            if (Time.time < _nextShot) return;
            _nextShot = Time.time + fireInterval;
            _animator?.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            _playerHealth.TakeDamage(damage, _player.position + Vector3.up, gameObject);
        }

        private void Patrol()
        {
            if (patrolPoints.Length == 0 || !_agent.isOnNavMesh) return;
            if (!_agent.pathPending && _agent.remainingDistance <= 0.7f)
            {
                _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
                Go(patrolPoints[_patrolIndex]);
            }
        }

        private void Go(Vector3 destination)
        {
            if (!_agent.isOnNavMesh) return;
            _agent.isStopped = false;
            if (NavMesh.SamplePosition(destination, out var hit, 2f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        private void Face(Vector3 position)
        {
            Vector3 direction = position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), 360f * Time.deltaTime);
        }

        private void SetSpeed(float speed)
        {
            if (_animator == null) return;
            _animator.SetFloat(PlayerCombat.AnimatorParams.Speed, speed);
        }
    }
}
