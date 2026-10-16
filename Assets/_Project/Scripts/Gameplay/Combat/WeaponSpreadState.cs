using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>How the shooter is standing when the trigger breaks. Steadiest to worst.</summary>
    public enum ShooterStance
    {
        Sneak = 0,
        Standing = 1,
        Walking = 2,
        Running = 3,
        Airborne = 4,
    }

    /// <summary>
    /// Where a shot actually goes, and how the view kicks afterwards.
    /// <para>
    /// Two counters, deliberately independent, because the things this has to do pull in
    /// different directions:
    /// </para>
    /// <list type="bullet">
    /// <item><b>ShotIndex</b> — position in the burst. Drives a fixed climb the player can learn
    /// and pull against. Being learnable is the point: a random climb cannot be mastered, only
    /// endured.</item>
    /// <item><b>Bloom</b> — a widening cone of genuine randomness that no amount of skill
    /// compensates for. This is what stops a held trigger from being the right answer at every
    /// range.</item>
    /// </list>
    /// <para>
    /// Keeping them apart is what lets single taps be exact, short bursts stay honest, and
    /// sustained fire still punish: taps and bursts never reach the bloom, while the climb is
    /// there from the first shot for the player to work against.
    /// </para>
    /// Plain C# — nothing from Unity beyond the maths types — so every number below can be
    /// asserted without entering play mode.
    /// </summary>
    public sealed class WeaponSpreadState
    {
        /// <summary>Seconds of quiet that end a burst. A tap is any shot that stands alone.</summary>
        public const float BurstResetSeconds = 0.25f;

        /// <summary>Shots at the start of a burst that add no bloom, so 2-3 round bursts stay exact.</summary>
        public const int FreeBloomShots = 3;

        /// <summary>Shots before the climb drifts sideways, so a short burst rises straight.</summary>
        public const int StraightShots = 4;

        private const float BloomPerShot = 0.085f;
        private const float BloomRecoveryPerSecond = 0.9f;

        /// <summary>Degrees of cone added at full bloom, on top of the stance's own.</summary>
        private const float BloomConeDegrees = 2.5f;

        // The vertical climb starts gentle and steepens, which is what makes the first few
        // rounds usable and a held trigger not. It totals about 26 degrees over a 30-round
        // magazine, of which the first three account for barely one.
        private const float VerticalFirstShot = 0.33f;
        private const float VerticalPeak = 1.00f;
        private const int VerticalRampShots = 12;

        // Horizontal drift arrives late and settles into a steady pull right — about 7.5 degrees
        // across the magazine. Late enough that a burst is a vertical line, not a diagonal.
        private const float HorizontalPeak = 0.34f;
        private const int HorizontalRampShots = 8;

        private float _lastShotTime = float.NegativeInfinity;

        /// <summary>Shots into the current burst. 0 means the next shot stands alone.</summary>
        public int ShotIndex { get; private set; }

        /// <summary>0 to 1. Scales the random cone that skill cannot cancel.</summary>
        public float Bloom { get; private set; }

        /// <summary>Half-angle of the cone a shot can land in, in degrees.</summary>
        public float ConeHalfAngle(ShooterStance stance, bool aiming)
            => StanceCone(stance, aiming) + Bloom * BloomConeDegrees;

        /// <summary>
        /// The base cone before any bloom.
        /// <para>
        /// Aiming is worth most when the shooter is settled and nothing at all once they are
        /// running, because sights do not steady a sprint. Running ignores the right mouse
        /// button for that reason.
        /// </para>
        /// </summary>
        public static float StanceCone(ShooterStance stance, bool aiming) => stance switch
        {
            ShooterStance.Sneak => aiming ? 0.14f : 1.10f,
            ShooterStance.Standing => aiming ? 0.20f : 1.60f,
            ShooterStance.Walking => aiming ? 0.55f : 2.40f,
            ShooterStance.Running => 5.50f,
            _ => aiming ? 3.00f : 7.00f,
        };

        /// <summary>
        /// Registers a shot and returns the view kick it causes, in degrees: x rises, y pulls
        /// right.
        /// </summary>
        public Vector2 Fire(float time)
        {
            if (time - _lastShotTime > BurstResetSeconds)
            {
                // Long enough between pulls that this is a fresh burst. Every deliberate tap is
                // therefore shot zero, the most accurate one the weapon has.
                ShotIndex = 0;
                Bloom = 0f;
            }
            _lastShotTime = time;

            int index = ShotIndex;
            ShotIndex++;

            if (index >= FreeBloomShots)
            {
                Bloom = Mathf.Min(1f, Bloom + BloomPerShot);
            }

            return new Vector2(VerticalKick(index), HorizontalKick(index));
        }

        /// <summary>Degrees the view rises on the given shot of a burst.</summary>
        public static float VerticalKick(int shotIndex)
        {
            float ramp = Mathf.Clamp01(shotIndex / (float)VerticalRampShots);
            return Mathf.Lerp(VerticalFirstShot, VerticalPeak, ramp);
        }

        /// <summary>Degrees the view pulls right on the given shot. Zero early in a burst.</summary>
        public static float HorizontalKick(int shotIndex)
        {
            if (shotIndex < StraightShots) return 0f;
            float ramp = Mathf.Clamp01((shotIndex - StraightShots) / (float)HorizontalRampShots);
            return HorizontalPeak * ramp;
        }

        /// <summary>
        /// Lets the cone close again between bursts. The burst index is deliberately left alone:
        /// it is reset by the gap before the next shot rather than by a clock running now, so a
        /// player who pauses and resumes gets the same pattern either way.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (Bloom > 0f)
            {
                Bloom = Mathf.Max(0f, Bloom - BloomRecoveryPerSecond * deltaTime);
            }
        }

        /// <summary>Clears the burst outright — swapping weapons, dying, reloading.</summary>
        public void Reset()
        {
            ShotIndex = 0;
            Bloom = 0f;
            _lastShotTime = float.NegativeInfinity;
        }

        /// <summary>
        /// A random direction inside the cone, distributed the way a real group is. The square
        /// root on the radius is what keeps shots from piling up against the rim — drawing the
        /// radius flat produces a ring rather than a group.
        /// </summary>
        public static Vector3 Scatter(Vector3 forward, float coneHalfAngleDegrees, System.Random random = null)
        {
            if (coneHalfAngleDegrees <= 0.0001f) return forward;

            double u1 = random?.NextDouble() ?? UnityEngine.Random.value;
            double u2 = random?.NextDouble() ?? UnityEngine.Random.value;

            float radius = coneHalfAngleDegrees * Mathf.Sqrt((float)u1);
            float spin = (float)(u2 * 360.0);

            Vector3 axis = Mathf.Abs(Vector3.Dot(forward.normalized, Vector3.up)) > 0.99f
                ? Vector3.right
                : Vector3.up;
            Vector3 perpendicular = Vector3.Cross(forward, axis).normalized;
            Vector3 tilt = Quaternion.AngleAxis(spin, forward) * perpendicular;
            return Quaternion.AngleAxis(radius, tilt) * forward;
        }
    }
}
