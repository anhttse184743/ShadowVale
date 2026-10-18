using UnityEngine;

namespace ShadowVale.AI.Contracts
{
    /// <summary>
    /// The enemy's body as the FSM drives it: movement, facing, animation, health. Implemented in
    /// Gameplay over NavMeshAgent + Animator + Health, and by fakes in tests. The FSM never touches
    /// those Unity components directly, so the same six states run on every map.
    /// </summary>
    public interface IEnemyBody
    {
        Vector3 Position { get; }
        Vector3 Forward { get; }
        Quaternion Rotation { get; set; }
        /// <summary>0..1 of max health.</summary>
        float HealthFraction { get; }
        bool IsDead { get; }

        /// <summary>Set a new path, even if already heading there.</summary>
        void MoveTo(Vector3 destination);
        /// <summary>Head there unless already on the way (avoids re-pathing every frame).</summary>
        void MoveToIfNew(Vector3 destination);
        void Stop();
        /// <summary>Carry on along the current path after a <see cref="Stop"/>.</summary>
        void Resume();
        /// <summary>A path is complete and within <paramref name="tolerance"/> metres of its end.</summary>
        bool HasArrived(float tolerance);
        /// <summary>Walking speed multiplier (1 = the archetype's move speed).</summary>
        void SetPace(float scale);
        /// <summary>Turn towards a point at the body's turn rate this frame.</summary>
        void Face(Vector3 point);
        /// <summary>Yaw by <paramref name="degrees"/> (looking around).</summary>
        void Turn(float degrees);
        /// <summary>Drive the locomotion animation: 0 when standing, the agent's speed when walking.</summary>
        void Animate(bool moving);
        /// <summary>A reachable point near <paramref name="desired"/> (NavMesh sample).</summary>
        bool TrySampleReachable(Vector3 desired, float maxDistance, out Vector3 point);
    }
}
