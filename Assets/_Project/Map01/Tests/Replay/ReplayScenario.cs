using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Audio;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01.Tests.Replay
{
    /// <summary>
    /// The exploit bots of the replay harness — the second "less exploitable" measure (the first is
    /// plan diversity). Each one repeats one cheap trick against a camp, every run the same; only the
    /// seed moves the start a little and delays it, so the guards are met at a different point of
    /// their patrol. A coordination that cannot be exploited should capture these bots more often.
    /// <list type="bullet">
    /// <item><c>same_route</c>: walks straight through the camp along the same lane, every time.</item>
    /// <item><c>same_bush</c>: shows himself at the camp's edge, then runs to the same bush and crouches in it.</item>
    /// <item><c>stone_lure</c>: throws a stone to one side of the camp, then sneaks through the other.</item>
    /// </list>
    /// </summary>
    public static class ReplayScenario
    {
        public static readonly string[] Bots = { "same_route", "same_bush", "stone_lure" };

        /// <summary>How far from the camp's centre the lane starts and ends.</summary>
        public const float LaneHalfLength = 38f;

        public sealed class Plan
        {
            public string Bot;
            public Vector3 Start, Facing;
            public float StartDelay;
            public List<ReplayBot.Step> Steps = new List<ReplayBot.Step>();
            /// <summary>Where the bot counts as through the camp.</summary>
            public Vector3 Exit;
            /// <summary>What he does the moment a guard spots him — always the same; null = carry on.</summary>
            public List<ReplayBot.Step> OnSpotted;
        }

        /// <param name="approach">Unit vector from the camp towards the side Nam comes from — fixed per camp, the exploited lane.</param>
        public static Plan Build(string bot, Map01Mission mission, Vector3 campCentre, Vector3 approach, int seed)
        {
            var rng = new System.Random(seed);
            // The lane itself never changes; the seed only nudges it (±6°) and when it starts (0–6 s).
            float jitter = (float)(rng.NextDouble() * 12 - 6);
            Vector3 dir = Quaternion.Euler(0, jitter, 0) * approach;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            var plan = new Plan { Bot = bot, StartDelay = (float)(rng.NextDouble() * 6) };
            plan.Start = OnNavMesh(campCentre + dir * LaneHalfLength, campCentre);
            plan.Exit = OnNavMesh(campCentre - dir * LaneHalfLength, campCentre);
            plan.Facing = campCentre;
            if (plan.StartDelay > 0) plan.Steps.Add(new ReplayBot.Hold(plan.StartDelay, crouch: bot != "same_route"));
            // Spotted: run on along the lane and out of the area — the same way every time. A sprint
            // lasts what Nam's stamina gives him (~8 s), then he walks.
            Vector3 away = OnNavMesh(campCentre - dir * (LaneHalfLength + 30f), campCentre);
            var flee = new List<ReplayBot.Step>
            {
                new ReplayBot.MoveTo(plan.Exit, MoveStance.Sprint, maxSeconds: 8f),
                new ReplayBot.MoveTo(away, MoveStance.Walk),
                new ReplayBot.Hold(6f, crouch: true),
            };
            switch (bot)
            {
                case "same_route":
                    plan.OnSpotted = flee;
                    plan.Steps.Add(new ReplayBot.MoveTo(plan.Exit, MoveStance.Walk));
                    plan.Steps.Add(new ReplayBot.Hold(8f, crouch: false));
                    break;
                case "same_bush":
                    Vector3 bush = SameBush(mission, campCentre, approach);
                    plan.Steps.Add(new ReplayBot.MoveTo(OnNavMesh(campCentre + dir * 12f, campCentre), MoveStance.Walk));
                    plan.Steps.Add(new ReplayBot.Hold(1.5f, crouch: false)); // Long enough to be seen.
                    plan.Steps.Add(new ReplayBot.MoveTo(bush, MoveStance.Sprint));
                    plan.Steps.Add(new ReplayBot.Hold(45f, crouch: true));
                    break;
                case "stone_lure":
                    plan.OnSpotted = flee;
                    float stone = mission.Settings.stoneNoise;
                    Vector3 lure = OnNavMesh(campCentre + side * 20f, campCentre);
                    plan.Steps.Add(new ReplayBot.Noise(lure, stone));
                    plan.Steps.Add(new ReplayBot.Hold(4f, crouch: true));
                    // Through the camp on the far side from the stone.
                    plan.Steps.Add(new ReplayBot.MoveTo(OnNavMesh(campCentre - side * 6f, campCentre), MoveStance.Sneak));
                    plan.Steps.Add(new ReplayBot.MoveTo(plan.Exit, MoveStance.Sneak));
                    plan.Steps.Add(new ReplayBot.Hold(8f, crouch: true));
                    break;
                default: throw new ArgumentException("Unknown bot " + bot);
            }
            return plan;
        }

        /// <summary>
        /// Always the same bush: the hiding spot closest to a point 16 m out on the approach side —
        /// chosen without the seed, which is the whole exploit.
        /// </summary>
        public static Vector3 SameBush(Map01Mission mission, Vector3 campCentre, Vector3 approach)
        {
            Vector3 near = campCentre + approach * 16f;
            var hide = mission.Points.Where(p => p != null && p.kind == ForestPointKind.Hide
                                                 && Flat(p.transform.position - campCentre) >= 8f)
                .OrderBy(p => Flat(p.transform.position - near)).FirstOrDefault();
            return hide != null ? hide.transform.position : OnNavMesh(near, campCentre);
        }

        private static Vector3 OnNavMesh(Vector3 p, Vector3 fallbackTowards)
        {
            for (int i = 0; i < 6; i++)
            {
                if (NavMesh.SamplePosition(p, out var hit, 5f, NavMesh.AllAreas)) return hit.position;
                p = Vector3.Lerp(p, fallbackTowards, .2f); // Off the mesh (river, cliff): a bit closer in.
            }
            return p;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
