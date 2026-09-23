using System.Collections.Generic;

namespace ShadowVale.Data.Save
{
    /// <summary>
    /// Full game state serialised to JSON by <c>SaveService</c>. Bump <see cref="Version"/> and add a
    /// migration under Persistence/Migrations whenever the shape changes; never edit old versions.
    /// </summary>
    public sealed class SaveGameV1
    {
        public int Version { get; set; } = 1;
        public string SavedAt { get; set; }
        public string ContentVersion { get; set; }
        public string CurrentMapId { get; set; }
        public PlayerState Player { get; set; } = new();
        public List<InventorySlotState> Inventory { get; set; } = new();
        public List<InventorySlotState> Storage { get; set; } = new();
        public Dictionary<string, int> SkillXp { get; set; } = new();
        public Dictionary<string, string> QuestStates { get; set; } = new();
        /// <summary>Per-system opaque blobs captured through <c>ISaveable</c> (key = SaveKey).</summary>
        public Dictionary<string, string> Systems { get; set; } = new();
    }

    public sealed class PlayerState
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Yaw { get; set; }
        public float Hp { get; set; }
        public float Stamina { get; set; }
        public string EquippedWeaponInstanceId { get; set; }
    }

    public sealed class InventorySlotState
    {
        public int Slot { get; set; }
        public string ItemId { get; set; }
        public int Count { get; set; }
        public string InstanceId { get; set; }
        public float Durability { get; set; }
        public int AmmoLoaded { get; set; }
    }
}
