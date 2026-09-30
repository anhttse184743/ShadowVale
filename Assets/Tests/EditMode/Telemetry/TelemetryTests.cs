using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Fsm;
using ShadowVale.Telemetry;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.Telemetry
{
    /// <summary>M7: the research metrics computed from the event stream — encounters, solver stats, plan diversity.</summary>
    public class TelemetryTests
    {
        // ---- Encounters (capture rate, escape time) ----------------------------------------------

        [Test]
        public void AnEncounterIsEscapedWhenEveryGuardGaveUpAndTheEscapeTimeEndsWithTheLastOne()
        {
            var e = new EncounterTracker();
            e.OnContact("g1", 10);
            e.OnStateChanged("g1", EnemyStateId.TakeCover, 10.1);
            e.OnStateChanged("g2", EnemyStateId.Engage, 11);
            e.OnStateChanged("g1", EnemyStateId.Investigate, 14);
            e.OnStateChanged("g2", EnemyStateId.Investigate, 15);
            e.Tick(16);
            Assert.IsTrue(e.InEncounter, "Within the grace period a guard may still come back.");
            e.Tick(17.1);
            Assert.IsFalse(e.InEncounter);
            var enc = e.Finished.Single();
            Assert.AreEqual("escaped", enc.Result);
            Assert.AreEqual(5.0, enc.DurationSeconds, 1e-9, "Contact at 10, the last guard gave up at 15.");
            Assert.AreEqual(2, enc.Agents);
        }

        [Test]
        public void AGuardBackInTheFightWithinTheGraceKeepsTheEncounterOpen()
        {
            var e = new EncounterTracker();
            e.OnContact("g1", 0);
            e.OnStateChanged("g1", EnemyStateId.Investigate, 3);
            e.OnStateChanged("g1", EnemyStateId.Engage, 4);
            e.Tick(6);
            Assert.IsTrue(e.InEncounter);
            e.OnStateChanged("g1", EnemyStateId.Patrol, 9);
            e.Tick(12);
            Assert.AreEqual(9.0, e.Finished.Single().DurationSeconds, 1e-9);
        }

        [Test]
        public void SuspicionAloneIsNoEncounter()
        {
            var e = new EncounterTracker();
            e.OnStateChanged("g1", EnemyStateId.SpotPlayer, 1);
            e.OnStateChanged("g1", EnemyStateId.Investigate, 2);
            e.Finish(10);
            Assert.IsEmpty(e.Finished);
        }

        [Test]
        public void DeathDuringAFightIsACaptureAndKillingTheLastFighterClearsIt()
        {
            var e = new EncounterTracker();
            e.OnContact("g1", 0);
            e.OnPlayerDied(3);
            e.OnContact("g2", 10);
            e.OnStateChanged("g3", EnemyStateId.TakeCover, 10.5);
            e.OnEnemyKilled("g2", 12);
            Assert.IsTrue(e.InEncounter, "g3 is still fighting.");
            e.OnEnemyKilled("g3", 13);
            e.OnContact("g4", 20);
            e.Finish(25);
            CollectionAssert.AreEqual(new[] { "captured", "cleared", "unresolved" }, e.Finished.Select(x => x.Result));
            Assert.AreEqual(2, e.Finished[1].Kills);

            var s = new ShadowVale.Data.Telemetry.TelemetrySummaryDto();
            e.Summarise(s);
            Assert.AreEqual(0.5, s.CaptureRate, 1e-9, "1 capture of 2 resolved; the unresolved one does not count.");
        }

        [Test]
        public void PlansAreAttributedToTheOpenEncounter()
        {
            var e = new EncounterTracker();
            e.OnPlan("sqa", 1, false); // No fight yet: ignored.
            e.OnContact("g1", 0);
            e.OnPlan("sqa", .5, false);
            e.OnPlan("sqa", 1, true);
            e.Finish(5);
            var enc = e.Finished.Single();
            Assert.AreEqual(2, enc.Replans);
            Assert.AreEqual(1, enc.Fallbacks);
            Assert.AreEqual(.75, enc.CoordinationScore, 1e-9);
            Assert.AreEqual("sqa", enc.Variants);
        }

        // ---- Solver stats ------------------------------------------------------------------------

        [Test]
        public void PercentilesInterpolateLikeNumpy()
        {
            var v = new List<double> { 1, 2, 3, 4 };
            Assert.AreEqual(2.5, SolverStats.Percentile(v, 50), 1e-12);
            Assert.AreEqual(3.85, SolverStats.Percentile(v, 95), 1e-12); // np.percentile([1,2,3,4], 95)
            Assert.AreEqual(0, SolverStats.Percentile(new List<double>(), 50));
        }

        [Test]
        public void SolverMetricsCountTimeoutsAndFallbacks()
        {
            var s = new SolverStats();
            s.Add("sqa", 80, false, false, 10, 1);
            s.Add("sqa", 170, true, true, 8, .5);
            s.Add("greedy", 1, false, false, 9, 0);
            var m = s.ToMetrics("greedy");
            Assert.AreEqual("sqa", m.SolverVariantId, "The variant most plans came from.");
            Assert.AreEqual(3, m.SolveCount);
            Assert.AreEqual(1, m.TimeoutCount);
            Assert.AreEqual(1, m.FallbackCount);
            Assert.AreEqual(80, m.LatencyP50Ms, 1e-9);
            Assert.AreEqual(170, m.LatencyMaxMs, 1e-9);
            Assert.AreEqual(9, m.ObjectiveMean, 1e-9);
            Assert.AreEqual(.5, m.CoordinationScore, 1e-9);
        }

        // ---- Plan diversity ----------------------------------------------------------------------

        [Test]
        public void TheSamePlanOverAndOverHasNoEntropy()
        {
            var d = new PlanDiversity();
            for (int i = 0; i < 3; i++)
                d.Add(new List<(string, Vector3)> { ("a", new Vector3(1, 0, 1)), ("b", new Vector3(10, 0, 10)) });
            // A metre off is the same spot (same 4 m cell).
            d.Add(new List<(string, Vector3)> { ("b", new Vector3(10.5f, 0, 10.5f)), ("a", new Vector3(1.5f, 0, 1.2f)) });
            Assert.AreEqual(4, d.Plans);
            Assert.AreEqual(1, d.DistinctPlans);
            Assert.AreEqual(0, d.PlanEntropyBits, 1e-12);
            Assert.AreEqual(0, d.AgentPositionEntropyBits, 1e-12);
        }

        [Test]
        public void FourDifferentPlansAreTwoBits()
        {
            var d = new PlanDiversity();
            for (int i = 0; i < 4; i++)
                d.Add(new List<(string, Vector3)> { ("a", new Vector3(i * 10, 0, 0)), ("b", new Vector3(0, 0, 50)) });
            Assert.AreEqual(2, d.PlanEntropyBits, 1e-12);
            Assert.AreEqual(1, d.AgentPositionEntropyBits, 1e-12, "a: 2 bits, b always the same spot: 0 bits.");
        }

        // ---- Event recorder ----------------------------------------------------------------------

        [Test]
        public void TheRecorderRateLimitsAndCapsButCountsEverything()
        {
            var r = new EventRecorder("map01", 3);
            Assert.IsTrue(r.Add("noise_emitted", 0, null, 1));
            Assert.IsFalse(r.Add("noise_emitted", .5, null, 1), "Within a second of the last kept one.");
            Assert.IsTrue(r.Add("noise_emitted", 1.2, null, 1));
            Assert.IsTrue(r.Add("takedown", 2, null));
            Assert.IsFalse(r.Add("takedown", 3, null), "Cap of 3 reached.");
            Assert.AreEqual(3, r.Events.Count);
            Assert.AreEqual(3, r.Counts["noise_emitted"]);
            Assert.AreEqual(2, r.Counts["takedown"]);
            Assert.AreEqual(1, r.Dropped);
            Assert.AreEqual("map01", r.Events[0].MapId);
        }

        [Test]
        public void RetryDelaysDoubleUpToFiveMinutes()
        {
            Assert.AreEqual(0, RetryPolicy.DelaySeconds(0));
            Assert.AreEqual(5, RetryPolicy.DelaySeconds(1));
            Assert.AreEqual(10, RetryPolicy.DelaySeconds(2));
            Assert.AreEqual(300, RetryPolicy.DelaySeconds(40));
        }

        [Test]
        public void StateNamesAreSnakeCase()
        {
            Assert.AreEqual("take_cover", TelemetrySession.Snake(nameof(EnemyStateId.TakeCover)));
            Assert.AreEqual("patrol", TelemetrySession.Snake(nameof(EnemyStateId.Patrol)));
        }
    }
}
