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
        /// <summary>Per round, on a guard's damage; only when <see cref="DamagePerRound"/> is 0.</summary>
        public float DamageMultiplier { get; set; } = 1.2f;
        public float VisionMultiplier { get; set; } = 1.2f;

        // Relocation between cover positions.
        /// <summary>Seconds of fighting in one spot (from his arrival, ±30%) before moving on; 0 = only when hurt.
        /// Was 12 s until 12/10/2026, then 3.5 s, which had him running more than fighting; 6 s since 18/10/2026,
        /// now that he fires on the move.</summary>
        public float RelocateIntervalSeconds { get; set; } = 6f;
        /// <summary>Share of max health lost since the last move that makes him move at once; 0 = never.</summary>
        public float RelocateDamageFraction { get; set; } = 0.15f;
        /// <summary>Short hops between nearby cover (was 6–22 m until 18/10/2026).</summary>
        public float RelocateMinDistance { get; set; } = 4f;
        public float RelocateMaxDistance { get; set; } = 12f;
        /// <summary>Never relocates to within this distance of the player.</summary>
        public float RelocateMinPlayerDistance { get; set; } = 8f;

        // Grenades — for a player who stays behind cover.
        /// <summary>Seconds the player must linger within <see cref="GrenadeCampRadius"/> of where he settled, mostly
        /// sheltered (<see cref="GrenadeShelteredShare"/>), before a throw. Peeking out to shoot does not reset it.</summary>
        public float GrenadeCampSeconds { get; set; } = 4f;
        /// <summary>The area counted as one hiding place. Was 3 m: shifting along a wall or a bush line reset the camp.</summary>
        public float GrenadeCampRadius { get; set; } = 6f;
        public float GrenadeCooldownSeconds { get; set; } = 8f;
        public float GrenadeRange { get; set; } = 24f;
        public float GrenadeRadius { get; set; } = 7.5f;
        /// <summary>At the centre; falls off linearly to a third at the edge.</summary>
        public float GrenadeDamage { get; set; } = 45f;
        /// <summary>Seconds on the ground before it goes off: enough time to leave the wider blast.</summary>
        public float GrenadeFuseSeconds { get; set; } = 1.5f;
        public float GrenadeNoiseRadius { get; set; } = 30f;

        // Reinforcements.
        /// <summary>Calls them once, the first time his health falls below this share; 0 = never.</summary>
        public float ReinforceHpThreshold { get; set; } = 0.5f;
        public int ReinforceCount { get; set; } = 4;
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
        /// <summary>Aimed at for this long (in view), he ducks behind his cover for <see cref="DuckSeconds"/>.</summary>
        public float AimDuckSeconds { get; set; } = .5f;
        public float DuckSeconds { get; set; } = 1f;
        /// <summary>Aimed at for this long in all (ducking in between), he moves to other cover. Was 0.5 s, with no
        /// duck, until 18/10/2026: every raised rifle sent him running.</summary>
        public float AimDodgeSeconds { get; set; } = 2f;
        /// <summary>A break in the aim longer than this and it counts from zero again (a duck is shorter).</summary>
        public float AimForgetSeconds { get; set; } = 1.5f;
        public float DodgeCooldownSeconds { get; set; } = 3f;
        /// <summary>At most one push order per this many seconds.</summary>
        public float PushCooldownSeconds { get; set; } = 6f;
        /// <summary>Share of the time in the area the player must have spent sheltered for a grenade: one standing in
        /// the open gets shot, not grenaded. 0 = any lingering.</summary>
        public float GrenadeShelteredShare { get; set; } = .5f;
        /// <summary>From cover he fights in bursts: out to the firing point, back behind the cover, out again.</summary>
        public bool PeekFromCover { get; set; } = true;
        /// <summary>Out 2.5 s, behind 0.8 s: most of the time firing (was 1.4 / 1.6 until 18/10/2026).</summary>
        public float PeekOutSeconds { get; set; } = 2.5f;
        public float PeekHideSeconds { get; set; } = .8f;

        // Added 18/10/2026: fight more, run less, with his men covering him. Code defaults only, like the above.
        /// <summary>Rounds per burst; 1 = steady fire, one round every <see cref="FireIntervalSeconds"/>.
        /// 3-round bursts from 18/10/2026, steady fire since 19/10/2026.</summary>
        public int BurstRounds { get; set; } = 1;
        public float BurstGapSeconds { get; set; } = .12f;
        /// <summary>From the last round of a burst to the first of the next.</summary>
        public float BurstIntervalSeconds { get; set; } = 1.2f;
        /// <summary>A wider area for a grenade: a player hopping between hides a few metres apart, mostly sheltered,
        /// for <see cref="GrenadeWideSeconds"/> within this of where he began. 0 = only the close area.</summary>
        public float GrenadeWideRadius { get; set; } = 14f;
        public float GrenadeWideSeconds { get; set; } = 8f;
        /// <summary>When he moves to new cover, this many of his men give covering fire for <see cref="CoverFireSeconds"/>.</summary>
        public int CoverFireMen { get; set; } = 2;
        public float CoverFireSeconds { get; set; } = 3f;

        // His own weapon and eyes (19/10/2026), not a cloned guard's times a multiplier. 0 = the multiplier way.
        /// <summary>Damage of each round that hits.</summary>
        public float DamagePerRound { get; set; } = 11.5f;
        /// <summary>Seconds between rounds of steady fire (a guard: 1.6).</summary>
        public float FireIntervalSeconds { get; set; } = .3f;
        /// <summary>How far he fires, metres (his sight comes from the archetype's vision_range).</summary>
        public float AttackRange { get; set; } = 18f;
        /// <summary>Seconds of steady mid-range sight to spot the player (a guard: 1.5).</summary>
        public float DetectionSeconds { get; set; } = 1f;
    }
}
