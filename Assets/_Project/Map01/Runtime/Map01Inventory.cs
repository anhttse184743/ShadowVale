using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Everything the player carries: raw counts, the five quick-use shortcuts, and the one
    /// crafting recipe Map 1 uses. Presentation (names, icons, descriptions, stack splitting)
    /// stays in the static <see cref="ForestInventory"/> helper; this component only owns state.
    /// </summary>
    public sealed class Map01Inventory : MonoBehaviour
    {
        private readonly Dictionary<string, int> items = new Dictionary<string, int>();
        private int stones;
        private string[] quickSlots = { "medkit_small", "stone", null, null, null };
        private int activeQuickSlot;
        private string selectedItem;
        private float nextQuickUse;
        private string crafting;
        private float craftUntil;

        /// <summary>Set true while the inventory/map panel is open, so a released mouse button
        /// does not fire a shot the moment the panel closes.</summary>
        public bool SuppressFire { get; set; }
        public bool IsCrafting => crafting != null;
        public string SelectedItem { get => selectedItem; set => selectedItem = value; }
        public int ActiveQuickSlot => activeQuickSlot;
        public float CraftRemaining => Mathf.Max(0, craftUntil - Time.time);
        public string CraftingId => crafting;

        private Map01Mission mission;
        private void Awake() { mission = GetComponent<Map01Mission>(); }

        public void SeedStartingLoadout(int startingAmmo, int startingStones)
        {
            items["ammo_rifle"] = startingAmmo;
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

        public bool AssignQuickSlot(int slot, string id)
        {
            if (slot < 0 || slot >= quickSlots.Length) return false;
            if (!string.IsNullOrEmpty(id) && (!ForestInventory.QuickUsable(id) || Count(id) <= 0))
            {
                mission.Say("Chỉ băng cứu thương và đá ném có thể gán vào ô nhanh."); return false;
            }
            // A shortcut references the total stock; assigning it never moves or duplicates inventory.
            quickSlots[slot] = string.IsNullOrEmpty(id) ? null : id;
            return true;
        }
        public string QuickItem(int slot) => slot >= 0 && slot < 5 ? quickSlots[slot] : null;
        public void RestoreQuickSlots(string[] saved)
        {
            activeQuickSlot = 0;
            quickSlots = new[] { "medkit_small", "stone", null, null, null };
            if (saved == null) return; // Old checkpoints retain the useful default layout.
            Array.Clear(quickSlots, 0, quickSlots.Length);
            for (int i = 0; i < Math.Min(5, saved.Length); i++)
                quickSlots[i] = ForestInventory.QuickUsable(saved[i]) ? saved[i] : null;
        }
        public bool UseQuickSlot(int slot)
        {
            if (slot < 0 || slot >= 5) return false;
            activeQuickSlot = slot;
            if (string.IsNullOrEmpty(quickSlots[slot])) { mission.Say("Ô nhanh trống. Mở túi đồ bằng Tab để gán vật phẩm.", 3); return false; }
            return UseItem(quickSlots[slot]);
        }
        public bool UseItem(string id, Vector3? throwTarget = null)
        {
            if (!mission.IsInitialized || mission.Stopped || ForestMenu.Visible || Time.time < nextQuickUse) return false;
            if (crafting != null) { mission.Say("Hãy hoàn thành chế tạo trước khi dùng vật phẩm.", 3); return false; }
            if (!ForestInventory.QuickUsable(id)) { mission.Say("Vật phẩm này không dùng trực tiếp.", 3); return false; }
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
                if (mission.InventoryOpen || mission.MapOpen) { mission.Say("Đóng túi đồ để ngắm hướng ném đá.", 3); return false; }
                if (throwTarget == null) return false;
                Spend("stone", 1);
                mission.EmitNoise(throwTarget.Value, mission.Settings.stoneNoise);
                mission.Say("Tiếng đá rơi — lính gần đó sẽ đến kiểm tra.", 3);
            }
            nextQuickUse = Time.time + .25f;
            return true;
        }
        public void HandleQuickKeys(Keyboard keyboard)
        {
            var keys = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key, keyboard.digit5Key };
            for (int i = 0; i < keys.Length; i++) if (keys[i].wasPressedThisFrame)
            {
                if (mission.InventoryOpen)
                {
                    if (selectedItem != null && AssignQuickSlot(i, selectedItem)) mission.Say("Đã gán vào ô nhanh " + (i + 1) + ".", 3);
                }
                else if (!mission.MapOpen) UseQuickSlot(i);
            }
        }

        public bool NearWorkbench() => mission.Points.Any(p => p.kind == ForestPointKind.Workbench
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
            stones = savedStones;
            crafting = string.IsNullOrEmpty(savedCrafting) ? null : savedCrafting;
            craftUntil = Time.time + craftRemaining;
        }
        public ForestIngredient[] CaptureItems() => items.Select(p => new ForestIngredient { item_id = p.Key, count = p.Value }).ToArray();
    }
}
