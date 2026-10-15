using System.Collections.Generic;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Coordination;
using UnityEngine;

namespace ShadowVale.AI.Boss
{
    /// <summary>
    /// A commander's hold on the squads around him. While he is alive and fighting within
    /// <see cref="Range"/> of a combat group, that group is planned as a pincer: the QUBO weighs
    /// flanking and the escape routes more, so his men close the ring while he keeps Nam pinned
    /// from cover. When he falls, the men he commanded lose heart and fall back sooner. The game's
    /// replay experiment attacks a camp without a commander, so it never meets either rule.
    /// </summary>
    public static class Command
    {
        /// <summary>How far his orders carry (radio and voice), metres.</summary>
        public const float Range = 45f;
        public const double FlankScale = 1.8;
        public const double CoverageScale = 1.3;
        /// <summary>Share of health below which his men retreat once he is down (their own threshold if higher).</summary>
        public const float MoraleBreakRetreat = .6f;

        /// <summary>The squad weights under his command: more flanking, more of the ways out covered.</summary>
        public static CoordinationWeights Pincer(CoordinationWeights w)
            => new CoordinationWeights(w.Coverage * CoverageScale, w.Redundancy, w.Flanking * FlankScale, w.Cover, w.Distance);

        /// <summary>The living, fighting commander within <see cref="Range"/> of any of these members, or null.</summary>
        public static IEnemyAgent CommanderOf(IReadOnlyList<IEnemyAgent> members, float now)
        {
            foreach (var a in AgentRegistry.All)
            {
                if (!a.Alive || a.Brain == null || !a.Brain.Tuning.IsBoss || !a.Brain.Memory.Engaged(now)) continue;
                foreach (var m in members)
                    if ((m.Position - a.Position).sqrMagnitude <= Range * Range) return a;
            }
            return null;
        }
    }
}
