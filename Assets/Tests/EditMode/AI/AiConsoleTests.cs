using NUnit.Framework;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using ShadowVale.Debugging;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.AI
{
    /// <summary>The debug console's log (3b): ring buffer, and AI events turned into lines tagged with the agent's squad.</summary>
    public class AiConsoleTests
    {
        private sealed class StubAgent : IEnemyAgent
        {
            public string Id { get; set; }
            public string SquadId { get; set; }
            public EnemyBrain Brain => null;
            public Vector3 Position => Vector3.zero;
            public Vector3 Forward => Vector3.forward;
            public bool Alive => true;
            public float HealthFraction => 1f;
            public bool SeesPlayer => false;
            public float VisionRange => 20f;
            public float VisionConeAngle => 110f;
            public void DebugEngage(float seconds) { }
            public void DebugDamage(float fractionOfMax) { }
        }

        [Test]
        public void RingBufferKeepsTheNewestLines()
        {
            var log = new AiConsoleLog(capacity: 16);
            for (int i = 0; i < 20; i++) log.Add(new AiLogEntry(i, AiLogCategory.State, "", "line " + i));
            Assert.AreEqual(16, log.Count);
            Assert.AreEqual("line 4", log[0].Text);
            Assert.AreEqual("line 19", log[15].Text);
            log.Paused = true;
            log.Add(new AiLogEntry(99, AiLogCategory.State, "", "ignored"));
            Assert.AreEqual("line 19", log[15].Text, "Paused: nothing is added.");
        }

        [Test]
        public void FeedLogsAiEventsWithTheAgentsSquad()
        {
            var agent = new StubAgent { Id = "guard_c2_1", SquadId = "C2" };
            AgentRegistry.Register(agent);
            var log = new AiConsoleLog();
            var feed = new AiConsoleFeed(log);
            try
            {
                EventBus.Publish(new PlayerContactEvent("guard_c2_1", new Vector3(3, 0, 4), seen: true));
                EventBus.Publish(new EnemyStateChangedEvent("guard_c2_1", EnemyStateId.SpotPlayer, EnemyStateId.Engage));
                EventBus.Publish(new NoiseEvent(Vector3.zero, 3f, NoiseKind.Footstep, 0));
                EventBus.Publish(new NoiseEvent(Vector3.zero, 14f, NoiseKind.Stone, 2));
                Assert.AreEqual(3, log.Count, "An unheard footstep is filtered out by default.");
                Assert.AreEqual(AiLogCategory.Contact, log[0].Category);
                Assert.AreEqual("C2", log[0].Squad);
                StringAssert.Contains("thấy Nam", log[0].Text);
                StringAssert.Contains("SpotPlayer → Engage", log[1].Text);
                StringAssert.Contains("2 lính nghe", log[2].Text);

                feed.VerboseNoise = true;
                EventBus.Publish(new NoiseEvent(Vector3.zero, 3f, NoiseKind.Footstep, 0));
                Assert.AreEqual(4, log.Count);
            }
            finally
            {
                feed.Dispose();
                AgentRegistry.Unregister(agent);
            }
            EventBus.Publish(new EnemyStateChangedEvent("x", EnemyStateId.Patrol, EnemyStateId.Investigate));
            Assert.AreEqual(4, log.Count, "Disposed feed stops listening.");
        }

        [Test]
        public void EveryLineSaysWhoDecidedIt()
        {
            var log = new AiConsoleLog();
            var feed = new AiConsoleFeed(log);
            try
            {
                var assignments = new[] { new ShadowVale.AI.Coordination.AgentAssignment("guard_c2_1", 37, "block", Vector3.zero, 0f) };
                EventBus.Publish(new ShadowVale.AI.Coordination.PlanAppliedEvent("C2", "qiea", "i1", 85.3, -14.2, 14.2, .67, false, null, assignments));
                StringAssert.StartsWith("[SOLVER qiea]", log[0].Text, "A plan from the chosen solver says so.");
                StringAssert.Contains("giao guard_c2_1 → node 37", log[1].Text);
                StringAssert.StartsWith("[SOLVER qiea]", log[1].Text);

                EventBus.Publish(new ShadowVale.AI.Coordination.PlanAppliedEvent("C2", "greedy", "i2", 160.2, -12, 12, .5, true,
                    "qiea: sidecar timeout (160 ms)", assignments));
                StringAssert.StartsWith("[GREEDY dự phòng]", log[2].Text, "When qiea did not answer, greedy made the plan — and the line says it.");
                StringAssert.Contains("fallback: qiea: sidecar timeout", log[2].Text);

                EventBus.Publish(new EnemyStateChangedEvent("guard_c2_1", EnemyStateId.Patrol, EnemyStateId.Investigate));
                StringAssert.StartsWith("[FSM]", log[4].Text, "A state change is the guard's own state machine.");
                EventBus.Publish(new PlayerContactEvent("guard_c2_1", Vector3.zero, seen: true));
                StringAssert.StartsWith("[CẢM BIẾN]", log[5].Text);
            }
            finally { feed.Dispose(); }
        }
    }
}
