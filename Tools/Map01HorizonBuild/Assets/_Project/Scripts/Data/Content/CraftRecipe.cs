using System.Collections.Generic;

namespace ShadowVale.Data.Content
{
    public sealed class CraftRecipe
    {
        public string Id { get; set; }
        public string OutputItemId { get; set; }
        public int OutputCount { get; set; } = 1;
        /// <summary>shooting | engineering | stealth</summary>
        public string RequiredSkill { get; set; }
        public int RequiredSkillLevel { get; set; }
        public float CraftSeconds { get; set; }
        public List<CraftInput> Inputs { get; set; } = new();
    }

    public sealed class CraftInput
    {
        public string ItemId { get; set; }
        public int Count { get; set; }
    }
}
