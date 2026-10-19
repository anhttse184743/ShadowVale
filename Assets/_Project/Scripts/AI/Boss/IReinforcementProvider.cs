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
    /// agents are put in the fight with the player's position and first rally to the commander, in
    /// front of him (role <see cref="NearbyAgentsReinforcements.RallyRole"/>); once there the squad
    /// coordinator plans them like any other engaged squad.
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
            for (int i = 0; i < sent.Count; i++) Send(sent[i], playerPosition, now, boss, i);
            return sent;
        }

        /// <summary>The role of a man answering the call on his way to the commander.</summary>
        public const string RallyRole = "rally";
        /// <summary>How far in front of the commander (towards the player) they gather, metres.</summary>
        public const float RallyDistance = 3f;
        /// <summary>Longest the rally may take before they join the fight from wherever they are.</summary>
        public const float RallySeconds = 5f;

        /// <summary>Into the fight: engaged, knowing the player's last position; with <paramref name="rallyTo"/>,
        /// first to his side, fanned out in front of him (the <paramref name="index"/>-th of those called).</summary>
        public static void Send(IEnemyAgent agent, Vector3 playerPosition, float now, IEnemyAgent rallyTo = null, int index = 0)
        {
            agent.Brain.ShareContact(playerPosition, now, now);
            if (rallyTo == null || !rallyTo.Alive) return;
            agent.Brain.Assign(RallyPoint(rallyTo.Position, playerPosition, index), -1, RallyRole);
            var m = agent.Brain.Memory;
            m.TakeCoverUntil = now + RallySeconds;
            // Called in, they stay in the fight through the rally even without sight of the player.
            m.EngagedUntil = Mathf.Max(m.EngagedUntil, now + RallySeconds + agent.Brain.Tuning.EngagedMemorySeconds);
        }

        /// <summary>Where the <paramref name="index"/>-th man gathers: in front of the commander, fanned out 50° apart.</summary>
        public static Vector3 RallyPoint(Vector3 commander, Vector3 player, int index)
        {
            Vector3 toPlayer = player - commander; toPlayer.y = 0;
            if (toPlayer.sqrMagnitude < 1e-4f) toPlayer = Vector3.forward;
            float turn = (index % 3 - 1) * 50f;
            return commander + Quaternion.Euler(0, turn, 0) * toPlayer.normalized * RallyDistance;
        }
    }
}
