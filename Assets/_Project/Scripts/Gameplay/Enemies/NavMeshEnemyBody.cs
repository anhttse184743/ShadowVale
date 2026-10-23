using ShadowVale.AI.Contracts;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Gameplay.Enemies
{
    /// <summary>
    /// <see cref="IEnemyBody"/> over the standard enemy rig: NavMeshAgent for locomotion, Animator
    /// "Speed" for the walk blend, Health for hit points. Any map's enemy MonoBehaviour builds one.
    /// </summary>
    public sealed class NavMeshEnemyBody : IEnemyBody
    {
        private const float TurnDegreesPerSecond = 360f;
        private const float DestinationSampleRadius = 2f;

        private readonly Transform _transform;
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private readonly Health _health;
        /// <summary>Walking speed at pace 1 (the agent's speed when built; a map may set its own).</summary>
        public float BaseSpeed { get; set; }

        public NavMeshEnemyBody(Transform transform, NavMeshAgent agent, Animator animator, Health health)
        {
            _transform = transform; _agent = agent; _animator = animator; _health = health;
            BaseSpeed = agent.speed;
        }

        public NavMeshAgent Agent => _agent;
        public Vector3 Position => _transform.position;
        public Vector3 Forward => _transform.forward;
        public Quaternion Rotation { get => _transform.rotation; set => _transform.rotation = value; }
        public float HealthFraction => _health.Max > 0f ? _health.Current / _health.Max : 0f;
        public bool IsDead => _health.IsDead;

        public void MoveTo(Vector3 destination)
        {
            if (!_agent.isOnNavMesh) return;
            _agent.isStopped = false;
            if (NavMesh.SamplePosition(destination, out var hit, DestinationSampleRadius, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        public void MoveToIfNew(Vector3 destination)
        {
            if (!_agent.isOnNavMesh) return;
            if (_agent.hasPath && !_agent.isStopped && (_agent.destination - destination).sqrMagnitude < 1f) return;
            MoveTo(destination);
        }

        public void Stop()
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
        }

        public void Resume()
        {
            if (_agent.isOnNavMesh) _agent.isStopped = false;
        }

        public bool HasArrived(float tolerance)
            => _agent.isOnNavMesh && !_agent.pathPending && _agent.remainingDistance <= tolerance;

        public void SetPace(float scale)
        {
            if (BaseSpeed > 0f) _agent.speed = BaseSpeed * scale;
        }

        public void Face(Vector3 point)
        {
            Vector3 direction = point - _transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > .001f)
                _transform.rotation = Quaternion.RotateTowards(_transform.rotation,
                    Quaternion.LookRotation(direction), TurnDegreesPerSecond * Time.deltaTime);
        }

        public Vector3 Velocity => _agent.isOnNavMesh ? _agent.velocity : Vector3.zero;

        public bool FacesManually
        {
            get => !_agent.updateRotation;
            set => _agent.updateRotation = !value;
        }

        public void Turn(float degrees) => _transform.Rotate(0f, degrees, 0f);

        public void Animate(bool moving)
        {
            if (_animator != null)
                _animator.SetFloat(PlayerCombat.AnimatorParams.Speed, moving ? _agent.velocity.magnitude : 0f);
        }

        public bool TrySampleReachable(Vector3 desired, float maxDistance, out Vector3 point)
        {
            point = default;
            if (!NavMesh.SamplePosition(desired, out var hit, maxDistance, NavMesh.AllAreas)) return false;
            point = hit.position;
            return true;
        }

        /// <summary>Teleport (save restore, reset to post).</summary>
        public void Warp(Vector3 position)
        {
            if (_agent.isOnNavMesh) _agent.Warp(position); else _transform.position = position;
        }
    }
}
