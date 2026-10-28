using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Enemies;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.Gameplay
{
    /// <summary>Enemy fire no longer always hits: range, the target's stance and movement, the shooter's
    /// movement and how long he has been aiming decide; misses fly past, never through.</summary>
    public class ShotAccuracyTests
    {
        private static readonly ShotAccuracyProfile P = new ShotAccuracyProfile();

        private static ShotContext Shot(float distance = 5f, float aimed = 10f, bool sprint = false, bool moving = false,
            bool crouched = false, bool cover = false, bool shooterMoving = false, bool boss = false)
            => new ShotContext(distance, 18f, aimed, sprint, sprint || moving, crouched, cover, shooterMoving, boss);

        [Test]
        public void CloseAndSettledHeUsuallyHitsFarHeOftenMisses()
        {
            Assert.AreEqual(P.closeHit, ShotAccuracy.HitChance(P, Shot(3f)), 1e-5f);
            Assert.AreEqual(P.farHit, ShotAccuracy.HitChance(P, Shot(18f)), 1e-5f);
            float mid = ShotAccuracy.HitChance(P, Shot(11.5f));
            Assert.AreEqual((P.closeHit + P.farHit) / 2, mid, 1e-4f, "Linear in between.");
            Assert.Less(mid, 1f, "Not every round hits any more.");
        }

        [Test]
        public void MovingCrouchingOrInCoverHeIsHarderToHit()
        {
            float still = ShotAccuracy.HitChance(P, Shot());
            Assert.Less(ShotAccuracy.HitChance(P, Shot(moving: true)), still);
            Assert.Less(ShotAccuracy.HitChance(P, Shot(sprint: true)), ShotAccuracy.HitChance(P, Shot(moving: true)), "Sprinting beats walking.");
            Assert.Less(ShotAccuracy.HitChance(P, Shot(crouched: true)), still);
            Assert.Less(ShotAccuracy.HitChance(P, Shot(cover: true)), still);
            Assert.Less(ShotAccuracy.HitChance(P, Shot(shooterMoving: true)), still, "Firing on the run is worse.");
            Assert.GreaterOrEqual(ShotAccuracy.HitChance(P, Shot(18f, aimed: 0, sprint: true, crouched: true, cover: true, shooterMoving: true)), P.minHit);
        }

        [Test]
        public void TheCommanderHitsAStillTargetByHisOwnCurve()
        {
            // 19/10/2026: 100% at 5 m, 89% at 10, 80% at 15, 72% at 18 against a still, upright Nam (aim settled).
            Assert.AreEqual(1f, ShotAccuracy.HitChance(P, Shot(5f, boss: true)), 1e-4f);
            Assert.AreEqual(.89f, ShotAccuracy.HitChance(P, Shot(10f, boss: true)), 1e-4f);
            Assert.AreEqual(.80f, ShotAccuracy.HitChance(P, Shot(15f, boss: true)), 1e-4f);
            Assert.AreEqual(.72f, ShotAccuracy.HitChance(P, Shot(18f, boss: true)), 1e-4f);
            Assert.AreEqual(.845f, ShotAccuracy.HitChance(P, Shot(12.5f, boss: true)), 1e-4f, "Straight line between the points.");
            Assert.AreEqual(1f, ShotAccuracy.HitChance(P, Shot(2f, boss: true)), 1e-4f, "Nearer than 5 m: as at 5 m.");
            // Nam's other states: the same multipliers as for a guard.
            Assert.AreEqual(.89f * P.walkingTarget, ShotAccuracy.HitChance(P, Shot(10f, moving: true, boss: true)), 1e-4f);
            Assert.AreEqual(.89f * P.sprintingTarget, ShotAccuracy.HitChance(P, Shot(10f, sprint: true, boss: true)), 1e-4f);
            Assert.AreEqual(.89f * P.crouchedTarget * P.coverTarget, ShotAccuracy.HitChance(P, Shot(10f, crouched: true, cover: true, boss: true)), 1e-4f);
            Assert.AreEqual(.89f * P.bossMovingShooter, ShotAccuracy.HitChance(P, Shot(10f, shooterMoving: true, boss: true)), 1e-4f);
            Assert.AreEqual(.5f, ShotAccuracy.HitChance(P, Shot(5f, aimed: 0, boss: true)), 1e-4f, "His first round still waits for his aim to settle.");
        }

        [Test]
        public void AimSettlesAfterTheFirstShotsAndTheCommanderShootsBetter()
        {
            Assert.AreEqual(P.closeHit * P.firstShot, ShotAccuracy.HitChance(P, Shot(aimed: 0)), 1e-5f);
            Assert.AreEqual(P.closeHit, ShotAccuracy.HitChance(P, Shot(aimed: P.settleSeconds)), 1e-5f);
            var noCurve = new ShotAccuracyProfile { bossCurveDistances = new float[0], bossCurveHits = new float[0] };
            float guard = ShotAccuracy.HitChance(noCurve, Shot(12f)), boss = ShotAccuracy.HitChance(noCurve, Shot(12f, boss: true));
            Assert.AreEqual(guard * noCurve.bossSkill, boss, 1e-5f, "Without his curve: a guard's line, better.");
            Assert.LessOrEqual(ShotAccuracy.HitChance(noCurve, Shot(1f, boss: true)), noCurve.maxHit, "Never a sure hit.");
            Assert.LessOrEqual(ShotAccuracy.HitChance(P, Shot(1f)), P.maxHit, "A guard: never a sure hit.");

            var aim = new AimTracker();
            Assert.AreEqual(0f, aim.Shot(10f));
            Assert.AreEqual(1.6f, aim.Shot(11.6f), 1e-5f);
            Assert.AreEqual(0f, aim.Shot(11.6f + AimTracker.ResetSeconds + .1f), "A pause: his aim starts over.");
        }

        [Test]
        public void OffItIsTheOldSureHit()
            => Assert.AreEqual(1f, ShotAccuracy.HitChance(new ShotAccuracyProfile { enabled = false }, Shot(18f, aimed: 0, sprint: true)));

        [Test]
        public void AMissFliesPastHimNeverThroughAndRollsTheSameEachRun()
        {
            var from = new Vector3(0, 1.3f, 0);
            var target = new Vector3(0, 1f, 12f);
            var rng = new System.Random(1);
            bool left = false, right = false;
            for (int i = 0; i < 200; i++)
            {
                Vector3 miss = ShotAccuracy.MissPoint(from, target, rng);
                Assert.Greater(miss.z, target.z + 5f, "On past him.");
                // Where the line crosses his depth: at least 0.6 m off his centre, clear of his body.
                Vector3 atHim = from + (miss - from) * ((target.z - from.z) / (miss.z - from.z));
                Assert.Greater(new Vector2(atHim.x - target.x, atHim.y - target.y).magnitude, .55f, "Never through him.");
                if (miss.x < 0) left = true; else right = true;
            }
            Assert.IsTrue(left && right, "Both sides.");

            var a = ShotAccuracy.RngFor("guard_c2_1");
            var b = ShotAccuracy.RngFor("guard_c2_1");
            Assert.IsTrue(Enumerable.Range(0, 20).All(_ => a.NextDouble() == b.NextDouble()), "Same guard, same rolls: replays repeat.");
        }
    }
}
