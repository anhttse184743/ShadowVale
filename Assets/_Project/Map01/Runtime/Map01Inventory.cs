using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Everything the player carries: raw counts and the one crafting recipe Map 1 uses. Items
    /// are used by their own keys — 1/2/3 weapons (PlayerCombat), H bandage, Q stone — not
    /// through assignable shortcuts. Presentation (names, icons, descriptions, stack splitting)
    /// stays in the static <see cref="ForestInventory"/> helper; this component only owns state.
    /// </summary>
    public sealed class Map01Inventory : MonoBehaviour
    {
        private readonly Dictionary<string, int> items = new Dictionary<string, int>();
        private int stones;
        private string selectedItem;
        private float nextQuickUse;
        private string crafting;
        private float craftUntil;

        /// <summary>Set when a panel toggles or a weapon is equipped, so the click that did it
        /// cannot also fire. Map01PlayerInteraction clears it once the panels are shut and the
        /// left button is up — it must never outlive that click, or combat stays blocked for good.</summary>
        public bool SuppressFire { get; set; }
        public bool IsCrafting => crafting != null;
        public string SelectedItem { get => selectedItem; set => selectedItem = value; }
        public float CraftRemaining => Mathf.Max(0, craftUntil - Time.time);
        public string CraftingId => crafting;

        private Map01Mission mission;
        private void Awake() { mission = GetComponent<Map01Mission>(); }

        /// <summary>Nam starts with his rifle and knife but no ammunition — ammo_rifle and herb
        /// only come from picking up loot in the field (see the "tutorial_loot" point).</summary>
        public void SeedStartingLoadout(int startingStones)
        {
            items["rifle_standard"] = 1;
            items["knife"] = 1;
            stones = startingStones;
        }
        public int Count(string id) => id == "stone" ? stones : items.TryGetValue(id, out int count) ? count : 0;
        public void Add(string id, int amount)
        {
            if (id == "stone") { stones += amount; return; }
            items[id] = Count(id) + amount;
        }
        public void Spend(string id, int amount)
        {
            if (id == "stone") { stones -= amount; return; }
            items[id] = Count(id) - amount;
        }
        public IReadOnlyDictionary<string, int> Items => items;
        public int Stones => stones;

        public int StackLimit(string id)
        {
            if (ForestInventory.IsWeapon(id)) return 1;
            if (id == "stone") return 20;
            if (id == "supplies" || id == "river_documents") return 1;
            return Mathf.Max(1, mission.Bundle?.items?.FirstOrDefault(i => i.id == id)?.stack_max ?? 1);
        }
        public List<ForestInventory.Stack> InventoryStacks()
        {
            var values = items.Where(p => p.Key != "stone").ToList();
            values.Add(new KeyValuePair<string, int>("stone", stones));
            return ForestInventory.Split(values, StackLimit);
        }

        public bool IsEquipped(string id)
        {
            if (!ForestInventory.IsWeapon(id) || mission == null || mission.ModernCombat == null) return false;
            return mission.ModernCombat.EquippedKind == (id == "knife"
                ? ShadowVale.Gameplay.Combat.WeaponKind.Knife : ShadowVale.Gameplay.Combat.WeaponKind.Rifle);
        }
        public bool EquipItem(string id)
        {
            if (!ForestInventory.IsWeapon(id) || Count(id) <= 0 || mission == null || !mission.IsInitialized ||
                mission.Stopped || ForestMenu.Visible || IsCrafting || mission.ModernCombat == null) return false;
            var kind = id == "knife" ? ShadowVale.Gameplay.Combat.WeaponKind.Knife : ShadowVale.Gameplay.Combat.WeaponKind.Rifle;
            SuppressFire = true;
            mission.ModernCombat.Equip(kind);
            return mission.ModernCombat.EquippedKind == kind;
        }

        public bool UseItem(string id, Vector3? throwTarget = null)
        {
            if (!mission.IsInitialized || mission.Stopped || ForestMenu.Visible || Time.time < nextQuickUse) return false;
            if (crafting != null) { mission.Say("Hãy hoàn thành chế tạo trước khi dùng vật phẩm.", 3); return false; }
            if (!ForestInventory.QuickUsable(id)) { mission.Say("Vật phẩm này không dùng trực tiếp.", 3); return false; }
            if (ForestInventory.IsWeapon(id)) return EquipItem(id);
            if (Count(id) <= 0) { mission.Say("Đã hết " + ForestInventory.Name(id).ToLowerInvariant() + ".", 3); return false; }
            if (id == "medkit_small")
            {
                if (mission.PlayerHealth >= mission.Settings.playerHP) { mission.Say("Máu đã đầy, chưa cần dùng băng cứu thương.", 3); return false; }
                Spend(id, 1);
                mission.RestoreHealthFromSave(Mathf.Min(mission.Settings.playerHP, mission.PlayerHealth + mission.Settings.medkitHeal));
                mission.Say("Đã dùng băng cứu thương.", 3);
            }
            else
            {
                // Only the stone leaves the bag here; its flight and the noise where it lands
                // belong to Map01StoneThrow.
                if (mission.InventoryOpen || mission.MapOpen) { mission.Say("Đóng túi đồ để ngắm hướng ném đá.", 3); return false; }
                if (throwTarget == null) return false;
                Spend("stone", 1);
            }
            nextQuickUse = Time.time + .25f;
            return true;
        }
        public bool NearWorkbench() => mission.Interactables.Any(p => p.kind == ForestPointKind.Workbench
            && Vector3.Distance(mission.player.position, p.transform.position) < mission.Settings.interactRange);
        public void TryCraft()
        {
            if (crafting != null || !NearWorkbench()) return;
            var recipe = mission.Bundle.craft_recipes.First(r => r.id == "craft_medkit_small");
            if (recipe.inputs.Any(i => Count(i.item_id) < i.count)) { mission.Say("Chưa đủ vật liệu: cần 2 vải và 1 thảo dược."); return; }
            foreach (var input in recipe.inputs) Spend(input.item_id, input.count);
            crafting = recipe.id; craftUntil = Time.time + recipe.craft_seconds;
        }
        public void Update()
        {
            if (crafting != null && Time.time >= craftUntil)
            {
                var recipe = mission.Bundle.craft_recipes.First(r => r.id == crafting);
                Add(recipe.output_item_id, recipe.output_count);
                crafting = null; mission.Say("Đã chế tạo băng cứu thương. Nhấn H để sử dụng.");
            }
        }

        public void RestoreFromSave(ForestIngredient[] savedItems, int savedStones, string savedCrafting, float craftRemaining)
        {
            items.Clear();
            foreach (var i in savedItems) items[i.item_id] = i.count;
            // Earlier saves carried these prefabs without inventory entries. Only migrate missing keys.
            if (!items.ContainsKey("rifle_standard")) items["rifle_standard"] = 1;
            if (!items.ContainsKey("knife")) items["knife"] = 1;
            stones = savedStones;
            crafting = string.IsNullOrEmpty(savedCrafting) ? null : savedCrafting;
            craftUntil = Time.time + craftRemaining;
        }
        public ForestIngredient[] CaptureItems() => items.Select(p => new ForestIngredient { item_id = p.Key, count = p.Value }).ToArray();
    }
}
