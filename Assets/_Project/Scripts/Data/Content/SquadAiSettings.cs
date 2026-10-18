namespace ShadowVale.Data.Content
{
    /// <summary>
    /// How squads are grouped and re-planned. Every field is optional: null keeps the level below
    /// (map override → bundle ai_settings.squad → the coordinator's defaults), so a map only states
    /// what it does differently — Map 2's village may merge squads over a shorter range than Map 1's
    /// forest camps, say, without copying the rest.
    /// </summary>
    public sealed class SquadAiSettings
    {
        /// <summary>Nodes nearest the player in one QUBO instance (agents' nodes come on top).</summary>
        public int? SubgraphNodes { get; set; }
        public int? MaxGroupAgents { get; set; }
        /// <summary>Fewer fighting agents than this and they fight on their own FSM, without a plan.</summary>
        public int? MinGroupAgents { get; set; }
        /// <summary>Squad-mates within this of a guard who makes contact join the fight.</summary>
        public float? SquadAlertRange { get; set; }
        /// <summary>Other squads within this of the player come to help; 0 = never.</summary>
        public float? ReinforcementRange { get; set; }
        /// <summary>Fighting squads this close to each other plan as one combat group.</summary>
        public float? GroupMergeRange { get; set; }
        public float? RefreshSeconds { get; set; }
        public int? ReplanHopDelta { get; set; }
        public float? StaleDistance { get; set; }
    }
}
