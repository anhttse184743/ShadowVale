using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed partial class Map01Hud
    {
        private bool fieldCraftTab;
        private Vector2 fieldRecipeScroll;
        private string fieldRecipe = Map01Inventory.RecoverM16Recipe;
        private int fieldQuantity = 1;
        public bool FieldCraftingOpen => fieldCraftTab && mission != null && mission.InventoryOpen && !mission.BenchOpen;
        public void ShowFieldCrafting() { fieldCraftTab = true; skillsTab = false; }

        private static string FieldRecipeName(ForestRecipe recipe) => recipe.id == Map01Inventory.RecoverM16Recipe
            ? "Thu hồi đạn M16" : recipe.id == Map01Inventory.AssembleAkRecipe ? "Ráp 10 đạn AK" : ForestInventory.Name(recipe.output_item_id);

        private void DrawFieldCraft(float width, Rect panel)
        {
            BenchStyles();
            DrawEquipColumn(width, Event.current, true);
            HudPanel(panel);
            GUI.Label(new Rect(panel.x + 25, 75, panel.width - 50, 60), "CHẾ TẠO DÃ CHIẾN", benchName);
            DrawInventoryTabs(panel);
            GUI.Label(new Rect(panel.x + 25, 200, panel.width - 50, 48),
                "Bộ dụng cụ dã chiến: luôn mang theo · không chiếm ô túi.\nĐứng yên để làm. Thế giới vẫn chạy; di chuyển / bị thương sẽ hủy.", benchSmall);
            var recipes = inventory.Recipes.OrderBy(r => r.id == Map01Inventory.RecoverM16Recipe ? 0
                : r.id == Map01Inventory.AssembleAkRecipe ? 1 : r.field_seconds > 0 ? 2 : 3).ToList();
            if (inventory.IsFieldCrafting) { fieldRecipe = inventory.FieldCraftingId; fieldQuantity = inventory.FieldCraftQuantity; }
            var picked = recipes.FirstOrDefault(r => r.id == fieldRecipe) ?? recipes.FirstOrDefault();
            if (picked == null) return;
            fieldRecipe = picked.id;
            float rowHeight = 58, listHeight = 235;
            var list = new Rect(panel.x + 22, 258, panel.width - 44, listHeight);
            fieldRecipeScroll = GUI.BeginScrollView(list, fieldRecipeScroll, new Rect(0, 0, list.width - 20, recipes.Count * rowHeight));
            for (int i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                var row = new Rect(0, i * rowHeight, list.width - 24, rowHeight - 5);
                HudPanel(row, fieldRecipe == recipe.id);
                HudIcon(new Rect(row.x + 7, row.y + 4, 44, 44), recipe.output_item_id);
                GUI.Label(new Rect(61, row.y + 6, row.width - 75, 24), FieldRecipeName(recipe), benchRowName);
                GUI.Label(new Rect(61, row.y + 29, row.width - 75, 20), recipe.field_seconds > 0
                    ? $"{recipe.field_seconds:0}s · Có thể làm {inventory.CanMake(recipe)} lần" : "Cần bàn chế tạo", benchSmallDim);
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && !inventory.IsFieldCrafting;
                bool choose = GUI.Button(row, GUIContent.none, GUIStyle.none);
                GUI.enabled = enabled;
                if (choose) { fieldRecipe = recipe.id; fieldQuantity = 1; }
            }
            GUI.EndScrollView();
            picked = recipes.First(r => r.id == fieldRecipe);
            int max = inventory.FieldBatchLimit(picked);
            fieldQuantity = Mathf.Clamp(fieldQuantity, 1, max);
            GUI.Label(new Rect(panel.x + 25, 506, panel.width - 50, 34), FieldRecipeName(picked).ToUpperInvariant(), benchHead);
            GUI.Label(new Rect(panel.x + 25, 547, panel.width - 50, 72),
                $"Nhận: {picked.output_count * fieldQuantity} {ForestInventory.Name(picked.output_item_id).ToLowerInvariant()}\n{(inventory.IsFieldCrafting ? "Đã giữ riêng" : "Cần")}: {Map01Inventory.Inputs(picked, fieldQuantity)}", benchText);
            string detail = picked.id == Map01Inventory.RecoverM16Recipe
                ? inventory.IsFieldCrafting
                    ? $"Đã giữ riêng {10 * fieldQuantity} đạn M16 dự trữ cho lượt này. Hủy để nhận lại; đạn trong băng được giữ nguyên."
                    : $"Chỉ lấy đạn M16 dự trữ. Đang có {inventory.Count(Map01M16.Ammo)}; sau thao tác còn {Mathf.Max(0, inventory.Count(Map01M16.Ammo) - 10 * fieldQuantity)}. Đạn trong băng được giữ nguyên."
                : picked.id == Map01Inventory.AssembleAkRecipe
                    ? "20 M16 thu hồi + 1 gói AK → 10 AK. Gói tiếp liệu đúng loại được chuẩn bị ở căn cứ; không đổi trực tiếp hai cỡ đạn."
                    : ForestInventory.Description(picked.output_item_id);
            GUI.Label(new Rect(panel.x + 25, 623, panel.width - 50, 62), detail, benchSmall);
            if (inventory.IsFieldCrafting)
            {
                var running = inventory.Recipe(inventory.FieldCraftingId);
                GUI.Label(new Rect(panel.x + 25, 698, panel.width - 50, 28), $"Đang làm: {FieldRecipeName(running)} · {inventory.FieldCraftRemaining:0.0}s", benchSmallBold);
                var bar = new Rect(panel.x + 25, 735, panel.width - 50, 10);
                HudFill(bar, BenchEdge); HudFill(new Rect(bar.x, bar.y, bar.width * inventory.FieldCraftProgress, bar.height), HudGold);
                if (BenchButton(new Rect(panel.x + 25, 770, panel.width - 50, 46), "HỦY — HOÀN VẬT LIỆU", benchButton)) inventory.CancelFieldCraft();
                return;
            }
            if (max > 1)
            {
                for (int q = 1; q <= max; q++)
                {
                    if (BenchButton(new Rect(panel.x + 25 + (q - 1) * 115, 692, 108, 36), $"{q * 10} M16", benchSmallBold, true, fieldQuantity == q)) fieldQuantity = q;
                }
            }
            string refusal = inventory.FieldCraftRefusal(picked.id, fieldQuantity);
            if (BenchButton(new Rect(panel.x + 25, 744, panel.width - 50, 48), picked.field_seconds > 0 ? $"BẮT ĐẦU · {picked.field_seconds:0}s" : "CẦN BÀN CHẾ TẠO", benchButton, refusal == null))
                inventory.TryFieldCraft(picked.id, fieldQuantity);
            GUI.Label(new Rect(panel.x + 25, 802, panel.width - 50, 48), refusal ?? "Tab / Esc hoặc nút Hủy: dừng và hoàn vật liệu.", benchSmall);
        }
    }
}
