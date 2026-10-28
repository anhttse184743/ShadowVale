namespace ShadowVale.AI.Perception
{
    /// <summary>What made a noise. Hearing treats them alike; telemetry and the debug console tell them apart.</summary>
    public enum NoiseKind
    {
        Other,
        Footstep,
        Landing,
        Gunshot,
        Stone,
        Grenade,
    }
}
