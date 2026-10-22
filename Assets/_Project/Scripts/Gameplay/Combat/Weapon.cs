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

        [Tooltip("An actual hit kills an enemy agent. Health still owns cinematic invulnerability and death events.")]
        [SerializeField] private bool lethalAgainstEnemies;

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

        [SerializeField] private bool usesM16Handling;
        public bool UsesM16Handling => usesM16Handling;

        public void ConfigureRifle(float shotDamage, float shotCooldown, Transform barrel, bool originalMagazine = true,
            float shotRange = -1f, bool m16Handling = false)
        {
            damage = shotDamage; cooldown = shotCooldown; muzzle = barrel; UsesOriginalMagazine = originalMagazine;
            if (shotRange > 0f) range = shotRange;
            usesM16Handling = m16Handling;
            if (flash != null) { Destroy(flash.gameObject); flash = null; }
        }
        public bool UsesOriginalMagazine { get; private set; } = true;
        private Transform reloadModel;
        public Transform ReloadModel => reloadModel != null ? reloadModel : transform;
        public void SetReloadModel(Transform model) => reloadModel = model;
        public WeaponKind Kind => kind;
        public float Damage => damage;
        public bool LethalAgainstEnemies => lethalAgainstEnemies;
        public float DamageAgainst(Health target) => lethalAgainstEnemies && target != null
            && target.GetComponentInParent<ShadowVale.AI.Agents.IEnemyAgent>() != null
                ? Mathf.Max(damage,target.Current) : damage;
        public float Range => range;
        public float Cooldown => cooldown;
        public bool Automatic => automatic;
        public float SweepRadius => sweepRadius;
        public float HipSpread => hipSpread;
        public bool IsGun => kind == WeaponKind.Rifle || kind == WeaponKind.Pistol || kind == WeaponKind.Sniper;
        public Transform Muzzle => muzzle != null ? muzzle : transform;

        /// <summary>Configure an additional scene weapon from its existing content profile.</summary>
        public void Configure(WeaponKind value, float hitDamage, float hitRange, float fireRate, bool repeats, Transform tip, bool lethalHits = false)
        {
            kind = value; damage = hitDamage; range = hitRange;
            cooldown = 1f / Mathf.Max(.01f, fireRate); automatic = repeats; muzzle = tip;
            lethalAgainstEnemies = lethalHits;
            usesM16Handling = false;
        }
        private MuzzleFlash flash;
        private void Awake(){if(IsGun)EnsureFlash();}
        private void EnsureFlash()
        {
            if(flash!=null)return;
            var host=new GameObject("Muzzle flash");host.transform.SetParent(Muzzle,false);
            flash=host.AddComponent<MuzzleFlash>();
        }
        public void PlayMuzzleFlash(){if(!IsGun)return;EnsureFlash();flash.Play();}
    }
}
