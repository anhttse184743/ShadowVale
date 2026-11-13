using UnityEngine;
using Cell = ShadowVale.Map01.Map01Inventory.BagCell;

namespace ShadowVale.Map01
{
    public sealed partial class Map01Hud
    {
        private const int StorageCellSize = 60, StorageStep = 64;
        private int storagePick = -1;
        private bool storagePickInChest, storageDragging;
        private Cell storageSelection;
        private Vector2 storagePress;
        private string storageMessage;
        private GUIStyle storageCount;

        internal void ResetStorageHud()
        {
            storagePick = -1;
            storageSelection = null;
            storageDragging = false;
            storageMessage = null;
        }

        private void DrawStorage(Map01StorageChest chest, float width, float height)
        {
            if (chest == null) return;
            BagStyles();
            storageCount ??= new GUIStyle(bagCount) { fontSize = 11 };
            var ev = Event.current;
            var cursor = ev.mousePosition;
            if (storageDragging && (ev.type == EventType.Ignore || ev.type == EventType.MouseLeaveWindow))
                storageDragging = false;

            const float chestWidth = Map01StorageChest.Columns * StorageStep + 36, bagWidth = 382, gap = 16, panelHeight = 701;
            float left = (width - chestWidth - bagWidth - gap) / 2;
            float top = (height - panelHeight) / 2;
            var chestPanel = new Rect(left, top, chestWidth, panelHeight);
            var bagPanel = new Rect(chestPanel.xMax + gap, top, bagWidth, panelHeight);
            HudFill(new Rect(0, 0, width, height), new Color(.025f, .03f, .02f, .48f));
            HudPanel(chestPanel);
            HudPanel(bagPanel);
            GUI.Label(new Rect(chestPanel.x + 18, top + 15, chestWidth - 110, 50), Map01StorageChest.DisplayName.ToUpperInvariant(), bagTitle);
            GUI.Label(new Rect(chestPanel.xMax - 83, top + 29, 65, 28), $"{chest.Occupied}/{Map01StorageChest.Capacity} ô", bagHint);
            GUI.Label(new Rect(bagPanel.x + 16, top + 15, 350, 50), "TÚI ĐỒ", bagTitle);
            HudFill(new Rect(chestPanel.x + 18, top + 73, chestWidth - 36, 1), HudGold);
            HudFill(new Rect(bagPanel.x + 16, top + 73, bagWidth - 32, 1), HudGold);
            GUI.Label(new Rect(chestPanel.x + 18, top + 83, chestWidth - 36, 25), "Kéo thả giữa rương và túi · Chuột phải để chuyển nhanh", bagHint);
            GUI.Label(new Rect(bagPanel.x + 16, top + 83, 345, 25), $"{Map01Inventory.BagSize - inventory.FreeCells}/{Map01Inventory.BagSize} ô", bagHint);

            int hotChest = -1, hotBag = -1;
            for (int i = 0; i < Map01StorageChest.Capacity; i++)
            {
                var rect = new Rect(chestPanel.x + 20 + i % Map01StorageChest.Columns * StorageStep,
                    top + 116 + i / Map01StorageChest.Columns * StorageStep, StorageCellSize, StorageCellSize);
                bool hot = rect.Contains(cursor);
                if (hot) hotChest = i;
                DrawStorageCell(rect, chest.CellAt(i), true, i, hot);
            }
            if (chest.OverflowCount > 0)
                GUI.Label(new Rect(chestPanel.x + 18, top + 644, chestWidth - 36, 30),
                    $"{chest.OverflowCount} chồng từ bản lưu cũ sẽ hiện khi có ô trống.", bagHint);

            const int bagStep = 43;
            for (int i = 0; i < Map01Inventory.BagSize; i++)
            {
                var rect = new Rect(bagPanel.x + 20 + i % Map01Inventory.BagColumns * bagStep,
                    top + 116 + i / Map01Inventory.BagColumns * bagStep, 39, 39);
                bool hot = rect.Contains(cursor);
                if (hot) hotBag = i;
                DrawStorageCell(rect, inventory.Cell(i), false, i, hot);
            }

            HandleStorageMouse(chest, hotChest, hotBag, ev);
            var hovered = hotChest >= 0 ? chest.CellAt(hotChest) : inventory.Cell(hotBag);
            var selected = storagePickInChest ? chest.CellAt(storagePick) : inventory.Cell(storagePick);
            var details = hovered ?? selected;
            if (details != null)
            {
                GUI.Label(new Rect(bagPanel.x + 17, top + 355, 348, 33), ForestInventory.Name(details.id), bagName);
                string count = Map01Inventory.HasCondition(details.id)
                    ? $"Độ bền {Percent(inventory.Fraction(details.id, details.condition))}"
                    : $"Số lượng {details.count}";
                GUI.Label(new Rect(bagPanel.x + 17, top + 391, 348, 26), count, bagLine);
                GUI.Label(new Rect(bagPanel.x + 17, top + 422, 348, 90), ForestInventory.Description(details.id), bagLine);
            }
            if (storageMessage != null)
                GUI.Label(new Rect(bagPanel.x + 17, bagPanel.yMax - 108, 348, 66), storageMessage, bagWarn);
            GUI.Label(new Rect(bagPanel.x + 17, bagPanel.yMax - 41, 348, 26), "E / Tab / Esc  Đóng rương", bagHint);

            if (storageDragging && storageSelection != null && (cursor - storagePress).sqrMagnitude >= 25)
                HudIcon(new Rect(cursor.x + 6, cursor.y + 6, 35, 35), storageSelection.id, showAmmoLabel: false);
        }

        private void DrawStorageCell(Rect rect, Cell cell, bool inChest, int index, bool hot)
        {
            HudFill(rect, new Color(.06f, .07f, .04f, .72f));
            HudBorder(rect, hot || (storagePickInChest == inChest && storagePick == index)
                ? HudGold : new Color(.3f, .3f, .2f, .8f), 1);
            if (cell == null) return;
            HudIcon(new Rect(rect.x + 3, rect.y + 2, rect.width - 6, rect.height - 4), cell.id, showAmmoLabel: false);
            if (Map01Inventory.HasCondition(cell.id))
                ConditionBar(new Rect(rect.x + 3, rect.yMax - 5, rect.width - 6, 3), inventory.Fraction(cell.id, cell.condition));
            else if (cell.count > 1)
                GUI.Label(new Rect(rect.x, rect.yMax - 16, rect.width - 2, 15), "×" + cell.count, storageCount);
        }

        private void HandleStorageMouse(Map01StorageChest chest, int hotChest, int hotBag, Event ev)
        {
            if (mission.Stopped)
            {
                storageDragging = false;
                return;
            }
            if (ev.type == EventType.MouseDown && (hotChest >= 0 || hotBag >= 0) && (ev.button == 0 || ev.button == 1))
            {
                storagePickInChest = hotChest >= 0;
                storagePick = storagePickInChest ? hotChest : hotBag;
                storageSelection = storagePickInChest ? chest.CellAt(storagePick) : inventory.Cell(storagePick);
                storageMessage = null;
                storagePress = ev.mousePosition;
                storageDragging = ev.button == 0 && storageSelection != null;
                if (ev.button == 1 && storageSelection != null) MoveStorageStack(chest, -1);
                ev.Use();
            }
            else if (ev.rawType == EventType.MouseUp && ev.button == 0 && storageDragging)
            {
                storageDragging = false;
                int target = storagePickInChest ? hotBag : hotChest;
                if (target >= 0) MoveStorageStack(chest, target);
                if (ev.type != EventType.Used) ev.Use();
            }
        }

        private void MoveStorageStack(Map01StorageChest chest, int target)
        {
            var current = storagePickInChest ? chest.CellAt(storagePick) : inventory.Cell(storagePick);
            if (current == null || current != storageSelection) return;
            if (Map01Inventory.IsMissionItem(current.id))
            {
                storageMessage = "Đồ nhiệm vụ cần giữ trong túi.";
                return;
            }
            int moved = inventory.TransferStorage(chest, storagePickInChest, storagePick, target);
            storageMessage = moved > 0 ? $"Đã chuyển {moved} × {ForestInventory.Name(current.id)}."
                : target >= 0 ? "Ô này không còn chỗ cho vật phẩm."
                : storagePickInChest ? "Túi không còn chỗ cho vật phẩm." : "Rương đã đầy.";
        }
    }
}
