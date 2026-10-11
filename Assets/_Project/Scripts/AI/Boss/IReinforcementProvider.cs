using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Agents;
using UnityEngine;

namespace ShadowVale.AI.Boss
{
    /// <summary>
    /// Who answers a boss's call for reinforcements. The default takes idle enemies already on the
    /// map; a map can bring in more (Map 1 sends fresh men when the camps are gone). The answering
    /// agents are put in the fight with the player's position; the squad coordinator then plans
    /// them like any other engaged squad.
    /// </summary>
    public interface IReinforcementProvider
    {
        /// <returns>The agents sent (possibly fewer than <paramref name="count"/>).</returns>
        IReadOnlyList<IEnemyAgent> Summon(IEnemyAgent boss, Vector3 playerPosition, int count, float radius, float now);
    }

    /// <summary>The nearest idle, living enemies within the radius of the boss, never another boss.</summary>
    public sealed class NearbyAgentsReinforcements : IReinforcementProvider
    {
        public static readonly NearbyAgentsReinforcements Instance = new NearbyAgentsReinforcements();

        public IReadOnlyList<IEnemyAgent> Summon(IEnemyAgent boss, Vector3 playerPosition, int count, float radius, float now)
        {
            if (count <= 0) return Array.Empty<IEnemyAgent>();
            float r2 = radius * radius;
            var sent = AgentRegistry.All
                .Where(a => a != boss && a.Alive && !a.Brain.Tuning.IsBoss && !a.Brain.Memory.Engaged(now)
                            && (a.Position - boss.Position).sqrMagnitude <= r2)
                .OrderBy(a => (a.Position - boss.Position).sqrMagnitude).ThenBy(a => a.Id, StringComparer.Ordinal)
                .Take(count).ToList();
            foreach (var a in sent) Send(a, playerPosition, now);
            return sent;
        }

        /// <summary>Into the fight: engaged, heading for the player's last known position.</summary>
        public static void Send(IEnemyAgent agent, Vector3 playerPosition, float now)
            => agent.Brain.ShareContact(playerPosition, now, now);
    }
}
