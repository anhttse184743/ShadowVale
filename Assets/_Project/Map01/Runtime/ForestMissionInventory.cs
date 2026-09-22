using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public sealed partial class ForestMission
    {
        private string[] quickSlots = { "medkit_small", "stone", null, null, null };
        private string selectedItem;
        private int activeQuickSlot;
        private float nextQuickUse;
        private bool suppressFireUntilRelease;
        public bool InventoryOpen => inventoryOpen;
        public float Health => hp;
        public string QuickItem(int slot) => slot >= 0 && slot < 5 ? quickSlots[slot] : null;

        public int StackLimit(string id)
        {
            if (id == "stone") return 20;
            if (id == "supplies" || id == "river_documents") return 1;
            return Mathf.Max(1, bundle?.items?.FirstOrDefault(i => i.id == id)?.stack_max ?? 1);
        }
        public List<ForestInventory.Stack> InventoryStacks()
        {
            var values = inventory.Where(p => p.Key != "stone").ToList();
            values.Add(new KeyValuePair<string, int>("stone", stones));
            return ForestInventory.Split(values, StackLimit);
        }
        public void SetInventoryOpen(bool value)
        {
            inventoryOpen = value; mapOpen = false; CancelHudDrag(); suppressFireUntilRelease = true;
        }
        public bool CloseGameplayPanel()
        {
            if (!inventoryOpen && !mapOpen) return false;
            inventoryOpen = mapOpen = false; CancelHudDrag(); suppressFireUntilRelease = true; return true;
        }
        public bool AssignQuickSlot(int slot, string id)
        {
            if (slot < 0 || slot >= quickSlots.Length) return false;
            if (!string.IsNullOrEmpty(id) && (!ForestInventory.QuickUsable(id) || Count(id) <= 0)) {
                Say("Chỉ băng cứu thương và đá ném có thể gán vào ô nhanh."); return false;
            }
            // A shortcut references the total stock; assigning it never moves or duplicates inventory.
            quickSlots[slot] = string.IsNullOrEmpty(id) ? null : id;
            return true;
        }
        private void RestoreQuickSlots(string[] saved)
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
            if (string.IsNullOrEmpty(quickSlots[slot])) { Say("Ô nhanh trống. Mở túi đồ bằng Tab để gán vật phẩm.", 3); return false; }
            return UseItem(quickSlots[slot]);
        }
        public bool UseItem(string id)
        {
            if (!IsInitialized || Stopped || ForestMenu.Visible || Time.time < nextQuickUse) return false;
            if (crafting != null) { Say("Hãy hoàn thành chế tạo trước khi dùng vật phẩm.", 3); return false; }
            if (!ForestInventory.QuickUsable(id)) { Say("Vật phẩm này không dùng trực tiếp.", 3); return false; }
            if (Count(id) <= 0) { Say("Đã hết " + ForestInventory.Name(id).ToLowerInvariant() + ".", 3); return false; }
            if (id == "medkit_small") {
                if (hp >= Settings.playerHP) { Say("Máu đã đầy, chưa cần dùng băng cứu thương.", 3); return false; }
                inventory[id]--; hp = Mathf.Min(Settings.playerHP, hp + Settings.medkitHeal);
                Say("Đã dùng băng cứu thương.", 3);
            } else {
                if (inventoryOpen || mapOpen) { Say("Đóng túi đồ để ngắm hướng ném đá.", 3); return false; }
                Aim(); stones--;
                var target = player.position + Vector3.ClampMagnitude(aim - player.position, Settings.stoneRange);
                EmitNoise(target, Settings.stoneNoise);
                Trace(player.position + Vector3.up, target + Vector3.up * .2f, Color.yellow);
                Say("Tiếng đá rơi — lính gần đó sẽ đến kiểm tra.", 3);
            }
            nextQuickUse = Time.time + .25f;
            return true;
        }
        private void HandleQuickKeys(Keyboard keyboard)
        {
            var keys = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key, keyboard.digit5Key };
            for (int i = 0; i < keys.Length; i++) if (keys[i].wasPressedThisFrame) {
                if (inventoryOpen) {
                    if (selectedItem != null && AssignQuickSlot(i, selectedItem)) Say("Đã gán vào ô nhanh " + (i + 1) + ".", 3);
                } else if (!mapOpen) UseQuickSlot(i);
            }
        }
    }
}
