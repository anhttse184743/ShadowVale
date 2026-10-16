using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.Gameplay
{
    public sealed class WeaponSpreadStateTests
    {
        private const float RoundInterval = 0.1f; // 600 rpm

        /// <summary>Empties a magazine at the weapon's cyclic rate and totals the view kick.</summary>
        private static Vector2 Dump(WeaponSpreadState spread, int rounds, float startTime = 0f)
        {
            var total = Vector2.zero;
            for (var i = 0; i < rounds; i++)
            {
                total += spread.Fire(startTime + i * RoundInterval);
            }
            return total;
        }

        [Test]
        public void EveryTapIsShotZero()
        {
            var spread = new WeaponSpreadState();
            var time = 0f;

            for (var tap = 0; tap < 5; tap++)
            {
                Vector2 kick = spread.Fire(time);
                Assert.AreEqual(WeaponSpreadState.VerticalKick(0), kick.x, 1e-4f,
                    "a deliberate tap must always be the weapon's most accurate shot");
                Assert.AreEqual(0f, kick.y, 1e-4f, "and must never pull sideways");
                Assert.AreEqual(0f, spread.Bloom, 1e-4f);
                time += WeaponSpreadState.BurstResetSeconds + 0.01f;
            }
        }

        [Test]
        public void AGapShorterThanTheResetKeepsTheBurstGoing()
        {
            var spread = new WeaponSpreadState();
            spread.Fire(0f);
            spread.Fire(WeaponSpreadState.BurstResetSeconds - 0.01f);

            Assert.AreEqual(2, spread.ShotIndex, "the burst continued");
        }

        [Test]
        public void ShortBurstsAddNoBloom()
        {
            var spread = new WeaponSpreadState();
            Dump(spread, WeaponSpreadState.FreeBloomShots);

            Assert.AreEqual(0f, spread.Bloom, 1e-4f,
                "two and three round bursts have to stay exact, or controlled fire is pointless");
        }

        [Test]
        public void TheFourthRoundStartsTheBloom()
        {
            var spread = new WeaponSpreadState();
            Dump(spread, WeaponSpreadState.FreeBloomShots + 1);

            Assert.Greater(spread.Bloom, 0f);
        }

        [Test]
        public void BloomIsFullByTheFifteenthRound()
        {
            var spread = new WeaponSpreadState();
            Dump(spread, 15);

            Assert.AreEqual(1f, spread.Bloom, 1e-3f, "half a magazine reaches the worst cone");
        }

        [Test]
        public void AThreeRoundBurstClimbsBarelyADegree()
        {
            // The burst length the design is built around. Anything more than about a degree
            // over three rounds and a controlled burst stops landing on a torso at range.
            var spread = new WeaponSpreadState();
            Vector2 total = Dump(spread, 3);

            Assert.That(total.x, Is.InRange(0.9f, 1.3f));
            Assert.AreEqual(0f, total.y, 1e-4f);
        }

        [Test]
        public void TheOpeningOfABurstRisesStraight()
        {
            var spread = new WeaponSpreadState();
            Vector2 total = Dump(spread, WeaponSpreadState.StraightShots);

            Assert.AreEqual(0f, total.y, 1e-4f, "a burst must be a vertical line, not a diagonal");
            Assert.Greater(total.x, 0f, "but it does climb");
        }

        [Test]
        public void AFullMagazineClimbsAndPullsRight()
        {
            var spread = new WeaponSpreadState();
            Vector2 total = Dump(spread, 30);

            Assert.That(total.x, Is.InRange(24f, 28f), "roughly 26 degrees up over 30 rounds");
            Assert.That(total.y, Is.InRange(6.5f, 8.5f), "and roughly 7.5 to the right");
        }

        [Test]
        public void TheClimbNeverEases()
        {
            // A pattern that gets gentler mid-burst cannot be learned by feel.
            for (var i = 1; i < 30; i++)
            {
                Assert.GreaterOrEqual(WeaponSpreadState.VerticalKick(i),
                    WeaponSpreadState.VerticalKick(i - 1) - 1e-5f,
                    $"shot {i} kicked less than shot {i - 1}");
            }
        }

        [Test]
        public void TheClimbIsDeterministic()
        {
            // Two magazines fired identically must kick identically, or there is nothing to
            // learn and the whole point of a fixed pattern is lost.
            Vector2 first = Dump(new WeaponSpreadState(), 30);
            Vector2 second = Dump(new WeaponSpreadState(), 30);

            Assert.AreEqual(first.x, second.x, 1e-5f);
            Assert.AreEqual(first.y, second.y, 1e-5f);
        }

        [Test]
        public void StancesRankFromSteadyToHopeless()
        {
            float sneak = WeaponSpreadState.StanceCone(ShooterStance.Sneak, aiming: false);
            float standing = WeaponSpreadState.StanceCone(ShooterStance.Standing, aiming: false);
            float walking = WeaponSpreadState.StanceCone(ShooterStance.Walking, aiming: false);
            float running = WeaponSpreadState.StanceCone(ShooterStance.Running, aiming: false);
            float airborne = WeaponSpreadState.StanceCone(ShooterStance.Airborne, aiming: false);

            Assert.Less(sneak, standing);
            Assert.Less(standing, walking);
            Assert.Less(walking, running);
            Assert.Less(running, airborne);
        }

        [Test]
        public void AimingHelpsMostWhenSettledAndNotAtAllWhenRunning()
        {
            foreach (ShooterStance stance in new[]
                     { ShooterStance.Sneak, ShooterStance.Standing, ShooterStance.Walking })
            {
                Assert.Less(WeaponSpreadState.StanceCone(stance, aiming: true),
                    WeaponSpreadState.StanceCone(stance, aiming: false),
                    $"{stance} should reward aiming");
            }

            Assert.AreEqual(WeaponSpreadState.StanceCone(ShooterStance.Running, aiming: true),
                WeaponSpreadState.StanceCone(ShooterStance.Running, aiming: false), 1e-5f,
                "sights do not steady a sprint");
        }

        [Test]
        public void BloomWidensTheConeOnTopOfTheStance()
        {
            var spread = new WeaponSpreadState();
            float clean = spread.ConeHalfAngle(ShooterStance.Standing, aiming: true);

            Dump(spread, 20);
            float hosed = spread.ConeHalfAngle(ShooterStance.Standing, aiming: true);

            Assert.Greater(hosed, clean * 4f,
                "holding the trigger has to cost more than the stance ever does");
        }

        [Test]
        public void TheConeClosesBetweenBursts()
        {
            var spread = new WeaponSpreadState();
            Dump(spread, 20);
            Assert.AreEqual(1f, spread.Bloom, 1e-3f);

            spread.Tick(2f);

            Assert.AreEqual(0f, spread.Bloom, 1e-4f, "a pause has to be worth taking");
        }

        [Test]
        public void ResetClearsTheBurstOutright()
        {
            var spread = new WeaponSpreadState();
            Dump(spread, 10);
            spread.Reset();

            Assert.AreEqual(0, spread.ShotIndex);
            Assert.AreEqual(0f, spread.Bloom, 1e-4f);
            Assert.AreEqual(WeaponSpreadState.VerticalKick(0), spread.Fire(99f).x, 1e-4f);
        }

        [Test]
        public void ScatterStaysInsideTheCone()
        {
            var random = new System.Random(12345);
            Vector3 forward = Vector3.forward;
            const float cone = 3f;

            for (var i = 0; i < 2000; i++)
            {
                Vector3 shot = WeaponSpreadState.Scatter(forward, cone, random);
                Assert.LessOrEqual(Vector3.Angle(forward, shot), cone + 1e-3f,
                    "a shot must never leave the cone the HUD is drawing");
            }
        }

        [Test]
        public void ScatterGroupsTowardTheCentre()
        {
            // A flat random radius piles shots against the rim and reads as a ring. Half the
            // shots should land inside the inner half of the cone's area, at r/sqrt(2).
            var random = new System.Random(987);
            Vector3 forward = Vector3.forward;
            const float cone = 4f;
            var inner = 0;

            for (var i = 0; i < 4000; i++)
            {
                if (Vector3.Angle(forward, WeaponSpreadState.Scatter(forward, cone, random)) <= cone / Mathf.Sqrt(2f))
                {
                    inner++;
                }
            }

            Assert.That(inner / 4000f, Is.InRange(0.45f, 0.55f));
        }

        [Test]
        public void AZeroConeShootsDeadStraight()
        {
            Vector3 shot = WeaponSpreadState.Scatter(Vector3.forward, 0f);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, shot), 1e-4f);
        }

        [Test]
        public void ScatterSurvivesShootingStraightUp()
        {
            // Cross(forward, up) collapses when the two are parallel; a naive implementation
            // returns NaN and the shot vanishes.
            foreach (Vector3 forward in new[] { Vector3.up, Vector3.down })
            {
                Vector3 shot = WeaponSpreadState.Scatter(forward, 2f, new System.Random(1));
                Assert.IsFalse(float.IsNaN(shot.x) || float.IsNaN(shot.y) || float.IsNaN(shot.z));
                Assert.LessOrEqual(Vector3.Angle(forward, shot), 2f + 1e-3f);
            }
        }
    }
}
