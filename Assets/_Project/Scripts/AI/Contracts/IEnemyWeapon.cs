using UnityEngine;

namespace ShadowVale.AI.Contracts
{
    /// <summary>How an enemy fires. Rate of fire is the weapon's business: calls between shots are ignored.</summary>
    public interface IEnemyWeapon
    {
        /// <summary>Aimed fire at the player (in range, in sight).</summary>
        void FireAtPlayer();
        /// <summary>Suppressive fire at a point: keeps the player's head down, never hits him.</summary>
        void FireSuppressive(Vector3 point);
    }
}
