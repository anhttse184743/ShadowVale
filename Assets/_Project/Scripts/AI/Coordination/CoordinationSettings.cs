using ShadowVale.Data.Content;

namespace ShadowVale.AI.Coordination
{
    /// <summary>
    /// Knobs of the squad coordinator. Weights, budget, cooldown and solver come from the content
    /// bundle's ai_settings (<see cref="From"/>); the ranges are per map.
    /// </summary>
    public sealed class CoordinationSettings
    {
        public CoordinationWeights Weights = CoordinationWeights.Default;
        public string Variant = "greedy";
        public int LatencyBudgetMs = 120;

        /// <summary>Local QUBO instance size: nodes nearest the player (agents' nodes come on top).</summary>
        public int SubgraphNodes = 24;
        public int MaxGroupAgents = 8;
        /// <summary>Fewer fighting agents than this and they fight on their own FSM, no plan.</summary>
        public int MinGroupAgents = 2;

        /// <summary>Squad-mates within this of a guard who makes contact join the fight (radio).</summary>
        public float SquadAlertRange = 40f;
        /// <summary>Other squads within this of the player come to help; 0 = never.</summary>
        public float ReinforcementRange = 75f;
        /// <summary>Fighting squads this close to each other plan as one combat group.</summary>
        public float GroupMergeRange = 60f;

        public float ReplanCooldownSeconds = .8f;
        /// <summary>Re-plan anyway this often while the group keeps fighting.</summary>
        public float RefreshSeconds = 6f;
        /// <summary>Re-plan when the player's node moves this many hops from the plan's.</summary>
        public int ReplanHopDelta = 2;
        /// <summary>A plan arriving after the player moved this far is thrown away.</summary>
        public float StaleDistance = 10f;

        public int Seed = 1729;
        /// <summary>Write every instance + plan to persistentDataPath/research (benchmark / replay input).</summary>
        public bool Record = true;

        public static CoordinationSettings From(AiSettings ai)
        {
            var s = new CoordinationSettings();
            if (ai == null) return s;
            s.Weights = CoordinationWeights.From(ai);
            s.Variant = string.IsNullOrEmpty(ai.DefaultSolverVariant) ? "greedy" : ai.DefaultSolverVariant;
            s.LatencyBudgetMs = ai.LatencyBudgetMs > 0 ? ai.LatencyBudgetMs : 120;
            s.ReplanCooldownSeconds = ai.ReplanCooldownMs > 0 ? ai.ReplanCooldownMs / 1000f : .8f;
            return s;
        }
    }
}
