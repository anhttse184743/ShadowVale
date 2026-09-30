namespace ShadowVale.AI.Fsm
{
    /// <summary>
    /// Every number the FSM uses. Filled from the content bundle / map balance data by whoever
    /// builds the agent; the defaults are Map 1's tuned values. Mutable so a boss upgrade or a
    /// difficulty change applies without rebuilding the brain.
    /// </summary>
    public sealed class EnemyTuning
    {
        // Spotting
        public float VisionRange = 24f;
        /// <summary>Seconds of steady mid-range sight to go from 0 to 1 suspicion.</summary>
        public float DetectionSeconds = 1.5f;
        public float SuspicionDecayPerSecond = .35f;
        /// <summary>How long he keeps fighting after last seeing the player.</summary>
        public float EngagedMemorySeconds = 6f;

        // Investigate
        /// <summary>An upper bound only: the look-around ends an investigation sooner.</summary>
        public float InvestigateTimeoutSeconds = 45f;
        public float InvestigatePace = 1.5f;
        public float InvestigateArriveDistance = 1.5f;
        public float SearchSeconds = 8f;
        public float LookAroundDegreesPerSecond = 70f;
        public float ReturnArriveDistance = .6f;
        public float PatrolArriveDistance = .7f;

        // Fighting
        public float AttackRange = 18f;
        /// <summary>Suppressive fire at the last-seen point after losing sight; 0 = hunt straight away.</summary>
        public float SuppressSeconds;
        public float TakeCoverTimeoutSeconds = 5f;
        public float CoverArriveDistance = 1f;

        // Retreat
        /// <summary>Fall back below this share of max health while fighting; 0 = never.</summary>
        public float RetreatHealthFraction;
        public float RetreatDistance = 12f;
        public float RetreatPace = 1.5f;
        public float RetreatHoldSeconds = 6f;
        public bool IsBoss;
    }
}
