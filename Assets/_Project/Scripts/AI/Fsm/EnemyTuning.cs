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
        /// <summary>How much faster suspicion builds right up close than at the edge of sight (×0.6 there).
        /// Map 1's rescue squad uses less (1.4): creeping up on the jetty is meant to be possible.</summary>
        public float SuspicionCloseGain = 3f;
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
        /// <summary>Stands this long at each patrol point before walking on; 0 = no stop.</summary>
        public float PatrolPauseSeconds;
        /// <summary>Walking speed on patrol, as a share of his base speed. 1 = his full speed (about 3.5 m/s for
        /// Map 1's guards, which reads as jogging); a calm beat walks at about 0.55 of it.</summary>
        public float PatrolPace = 1f;

        // Fighting
        public float AttackRange = 18f;
        /// <summary>Suppressive fire at the last-seen point after losing sight; 0 = hunt straight away.</summary>
        public float SuppressSeconds;
        /// <summary>
        /// Longest he walks to an assignment before hunting the player instead. A plan's positions
        /// are drawn around where the player was last seen; a player on the run is gone from there
        /// within seconds. Was 8 s until 04/10/2026: on the calibration seeds (500–509, greedy) 3 s
        /// took same_route's capture rate from 40 % to 100 % with every other rule unchanged.
        /// </summary>
        public float TakeCoverTimeoutSeconds = 3f;
        public float CoverArriveDistance = 1f;
        /// <summary>
        /// Roles that hold their position while the player is out of range; every other role
        /// closes in once there. Comma-separated, "*" = all. Default: only a lone guard's own nearby
        /// cover ("local"). A squad plan's positions — block, cover, flank — are where to deploy,
        /// not where to stay: the "escape" nodes are the edge of the 24-node subgraph around where
        /// the player was seen, not the map's exits, and a player on the run passes them. Until
        /// 04/10/2026 every assignment held (exp-v1.1: planned squads captured 43 %, guards without
        /// plans 99 %); on the calibration seeds holding block nodes kept same_route at 40 %.
        /// </summary>
        public string HoldRoles = "local";
        /// <summary>
        /// On a re-plan, a new assignment within this of where he stands counts as reached: a plan
        /// that barely moves him does not stop his fire to walk a metre. The first assignment is
        /// always walked to. 0 = always walk.
        /// </summary>
        public float ReassignKeepDistance = 3f;
        /// <summary>
        /// Fight first: with the player in sight and in range he stops and fires instead of walking
        /// on to his assignment, and resumes it once he cannot. Off = an assignment is walked to
        /// whatever happens (the behaviour before 04/10/2026): every re-plan pulled the squad out of
        /// a fight it was winning — on the calibration seeds that alone cost greedy 20–40 points of
        /// capture rate against the exploit bots.
        /// </summary>
        public bool EngageInRangeFirst = true;
        /// <summary>
        /// Having lost sight while fighting, he walks to where the player was last seen (and on to a
        /// search). Off (bosses): he stays where he is, watching that spot and suppressing it for
        /// <see cref="SuppressSeconds"/>, as long as he is fighting — a commander does not walk up to
        /// the bush the player hides in; he lets grenades and his men do it.
        /// </summary>
        public bool HuntWhenLost = true;
        /// <summary>
        /// On the way to a position, fire only with the player ahead — within <see cref="MoveFireConeDegrees"/> of
        /// where he walks — turning his upper body towards him by up to <see cref="MoveFireTwistDegrees"/>, at the
        /// slower <see cref="MoveFirePace"/>; with the player behind or to the side, run without firing. Off (the
        /// default, every guard): fire whenever the player is in sight and range, facing the way he runs.
        /// The rig has no strafe or backpedal clips, so a body turned far from its walk would slide.
        /// </summary>
        public bool FireOnTheMove;
        public float MoveFirePace = .6f;
        public float MoveFireConeDegrees = 75f;
        public float MoveFireTwistDegrees = 60f;

        /// <summary>Whether a guard on an assignment with this role holds it when the player is out of range.</summary>
        public bool Holds(string role)
        {
            if (string.IsNullOrEmpty(HoldRoles)) return false;
            if (HoldRoles.Trim() == "*") return true;
            foreach (var r in HoldRoles.Split(','))
                if (r.Trim() == (role ?? "")) return true;
            return false;
        }

        // Retreat
        /// <summary>Fall back below this share of max health while fighting; 0 = never.</summary>
        public float RetreatHealthFraction;
        public float RetreatDistance = 12f;
        public float RetreatPace = 1.5f;
        public float RetreatHoldSeconds = 6f;
        public bool IsBoss;
    }
}
