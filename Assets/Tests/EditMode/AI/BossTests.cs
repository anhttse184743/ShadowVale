using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Boss;
using ShadowVale.AI.Contracts;
using ShadowVale.Content;
using ShadowVale.Data.Content;
using ShadowVale.Gameplay.Enemies.Boss;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.AI
{
    /// <summary>M8 (A04): when a boss relocates, throws a grenade or calls reinforcements, and where he goes.</summary>
    public class BossTests
    {
        private static readonly BossProfile Profile = new BossProfile();

        private static BossSense Sense(float now, bool engaged = true, float hp = 1f, Vector3? player = null, bool sheltered = false,
            Vector3? boss = null)
            => new BossSense(now, engaged, hp, boss ?? Vector3.zero, player ?? new Vector3(0, 0, 15), sheltered);

        // ---- Relocation --------------------------------------------------------------------------

        [Test]
        public void HeRelocatesAfterStayingTooLongAndAtOnceWhenHurtThere()
        {
            var d = new BossDirector(Profile);
            float stay = Profile.RelocateIntervalSeconds;
            Assert.AreEqual(6f, stay, 1e-6, "A few seconds of fighting at each cover (3.5 s had him running more than fighting).");
            Assert.IsFalse(d.Tick(Sense(0)).Relocate, "A fight starts where he stands.");
            Assert.IsFalse(d.Tick(Sense(stay - .1f)).Relocate);
            var move = d.Tick(Sense(stay));
            Assert.IsTrue(move.Relocate);
            Assert.AreEqual("interval", move.RelocateReason);
            Assert.IsFalse(d.Tick(Sense(stay + .5f)).Relocate, "The clock restarts at the new spot.");

            var hurt = d.Tick(Sense(stay + 1.5f, hp: .84f));
            Assert.IsTrue(hurt.Relocate, "15% of his health lost in one spot moves him.");
            Assert.AreEqual("hurt", hurt.RelocateReason);
            Assert.IsFalse(d.Tick(Sense(stay + 2f, hp: .8f)).Relocate, "Measured from the spot he moved to.");
        }

        [Test]
        public void TheStopIsCountedFromHisArrivalAndVaries()
        {
            var d = new BossDirector(Profile);
            d.Tick(Sense(0));
            for (float t = 0; t < 10; t += .5f)
                Assert.IsFalse(d.Tick(new BossSense(t, true, 1f - t * .02f, Vector3.zero, new Vector3(0, 0, 15), false, settled: false)).Relocate,
                    "Running to his position, however long, and hit on the way: no new move yet.");
            Assert.IsFalse(d.Tick(Sense(10f, hp: .8f)).Relocate, "Arrived.");
            // The stop counts from the last tick on the way (9.5 s).
            Assert.IsFalse(d.Tick(Sense(9.5f + Profile.RelocateIntervalSeconds - .1f, hp: .8f)).Relocate);
            Assert.IsTrue(d.Tick(Sense(9.5f + Profile.RelocateIntervalSeconds, hp: .8f)).Relocate, "A stop there, then on to the next cover.");

            var stops = new HashSet<float>();
            var jittered = new BossDirector(Profile, new System.Random(3));
            float now = 0, since = 0;
            jittered.Tick(Sense(0));
            while (stops.Count < 5 && now < 100)
            {
                now += .05f;
                if (!jittered.Tick(Sense(now)).Relocate) continue;
                float stop = now - since;
                Assert.That(stop, Is.InRange(Profile.RelocateIntervalSeconds * (1 - BossDirector.IntervalJitter) - .06f,
                    Profile.RelocateIntervalSeconds * (1 + BossDirector.IntervalJitter) + .06f));
                stops.Add(Mathf.Round(stop * 10));
                since = now;
            }
            Assert.Greater(stops.Count, 2, "Stops of different lengths: the rhythm cannot be timed.");
        }

        [Test]
        public void OutOfAFightHeDoesNothing()
        {
            var d = new BossDirector(Profile);
            for (float t = 0; t < 60; t += 1) Assert.IsFalse(d.Tick(Sense(t, engaged: false, hp: .1f, sheltered: true)).Any);
            Assert.IsFalse(d.ReinforcementsCalled);
        }

        // ---- Grenades ----------------------------------------------------------------------------

        [Test]
        public void AGrenadeGoesToAPlayerWhoCampsBehindCover()
        {
            var d = new BossDirector(Profile);
            var hide = new Vector3(2, 0, 15);
            d.Tick(Sense(0, player: hide, sheltered: true));
            Assert.IsFalse(d.Tick(Sense(3.9f, player: hide + new Vector3(1, 0, 1), sheltered: true)).ThrowGrenade,
                "Shuffling inside the camp radius still counts as camping — but not for 4 s yet.");
            var t = d.Tick(Sense(4.1f, player: hide, sheltered: true));
            Assert.IsTrue(t.ThrowGrenade);
            Assert.AreEqual(hide, t.GrenadeTarget);
            Assert.IsFalse(d.Tick(Sense(9f, player: hide, sheltered: true)).ThrowGrenade, "Cooldown.");
            Assert.IsTrue(d.Tick(Sense(14.2f, player: hide, sheltered: true)).ThrowGrenade, "Still there after the cooldown: another.");
        }

        [Test]
        public void NoGrenadeForAPlayerWhoKeepsMovingOrStandsInTheOpen()
        {
            var d = new BossDirector(Profile);
            for (int i = 0; i <= 20; i++)
                Assert.IsFalse(d.Tick(Sense(i, player: new Vector3(i * 4, 0, 15), sheltered: true)).ThrowGrenade, "Moving: no camp.");
            var e = new BossDirector(Profile);
            for (int i = 0; i <= 20; i++)
                Assert.IsFalse(e.Tick(Sense(i, player: new Vector3(0, 0, 15), sheltered: false)).ThrowGrenade, "In the open: he shoots instead.");
        }

        [Test]
        public void NoGrenadeOutOfRangeOrAtHisOwnFeet()
        {
            var far = new BossDirector(Profile);
            far.Tick(Sense(0, player: new Vector3(0, 0, 40), sheltered: true));
            Assert.IsFalse(far.Tick(Sense(5, player: new Vector3(0, 0, 40), sheltered: true)).ThrowGrenade);
            var near = new BossDirector(Profile);
            near.Tick(Sense(0, player: new Vector3(0, 0, 3), sheltered: true));
            Assert.IsFalse(near.Tick(Sense(5, player: new Vector3(0, 0, 3), sheltered: true)).ThrowGrenade);
        }

        [Test]
        public void GrenadeDamageFallsOffToAThirdAtTheEdge()
        {
            Assert.AreEqual(35f, Grenade.DamageAt(0, Profile.GrenadeRadius, 35f), 1e-4);
            Assert.AreEqual(35f / 3f, Grenade.DamageAt(Profile.GrenadeRadius, Profile.GrenadeRadius, 35f), 1e-4);
            Assert.AreEqual(0f, Grenade.DamageAt(Profile.GrenadeRadius + .1f, Profile.GrenadeRadius, 35f));
            Assert.AreEqual(Vector3.zero, Grenade.ArcPoint(Vector3.zero, new Vector3(10, 0, 0), 0));
            Assert.AreEqual(new Vector3(10, 0, 0), Grenade.ArcPoint(Vector3.zero, new Vector3(10, 0, 0), 1));
            Assert.Greater(Grenade.ArcPoint(Vector3.zero, new Vector3(10, 0, 0), .5f).y, 1.4f, "A lob, not a straight line.");
        }

        // ---- Reinforcements ----------------------------------------------------------------------

        [Test]
        public void ReinforcementsAreCalledOnceBelowHalfHealth()
        {
            var d = new BossDirector(Profile);
            Assert.IsFalse(d.Tick(Sense(0, hp: .6f)).CallReinforcements);
            Assert.IsTrue(d.Tick(Sense(1, hp: .49f)).CallReinforcements);
            Assert.IsFalse(d.Tick(Sense(2, hp: .2f)).CallReinforcements, "Once.");
            Assert.IsTrue(d.ReinforcementsCalled);

            var none = new BossDirector(new BossProfile { ReinforceHpThreshold = 0 });
            Assert.IsFalse(none.Tick(Sense(0, hp: .01f)).CallReinforcements, "0 = never.");
        }

        // ---- Where he goes -----------------------------------------------------------------------

        /// <summary>A handful of nodes; cover nodes face +z (towards a player standing north).</summary>
        private sealed class World : IWorldQuery
        {
            public readonly List<Vector3> Nodes = new List<Vector3>();
            public readonly HashSet<int> Cover = new HashSet<int>();
            public readonly Dictionary<int, float> Facing = new Dictionary<int, float>();
            public int NodeCount => Nodes.Count;
            public Vector3 NodePosition(int node) => Nodes[node];
            public IReadOnlyList<int> Neighbors(int node) => new int[0];
            public bool IsCoverNode(int node) => Cover.Contains(node);
            public float CoverFacing(int node) => Facing.TryGetValue(node, out var f) ? f : 0f;
            public IReadOnlyList<int> CoverNodes => Cover.OrderBy(c => c).ToList();
            public IReadOnlyList<int> EscapeRoutes => new int[0];
            public int NearestNode(Vector3 p) => Enumerable.Range(0, Nodes.Count).OrderBy(i => (Nodes[i] - p).sqrMagnitude).First();
            public int Hops(int a, int b) => 1;
            public bool HasLineOfSight(Vector3 from, Vector3 to) => true;
            public bool NodeSeesNode(int a, int b) => true;
            public bool VisibilityReady => true;

            public int Add(Vector3 p, bool cover = true, float facing = 0)
            {
                Nodes.Add(p);
                if (cover) { Cover.Add(Nodes.Count - 1); Facing[Nodes.Count - 1] = facing; }
                return Nodes.Count - 1;
            }
        }

        [Test]
        public void HeMovesToCoverWithinReachNeverOntoThePlayerNorBackToWhereHeIs()
        {
            var w = new World();
            int here = w.Add(Vector3.zero);
            int tooClose = w.Add(new Vector3(3, 0, 0));
            int tooFar = w.Add(new Vector3(40, 0, 0));
            int atPlayer = w.Add(new Vector3(0, 0, 14));
            int facingAway = w.Add(new Vector3(-10, 0, 0), facing: 180);
            int facingPlayer = w.Add(new Vector3(10, 0, 0), facing: 0);
            w.Add(new Vector3(0, 0, 20), cover: false); // The player's node: not cover.
            var player = new Vector3(0, 0, 20);

            Assert.IsTrue(BossRelocation.TryPick(w, Profile, Vector3.zero, player, here, rng: null, out int node, out var to));
            Assert.AreEqual(facingPlayer, node, "Best score: cover facing the player, in range.");
            Assert.AreEqual(w.Nodes[facingPlayer], to);

            var seen = new HashSet<int>();
            var rng = new System.Random(7);
            for (int i = 0; i < 50; i++)
            {
                Assert.IsTrue(BossRelocation.TryPick(w, Profile, Vector3.zero, player, here, rng, out node, out _));
                seen.Add(node);
            }
            CollectionAssert.IsSubsetOf(seen, new[] { facingPlayer, facingAway });
            Assert.AreEqual(2, seen.Count, "The pick varies between the eligible nodes: no fixed circuit to learn.");
            CollectionAssert.DoesNotContain(seen, here);
            CollectionAssert.DoesNotContain(seen, tooClose);
            CollectionAssert.DoesNotContain(seen, tooFar);
            CollectionAssert.DoesNotContain(seen, atPlayer);
        }

        [Test]
        public void CallingReinforcementsHeFallsBackTowardsThem()
        {
            var d = new BossDirector(Profile);
            d.Tick(Sense(0, hp: .6f));
            var call = d.Tick(Sense(1, hp: .49f));
            Assert.IsTrue(call.CallReinforcements);
            Assert.IsTrue(call.Relocate, "He does not hold a spot the player is winning while help is on the way.");
            Assert.AreEqual("regroup", call.RelocateReason);

            var w = new World();
            int here = w.Add(Vector3.zero);
            int forward = w.Add(new Vector3(8, 0, 8));  // 14 m from the player, within his reach.
            int back = w.Add(new Vector3(0, 0, -10));   // 30 m from the player.
            var player = new Vector3(0, 0, 20);
            Assert.IsTrue(BossRelocation.TryPick(w, Profile, Vector3.zero, player, here, null, out int node, out _, fightRange: 18f));
            Assert.AreEqual(forward, node, "Fighting: a position from which he can hit the player scores higher.");
            Assert.IsTrue(BossRelocation.TryPick(w, Profile, Vector3.zero, player, here, null, out node, out _, 18f, minPlayerDistance: 20f));
            Assert.AreEqual(back, node, "Regrouping: never closer to the player than he is.");
        }

        [Test]
        public void LingeringInAnAreaCountsNotStandingOnOneSpot()
        {
            // Sliding 1 m/s along a wall: with a 3 m area the clock restarted at 4 m, now it does not.
            var wall = new BossDirector(Profile);
            for (int i = 0; i < 4; i++)
                Assert.IsFalse(wall.Tick(Sense(i, player: new Vector3(i, 0, 15), sheltered: true)).ThrowGrenade);
            Assert.IsTrue(wall.Tick(Sense(4.05f, player: new Vector3(4, 0, 15), sheltered: true)).ThrowGrenade,
                "Four seconds along the same stretch of cover is one hiding place.");

            // Peeking out to shoot every other second used to reset the clock each time he showed himself.
            var peeker = new BossDirector(Profile);
            bool thrown = false;
            for (int i = 0; i <= 5 && !thrown; i++)
                thrown = peeker.Tick(Sense(i, player: new Vector3(2 + (i % 2) * 4, 0, 15), sheltered: i % 2 == 0)).ThrowGrenade;
            Assert.IsTrue(thrown, "Half the time behind cover, half peeking out: still camping that spot.");
        }

        [Test]
        public void FromCoverHeFightsInBursts()
        {
            var c = new PeekCycle(outSeconds: 1.4f, hideSeconds: 1.6f);
            var fire = new Vector3(0, 0, 0);
            var duck = new Vector3(0, 0, -1.2f);
            c.Begin(fire, duck, 7, now: 10f);
            Assert.IsTrue(c.Active && c.IsOut, "He arrives at the firing point and fires first.");
            Assert.AreEqual(PeekCycle.Step.None, c.Tick(11.3f));
            Assert.AreEqual(PeekCycle.Step.Back, c.Tick(11.4f), "Then ducks behind the cover.");
            Assert.IsFalse(c.IsOut);
            Assert.AreEqual(PeekCycle.Step.None, c.Tick(12.9f));
            Assert.AreEqual(PeekCycle.Step.Out, c.Tick(13.0f), "And comes out again.");
            Assert.AreEqual(7, c.Node);
            c.Stop();
            Assert.AreEqual(PeekCycle.Step.None, c.Tick(30f), "Moving on ends it.");

            var varied = new PeekCycle(1.4f, 1.6f, new System.Random(5));
            varied.Begin(fire, duck, 0, 0);
            var switches = new List<float>();
            for (float t = 0; t < 60 && switches.Count < 6; t += .01f)
                if (varied.Tick(t) != PeekCycle.Step.None) switches.Add(t);
            var gaps = switches.Zip(switches.Skip(1), (a, b) => Mathf.Round((b - a) * 10)).Distinct().Count();
            Assert.Greater(gaps, 2, "Bursts of different lengths: no rhythm to time a shot to.");
        }

        [Test]
        public void ARifleOnHimSendsHimBehindTheCoverAtOnce()
        {
            var c = new PeekCycle(outSeconds: 2.5f, hideSeconds: .8f);
            c.Begin(Vector3.zero, new Vector3(0, 0, -1.2f), 3, now: 0f);
            Assert.IsTrue(c.Duck(1f, 1f), "Out at the firing point: back behind the cover now.");
            Assert.IsFalse(c.IsOut);
            Assert.IsFalse(c.Duck(1.2f, 1f), "Already behind it.");
            Assert.AreEqual(PeekCycle.Step.None, c.Tick(1.9f));
            Assert.AreEqual(PeekCycle.Step.Out, c.Tick(2.0f), "Out again after the duck.");
            c.Stop();
            Assert.IsFalse(c.Duck(3f, 1f), "No cover cycle, nothing to duck behind.");
        }

        [Test]
        public void HeFightsFromCoverMostOfTheTimeWithSteadyFire()
        {
            Assert.That(Profile.PeekOutSeconds / (Profile.PeekOutSeconds + Profile.PeekHideSeconds), Is.InRange(.55f, .7f),
                "Out firing most of the time, not three quarters of it (2.5/0.8 from 18/10 had him easy to spray down)."); 
            Assert.AreEqual(1, Profile.BurstRounds, "Steady fire, not bursts (19/10/2026).");
            Assert.AreEqual(.3f, Profile.FireIntervalSeconds, 1e-6, "A round every 0.3 s.");
            Assert.AreEqual(11.5f, Profile.DamagePerRound, 1e-6);
            Assert.AreEqual(18f, Profile.AttackRange, 1e-6);
            Assert.AreEqual(1f, Profile.DetectionSeconds, 1e-6);
            Assert.That(Profile.RelocateMaxDistance, Is.LessThanOrEqualTo(12f), "Short hops between nearby cover.");
        }

        [Test]
        public void HoppingBetweenHidesNearbyStillDrawsAGrenade()
        {
            // Two bushes 10 m apart, a dash between them every 2 s: each dash resets the close area (6 m).
            var d = new BossDirector(Profile);
            bool thrown = false;
            float at;
            for (at = 0; at <= 12f; at += .1f)
            {
                var bush = (int)(at / 2f) % 2 == 0 ? new Vector3(-3, 0, 15) : new Vector3(7, 0, 15);
                if (thrown = d.Tick(Sense(at, player: bush, sheltered: true)).ThrowGrenade) break;
            }
            Assert.IsTrue(thrown, "Within the wide area, mostly sheltered: one hiding place after all.");
            Assert.That(at, Is.InRange(Profile.GrenadeWideSeconds - .05f, Profile.GrenadeWideSeconds + .5f));

            var closeOnly = new BossDirector(new BossProfile { GrenadeWideRadius = 0 });
            for (float t = 0; t <= 12f; t += .1f)
            {
                var bush = (int)(t / 2f) % 2 == 0 ? new Vector3(-3, 0, 15) : new Vector3(7, 0, 15);
                Assert.IsFalse(closeOnly.Tick(Sense(t, player: bush, sheltered: true)).ThrowGrenade, "Without it: never (the gap it closes).");
            }
        }

        [Test]
        public void HeDoesNotTurnHisBackAndRunAndPrefersCoverBehindHisMen()
        {
            var w = new World();
            int here = w.Add(Vector3.zero);
            int away = w.Add(new Vector3(0, 0, -8));   // Straight away from the player: his back to him.
            int side = w.Add(new Vector3(8, 0, 2));
            var player = new Vector3(0, 0, 20);
            Assert.IsTrue(BossRelocation.TryPick(w, Profile, Vector3.zero, player, here, null, out int node, out _));
            Assert.AreEqual(side, node, "Not running away from the player he is fighting.");
            Assert.IsTrue(BossRelocation.TryPick(w, Profile, Vector3.zero, player, here, null, out node, out _, fallingBack: true));
            Assert.AreEqual(away, node, "Falling back on purpose (regrouping): away is the point.");
            Assert.IsTrue(BossRelocation.RunsAway(Vector3.zero, new Vector3(0, 0, -8), player));
            Assert.IsFalse(BossRelocation.RunsAway(Vector3.zero, new Vector3(8, 0, 0), player), "Sideways is not away.");

            var w2 = new World();
            int from = w2.Add(Vector3.zero);
            w2.Add(new Vector3(-8, 0, 0));
            int shielded = w2.Add(new Vector3(8, 0, 0));
            var men = new Vector3(6, 0, 8);
            Assert.IsTrue(BossRelocation.TryPick(w2, Profile, Vector3.zero, player, from, null, out node, out _, men: men));
            Assert.AreEqual(shielded, node, "With his men between him and the player.");
            Assert.IsTrue(BossRelocation.Shielded(new Vector3(8, 0, 0), men, player));
            Assert.IsFalse(BossRelocation.Shielded(new Vector3(-8, 0, 0), men, player));
        }

        [Test]
        public void NewFallbackAndShieldingNeverOverrideTheMapBoundary()
        {
            var graph = new World();
            int here = graph.Add(Vector3.zero);
            graph.Add(new Vector3(0, 0, -8));
            int inside = graph.Add(new Vector3(8, 0, 2));
            var player = new Vector3(0, 0, 20);
            foreach (bool fallingBack in new[] { false, true })
            {
                Assert.IsTrue(BossRelocation.TryPick(graph, Profile, Vector3.zero, player, here, null,
                    out int chosen, out var point, men: new Vector3(0, 0, 8), fallingBack: fallingBack,
                    allowed: p => p.z >= 0));
                Assert.AreEqual(inside, chosen);
                Assert.GreaterOrEqual(point.z, 0);
            }
            Assert.IsFalse(BossRelocation.TryPick(graph, Profile, Vector3.zero, player, here, null,
                out _, out _, fallingBack: true, allowed: p => false));
        }

        [Test]
        public void RoundsLandingMakeHimDuckAndMoreAfterwardsMoveHim()
        {
            var d = new BossDirector(Profile);
            d.Tick(Sense(0));
            Assert.IsFalse(d.Tick(Sense(1f, hp: .98f)).Duck);
            Assert.IsFalse(d.Tick(Sense(1.2f, hp: .96f)).Duck);
            var duck = d.Tick(Sense(1.4f, hp: .94f));
            Assert.IsTrue(duck.Duck, "Three rounds in a second: down behind the cover at once.");
            Assert.AreEqual("suppressed", duck.DuckReason);
            Assert.IsFalse(duck.Relocate);
            Assert.IsFalse(d.Tick(Sense(1.8f, hp: .92f)).Relocate, "Behind the cover: a round through it is not a reason to run yet.");
            var move = d.Tick(Sense(1.4f + Profile.DuckSeconds + .3f, hp: .90f));
            Assert.IsTrue(move.Relocate, "Hit again as he comes back out: he moves.");
            Assert.AreEqual("suppressed", move.RelocateReason);

            var steady = new BossDirector(Profile);
            steady.Tick(Sense(0));
            for (int i = 1; i <= 5; i++)
                Assert.IsFalse(steady.Tick(Sense(i * 1.1f, hp: 1f - i * .02f)).Duck, "A round now and then is not being sprayed down.");
        }

        [Test]
        public void ArmourHalvesBodyHitsAndHeadshotsDouble()
        {
            Assert.AreEqual(.5f, BossDirector.ArmorFactor(Profile, headshot: false), 1e-6);
            Assert.AreEqual(2f, BossDirector.ArmorFactor(Profile, headshot: true), 1e-6);
            float health = 700f, perRound = 24f;
            Assert.Greater(Mathf.CeilToInt(health / (perRound * Profile.ArmorBodyFactor)), 30 * 1.9f, "Body shots: about two magazines, not one.");
        }

        [Test]
        public void HeComesOutAtADifferentSpotAndNeverWhereHeWasHit()
        {
            var rng = new System.Random(4);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(2, BossBehaviour.NextPeek(3, last: 0, burned: 1, rng), "Not the last spot, not where he was hit.");
            Assert.AreEqual(1, BossBehaviour.NextPeek(2, 0, -1, rng));
            Assert.AreEqual(0, BossBehaviour.NextPeek(1, 0, 0, rng), "Only one spot: that one.");
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 40; i++) seen.Add(BossBehaviour.NextPeek(3, 2, 2, rng));
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, seen, "Either of the others, at random.");
        }

        // ---- Commanding ----------------------------------------------------------------------------

        [Test]
        public void UnderHisCommandASquadIsPlannedAsAPincer()
        {
            var w = ShadowVale.AI.Coordination.CoordinationWeights.Default;
            var p = Command.Pincer(w);
            Assert.AreEqual(w.Flanking * Command.FlankScale, p.Flanking, 1e-9, "More flanking.");
            Assert.AreEqual(w.Coverage * Command.CoverageScale, p.Coverage, 1e-9, "More of the ways out covered.");
            Assert.AreEqual(w.Redundancy, p.Redundancy, 1e-9);
            Assert.AreEqual(w.Cover, p.Cover, 1e-9);
            Assert.AreEqual(w.Distance, p.Distance, 1e-9);
        }

        [Test]
        public void HisGrenadeDrivesThePlayerTowardsHisMen()
        {
            var player = new Vector3(0, 0, 10);
            Assert.AreEqual(new Vector3(0, 0, 8.5f), BossDirector.FlushTarget(player, new Vector3(0, 0, 20), 1.5f),
                "Beyond the player, on the side away from the men: running from it means running at them.");
            Assert.AreEqual(player, BossDirector.FlushTarget(player, null, 1.5f), "No men near: right on him.");
            Assert.AreEqual(player, BossDirector.FlushTarget(player, new Vector3(0, 0, 20), 0f), "Offset 0: the old throw.");

            var d = new BossDirector(Profile);
            var hide = new Vector3(2, 0, 15);
            var men = new Vector3(2, 0, 25);
            d.Tick(new BossSense(0, true, 1, Vector3.zero, hide, true, squad: men));
            var t = d.Tick(new BossSense(4.1f, true, 1, Vector3.zero, hide, true, squad: men));
            Assert.IsTrue(t.ThrowGrenade);
            Assert.Less(t.GrenadeTarget.z, hide.z, "Thrown on the far side from his men.");
        }

        [Test]
        public void WhereThePlayerHidOnceTheGrenadeComesSooner()
        {
            var d = new BossDirector(Profile);
            var hide = new Vector3(2, 0, 15);
            d.Tick(Sense(0, player: hide, sheltered: true));
            Assert.IsTrue(d.Tick(Sense(4.1f, player: hide, sheltered: true)).ThrowGrenade);
            Assert.AreEqual(1, d.KnownHides.Count);
            d.Tick(Sense(5f, player: new Vector3(30, 0, 15), sheltered: false)); // He leaves...
            d.Tick(Sense(20f, player: hide, sheltered: true));                   // ...and comes back, cooldown over.
            float known = Profile.GrenadeCampSeconds * Profile.KnownHideCampFactor;
            Assert.IsFalse(d.Tick(Sense(20f + known - .1f, player: hide, sheltered: true)).ThrowGrenade);
            Assert.IsTrue(d.Tick(Sense(20f + known + .05f, player: hide, sheltered: true)).ThrowGrenade,
                "Half the camp time: he expects him there.");
        }

        // ---- Reading the player -------------------------------------------------------------------

        private static BossSense Read(float now, bool aimed = false, bool reloading = false)
            => new BossSense(now, true, 1, Vector3.zero, new Vector3(0, 0, 15), false, true, reloading, aimed);

        [Test]
        public void ARifleAimedAtHimMakesHimDuckAndMoveOnlyIfItStaysOnHim()
        {
            var d = new BossDirector(Profile);
            d.Tick(Read(0));
            Assert.IsFalse(d.Tick(Read(1f, aimed: true)).Any, "A glance is not a threat.");
            Assert.IsFalse(d.Tick(Read(1.4f, aimed: true)).Any);
            var duck = d.Tick(Read(1.5f, aimed: true));
            Assert.IsTrue(duck.Duck, "Aimed at for half a second: he ducks behind his cover...");
            Assert.IsFalse(duck.Relocate, "...and stays at it (until 18/10/2026 he ran for other cover).");
            for (float t = 1.6f; t < 2.55f; t += .1f) Assert.IsFalse(d.Tick(Read(t)).Any, "Behind the cover: out of the rifle's sight.");
            Assert.IsFalse(d.Tick(Read(2.6f, aimed: true)).Any, "Out again and the rifle is still there: not yet two seconds.");
            var dodge = d.Tick(Read(3.0f, aimed: true));
            Assert.IsTrue(dodge.Relocate, "Two seconds with the rifle on him, the duck between: now he moves.");
            Assert.AreEqual("aimed", dodge.RelocateReason);
            for (float t = 3.1f; t < 3f + Profile.DodgeCooldownSeconds - .1f; t += .1f)
                Assert.AreNotEqual("aimed", d.Tick(Read(t, aimed: true)).RelocateReason, "Not every frame: a cooldown.");

            var forgets = new BossDirector(Profile);
            forgets.Tick(Read(0));
            forgets.Tick(Read(.1f, aimed: true));
            Assert.IsTrue(forgets.Tick(Read(.65f, aimed: true)).Duck);
            forgets.Tick(Read(.7f));
            forgets.Tick(Read(.7f + Profile.AimForgetSeconds + .1f)); // The rifle went elsewhere a while.
            for (float t = 2.5f; t < 4.4f; t += .1f)
                Assert.IsFalse(forgets.Tick(Read(t, aimed: true)).Relocate, "A new aim counts from zero.");

            var calm = new BossDirector(new BossProfile { ReadsPlayer = false });
            calm.Tick(Read(0));
            for (float t = .1f; t < 3f; t += .1f) Assert.IsFalse(calm.Tick(Read(t, aimed: true)).Relocate, "Switched off.");
        }

        [Test]
        public void AReloadIsTheMensCueToPush()
        {
            var d = new BossDirector(Profile);
            d.Tick(Read(0));
            Assert.IsTrue(d.Tick(Read(1f, reloading: true)).Push, "The moment he starts reloading.");
            Assert.IsFalse(d.Tick(Read(1.5f, reloading: true)).Push, "Once per reload.");
            d.Tick(Read(2f));
            Assert.IsFalse(d.Tick(Read(3f, reloading: true)).Push, "Not again within the cooldown.");
            d.Tick(Read(6.5f));
            Assert.IsTrue(d.Tick(Read(1f + Profile.PushCooldownSeconds + .5f, reloading: true)).Push);

            Assert.IsTrue(BossBehaviour.AimsAt(Vector3.zero, Vector3.forward, new Vector3(0, 0, 10), BossBehaviour.AimConeDegrees));
            Assert.IsFalse(BossBehaviour.AimsAt(Vector3.zero, Vector3.forward, new Vector3(5, 0, 10), BossBehaviour.AimConeDegrees),
                "27° off: aiming at someone else.");
        }

        [Test]
        public void TheCommanderWearsAStarOverHisHead()
        {
            var go = new GameObject("commander");
            try
            {
                var marker = CommanderMarker.Attach(go);
                Assert.AreSame(marker, CommanderMarker.Attach(go), "Attached twice: still one marker.");
                var star = go.transform.Find(CommanderMarker.StarName);
                Assert.IsNotNull(star);
                Assert.AreEqual(1, go.GetComponentsInChildren<MeshRenderer>().Length, "One star.");
                Assert.Greater(star.localPosition.y, 2f, "Above his head.");
                Assert.IsNull(star.GetComponent<Collider>(), "Never blocks a shot or a line of sight.");
                var mesh = star.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(11, mesh.vertexCount, "Centre plus five points and five notches.");
                Assert.AreEqual(2 * 10 * 3, mesh.triangles.Length, "Both faces.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NoCoverInReachMeansNoMove()
        {
            var w = new World();
            w.Add(Vector3.zero);
            w.Add(new Vector3(60, 0, 0));
            Assert.IsFalse(BossRelocation.TryPick(w, Profile, Vector3.zero, new Vector3(0, 0, 20), 0, null, out _, out _));
        }

        // ---- Data --------------------------------------------------------------------------------

        [Test]
        public void TheFallbackBundlesBossCarriesItsProfileAndBadProfilesAreRejected()
        {
            Assert.IsTrue(new FallbackBundleProvider().TryLoad(out var bundle, out var error), error);
            var boss = bundle.EnemyArchetypes.Single(e => e.IsBoss);
            Assert.IsNotNull(boss.Boss);
            Assert.AreEqual(.5f, boss.Boss.ReinforceHpThreshold, 1e-6);
            Assert.AreEqual(3, boss.Boss.ReinforceCount);
            Assert.AreEqual(new BossProfile().RelocateMaxDistance, boss.Boss.RelocateMaxDistance, 1e-6, "Fields left out keep the client defaults.");
            CollectionAssert.IsEmpty(SchemaValidator.Validate(bundle));

            boss.Boss.ReinforceHpThreshold = 1.5f;
            bundle.EnemyArchetypes.First(e => !e.IsBoss).Boss = new BossProfile();
            var errors = SchemaValidator.Validate(bundle);
            Assert.IsTrue(errors.Any(e => e.Contains("fractions")), string.Join("; ", errors));
            Assert.IsTrue(errors.Any(e => e.Contains("only allowed with is_boss")), string.Join("; ", errors));
        }
    }
}
