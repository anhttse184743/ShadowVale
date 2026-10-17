using System;
using System.Collections.Generic;
using ShadowVale.AI.Contracts;
using ShadowVale.Data.Content;
using UnityEngine;

namespace ShadowVale.AI.Boss
{
    /// <summary>
    /// Where a relocating boss goes: a cover node of the map's tactical graph between
    /// <see cref="BossProfile.RelocateMinDistance"/> and <see cref="BossProfile.RelocateMaxDistance"/> away,
    /// not too close to the player, not the spot he just left. Nodes that face the player and see him
    /// score higher; among the best few the pick is random (seeded), so the boss has no fixed
    /// circuit to learn — the same "less exploitable" aim as the squad plans.
    /// </summary>
    public static class BossRelocation
    {
        /// <summary>How many of the best-scoring nodes the random pick is made from.</summary>
        public const int Shortlist = 3;

        /// <param name="fightRange">His weapon's reach: a node within it of the player scores higher, so a
        /// move does not leave him watching a player he cannot hit. 0 = ignore.</param>
        /// <param name="minPlayerDistance">Keep at least this far from the player, if more than the profile's
        /// <see cref="BossProfile.RelocateMinPlayerDistance"/> (regrouping: as far as he is now).</param>
        /// <param name="allowed">The map's bounds for him (Map 1: inside the command post's fence). Null = anywhere.</param>
        public static bool TryPick(IWorldQuery graph, BossProfile profile, Vector3 from, Vector3 player, int avoidNode,
            System.Random rng, out int node, out Vector3 position, float fightRange = 0f, float minPlayerDistance = 0f,
            Func<Vector3, bool> allowed = null)
        {
            node = -1; position = from;
            if (graph == null || graph.NodeCount == 0) return false;
            int playerNode = graph.NearestNode(player);
            float minToPlayer = Mathf.Max(profile.RelocateMinPlayerDistance, minPlayerDistance);
            var scored = new List<(double score, int node)>();
            foreach (int c in graph.CoverNodes)
            {
                if (c == avoidNode) continue;
                Vector3 at = graph.NodePosition(c);
                float move = Flat(at - from), toPlayer = Flat(at - player);
                if (move < profile.RelocateMinDistance || move > profile.RelocateMaxDistance) continue;
                if (toPlayer < minToPlayer) continue;
                if (allowed != null && !allowed(at)) continue;
                double score = Score(graph, c, at, player, playerNode, move, profile);
                if (fightRange > 0 && toPlayer <= fightRange) score += .5;
                scored.Add((score, c));
            }
            if (scored.Count == 0) return false;
            scored.Sort((a, b) => b.score != a.score ? b.score.CompareTo(a.score) : a.node.CompareTo(b.node));
            int pick = rng != null ? rng.Next(Math.Min(Shortlist, scored.Count)) : 0;
            node = scored[pick].node;
            position = graph.NodePosition(node);
            return true;
        }

        /// <summary>Cover facing the player (0..1), plus 1 if the node sees his node, minus a little for a long run.</summary>
        public static double Score(IWorldQuery graph, int node, Vector3 at, Vector3 player, int playerNode, float move, BossProfile profile)
        {
            Vector3 toPlayer = player - at; toPlayer.y = 0;
            double facing = 0;
            if (toPlayer.sqrMagnitude > 1e-4f)
            {
                float yaw = graph.CoverFacing(node) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
                facing = Math.Max(0, Vector3.Dot(dir, toPlayer.normalized));
            }
            double sees = playerNode >= 0 && graph.VisibilityReady && graph.NodeSeesNode(node, playerNode) ? 1 : 0;
            return facing + sees - 0.25 * move / Math.Max(1f, profile.RelocateMaxDistance);
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
