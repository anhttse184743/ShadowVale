namespace ShadowVale.AI.Perception
{
    /// <summary>
    /// How one enemy sees. Built from the archetype/balance data; defaults are Map 1's tuned values.
    /// </summary>
    public readonly struct VisionProfile
    {
        /// <summary>Metres at which he sees the player standing in the open.</summary>
        public readonly float Range;
        /// <summary>Full cone angle (degrees) while unaware; once engaged he tracks all around.</summary>
        public readonly float ConeAngle;
        /// <summary>Range multiplier while the player crouches.</summary>
        public readonly float CrouchRangeScale;
        /// <summary>Range multiplier while the player is hidden.</summary>
        public readonly float HiddenRangeScale;
        /// <summary>Brushing past this close is noticed from any side (standing / crouched).</summary>
        public readonly float TouchRadiusStanding, TouchRadiusCrouched;
        public readonly float EyeHeight;

        public VisionProfile(float range, float coneAngle, float crouchRangeScale = .6f, float hiddenRangeScale = .32f,
            float touchRadiusStanding = 2.5f, float touchRadiusCrouched = 1.2f, float eyeHeight = 1.3f)
        {
            Range = range; ConeAngle = coneAngle;
            CrouchRangeScale = crouchRangeScale; HiddenRangeScale = hiddenRangeScale;
            TouchRadiusStanding = touchRadiusStanding; TouchRadiusCrouched = touchRadiusCrouched;
            EyeHeight = eyeHeight;
        }
    }
}
