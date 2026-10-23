using System.Linq;
using ShadowVale.Content;
using ShadowVale.Core.Services;
using ShadowVale.Data.Content;
using UnityEngine;

namespace ShadowVale.AI.Coordination
{
    /// <summary>
    /// Knobs of the squad coordinator, from the content bundle (<see cref="ForScene"/>): weights,
    /// budget, cooldown and solver from ai_settings; group sizes and radio ranges from
    /// ai_settings.squad, overridable per map (maps[].coordination). The defaults are Map 1's.
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
        /// <summary>Other squads within this of the player come to help; 0 = never. Was 75 m until 19/10/2026: Map 1's
        /// camps stand 63-71 m apart, so a fight at one brought the next camp over; 30 m keeps each camp to itself
        /// (C3 and its commander, 16 m apart, still help each other).</summary>
        public float ReinforcementRange = 30f;
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

        /// <param name="mapOverride">The map's own <c>coordination</c>, applied over <c>ai_settings.squad</c>.</param>
        public static CoordinationSettings From(AiSettings ai, SquadAiSettings mapOverride = null)
        {
            var s = new CoordinationSettings();
            if (ai != null)
            {
                s.Weights = CoordinationWeights.From(ai);
                s.Variant = string.IsNullOrEmpty(ai.DefaultSolverVariant) ? "greedy" : ai.DefaultSolverVariant;
                s.LatencyBudgetMs = ai.LatencyBudgetMs > 0 ? ai.LatencyBudgetMs : 120;
                s.ReplanCooldownSeconds = ai.ReplanCooldownMs > 0 ? ai.ReplanCooldownMs / 1000f : .8f;
                s.Apply(ai.Squad);
            }
            s.Apply(mapOverride);
            return s;
        }

        /// <summary>
        /// The settings for the map in <paramref name="sceneName"/>: the loaded content bundle (or, when
        /// a map is opened straight from the editor or a test without going through Boot, the
        /// fallback bundle), with that map's own coordination over the bundle-wide one.
        /// </summary>
        public static CoordinationSettings ForScene(string sceneName)
        {
            ContentBundle bundle = null;
            if (ServiceLocator.TryResolve<ContentService>(out var content)) bundle = content.Bundle;
            if (bundle == null && !new FallbackBundleProvider().TryLoad(out bundle, out var error))
            {
                Debug.LogWarning("[Coordination] no content bundle, default settings: " + error);
                return new CoordinationSettings();
            }
            var map = bundle.Maps?.FirstOrDefault(m => m != null && m.SceneName == sceneName);
            return From(bundle.AiSettings, map?.Coordination);
        }

        private void Apply(SquadAiSettings o)
        {
            if (o == null) return;
            if (o.SubgraphNodes > 0) SubgraphNodes = o.SubgraphNodes.Value;
            if (o.MaxGroupAgents > 0) MaxGroupAgents = o.MaxGroupAgents.Value;
            if (o.MinGroupAgents > 0) MinGroupAgents = o.MinGroupAgents.Value;
            if (o.SquadAlertRange >= 0) SquadAlertRange = o.SquadAlertRange.Value;
            if (o.ReinforcementRange >= 0) ReinforcementRange = o.ReinforcementRange.Value;
            if (o.GroupMergeRange >= 0) GroupMergeRange = o.GroupMergeRange.Value;
            if (o.RefreshSeconds > 0) RefreshSeconds = o.RefreshSeconds.Value;
            if (o.ReplanHopDelta > 0) ReplanHopDelta = o.ReplanHopDelta.Value;
            if (o.StaleDistance > 0) StaleDistance = o.StaleDistance.Value;
        }
    }
}
