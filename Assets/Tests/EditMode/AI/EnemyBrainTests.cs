using System.Collections.Generic;
using NUnit.Framework;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Fsm;
using ShadowVale.Core.Events;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.AI
{
    /// <summary>
    /// The shared six-state FSM (M2) on a fake body: every state is reachable, transitions follow
    /// the priority in <see cref="EnemyTransitions"/>, and the Map 1 behaviour it replaced (stare,
    /// shoot or chase, investigate → look around → walk back) is unchanged.
    /// </summary>
    public class EnemyBrainTests
    {
        private const float Dt = .1f;

        private sealed class FakeBody : IEnemyBody
        {
            public Vector3 Position { get; set; }
            public Quaternion Rotation { get; set; } = Quaternion.identity;
            public Vector3 Forward => Rotation * Vector3.forward;
            public float HealthFraction { get; set; } = 1f;
            public bool IsDead { get; set; }
            public Vector3? Destination;
            public bool Stopped;
            public float Pace = 1f;
            public Vector3 LastFaced;

            public void MoveTo(Vector3 destination) { Destination = destination; Stopped = false; }
            public void MoveToIfNew(Vector3 destination)
            {
                if (Destination.HasValue && !Stopped && (Destination.Value - destination).sqrMagnitude < 1f) return;
                MoveTo(destination);
            }
            public void Stop() => Stopped = true;
            public void Resume() => Stopped = false;
            public bool HasArrived(float tolerance) => !Destination.HasValue || (Position - Destination.Value).magnitude <= tolerance;
            public void SetPace(float scale) => Pace = scale;
            public void Face(Vector3 point) => LastFaced = point;
            public void Turn(float degrees) => Rotation *= Quaternion.Euler(0, degrees, 0);
            public void Animate(bool moving) { }
            public bool TrySampleReachable(Vector3 desired, float maxDistance, out Vector3 point) { point = desired; return true; }
            /// <summary>Teleport to the current destination, as if the walk finished.</summary>
            public void Arrive() { if (Destination.HasValue) Position = Destination.Value; }
        }

        private sealed class FakeWeapon : IEnemyWeapon
        {
            public int Aimed, Suppressive;
            public void FireAtPlayer() => Aimed++;
            public void FireSuppressive(Vector3 point) => Suppressive++;
        }

        private sealed class CoverNearby : DefaultEnemyTactics
        {
            public Vector3 Cover;
            public override bool TryFindCover(EnemyContext ctx, Vector3 threat, out Vector3 cover) { cover = Cover; return true; }
        }

        private FakeBody _body;
        private FakeWeapon _weapon;
        private EnemyBrain _brain;
        private float _now;
        private readonly List<EnemyStateId> _visited = new List<EnemyStateId>();

        private void Build(EnemyTuning tuning = null, IEnemyTactics tactics = null)
        {
            _body = new FakeBody();
            _weapon = new FakeWeapon();
            _brain = new EnemyBrain("guard_test", _body, _weapon, tuning ?? new EnemyTuning(), tactics);
            _now = 100f;
            _visited.Clear();
            _visited.Add(_brain.State);
            _brain.StateChanged += (_, to) => _visited.Add(to);
        }

        private void Tick(bool sees = false, Vector3 player = default, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                _now += Dt;
                float d = (player - _body.Position).magnitude;
                _brain.Tick(new PerceptionSample(sees, sees ? d : float.PositiveInfinity, player), _now, Dt);
            }
        }

        private void SpotUntilEngaged(Vector3 player)
        {
            for (int i = 0; i < 100 && !_brain.Memory.Engaged(_now); i++) Tick(true, player);
            Assert.IsTrue(_brain.Memory.Engaged(_now), "He should have spotted the player.");
        }

        [Test]
        public void PatrolWalksTheLoopPointToPoint()
        {
            Build();
            _brain.Memory.PatrolPoints = new[] { new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
            Tick();
            Assert.AreEqual(EnemyStateId.Patrol, _brain.State);
            Assert.AreEqual(new Vector3(10, 0, 10), _body.Destination, "Arrived (no path yet): heads to the next point.");
            _body.Arrive(); Tick();
            Assert.AreEqual(new Vector3(10, 0, 0), _body.Destination);
        }

        [Test]
        public void OnPatrolHeWalksAtHisPatrolPace()
        {
            Build(new EnemyTuning { PatrolPace = .55f });
            _brain.Memory.PatrolPoints = new[] { new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
            Tick();
            Assert.AreEqual(.55f, _body.Pace, 1e-6f, "A calm beat, not his full speed.");
            Build();
            _brain.Memory.PatrolPoints = new[] { new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
            Tick();
            Assert.AreEqual(1f, _body.Pace, 1e-6f, "Default: unchanged.");
        }

        [Test]
        public void NoiseIsInvestigatedThenSearchedThenHeWalksBack()
        {
            Build(new EnemyTuning { SearchSeconds = 1f });
            _body.Position = new Vector3(0, 0, 0);
            _brain.BeginInvestigation(new Vector3(20, 0, 0), _now);
            Tick();
            Assert.AreEqual(EnemyStateId.Investigate, _brain.State);
            Assert.AreEqual(new Vector3(20, 0, 0), _body.Destination);
            Assert.AreEqual(1.5f, _body.Pace, "He hurries.");
            _body.Arrive(); Tick();
            Assert.IsTrue(_brain.Memory.Searching(_now), "Looking around at the spot.");
            Tick(times: 12);
            Assert.IsTrue(_brain.Memory.Returning, "Nothing there: back where he came from.");
            Tick();
            Assert.AreEqual(Vector3.zero, _body.Destination);
            _body.Arrive(); Tick(); Tick();
            Assert.AreEqual(EnemyStateId.Patrol, _brain.State);
        }

        [Test]
        public void SuspicionBuildsBeforeHeEngagesAndFiresInRange()
        {
            Build();
            var player = new Vector3(0, 0, 12);
            var contacts = new List<PlayerContactEvent>();
            System.Action<PlayerContactEvent> spy = contacts.Add;
            EventBus.Subscribe(spy);
            try
            {
                Tick(true, player);
                Assert.AreEqual(EnemyStateId.SpotPlayer, _brain.State);
                Assert.Greater(_brain.Memory.Suspicion, 0f);
                Assert.Less(_brain.Memory.Suspicion, 1f, "Not an instant spot.");
                Assert.IsTrue(_body.Stopped, "He stops and stares.");
                SpotUntilEngaged(player);
                Tick(true, player);
                Assert.AreEqual(EnemyStateId.Engage, _brain.State);
                Assert.Greater(_weapon.Aimed, 0, "Within 18 m he fires.");
                Assert.AreEqual(1, contacts.Count, "One contact report per engagement.");
                Assert.IsTrue(contacts[0].Seen);
            }
            finally { EventBus.Unsubscribe(spy); }
        }

        [Test]
        public void OutOfRangeWithoutAPlanHeClosesIn()
        {
            Build();
            var player = new Vector3(0, 0, 22);
            SpotUntilEngaged(player);
            Tick(true, player);
            Assert.AreEqual(EnemyStateId.Engage, _brain.State);
            Assert.AreEqual(player, _body.Destination);
            Assert.AreEqual(0, _weapon.Aimed);
        }

        [Test]
        public void LosingSightMeansSuppressiveFireThenHuntingTheLastSeenSpot()
        {
            Build(new EnemyTuning { SuppressSeconds = 1f });
            var player = new Vector3(0, 0, 10);
            SpotUntilEngaged(player);
            Tick(false, player);
            Assert.AreEqual(EnemyStateId.Engage, _brain.State, "Just lost sight: suppressing.");
            Assert.Greater(_weapon.Suppressive, 0);
            Assert.AreEqual(player, _body.LastFaced);
            Tick(false, player, times: 12);
            Assert.AreEqual(EnemyStateId.Investigate, _brain.State, "Then he hunts the last-seen spot.");
            Assert.AreEqual(player, _body.Destination);
            Assert.IsTrue(_brain.Memory.Engaged(_now), "Still fighting — the HUD keeps the eye up.");
        }

        [Test]
        public void ACommanderTakesCoverUnderFireThenWatchesInsteadOfHunting()
        {
            var tuning = new EnemyTuning { IsBoss = true };
            ShadowVale.Gameplay.Enemies.Boss.BossBehaviour.ApplyBossTactics(tuning);
            var cover = new Vector3(-6, 0, 0);
            Build(tuning, new CoverNearby { Cover = cover });
            var player = new Vector3(0, 0, 12);
            SpotUntilEngaged(player);
            Tick(true, player);
            Assert.AreEqual(EnemyStateId.TakeCover, _brain.State, "In range, yet he goes to his cover — firing on the way.");
            Assert.AreEqual(cover, _body.Destination);
            Assert.Greater(_weapon.Aimed, 0);

            _body.Arrive(); Tick(true, player); Tick(true, player);
            Assert.AreEqual(EnemyStateId.Engage, _brain.State);
            Tick(false, player, times: 30);
            int suppressed = _weapon.Suppressive;
            Assert.Greater(suppressed, 0, "Lost sight: he suppresses the spot first.");
            Tick(false, player, times: 60);
            Assert.AreEqual(EnemyStateId.Engage, _brain.State, "9 s later he still holds — a rifleman would be hunting by now.");
            Assert.AreEqual(cover, _body.Destination, "He never set off towards the player.");
            Assert.AreEqual(player, _body.LastFaced, "Watching where the player went.");
            Assert.AreEqual(suppressed, _weapon.Suppressive, "Past the suppression window he watches, not wastes fire.");
        }

        [Test]
        public void WithCoverNearbyHeTakesItThenHoldsItInsteadOfChasing()
        {
            var cover = new Vector3(4, 0, 0);
            Build(tactics: new CoverNearby { Cover = cover });
            var player = new Vector3(0, 0, 22);
            SpotUntilEngaged(player);
            Tick(true, player);
            Assert.AreEqual(EnemyStateId.TakeCover, _brain.State);
            Assert.AreEqual(cover, _body.Destination);
            _body.Arrive(); Tick(true, player); Tick(true, player);
            Assert.AreEqual(EnemyStateId.Engage, _brain.State);
            Assert.AreEqual(cover, _body.Position);
            Assert.IsTrue(_body.Stopped, "Out of range he holds the cover rather than chasing.");
        }

        [TestCase("block", null, false)]
        [TestCase("cover", null, false)]
        [TestCase("flank", null, false)]
        [TestCase("block", "block,local", true)]
        [TestCase("flank", "*", true)]
        public void APlanPositionIsWhereToDeployNotWhereToStayUnlessItsRoleHolds(string role, string holdRoles, bool holds)
        {
            var tuning = new EnemyTuning();
            if (holdRoles != null) tuning.HoldRoles = holdRoles;
            Build(tuning);
            var player = new Vector3(0, 0, 22);           // seen, beyond the 18 m attack range
            SpotUntilEngaged(player);
            var node = new Vector3(6, 0, 0);
            _brain.Assign(node, 7, role);
            Tick(true, player);
            Assert.AreEqual(EnemyStateId.TakeCover, _brain.State);
            _body.Arrive(); Tick(true, player); Tick(true, player);
            Assert.AreEqual(EnemyStateId.Engage, _brain.State);
            if (holds)
                Assert.IsTrue(_body.Stopped, "A holding role stays on its position.");
            else
                Assert.AreEqual(player, _body.Destination, "A " + role + " position does not stop him closing in.");
        }

        [Test]
        public void ARePlanThatBarelyMovesHimDoesNotSendHimWalking()
        {
            Build();
            var player = new Vector3(0, 0, 22);           // seen, out of range: he walks to his first position
            SpotUntilEngaged(player);
            _brain.Assign(new Vector3(5, 0, 0), 2, "cover");
            Assert.IsFalse(_brain.Memory.ReachedAssignment, "A first assignment is walked to.");
            Tick(true, player);
            _body.Arrive(); Tick(true, player);
            _brain.Assign(new Vector3(6, 0, 1), 3, "cover");      // re-plan, ~1.4 m away
            Assert.IsTrue(_brain.Memory.ReachedAssignment, "Already there, as far as fighting goes.");
            Tick(true, player);
            Assert.AreEqual(EnemyStateId.Engage, _brain.State, "He keeps fighting from where he is.");
            _brain.Assign(new Vector3(20, 0, 0), 4, "cover");      // 15 m away
            Assert.IsFalse(_brain.Memory.ReachedAssignment);
            Tick(false, player);
            Assert.AreEqual(EnemyStateId.TakeCover, _brain.State, "A real move is a move once he cannot fire.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void WithThePlayerInRangeHeFightsBeforeWalkingToHisAssignment(bool engageFirst)
        {
            Build(new EnemyTuning { EngageInRangeFirst = engageFirst });
            var player = new Vector3(0, 0, 10);                    // in sight and in range
            SpotUntilEngaged(player);
            _brain.Assign(new Vector3(15, 0, 0), 5, "block");
            Tick(true, player);
            if (engageFirst)
            {
                Assert.AreEqual(EnemyStateId.Engage, _brain.State, "He keeps shooting while he can.");
                Assert.Greater(_weapon.Aimed, 0);
                Tick(false, player, times: 2);                     // lost sight: now the plan matters
                Assert.AreEqual(EnemyStateId.TakeCover, _brain.State);
                Assert.AreEqual(new Vector3(15, 0, 0), _body.Destination);
            }
            else Assert.AreEqual(EnemyStateId.TakeCover, _brain.State, "The old way: off to the assignment at once.");
        }

        [Test]
        public void BadlyHurtInAFightHeRetreatsOnceThenRejoins()
        {
            Build(new EnemyTuning { RetreatHealthFraction = .25f, RetreatHoldSeconds = 1f, RetreatDistance = 12f });
            var player = new Vector3(0, 0, 10);
            SpotUntilEngaged(player);
            _body.HealthFraction = .2f;
            Tick(true, player);
            Assert.AreEqual(EnemyStateId.Retreat, _brain.State);
            Assert.Less(_body.Destination.Value.z, 0f, "Away from the player.");
            _body.Arrive(); Tick(true, player);
            Tick(true, player, times: 12);
            Assert.IsTrue(_brain.Memory.RetreatDone);
            Assert.AreEqual(EnemyStateId.Engage, _brain.State, "Back in the fight, and he does not run again.");
        }

        [Test]
        public void NoRetreatWhenNotFightingOrWhenBoss()
        {
            Build(new EnemyTuning { RetreatHealthFraction = .25f });
            _body.HealthFraction = .1f;
            _brain.BeginInvestigation(new Vector3(5, 0, 0), _now);
            Tick(times: 3);
            Assert.AreEqual(EnemyStateId.Investigate, _brain.State, "Wounded but only checking a noise.");

            Build(new EnemyTuning { RetreatHealthFraction = .25f, IsBoss = true });
            _body.HealthFraction = .1f;
            SpotUntilEngaged(new Vector3(0, 0, 10));
            Tick(true, new Vector3(0, 0, 10));
            Assert.AreEqual(EnemyStateId.Engage, _brain.State);
        }

        [Test]
        public void AllSixStatesAreReachableAndReportedOnTheEventBus()
        {
            var published = new List<EnemyStateChangedEvent>();
            System.Action<EnemyStateChangedEvent> spy = published.Add;
            EventBus.Subscribe(spy);
            try
            {
                Build(new EnemyTuning { SuppressSeconds = .5f, RetreatHealthFraction = .3f, RetreatHoldSeconds = .5f },
                    new CoverNearby { Cover = new Vector3(3, 0, 0) });
                _brain.BeginInvestigation(new Vector3(2, 0, 0), _now);
                Tick();                                        // Investigate
                var player = new Vector3(0, 0, 20);            // seen, but beyond attack range: he moves to cover first
                SpotUntilEngaged(player);                      // SpotPlayer
                Tick(true, player);                            // TakeCover
                _body.Arrive(); Tick(true, player); Tick(true, player); // Engage
                _body.HealthFraction = .1f; Tick(true, player); // Retreat
                _body.Arrive(); Tick(times: 20);
                _brain.ResetToPost(_now);
                Tick();                                        // Patrol
                foreach (EnemyStateId id in System.Enum.GetValues(typeof(EnemyStateId)))
                    CollectionAssert.Contains(_visited, id, id.ToString());
                Assert.AreEqual(_visited.Count - 1, published.FindAll(e => e.AgentId == "guard_test").Count);
            }
            finally { EventBus.Unsubscribe(spy); }
        }

        [Test]
        public void HurtByThePlayerHeKnowsWhereItCameFrom()
        {
            Build();
            _brain.OnHurtByPlayer(new Vector3(0, 0, -5), _now);
            Assert.AreEqual(1f, _brain.Memory.Suspicion);
            Assert.IsTrue(_brain.Memory.Engaged(_now));
            Tick();
            Assert.AreEqual(EnemyStateId.Investigate, _brain.State, "Out of sight: he goes to where it came from.");
            Assert.AreEqual(new Vector3(0, 0, -5), _body.Destination);
        }

        [Test]
        public void DeadBodiesDoNothing()
        {
            Build();
            _body.IsDead = true;
            _brain.BeginInvestigation(new Vector3(9, 0, 0), _now);
            Tick(true, new Vector3(0, 0, 5), times: 5);
            Assert.AreEqual(EnemyStateId.Patrol, _brain.State);
            Assert.AreEqual(0, _weapon.Aimed);
        }
    }
}
