using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// A held weapon. Carries only what the swing/shot needs — the model itself is whatever art
    /// sits under this transform. Hitscan for guns, a sphere sweep for melee; no projectiles yet.
    /// Numbers here are greybox defaults and will move to the content bundle.
    /// </summary>
    public sealed class Weapon : MonoBehaviour
    {
        [SerializeField] private WeaponKind kind = WeaponKind.Knife;

        [Header("Damage")]
        [SerializeField] private float damage = 25f;

        [Tooltip("Metres. Melee is a short sweep, guns are effectively hitscan.")]
        [SerializeField] private float range = 2f;

        [Tooltip("Seconds between attacks.")]
        [SerializeField] private float cooldown = 0.5f;

        [Tooltip("Holding the button keeps firing. Off = one shot per click.")]
        [SerializeField] private bool automatic;

        [Header("Melee sweep")]
        [Tooltip("Radius of the sphere swept forward on a melee hit. Ignored by guns.")]
        [SerializeField] private float sweepRadius = 0.35f;

        [Header("Gun")]
        [Tooltip("Where the shot originates for effects. Optional.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Degrees of cone spread while hip-firing. Aiming halves it.")]
        [SerializeField] private float hipSpread = 3f;

        public WeaponKind Kind => kind;
        public float Damage => damage;
        public float Range => range;
        public float Cooldown => cooldown;
        public bool Automatic => automatic;
        public float SweepRadius => sweepRadius;
        public float HipSpread => hipSpread;
        public bool IsGun => kind == WeaponKind.Rifle;
        public Transform Muzzle => muzzle != null ? muzzle : transform;
    }
}
