namespace ShadowVale.Data.Content
{
    /// <summary>
    /// How a boss fights on top of the shared six-state FSM (A04): it moves between cover positions,
    /// throws grenades at a player who camps behind cover, and calls reinforcements once when badly
    /// hurt. Lives in the boss's <see cref="EnemyArchetype.Boss"/>; the defaults are Map 1's tuned values.
    /// </summary>
    public sealed class BossProfile
    {
        // Stats on top of the archetype it was made from.
        public float HealthMultiplier { get; set; } = 3f;
        public float DamageMultiplier { get; set; } = 1.6f;
        public float VisionMultiplier { get; set; } = 1.2f;

        // Relocation between cover positions.
        /// <summary>Seconds of fighting in one spot before moving on; 0 = only when hurt.</summary>
        public float RelocateIntervalSeconds { get; set; } = 12f;
        /// <summary>Share of max health lost since the last move that makes him move at once; 0 = never.</summary>
        public float RelocateDamageFraction { get; set; } = 0.15f;
        public float RelocateMinDistance { get; set; } = 6f;
        public float RelocateMaxDistance { get; set; } = 22f;
        /// <summary>Never relocates to within this distance of the player.</summary>
        public float RelocateMinPlayerDistance { get; set; } = 8f;

        // Grenades — for a player who stays behind cover.
        /// <summary>Seconds the player must stay within <see cref="GrenadeCampRadius"/>, out of sight or in cover, before a throw.</summary>
        public float GrenadeCampSeconds { get; set; } = 4f;
        public float GrenadeCampRadius { get; set; } = 3f;
        public float GrenadeCooldownSeconds { get; set; } = 10f;
        public float GrenadeRange { get; set; } = 24f;
        public float GrenadeRadius { get; set; } = 4.5f;
        /// <summary>At the centre; falls off linearly to a third at the edge.</summary>
        public float GrenadeDamage { get; set; } = 35f;
        /// <summary>Seconds on the ground before it goes off: time to run.</summary>
        public float GrenadeFuseSeconds { get; set; } = 1.2f;
        public float GrenadeNoiseRadius { get; set; } = 30f;

        // Reinforcements.
        /// <summary>Calls them once, the first time his health falls below this share; 0 = never.</summary>
        public float ReinforceHpThreshold { get; set; } = 0.5f;
        public int ReinforceCount { get; set; } = 3;
        /// <summary>Idle enemies within this distance answer first; the map may bring in more.</summary>
        public float ReinforceRadius { get; set; } = 80f;
    }
}
