namespace ShadowVale.Data.Content
{
    public sealed class WeaponDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        /// <summary>rifle | smg | shotgun | pistol | sniper | carbine</summary>
        public string Class { get; set; }
        public float Damage { get; set; }
        /// <summary>Shots per second.</summary>
        public float FireRate { get; set; }
        public float Range { get; set; }
        /// <summary>Item id of the ammo this weapon consumes.</summary>
        public string AmmoType { get; set; }
        public int MagazineSize { get; set; }
        public float ReloadSeconds { get; set; }
        public float DurabilityMax { get; set; }
        public float DurabilityPerShot { get; set; }
        /// <summary>Radius (world units) of the noise event emitted per shot. 0 = silenced.</summary>
        public float NoiseRadius { get; set; }
        public bool Suppressed { get; set; }
    }
}
