using ShadowVale.AI.Fsm;
using UnityEngine;

namespace ShadowVale.AI.Agents
{
    /// <summary>
    /// A live enemy as the squad coordinator, telemetry and the debug tools see it — whatever map
    /// component actually runs it. Registered in <see cref="AgentRegistry"/> while enabled.
    /// </summary>
    public interface IEnemyAgent
    {
        string Id { get; }
        /// <summary>Squad this enemy belongs to (Map 1: "B", "C1".."C3", "boss"); empty = none.</summary>
        string SquadId { get; }
        EnemyBrain Brain { get; }
        Vector3 Position { get; }
        Vector3 Forward { get; }
        bool Alive { get; }
        float HealthFraction { get; }
        /// <summary>The last perception tick saw the player.</summary>
        bool SeesPlayer { get; }
        float VisionRange { get; }
        float VisionConeAngle { get; }

        /// <summary>Debug/test hooks.</summary>
        void DebugEngage(float seconds);
        void DebugDamage(float fractionOfMax);
    }
}
