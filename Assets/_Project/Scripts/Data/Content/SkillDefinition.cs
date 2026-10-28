using System.Collections.Generic;

namespace ShadowVale.Data.Content
{
    /// <summary>
    /// A usage-based skill (shooting | engineering | stealth). <see cref="XpCurve"/> is the XP needed for each
    /// level; <see cref="Effects"/> holds one value per level for each named effect (recoil_multipliers,
    /// reload_multipliers, takedown_multipliers, repair_bonus_fractions, field_repair_caps).
    /// </summary>
    public sealed class SkillDefinition
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public int MaxLevel { get; set; }
        public List<float> XpCurve { get; set; } = new();
        public Dictionary<string, List<float>> Effects { get; set; } = new();
    }
}
