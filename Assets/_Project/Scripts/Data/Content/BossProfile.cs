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
        /// <summary>Seconds of fighting in one spot (from his arrival, ±30%) before moving on; 0 = only when hurt.
        /// Short on purpose: a commander on the move, stopping briefly to fire. Was 12 s until 12/10/2026.</summary>
        public float RelocateIntervalSeconds { get; set; } = 3.5f;
        /// <summary>Share of max health lost since the last move that makes him move at once; 0 = never.</summary>
        public float RelocateDamageFraction { get; set; } = 0.15f;
        public float RelocateMinDistance { get; set; } = 6f;
        public float RelocateMaxDistance { get; set; } = 22f;
        /// <summary>Never relocates to within this distance of the player.</summary>
        public float RelocateMinPlayerDistance { get; set; } = 8f;

        // Grenades — for a player who stays behind cover.
        /// <summary>Seconds the player must linger within <see cref="GrenadeCampRadius"/> of where he settled, mostly
        /// sheltered (<see cref="GrenadeShelteredShare"/>), before a throw. Peeking out to shoot does not reset it.</summary>
        public float GrenadeCampSeconds { get; set; } = 4f;
        /// <summary>The area counted as one hiding place. Was 3 m: shifting along a wall or a bush line reset the camp.</summary>
        public float GrenadeCampRadius { get; set; } = 6f;
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

        // Tactics added 15/10/2026. Code defaults only, not in the content schema yet: each one can be
        // switched off here to show the difference (0 or false = the behaviour before).
        /// <summary>A grenade lands this far beyond the player, on the side away from the commander's men,
        /// so the blast drives him towards them; 0 = right on him.</summary>
        public float GrenadeFlushOffset { get; set; } = 1.5f;
        /// <summary>Camping where he already hid once needs only this share of <see cref="GrenadeCampSeconds"/>; 1 = no memory.</summary>
        public float KnownHideCampFactor { get; set; } = .5f;
        /// <summary>Reads the player's weapon: presses a reload, dodges a rifle aimed at him.</summary>
        public bool ReadsPlayer { get; set; } = true;
        /// <summary>Aimed at for this long (in view), he moves to other cover.</summary>
        public float AimDodgeSeconds { get; set; } = .5f;
        public float DodgeCooldownSeconds { get; set; } = 3f;
        /// <summary>At most one push order per this many seconds.</summary>
        public float PushCooldownSeconds { get; set; } = 6f;
        /// <summary>Share of the time in the area the player must have spent sheltered for a grenade: one standing in
        /// the open gets shot, not grenaded. 0 = any lingering.</summary>
        public float GrenadeShelteredShare { get; set; } = .5f;
        /// <summary>From cover he fights in bursts: out to the firing point, back behind the cover, out again.</summary>
        public bool PeekFromCover { get; set; } = true;
        public float PeekOutSeconds { get; set; } = 1.4f;
        public float PeekHideSeconds { get; set; } = 1.6f;
    }
}
