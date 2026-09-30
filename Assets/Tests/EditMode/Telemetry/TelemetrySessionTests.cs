using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.Content;
using ShadowVale.Core.Events;
using ShadowVale.Data.Telemetry;
using ShadowVale.Telemetry;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.Telemetry
{
    /// <summary>
    /// M7: a whole session driven through the EventBus, the way the game drives it — and the JSON it
    /// becomes, checked against the contract the backend is built from
    /// (<c>Docs/Contracts/telemetry_session.sample.json</c>). Regenerate the sample after an intended
    /// change with <c>SVL_WRITE_CONTRACT=1</c>, and tell the backend team.
    /// </summary>
    public class TelemetrySessionTests
    {
        private static string ContractPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "Contracts", "telemetry_session.sample.json"));

        /// <summary>
        /// Two fights on Map 1: the first escaped (contact at 1 s, both guards gone by 6 s, one of them
        /// killed), the second a capture. The sqa plan fell back to greedy after a timeout.
        /// </summary>
        private static TelemetrySessionDto Scenario(out TelemetrySession session)
        {
            double t = 0;
            var context = new SessionContext
            {
                SessionId = "00000000-0000-4000-8000-000000000001", MapId = "map01", ContentVersion = "1.0.0",
                BuildVersion = "0.1.0", Platform = "editor", ConfiguredVariant = "sqa",
                StartedAtUtc = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc),
            };
            session = new TelemetrySession(context, new TelemetryConfig { PositionSampleSeconds = 5 }, () => t);
            session.Subscribe();
            try
            {
                session.SamplePlayer(new Vector3(1, 0, 2));
                EventBus.Publish(new NoiseEvent(new Vector3(1, 0, 2), 9, NoiseKind.Footstep, 0));
                t = .4; EventBus.Publish(new NoiseEvent(new Vector3(1, 0, 3), 9, NoiseKind.Footstep, 1));
                t = .8; EventBus.Publish(new NoiseEvent(new Vector3(2, 0, 3), 30, NoiseKind.Stone, 2));
                t = 1; EventBus.Publish(new PlayerContactEvent("guard_c1_0", new Vector3(3, 0, 4), true));
                EventBus.Publish(new SquadAlertEvent("guard_c1_0", "C1", 1, 0, ""));
                t = 1.1; EventBus.Publish(new EnemyStateChangedEvent("guard_c1_0", EnemyStateId.SpotPlayer, EnemyStateId.TakeCover));
                EventBus.Publish(new EnemyStateChangedEvent("guard_c1_1", EnemyStateId.Patrol, EnemyStateId.TakeCover));
                EventBus.Publish(new ReplanStartedEvent("C1", "first_contact", "sqa", "Map 1-abc-1", 2, 24, 3));
                t = 1.3;
                EventBus.Publish(new PlanAppliedEvent("C1", "greedy", "Map 1-abc-1", 161.2, -12.5, 12.5, 2.0 / 3, true,
                    "sqa: sidecar timeout (160 ms)", new[]
                    {
                        new AgentAssignment("guard_c1_0", 4, "block", new Vector3(10, 0, 10), 0),
                        new AgentAssignment("guard_c1_1", 7, "flank", new Vector3(-6, 0, 12), 140),
                    }));
                t = 2; EventBus.Publish(new PlayerAttackEvent("rifle", new Vector3(3, 0, 4)));
                t = 2.1; EventBus.Publish(new PlayerAttackEvent("rifle", new Vector3(3, 0, 4))); // Rate-limited.
                t = 2.5; EventBus.Publish(new EnemyKilledEvent("guard_c1_1", "weapon", "rifle", new Vector3(-5, 0, 11)));
                t = 3; EventBus.Publish(new PlanDiscardedEvent("C1", "player moved 12 m"));
                t = 6; EventBus.Publish(new EnemyStateChangedEvent("guard_c1_0", EnemyStateId.Engage, EnemyStateId.Investigate));
                session.SamplePlayer(new Vector3(20, 0, 30));
                t = 9; session.Tick();
                EventBus.Publish(new ObjectiveCompletedEvent("map01", "rescue_hung", 0));
                EventBus.Publish(new TakedownEvent("guard_b_0", new Vector3(0, 0, 50)));
                EventBus.Publish(new EnemyKilledEvent("guard_b_0", "takedown", "knife", new Vector3(0, 0, 50)));
                EventBus.Publish(new SolverChangedEvent("sqa", null));
                EventBus.Publish(new SidecarStatusEvent(false, "http://127.0.0.1:8001"));
                t = 20; EventBus.Publish(new PlayerContactEvent("guard_c2_0", new Vector3(40, 0, 60), false));
                t = 22; EventBus.Publish(new PlayerDiedEvent(new Vector3(40, 0, 61)));
                EventBus.Publish(new MissionEndedEvent("map01", "failed"));
                t = 25;
                return session.Finish(playerDead: true, coordinationLog: "coordination-abc.jsonl");
            }
            finally { session.Unsubscribe(); }
        }

        [Test]
        public void ASessionBecomesEncountersMetricsAndSampledEvents()
        {
            var dto = Scenario(out var session);

            Assert.AreEqual("failed", dto.MissionResult, "The map's own result wins over 'died'.");
            Assert.AreEqual(25, dto.DurationSeconds, 1e-9);
            Assert.AreEqual("sqa", dto.AiVariant, "Attributed to the variant asked for, though greedy answered.");

            Assert.AreEqual(2, dto.Encounters.Count);
            Assert.AreEqual("escaped", dto.Encounters[0].Result);
            Assert.AreEqual(5.0, dto.Encounters[0].DurationSeconds, 1e-9);
            Assert.AreEqual(1, dto.Encounters[0].Kills);
            Assert.AreEqual(1, dto.Encounters[0].Replans);
            Assert.AreEqual("captured", dto.Encounters[1].Result);

            var s = dto.Summary;
            Assert.AreEqual(.5, s.CaptureRate, 1e-9);
            Assert.AreEqual(5.0, dto.EscapeTimeSeconds, 1e-9);
            Assert.AreEqual(2, s.EnemiesKilled);
            Assert.AreEqual(1, s.Takedowns);
            Assert.AreEqual(2, s.PlayerAttacks);
            Assert.AreEqual(1, s.PlayerDeaths);
            Assert.AreEqual(1, s.Plans);
            Assert.AreEqual(1.0, s.FallbackRate, 1e-9);
            Assert.IsFalse(s.ValidForComparison, "Every plan fell back: this session measured greedy, not sqa.");
            Assert.AreEqual("coordination-abc.jsonl", s.CoordinationLog);

            Assert.AreEqual(1, dto.Solver.TimeoutCount);
            Assert.AreEqual(1, dto.Solver.FallbackCount);
            Assert.AreEqual(.6667, dto.Solver.CoordinationScore, 1e-4);

            Assert.AreEqual(3, s.EventCounts["noise_emitted"], "3 noises counted...");
            Assert.AreEqual(2, dto.Events.Count(e => e.Type == "noise_emitted"), "...one footstep sampled out.");
            Assert.AreEqual(1, dto.Events.Count(e => e.Type == "shot_fired"));
            Assert.AreEqual(2, dto.Events.Count(e => e.Type == "player_position"));
            Assert.AreEqual(1, dto.Events.Count(e => e.Type == "solver_timeout"));
            var replan = dto.Events.Single(e => e.Type == "coordination_replan");
            Assert.AreEqual("first_contact", replan.Payload["trigger"]);
            Assert.AreEqual("sqa", replan.Payload["variant"]);
            Assert.AreEqual("take_cover", dto.Events.First(e => e.Type == "enemy_state_changed").Payload["to"]);

            Assert.IsTrue(session.Finished);
            Assert.AreSame(dto, session.Finish(), "Finishing twice returns the same batch.");
        }

        [Test]
        public void AFinishedSessionNoLongerListens()
        {
            Scenario(out var session);
            int before = session.Events.Events.Count;
            EventBus.Publish(new PlayerContactEvent("late", Vector3.zero, true));
            Assert.AreEqual(before, session.Events.Events.Count);
        }

        [Test]
        public void TheBatchCarriesNothingPersonal()
        {
            string json = ContentJson.Serialize(Scenario(out _));
            foreach (string personal in new[] { Environment.UserName, Environment.MachineName, SystemInfo.deviceUniqueIdentifier, SystemInfo.deviceName })
                if (!string.IsNullOrEmpty(personal) && personal.Length > 2)
                    Assert.IsFalse(json.IndexOf(personal, StringComparison.OrdinalIgnoreCase) >= 0, $"The batch contains '{personal}'.");
            var top = JObject.Parse(json).Properties().Select(p => p.Name).ToList();
            CollectionAssert.IsSubsetOf(top, new[]
            {
                "schema_version", "session_id", "content_version", "ai_variant", "build_version", "platform", "started_at",
                "ended_at", "map_id", "duration_seconds", "mission_result", "escape_time_seconds", "solver", "summary",
                "encounters", "events",
            }, "A new top-level field must be reviewed for personal data first.");
        }

        [Test]
        public void TheJsonMatchesTheBackendContract()
        {
            string json = ContentJson.Serialize(Scenario(out _), indented: true);
            if (Environment.GetEnvironmentVariable("SVL_WRITE_CONTRACT") == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ContractPath));
                File.WriteAllText(ContractPath, json.Replace("\r\n", "\n") + "\n");
                Assert.Pass("Contract sample rewritten: " + ContractPath);
            }
            Assert.IsTrue(File.Exists(ContractPath), "Missing " + ContractPath + " (run once with SVL_WRITE_CONTRACT=1).");
            var expected = Paths(JToken.Parse(File.ReadAllText(ContractPath)));
            var actual = Paths(JToken.Parse(json));
            var missing = expected.Except(actual).ToList();
            var extra = actual.Except(expected).ToList();
            Assert.IsTrue(missing.Count == 0 && extra.Count == 0,
                "The telemetry JSON no longer matches the contract the backend builds against.\n" +
                $"missing: {string.Join(", ", missing)}\nnew: {string.Join(", ", extra)}\n" +
                "If intended: SVL_WRITE_CONTRACT=1, bump TelemetrySessionDto.CurrentSchemaVersion if a field changed meaning, tell the backend team.");
        }

        /// <summary>Every field path, events keyed by type so each event's payload is part of the contract.</summary>
        private static SortedSet<string> Paths(JToken token, string prefix = "", SortedSet<string> acc = null)
        {
            acc ??= new SortedSet<string>(StringComparer.Ordinal);
            if (token is JObject o)
                foreach (var p in o.Properties())
                {
                    string key = prefix + "." + p.Name;
                    acc.Add(key);
                    Paths(p.Value, key, acc);
                }
            else if (token is JArray a)
                foreach (var item in a)
                {
                    string key = prefix + "[]";
                    if (prefix == ".events" && item is JObject e && e["type"] != null) key = prefix + "[" + e["type"] + "]";
                    Paths(item, key, acc);
                }
            return acc;
        }
    }
}
