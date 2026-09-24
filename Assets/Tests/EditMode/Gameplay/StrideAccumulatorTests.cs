using NUnit.Framework;
using ShadowVale.Gameplay.Player;

namespace ShadowVale.Tests.EditMode.Gameplay
{
    public sealed class StrideAccumulatorTests
    {
        private const float Stride = 1f;

        [Test]
        public void StepsOncePerStrideLength()
        {
            var strider = new StrideAccumulator();

            Assert.IsFalse(strider.Advance(0.5f, Stride, out _), "half a stride is not a step");
            Assert.IsTrue(strider.Advance(0.5f, Stride, out _), "the other half completes it");
            Assert.IsFalse(strider.Advance(0.5f, Stride, out _), "and the count starts over");
        }

        [Test]
        public void FeetAlternate()
        {
            var strider = new StrideAccumulator();

            strider.Advance(Stride, Stride, out bool first);
            strider.Advance(Stride, Stride, out bool second);
            strider.Advance(Stride, Stride, out bool third);

            Assert.IsTrue(first, "the first step is the left foot");
            Assert.IsFalse(second);
            Assert.IsTrue(third);
        }

        [Test]
        public void OneCallYieldsAtMostOneStep()
        {
            var strider = new StrideAccumulator();

            Assert.IsTrue(strider.Advance(Stride * 5f, Stride, out _));
            // The backlog is capped, so the burst is spread over later frames rather than
            // firing five footfalls inside one.
            Assert.IsTrue(strider.Advance(0f, Stride, out _));
            Assert.IsFalse(strider.Advance(0f, Stride, out _), "the backlog is capped at two strides");
        }

        [Test]
        public void ResetDropsThePartialStrideButKeepsTheFoot()
        {
            var strider = new StrideAccumulator();

            strider.Advance(0.9f, Stride, out _);
            strider.Reset();

            Assert.AreEqual(0f, strider.Pending, 1e-6f);
            Assert.IsFalse(strider.Advance(0.9f, Stride, out bool foot), "the banked 0.9 m is gone");
            Assert.IsTrue(foot, "the left foot is still the one owed a step");
        }

        [Test]
        public void TakeFootAdvancesTheAlternationWithoutWalking()
        {
            var strider = new StrideAccumulator();
            strider.Advance(0.6f, Stride, out _);

            Assert.IsTrue(strider.TakeFoot(), "a landing comes down on the foot that was owed");
            Assert.AreEqual(0.6f, strider.Pending, 1e-6f, "it walks no distance of its own");

            // The stride that follows must use the other foot, or a jump makes the player limp.
            strider.Advance(Stride, Stride, out bool next);
            Assert.IsFalse(next);
        }

        [Test]
        public void ResetFullyReturnsToTheLeftFoot()
        {
            var strider = new StrideAccumulator();

            strider.Advance(Stride, Stride, out _);
            Assert.IsFalse(strider.NextIsLeft);

            strider.ResetFully();

            Assert.IsTrue(strider.NextIsLeft);
            Assert.AreEqual(0f, strider.Pending, 1e-6f);
        }

        [Test]
        public void ZeroStrideLengthNeverStepsAndNeverDividesByZero()
        {
            var strider = new StrideAccumulator();

            Assert.IsFalse(strider.Advance(100f, 0f, out _));
            Assert.IsFalse(strider.Advance(100f, -1f, out _));
            Assert.AreEqual(0f, strider.Pending, 1e-6f, "nothing is banked against an unusable stride");
        }

        [Test]
        public void StandingStillNeverSteps()
        {
            var strider = new StrideAccumulator();

            for (var i = 0; i < 100; i++)
            {
                Assert.IsFalse(strider.Advance(0f, Stride, out _));
            }
        }

        [Test]
        public void CadenceFollowsSpeed()
        {
            // Same stride length, two speeds: the faster one must step more often over the
            // same span of time. This is the whole reason the accumulator counts metres.
            Assert.AreEqual(2, StepsOverOneSecond(speed: 2f), "2 m/s covers two 1 m strides");
            Assert.AreEqual(6, StepsOverOneSecond(speed: 6f), "6 m/s covers six");
        }

        private static int StepsOverOneSecond(float speed)
        {
            var strider = new StrideAccumulator();
            // 1/64 s frames keep every product exact in binary floating point, so the test
            // measures the accumulator rather than the rounding of 1/60.
            const int frames = 64;
            const float delta = 1f / frames;
            var steps = 0;
            for (var frame = 0; frame < frames; frame++)
            {
                if (strider.Advance(speed * delta, Stride, out _)) steps++;
            }
            return steps;
        }
    }
}
