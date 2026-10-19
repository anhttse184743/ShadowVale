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
    /// circuit to learn — the same "less exploitable" aim as the squad plans. A node his men stand in
    /// front of (between it and the player) scores higher; one that means turning his back on the
    /// player and running scores lower, unless he is falling back on purpose.
    /// </summary>
    public static class BossRelocation
    {
        /// <summary>How many of the best-scoring nodes the random pick is made from.</summary>
        public const int Shortlist = 3;
        /// <summary>A move more than this many degrees off the way to the player is running away from him.</summary>
        public const float RunAwayDegrees = 120f;
        public const double RunAwayPenalty = .75, BehindMenBonus = .4;

        /// <param name="fightRange">His weapon's reach: a node within it of the player scores higher, so a
        /// move does not leave him watching a player he cannot hit. 0 = ignore.</param>
        /// <param name="minPlayerDistance">Keep at least this far from the player, if more than the profile's
        /// <see cref="BossProfile.RelocateMinPlayerDistance"/> (regrouping: as far as he is now).</param>
        /// <param name="men">Where his men stand (their centre), if any are fighting near him.</param>
        /// <param name="fallingBack">Regrouping towards reinforcements: running away from the player is the point.</param>
        public static bool TryPick(IWorldQuery graph, BossProfile profile, Vector3 from, Vector3 player, int avoidNode,
            System.Random rng, out int node, out Vector3 position, float fightRange = 0f, float minPlayerDistance = 0f,
            Vector3? men = null, bool fallingBack = false)
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
                double score = Score(graph, c, at, player, playerNode, move, profile);
                if (fightRange > 0 && toPlayer <= fightRange) score += .5;
                if (!fallingBack && RunsAway(from, at, player)) score -= RunAwayPenalty;
                if (men.HasValue && Shielded(at, men.Value, player)) score += BehindMenBonus;
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

        /// <summary>Moving from <paramref name="from"/> to <paramref name="to"/> turns his back on the player (more than <see cref="RunAwayDegrees"/> off).</summary>
        public static bool RunsAway(Vector3 from, Vector3 to, Vector3 player)
        {
            Vector3 move = to - from, toPlayer = player - from;
            move.y = 0; toPlayer.y = 0;
            return move.sqrMagnitude > 1e-4f && toPlayer.sqrMagnitude > 1e-4f && Vector3.Angle(move, toPlayer) > RunAwayDegrees;
        }

        /// <summary>His men stand between <paramref name="at"/> and the player: closer to the player, on the same side (within 35°).</summary>
        public static bool Shielded(Vector3 at, Vector3 men, Vector3 player)
        {
            Vector3 a = at - player, m = men - player;
            a.y = 0; m.y = 0;
            return m.sqrMagnitude > 1e-4f && m.sqrMagnitude < a.sqrMagnitude && Vector3.Angle(a, m) <= 35f;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
