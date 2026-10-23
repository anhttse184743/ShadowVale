namespace ShadowVale.AI.Fsm
{
    /// <summary>
    /// The six behaviour states required by the proposal (A01). Death is the agent's lifecycle, not a
    /// state; flanking and reinforcements are squad/boss behaviour layered on top.
    /// </summary>
    public enum EnemyStateId
    {
        Patrol,
        /// <summary>Checking a noise or a last-seen position: go there, look around, walk back.</summary>
        Investigate,
        /// <summary>Something is in view; suspicion builds until he is sure.</summary>
        SpotPlayer,
        /// <summary>Spotted: moving to an assigned or nearby cover position, firing when he can.</summary>
        TakeCover,
        /// <summary>Fighting: aimed fire in sight, suppressive fire just after losing sight.</summary>
        Engage,
        /// <summary>Badly hurt in a fight: falls back away from the player, then rejoins.</summary>
        Retreat,
    }
}
