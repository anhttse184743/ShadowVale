using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Fsm;
using ShadowVale.Core.Events;
using ShadowVale.Core.Services;
using ShadowVale.Data.Telemetry;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Telemetry;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests.Replay
{
    /// <summary>
    /// Deterministic replay harness (O5, RQ4): plays Map 1 with an exploit bot instead of a person,
    /// many times per solver variant, and writes one line per run to
    /// <c>persistentDataPath/research/replay-&lt;time&gt;.jsonl</c> for <c>svl-bench replay</c>.
    /// <para>
    /// Determinism: a fixed game step (<see cref="Time.captureFramerate"/>), the run's seed for the bot
    /// and the coordinator, no unseeded randomness in the AI. Run i uses the same seed for every
    /// variant, so variants are compared on paired runs. What is not frame-exact is when a solve
    /// finishes (it runs on a worker thread, a remote one over HTTP) — the reason in-game results are
    /// compared over many runs with statistics, while the solvers themselves are compared exactly on
    /// the recorded instances (the coordination-*.jsonl this harness also produces).
    /// </para>
    /// </summary>
    public static class ReplayHarness
    {
        public const string Scene = "Map 1";

        public sealed class Spec
        {
            public string Bot = "same_route";
            public string Variant = "greedy";
            public int Seed = 1000;
            public int Run;
            /// <summary>Index into Map01Scouting.Camps.</summary>
            public int Camp = 1;
            public int FramesPerSecond = 30;
            /// <summary>Game seconds before a run is cut off.</summary>
            public float MaxSeconds = 90f;
            /// <summary>After the script ends, how long a fight may still go on.</summary>
            public float SettleSeconds = 20f;
        }

        public sealed class Result
        {
            public Spec Spec;
            /// <summary>captured (Nam died) | escaped (seen, survived) | undetected (never seen).</summary>
            public string Outcome;
            public float Seconds, PlayerHealth, Distance;
            public bool TimedOut;
            public TelemetrySessionDto Session;
        }

        /// <summary>One run: reload the map, set the variant and seed, play the bot, collect telemetry.</summary>
        public static IEnumerator RunOne(Spec spec, Action<Result> done)
        {
            int previousCapture = Time.captureFramerate;
            Time.captureFramerate = Math.Max(10, spec.FramesPerSecond);
            try
            {
                SceneManager.LoadScene(Scene);
                yield return null;
                yield return null;
                var mission = Object.FindFirstObjectByType<Map01Mission>();
                var quest = mission.GetComponent<Map01Quest>();
                var scouting = mission.GetComponent<Map01Scouting>();
                // The camps fight normally here; during the scouting order a sighting ends the run instead.
                quest.RestoreStage(Map01Quest.CampsStage);
                Vector3 spawn = mission.player.position;

                var coordinator = ServiceLocator.Resolve<SquadCoordinator>();
                coordinator.Settings.Seed = spec.Seed;
                coordinator.Settings.Variant = spec.Variant;
                coordinator.SetVariant(spec.Variant);

                var running = mission.GetComponent<TelemetryService>();
                if (running != null) running.End(upload: false);
                var telemetry = TelemetryService.Attach(mission.gameObject, Map01Mission.MapId,
                    new TelemetryConfig { UploadEnabled = false, RootDirectory = null, ResearchDirectory = null });

                var camp = scouting.Camps[Mathf.Clamp(spec.Camp, 0, scouting.Camps.Count - 1)];
                Vector3 approach = spawn - camp.Center; approach.y = 0;
                approach = approach.sqrMagnitude > 1 ? approach.normalized : Vector3.forward;
                var plan = ReplayScenario.Build(spec.Bot, mission, camp.Center, approach, spec.Seed);
                var bot = new ReplayBot(mission, plan.Steps);
                bot.Place(plan.Start, plan.Facing);
                var health = mission.player.GetComponent<Health>();

                // The bot's one reaction: the moment a guard spots him, its OnSpotted script takes over.
                Action<PlayerContactEvent> onContact = _ => bot.Interrupt(plan.OnSpotted);
                EventBus.Subscribe(onContact);
                float t = 0, settle = 0;
                bool timedOut = false;
                try
                {
                    while (true)
                    {
                        yield return null;
                        float dt = Time.deltaTime;
                        t += dt;
                        if (health != null && health.IsDead) break;
                        bool scripted = bot.Tick(dt);
                        if (!scripted)
                        {
                            settle += dt;
                            if (!telemetry.Session.Encounters.InEncounter || settle >= spec.SettleSeconds) break;
                        }
                        if (t >= spec.MaxSeconds) { timedOut = true; break; }
                    }
                }
                finally { EventBus.Unsubscribe(onContact); }
                bot.Release();
                var session = telemetry.End(upload: false);
                bool dead = health != null && health.IsDead;
                done(new Result
                {
                    Spec = spec, Session = session, Seconds = t, TimedOut = timedOut, Distance = bot.DistanceWalked,
                    PlayerHealth = health != null ? health.Current : 0,
                    Outcome = dead ? "captured" : session.Summary.Encounters > 0 ? "escaped" : "undetected",
                });
            }
            finally { Time.captureFramerate = previousCapture; }
        }

        // ---- Output ------------------------------------------------------------------------------

        public static string DefaultOutputPath()
        {
            string env = Environment.GetEnvironmentVariable("SVL_REPLAY_OUT");
            if (!string.IsNullOrWhiteSpace(env)) return env;
            return Path.Combine(Application.persistentDataPath, "research",
                "replay-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".jsonl");
        }

        public static void Append(string path, Result r)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.AppendAllText(path, ToJson(r) + "\n", new UTF8Encoding(false));
        }

        /// <summary>One flat JSON object per run — the format <c>svl-bench replay</c> reads.</summary>
        public static string ToJson(Result r)
        {
            var s = r.Session; var sum = s.Summary; var solver = s.Solver;
            var f = new List<string>
            {
                Str("map", s.MapId), Str("bot", r.Spec.Bot), Str("variant", r.Spec.Variant), Num("seed", r.Spec.Seed),
                Num("run", r.Spec.Run), Num("camp", r.Spec.Camp), Num("fps", r.Spec.FramesPerSecond),
                Str("outcome", r.Outcome), Bool("captured", r.Outcome == "captured"), Bool("timed_out", r.TimedOut),
                Num("seconds", r.Seconds), Num("player_hp", r.PlayerHealth), Num("distance", r.Distance),
                Num("encounters", sum.Encounters), Num("captures", sum.Captures), Num("escapes", sum.Escapes), Num("cleared", sum.Cleared),
                Num("escape_time_mean", sum.EscapeTimeMeanSeconds), Num("coordination_score", sum.CoordinationScoreMean),
                Num("plans", sum.Plans), Num("distinct_plans", sum.DistinctPlans),
                Num("plan_entropy_bits", sum.PlanEntropyBits), Num("agent_position_entropy_bits", sum.AgentPositionEntropyBits),
                Num("fallback_rate", sum.FallbackRate), Num("timeouts", solver.TimeoutCount),
                Num("latency_p50_ms", solver.LatencyP50Ms), Num("latency_p95_ms", solver.LatencyP95Ms),
                Str("ai_variant", s.AiVariant), Str("variants_used", sum.Variants), Bool("valid_for_comparison", sum.ValidForComparison),
                Num("contacts", sum.EventCounts.TryGetValue("player_spotted", out int c) ? c : 0),
                Str("coordination_log", sum.CoordinationLog ?? ""), Str("session_id", s.SessionId),
            };
            return "{" + string.Join(",", f) + "}";
        }

        private static string Str(string k, string v) => $"\"{k}\":\"{(v ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        private static string Num(string k, double v) => $"\"{k}\":{v.ToString("R", CultureInfo.InvariantCulture)}";
        private static string Bool(string k, bool v) => $"\"{k}\":{(v ? "true" : "false")}";
    }
}
