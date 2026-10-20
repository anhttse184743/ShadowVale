using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>Accepted damage, captured when it happens rather than from the attacker's later loadout.</summary>
    public readonly struct DamageNotice
    {
        public readonly GameObject Source;
        public readonly Vector3 HitPoint, SourcePosition;
        public readonly float Amount;
        public readonly bool IsBullet;
        public readonly WeaponKind Weapon;
        public DamageNotice(GameObject source,Vector3 hitPoint,Vector3 sourcePosition,float amount,bool isBullet,WeaponKind weapon)
        {Source=source;HitPoint=hitPoint;SourcePosition=sourcePosition;Amount=amount;IsBullet=isBullet;Weapon=weapon;}
    }
}
