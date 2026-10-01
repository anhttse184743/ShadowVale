using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Perception;

namespace ShadowVale.Tests.EditMode.AI
{
    /// <summary>M10 (NFR 60 FPS): calm guards look 10 times a second, staggered; alert ones every frame.</summary>
    public class PerceptionScheduleTests
    {
        [Test]
        public void ACalmGuardLooksTenTimesASecond()
        {
            var s = new PerceptionSchedule();
            int looks = 0;
            for (int f = 0; f < 600; f++) if (s.Due(f / 60f, alert: false, "guard_c1_0")) looks++; // 10 s at 60 FPS
            Assert.That(looks, Is.InRange(99, 101));
        }

        [Test]
        public void AnAlertGuardLooksEveryFrame()
        {
            var s = new PerceptionSchedule();
            int looks = 0;
            for (int f = 0; f < 60; f++) if (s.Due(f / 60f, alert: true, "guard_c1_0")) looks++;
            Assert.AreEqual(60, looks);
        }

        [Test]
        public void GuardsAreSpreadOverTheIntervalTheSameWayEveryRun()
        {
            var ids = Enumerable.Range(0, 12).Select(i => "guard_" + i).ToList();
            var phases = ids.Select(PerceptionSchedule.Phase).ToList();
            Assert.Greater(phases.Distinct().Count(), 8, "Not all on the same frame.");
            Assert.IsTrue(phases.All(p => p >= 0 && p < 1));
            CollectionAssert.AreEqual(phases, ids.Select(PerceptionSchedule.Phase).ToList(), "Deterministic.");
        }

        [Test]
        public void AfterAResetTheGuardLooksAtOnce()
        {
            var s = new PerceptionSchedule();
            s.Due(0f, false, "g");
            s.Due(0.01f, false, "g");
            s.Reset();
            Assert.IsTrue(s.Due(0.02f, false, "g"));
        }
    }
}
