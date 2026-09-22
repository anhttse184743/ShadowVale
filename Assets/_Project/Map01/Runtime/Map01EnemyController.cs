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
        private ForestMission _mission;
        private float _alertUntil;
        private Vector3 _investigate;
        private bool _lootCreated;
        public bool Alive => !GetComponent<Health>().IsDead;
        public bool Alerted => Time.time < _alertUntil;
        public string LegacyId => name.StartsWith("Outpost guard ", System.StringComparison.Ordinal)
            ? "expansion_guard_" + name.Substring("Outpost guard ".Length) : name;
        public string SaveId {
            get {
                string path = name;
                for (var parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
                return path;
            }
        }
        public void BindMission(ForestMission mission) { _mission = mission; GetComponent<Health>().KeepCheckpointCorpse(); }
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
        public void RestoreLegacy(ForestGuard.Snapshot saved) => RestoreSnapshot(new Snapshot {
            position = saved.position, rotation = saved.rotation, hp = saved.state == ForestGuardState.Down ? 0 : saved.hp,
            patrolIndex = saved.waypoint, shotRemaining = saved.shotRemaining, destination = saved.destination,
            investigate = saved.lastKnown, alertRemaining = saved.state == ForestGuardState.Patrol ? 0 : 6, stopped = saved.stopped
        });
        private void CreateLoot()
        {
            if (_lootCreated || _mission == null) return;
            _lootCreated = true;
            var loot = gameObject.AddComponent<ForestPoint>();
            loot.id = "loot_" + LegacyId; loot.label = "Túi lính tuần tra"; loot.kind = ForestPointKind.Loot;
            loot.items = new[] { new ForestIngredient { item_id = "ammo_rifle", count = 12 } };
            _mission.RegisterPoint(loot);
        }

        public void Configure(Vector3[] points) => patrolPoints = points ?? System.Array.Empty<Vector3>();

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _health = GetComponent<Health>();
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
            if (seesPlayer)
            {
                _alertUntil = Time.time + 6; _investigate = _player.position;
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

            if (Alerted) Go(_investigate);
            else { if (_agent.isOnNavMesh) _agent.isStopped = false; Patrol(); }
            SetSpeed(_agent.velocity.magnitude);
        }

        private bool CanSeePlayer(out float distance)
        {
            distance = float.PositiveInfinity;
            if (_player == null || _playerHealth == null || _playerHealth.IsDead) return false;
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            Vector3 target = _player.position + Vector3.up * 1.1f;
            Vector3 delta = target - origin;
            distance = delta.magnitude;
            if (distance > visionRange) return false;
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
