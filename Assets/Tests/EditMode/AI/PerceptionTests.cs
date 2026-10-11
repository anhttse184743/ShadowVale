using System.Collections.Generic;
using NUnit.Framework;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.AI
{
    /// <summary>
    /// The shared sensors (M1): vision cone/range by stance, occluders, hearing radius, noise
    /// dispatch and the silent-takedown rule. Values are Map 1's tuned defaults, so these tests
    /// also pin the behaviour the Map 1 guards had before the sensors were extracted.
    /// Everything happens far above the origin so no scene collider can interfere.
    /// </summary>
    public class PerceptionTests
    {
        private static readonly Vector3 Guard = new Vector3(0, 5000, 0);
        private static readonly VisionProfile Profile = new VisionProfile(range: 24f, coneAngle: 110f);

        private sealed class FakePlayer : IPlayerObservable
        {
            public Vector3 Position { get; set; }
            public Vector3 Forward => Vector3.forward;
            public bool IsCrouching { get; set; }
            public bool IsHidden { get; set; }
            public bool IsInCover => false;
            public float VisibilityFactor => 1f;
            public float NoiseScale => 1f;
            public Vector3 SightPoint => Position + Vector3.up * (IsCrouching ? .75f : 1.1f);
            public int CurrentNavNode => -1;
            public bool IsAlive { get; set; } = true;
            public bool Owns(Transform t) => false;
        }

        private sealed class Wall : ISightOccluder
        {
            public bool Blocks(Vector3 eye, Vector3 target) => true;
        }

        private static bool Sees(FakePlayer p, bool engaged = false, ISightOccluder occluder = null)
            => VisionSensor.CanSee(Guard, Vector3.forward, engaged, Profile, p, occluder, ~0, out _);

        private static FakePlayer At(float x, float z) => new FakePlayer { Position = Guard + new Vector3(x, 0, z) };

        // ---- vision --------------------------------------------------------------------------

        [Test]
        public void SeesAStandingPlayerInFrontWithinRange()
        {
            Assert.IsTrue(Sees(At(0, 20)));
            Assert.IsFalse(Sees(At(0, 26)), "Beyond 24 m.");
        }

        [Test]
        public void UnawareGuardDoesNotSeeBehindUnlessBrushedPast()
        {
            Assert.IsFalse(Sees(At(0, -10)), "Behind, outside the 110° cone.");
            Assert.IsFalse(Sees(At(15, 1)), "To the side, outside the cone.");
            Assert.IsTrue(Sees(At(0, -2f)), "Standing within 2.5 m is noticed from any side.");
            Assert.IsFalse(Sees(new FakePlayer { Position = Guard + new Vector3(0, 0, -2f), IsCrouching = true }),
                "Crouched, 2 m behind is inside knife reach but outside the 1.2 m touch radius.");
        }

        [Test]
        public void CrouchingAndHidingShortenTheRange()
        {
            var crouched = At(0, 16); crouched.IsCrouching = true;
            Assert.IsFalse(Sees(crouched), "Crouched: 24 × 0.6 = 14.4 m.");
            crouched.Position = Guard + new Vector3(0, 0, 13);
            Assert.IsTrue(Sees(crouched));

            var hidden = At(0, 9); hidden.IsCrouching = true; hidden.IsHidden = true;
            Assert.IsFalse(Sees(hidden), "Hidden: 24 × 0.32 = 7.7 m.");
            hidden.Position = Guard + new Vector3(0, 0, 7);
            Assert.IsTrue(Sees(hidden));
        }

        [Test]
        public void EngagedGuardTracksAllAroundAtFullRangeEvenIfTheTargetCrouches()
        {
            var p = At(0, -20); p.IsCrouching = true;
            Assert.IsFalse(Sees(p));
            Assert.IsTrue(Sees(p, engaged: true));
        }

        [Test]
        public void OccludersAndDeathHideThePlayer()
        {
            Assert.IsFalse(Sees(At(0, 10), occluder: new Wall()));
            Assert.IsFalse(Sees(At(0, 10), engaged: true, occluder: new Wall()), "Foliage hides him even from a hunter.");
            var dead = At(0, 10); dead.IsAlive = false;
            Assert.IsFalse(Sees(dead));
            Assert.IsFalse(VisionSensor.CanSee(Guard, Vector3.forward, false, Profile, null, null, ~0, out _));
        }

        [Test]
        public void PhysicsObstacleBlocksSightUnlessItIsThePlayer()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = Guard + new Vector3(0, 1.2f, 6);
                wall.transform.localScale = new Vector3(4, 4, .5f);
                Physics.SyncTransforms();
                Assert.IsFalse(Sees(At(0, 12)), "A wall between them.");
                var owner = new OwningPlayer(wall.transform) { Position = Guard + new Vector3(0, 0, 12) };
                Assert.IsTrue(VisionSensor.CanSee(Guard, Vector3.forward, false, Profile, owner, null, ~0, out _),
                    "The ray hit the player himself.");
            }
            finally { Object.DestroyImmediate(wall); }
        }

        private sealed class OwningPlayer : IPlayerObservable
        {
            private readonly Transform _body;
            public OwningPlayer(Transform body) { _body = body; }
            public Vector3 Position { get; set; }
            public Vector3 Forward => Vector3.forward;
            public bool IsCrouching => false;
            public bool IsHidden => false;
            public bool IsInCover => false;
            public float VisibilityFactor => 1f;
            public float NoiseScale => 1f;
            public Vector3 SightPoint => Position + Vector3.up * 1.1f;
            public int CurrentNavNode => -1;
            public bool IsAlive => true;
            public bool Owns(Transform t) => t == _body;
        }

        // ---- hearing -------------------------------------------------------------------------

        [Test]
        public void HearingIsInsideTheNoiseRadiusScaledByTheListener()
        {
            Assert.IsTrue(HearingSensor.Hears(Guard, Guard + new Vector3(9, 0, 0), 9f));
            Assert.IsFalse(HearingSensor.Hears(Guard, Guard + new Vector3(9.1f, 0, 0), 9f));
            Assert.IsTrue(HearingSensor.Hears(Guard, Guard + new Vector3(12, 0, 0), 9f, hearingScale: 1.5f));
            Assert.IsFalse(HearingSensor.Hears(Guard, Guard, 0f), "A silent action carries nowhere.");
        }

        private sealed class Ear : INoiseListener
        {
            public Vector3 At;
            public readonly List<NoiseKind> Heard = new List<NoiseKind>();
            public bool OnNoise(Vector3 position, float radius, NoiseKind kind)
            {
                if (!HearingSensor.Hears(At, position, radius)) return false;
                Heard.Add(kind);
                return true;
            }
        }

        [Test]
        public void NoiseSystemDispatchesCountsAndPublishes()
        {
            var near = new Ear { At = Guard + Vector3.right * 3 };
            var far = new Ear { At = Guard + Vector3.right * 30 };
            var noise = new NoiseSystem();
            noise.Register(near); noise.Register(far); noise.Register(near);
            Assert.AreEqual(2, noise.ListenerCount, "Registering twice is harmless.");

            NoiseEvent? published = null;
            System.Action<NoiseEvent> spy = e => published = e;
            EventBus.Subscribe(spy);
            try
            {
                Assert.AreEqual(1, noise.Emit(Guard, 10f, NoiseKind.Stone));
                Assert.IsTrue(published.HasValue);
                Assert.AreEqual(1, published.Value.HeardBy);
                Assert.AreEqual(NoiseKind.Stone, published.Value.Kind);
                CollectionAssert.AreEqual(new[] { NoiseKind.Stone }, near.Heard);
                Assert.IsEmpty(far.Heard);

                noise.Unregister(near);
                Assert.AreEqual(0, noise.Emit(Guard, 10f, NoiseKind.Gunshot));
            }
            finally { EventBus.Unsubscribe(spy); }
        }

        // ---- takedown ------------------------------------------------------------------------

        [Test]
        public void SilentTakedownNeedsKnifeFromBehindOnAnUnawareNonBoss()
        {
            Vector3 behind = Guard + new Vector3(0, 0, -2), front = Guard + new Vector3(0, 0, 2);
            Assert.IsTrue(TakedownRule.IsSilent(Guard, Vector3.forward, behind, true, false, false));
            Assert.IsFalse(TakedownRule.IsSilent(Guard, Vector3.forward, front, true, false, false), "From the front.");
            Assert.IsFalse(TakedownRule.IsSilent(Guard, Vector3.forward, Guard + new Vector3(0, 0, -3.5f), true, false, false), "Too far.");
            Assert.IsFalse(TakedownRule.IsSilent(Guard, Vector3.forward, behind, false, false, false), "Not a silent weapon.");
            Assert.IsFalse(TakedownRule.IsSilent(Guard, Vector3.forward, behind, true, true, false), "Already engaged.");
            Assert.IsFalse(TakedownRule.IsSilent(Guard, Vector3.forward, behind, true, false, true), "A boss.");
        }
    }
}
