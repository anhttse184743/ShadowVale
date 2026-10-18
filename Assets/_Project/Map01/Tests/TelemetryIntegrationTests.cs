using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Coordination;
using ShadowVale.Core.Events;
using ShadowVale.Telemetry;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// M7 in Map 1: a fight, a kill and an objective reach telemetry through the shared events alone,
    /// and the finished session lands on disk (research export + upload queue) with uploads off.
    /// </summary>
    public sealed class TelemetryIntegrationTests : ForestSceneTestBase
    {
        [UnityTest]
        public IEnumerator AFightAKillAndAnObjectiveEndUpInTheSessionBatch()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }

        private static IEnumerator InPlayMode()
        {
            yield return null;
            yield return null;
            string root = Path.Combine(Path.GetTempPath(), "svl-map01-telemetry-" + Guid.NewGuid().ToString("N"));
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            // Batch-mode runs have telemetry off; an editor run already has a session — close it first.
            var running = mission.GetComponent<TelemetryService>();
            if (running != null) running.End(upload: false);
            var config = new TelemetryConfig
            {
                UploadEnabled = false, RootDirectory = Path.Combine(root, "telemetry"), ResearchDirectory = Path.Combine(root, "research"),
            };
            var service = TelemetryService.Attach(mission.gameObject, Map01Mission.MapId, config);
            Assert.IsNotNull(service);

            int plans = 0;
            Action<PlanAppliedEvent> onPlan = _ => plans++;
            EventBus.Subscribe(onPlan);
            try
            {
                var guard = mission.Enemies.First(e => e.SquadId == "C2" && e.Alive);
                guard.DebugDamage(.1f); // Shot at: contact, the camp fights, a plan.
                for (float until = Time.time + 5f; plans == 0 && Time.time < until;) yield return null;
                Assert.Greater(plans, 0, "The fight was planned.");
                guard.DebugDamage(1f);
                yield return null;
                yield return null;

                var quest = mission.GetComponent<Map01Quest>();
                quest.RestoreStage(Map01Quest.ScoutStage); // A checkpoint restore: not an objective.
                quest.SuppliesRecovered();                 // Played: point 1's supplies are back, its objective is done.

                var dto = service.End();
                Assert.AreEqual("map01", dto.MapId);
                Assert.IsTrue(dto.Events.Any(e => e.Type == "player_spotted"));
                Assert.IsTrue(dto.Events.Any(e => e.Type == "coordination_replan"));
                var kill = dto.Events.Single(e => e.Type == "enemy_killed");
                Assert.AreEqual(guard.LegacyId, kill.Payload["victim"]);
                Assert.AreEqual("weapon", kill.Payload["method"]);
                var objective = dto.Events.Single(e => e.Type == "objective_completed");
                Assert.AreEqual("scout_camps", objective.Payload["objective"]);
                Assert.AreEqual(Map01Quest.ScoutStage, objective.Payload["index"]);
                Assert.GreaterOrEqual(dto.Encounters.Count, 1);
                Assert.AreEqual(1, dto.Summary.EnemiesKilled);
                Assert.Greater(dto.Solver.SolveCount, 0);
                Assert.IsNotNull(dto.Summary.CoordinationLog, "Linked to the coordination-instance log of the same fights.");

                var queued = new DiskQueue(config.RootDirectory).Pending();
                Assert.AreEqual(1, queued.Count);
                Assert.AreEqual(dto.SessionId, DiskQueue.SessionIdOf(queued[0]));
                Assert.IsTrue(File.Exists(Path.Combine(config.ResearchDirectory, ResearchExporter.FileName)));
            }
            finally
            {
                EventBus.Unsubscribe(onPlan);
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }
    }
}
