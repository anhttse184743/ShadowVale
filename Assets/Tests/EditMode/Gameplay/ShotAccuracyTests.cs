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
        public void AimSettlesAfterTheFirstShotsAndTheCommanderShootsBetter()
        {
            Assert.AreEqual(P.closeHit * P.firstShot, ShotAccuracy.HitChance(P, Shot(aimed: 0)), 1e-5f);
            Assert.AreEqual(P.closeHit, ShotAccuracy.HitChance(P, Shot(aimed: P.settleSeconds)), 1e-5f);
            float guard = ShotAccuracy.HitChance(P, Shot(12f)), boss = ShotAccuracy.HitChance(P, Shot(12f, boss: true));
            Assert.AreEqual(guard * P.bossSkill, boss, 1e-5f);
            Assert.LessOrEqual(ShotAccuracy.HitChance(P, Shot(1f, boss: true)), P.maxHit, "Never a sure hit.");

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
