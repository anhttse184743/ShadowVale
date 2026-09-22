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
            if (_health.IsDead)
            {
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                SetSpeed(0f);
                return;
            }

            bool seesPlayer = CanSeePlayer(out float distance);
            if (seesPlayer)
            {
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

            Patrol();
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
