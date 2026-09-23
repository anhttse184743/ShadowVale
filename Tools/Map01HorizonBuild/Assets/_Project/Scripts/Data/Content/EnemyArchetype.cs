namespace ShadowVale.Data.Content
{
    public sealed class EnemyArchetype
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public float MaxHp { get; set; }
        public float MoveSpeed { get; set; }
        public float VisionRange { get; set; }
        /// <summary>Full cone angle in degrees.</summary>
        public float VisionAngle { get; set; }
        public float HearingRange { get; set; }
        public string WeaponId { get; set; }
        public float AlertDecaySeconds { get; set; }
        /// <summary>HP fraction (0..1) below which the FSM enters Retreat.</summary>
        public float RetreatHpThreshold { get; set; }
        public string LootTableId { get; set; }
        public bool IsBoss { get; set; }
    }
}
