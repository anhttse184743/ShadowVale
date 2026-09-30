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
            Assert.IsFalse(d.Tick(Sense(0)).Relocate, "A fight starts where he stands.");
            Assert.IsFalse(d.Tick(Sense(11.9f)).Relocate);
            var move = d.Tick(Sense(12f));
            Assert.IsTrue(move.Relocate);
            Assert.AreEqual("interval", move.RelocateReason);
            Assert.IsFalse(d.Tick(Sense(13f)).Relocate, "The clock restarts at the new spot.");

            var hurt = d.Tick(Sense(14f, hp: .84f));
            Assert.IsTrue(hurt.Relocate, "15% of his health lost in one spot moves him.");
            Assert.AreEqual("hurt", hurt.RelocateReason);
            Assert.IsFalse(d.Tick(Sense(15f, hp: .8f)).Relocate, "Measured from the spot he moved to.");
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
            Assert.AreEqual(35f, Grenade.DamageAt(0, 4.5f, 35f), 1e-4);
            Assert.AreEqual(35f / 3f, Grenade.DamageAt(4.5f, 4.5f, 35f), 1e-4);
            Assert.AreEqual(0f, Grenade.DamageAt(4.6f, 4.5f, 35f));
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
            Assert.AreEqual(22f, boss.Boss.RelocateMaxDistance, 1e-6, "Fields left out keep the client defaults.");
            CollectionAssert.IsEmpty(SchemaValidator.Validate(bundle));

            boss.Boss.ReinforceHpThreshold = 1.5f;
            bundle.EnemyArchetypes.First(e => !e.IsBoss).Boss = new BossProfile();
            var errors = SchemaValidator.Validate(bundle);
            Assert.IsTrue(errors.Any(e => e.Contains("fractions")), string.Join("; ", errors));
            Assert.IsTrue(errors.Any(e => e.Contains("only allowed with is_boss")), string.Join("; ", errors));
        }
    }
}
