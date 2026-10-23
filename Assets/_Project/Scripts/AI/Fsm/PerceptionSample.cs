using UnityEngine;

namespace ShadowVale.AI.Fsm
{
    /// <summary>What the sensors reported this tick (computed by the agent with <c>VisionSensor</c>).</summary>
    public readonly struct PerceptionSample
    {
        public readonly bool SeesPlayer;
        public readonly float Distance;
        public readonly Vector3 PlayerPosition;

        public PerceptionSample(bool seesPlayer, float distance, Vector3 playerPosition)
        {
            SeesPlayer = seesPlayer; Distance = distance; PlayerPosition = playerPosition;
        }

        public static PerceptionSample Nothing(Vector3 playerPosition) => new PerceptionSample(false, float.PositiveInfinity, playerPosition);
    }
}
