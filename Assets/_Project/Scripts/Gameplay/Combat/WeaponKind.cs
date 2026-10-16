namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// What the player is holding. The value doubles as the <c>Weapon</c> animator parameter,
    /// so the order here is part of the animator contract — append, never reorder.
    /// </summary>
    public enum WeaponKind
    {
        Unarmed = 0,
        Knife = 1,
        Rifle = 2,
    }
}
