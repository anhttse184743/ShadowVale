namespace ShadowVale.AI.Contracts
{
    /// <summary>
    /// What a sharp-eyed enemy can read off the player's weapon handling. Optional: the player's
    /// <see cref="IPlayerObservable"/> may also implement it; enemies that do not care never ask.
    /// Only the commander reads it today (pressing a reload, dodging a raised rifle).
    /// </summary>
    public interface IPlayerCombatState
    {
        bool IsReloading { get; }
        /// <summary>Raised and aiming along <see cref="IPlayerObservable.Forward"/>.</summary>
        bool IsAiming { get; }
    }
}
