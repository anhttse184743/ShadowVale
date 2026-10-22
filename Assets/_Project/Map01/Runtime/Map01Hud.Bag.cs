using UnityEngine;
using Slot = ShadowVale.Map01.Map01Inventory.Slot;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The bag screen (Tab), as in the approved "Trang bị" design: the equipment column on the left (1 long gun,
    /// 2 pistol, 3 knife, 4 throwable, and the VỨT BỎ bin), the 6 x 4 bag on the right with SẮP XẾP, and the picked
    /// thing's details under it.
    /// <list type="bullet">
    /// <item>Drag a stack onto an empty cell: it goes there; onto another item: they swap; onto the same item: they
    /// merge up to the stack size. A weapon dragged onto its slot is equipped (the old one takes its cell); out of a
    /// slot onto the bag it is put away. Anything dragged onto VỨT BỎ asks before going on the ground.</item>
    /// <item>Right click: a menu with the same actions, so nothing needs dragging. The detail plate has TRANG BỊ,
    /// TÁCH ĐÔI and VỨT buttons.</item>
    /// <item>While dragging, the cell under the cursor says what letting go would do.</item>
    /// </list>
    /// </summary>
    public sealed partial class Map01Hud
    {
        // What is picked (a cell, or a slot) and what is being dragged.
        private int bagPick = -1; private Slot? slotPick;
        private int dragCell = -1; private Slot? dragSlot; private Vector2 dragFrom; private bool dragging;
        // The right-click menu and the drop question.
        private bool menuOpen; private int menuCell = -1; private Slot? menuSlot; private Vector2 menuAt;
        private bool askDrop; private int askCell = -1; private Slot? askSlot; private int askAmount;
        private GUIStyle bagAccept, bagTitle, bagHint, bagSlotHead, bagName, bagLine, bagButton, bagCount, bagTag, bagWarn, bagGhost;
        private const float BagStep = 110, BagCellSize = 98;

        // ---- layout (HUD grid units; the two plates are anchored to the right edge) ------------------------------
        private static Rect EquipPlate(float width) => new Rect(width - 1175, 60, 404, 805);
        private static Rect BagPlate(float width) => new Rect(width - 748, 60, 705, 805);
        private static Rect BagCellRect(float width, int i) { var p = BagPlate(width); return new Rect(p.x + 25 + i % Map01Inventory.BagColumns * BagStep, p.y + 132 + i / Map01Inventory.BagColumns * BagStep, BagCellSize, BagCellSize); }
        private static Rect SlotCard(float width, Slot slot)
        {
            var p = EquipPlate(width);
            return slot == Slot.Primary ? new Rect(p.x + 18, p.y + 85, p.width - 36, 160)
                 : slot == Slot.Pistol ? new Rect(p.x + 18, p.y + 255, p.width - 36, 120)
                 : new Rect(p.x + 18, p.y + 385, p.width - 36, 100);
        }
        private static Rect ThrowCard(float width) { var p = EquipPlate(width); return new Rect(p.x + 18, p.y + 495, p.width - 36, 160); }
        private static Rect BinRect(float width) { var p = EquipPlate(width); return new Rect(p.x + 18, p.y + 668, p.width - 36, 117); }
        private static Rect SortButton(float width) { var p = BagPlate(width); return new Rect(p.xMax - 175, p.y + 24, 150, 40); }
        private static Rect DetailPlate(float width) { var p = BagPlate(width); return new Rect(p.x + 22, p.y + 578, p.width - 44, 150); }

        private void BagStyles()
        {
            if (bagTitle != null) return;
            bagTitle = new GUIStyle(hudHeading) { fontSize = 40, wordWrap = false };
            bagHint = new GUIStyle(hudSmall) { fontSize = 15, wordWrap = false, normal = { textColor = new Color(.7f, .67f, .52f) } };
            bagSlotHead = new GUIStyle(hudHeading) { fontSize = 20, wordWrap = false, normal = { textColor = HudGold } };
            bagName = new GUIStyle(hudHeading) { fontSize = 21, wordWrap = false };
            bagLine = new GUIStyle(hudSmall) { fontSize = 14, wordWrap = true };
            bagButton = new GUIStyle(hudHeading) { fontSize = 17, wordWrap = false, alignment = TextAnchor.MiddleCenter };
            bagCount = new GUIStyle(hudHeading) { fontSize = 17, wordWrap = false, alignment = TextAnchor.LowerRight };
            bagTag = new GUIStyle(hudHeading) { fontSize = 12, wordWrap = false, alignment = TextAnchor.UpperLeft, normal = { textColor = HudGold } };
            bagWarn = new GUIStyle(hudSmall) { fontSize = 15, wordWrap = true, alignment = TextAnchor.UpperCenter, normal = { textColor = new Color(.92f, .55f, .42f) } };
            bagAccept = new GUIStyle(hudSmall) { fontSize = 13, wordWrap = true, normal = { textColor = new Color(.7f, .67f, .52f) } };
            bagGhost = new GUIStyle(hudHeading) { fontSize = 14, wordWrap = false, alignment = TextAnchor.MiddleCenter, normal = { textColor = HudPaper } };
        }

        private int lastSelectedStack;
        private void DrawInventory(float width)
        {
            BagStyles();
            var ev = Event.current;
            var skillPanel = BagPlate(width);
            if (skillsTab)
            {
                DrawEquipColumn(width, ev, true);
                HudPanel(skillPanel);
                GUI.Label(new Rect(skillPanel.x + 25, 75, skillPanel.width - 50, 65), "KĨ NĂNG", hudTitle);
                DrawInventoryTabs(skillPanel);
                DrawSkills(skillPanel);
                return;
            }
            // The photo tests pick a stack by its place in InventoryStacks (selectedStack): the n-th non-empty cell.
            if (selectedStack != lastSelectedStack)
            {
                lastSelectedStack = selectedStack; bagPick = -1; slotPick = null;
                for (int i = 0, n = 0; i < Map01Inventory.BagSize; i++) if (inventory.Cell(i) != null && n++ == selectedStack) { bagPick = i; break; }
            }
            if (!mission.InventoryOpen) { dragCell = -1; dragSlot = null; dragging = false; menuOpen = false; askDrop = false; }
            if (!mission.InventoryOpen) askSalvage = false;
            // Modal first: the drop (or salvage) question takes every click until answered.
            bool modal = askDrop || askSalvage;

            DrawEquipColumn(width, ev, modal);
            DrawBagGrid(width, ev, modal);
            DrawBagDetail(width, ev, modal);
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && !modal && !dragging && !menuOpen;
            DrawInventoryTabs(skillPanel);
            GUI.enabled = previousGuiEnabled;
            if (menuOpen) DrawBagMenu(width, ev);
            if (askDrop) DrawDropQuestion(width, ev);
            if (askSalvage) DrawSalvageQuestion(width, ev);   // Map01Hud.Gunsmith.cs
            if (!modal) HandleDrag(width, ev);
            if (dragging) DrawDragGhost(width, ev);
            // Old field kept in step for the photo tests that pick a stack by index.
            if (bagPick >= 0) { inventory.SelectedItem = inventory.Cell(bagPick)?.id; }
            else if (slotPick != null) inventory.SelectedItem = inventory.SlotItem(slotPick.Value);
        }

        // ---- equipment column -----------------------------------------------------------------------------------
        private void DrawEquipColumn(float width, Event ev, bool modal)
        {
            var plate = EquipPlate(width); HudPanel(plate);
            GUI.Label(new Rect(plate.x + 22, plate.y + 18, 220, 50), "TRANG BỊ", bagTitle);
            GUI.Label(new Rect(plate.xMax - 178, plate.y + 34, 160, 24), "Kéo súng vào ô để đổi", bagHint);
            HudFill(new Rect(plate.x + 22, plate.y + 72, plate.width - 44, 1), HudGold);
            SlotCardDraw(width, Slot.Primary, "1", "SÚNG CHÍNH", "AK · M16 · Mosin", ev, modal);
            SlotCardDraw(width, Slot.Pistol, "2", "SÚNG NGẮN", "Nhận: súng lục · đạn 7,62×25", ev, modal);
            SlotCardDraw(width, Slot.Knife, "3", "DAO", null, ev, modal);
            DrawThrowCard(width, ev, modal);
            var bin = BinRect(width);
            bool binHot = dragging && bin.Contains(ev.mousePosition) && CanDrop(DragId());
            HudFill(bin, binHot ? new Color(.35f, .12f, .07f, .55f) : new Color(.12f, .05f, .03f, .35f));
            Dashed(bin, new Color(.85f, .42f, .3f, binHot ? 1f : .75f));
            GUI.Label(new Rect(bin.x, bin.y + 16, bin.width, 32), "VỨT BỎ", new GUIStyle(bagTitle) { fontSize = 24, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.92f, .55f, .42f) } });
            GUI.Label(new Rect(bin.x + 14, bin.y + 54, bin.width - 28, 60), "Kéo đồ vào đây để thả xuống đất. [E] ở chỗ đó để nhặt lại. Đồ nhiệm vụ không vứt được.", bagWarn);
        }

        private void SlotCardDraw(float width, Slot slot, string key, string title, string accepts, Event ev, bool modal)
        {
            var card = SlotCard(width, slot);
            string id = inventory.SlotItem(slot);
            if (id == Map01M16.Id) accepts = "5,56×45 · 600 viên/phút";
            string dragId = DragId();
            bool fits = dragging && dragId != null && Map01Inventory.SlotOf(dragId) == slot && slot != Slot.Knife;
            bool refuses = dragging && dragId != null && !fits && dragSlot != slot;
            var tint = GUI.color; if (refuses) GUI.color = new Color(1, 1, 1, .45f);
            HudPanel(card, slotPick == slot || fits && card.Contains(ev.mousePosition));
            var keyBox = new Rect(card.x + 14, card.y + 14, 32, 32);
            HudBorder(keyBox, HudGold, 2); GUI.Label(keyBox, key, bagButton);
            GUI.Label(new Rect(card.x + 58, card.y + 12, 260, 34), title, bagSlotHead);
            var pic = new Rect(card.x + 14, card.y + 52, 150, card.height - 62);
            HudFill(pic, new Color(.06f, .07f, .04f, .7f));
            if (id != null && !(dragging && dragSlot == slot))
            {
                HudIcon(new Rect(pic.x + 4, pic.y + 2, pic.width - 8, pic.height - 4), id);
                if (Map01Inventory.HasCondition(id)) GunConditionOverlay(pic, inventory.FractionOf(Map01Inventory.Gun.InSlot(slot)));
            }
            var text = new Rect(card.x + 178, card.y + 52, card.width - 190, card.height - 58);
            if (id != null)
            {
                GUI.Label(new Rect(text.x, text.y, text.width, 28), id == "rifle_mosin" ? "Mosin PU" : ForestInventory.Name(id), bagName);   // the full name is in the details
                string held = inventory.IsEquipped(id) ? "Đang cầm" : Map01Inventory.Drawable(id) ? "Phím " + key + " để cầm" : "Chưa có hoạt ảnh";
                if (slot == Slot.Knife) held += " · luôn mang theo";
                GUI.Label(new Rect(text.x, text.y + 28, text.width, 24), held + AmmoLine(id), bagLine);
            }
            else GUI.Label(new Rect(text.x, text.y, text.width, 28), "Trống", bagName);
            if (accepts != null) GUI.Label(new Rect(text.x, card.yMax - (slot == Slot.Primary ? 48 : 26), text.width, 44), accepts, bagAccept);
            GUI.color = tint;
            if (!modal && !menuOpen && ev.type == EventType.MouseDown && card.Contains(ev.mousePosition))
            {
                slotPick = slot; bagPick = -1;
                if (ev.button == 0 && id != null && slot != Slot.Knife) { dragSlot = slot; dragCell = -1; dragFrom = ev.mousePosition; }
                if (ev.button == 1 && id != null) OpenMenu(-1, slot, ev.mousePosition);
                ev.Use();
            }
        }

        private string AmmoLine(string id)
        {
            switch (id)
            {
                case "rifle_m16": return " · " + (mission.ModernCombat != null ? mission.ModernCombat.RoundsInMagazine : 0) + "/" + inventory.Count(Map01M16.Ammo) + " viên";
                case "rifle_standard": return " · " + (mission.ModernCombat != null ? mission.ModernCombat.RoundsInMagazine : 0) + "/" + inventory.Count(id == Map01M16.Id ? Map01M16.Ammo : "ammo_rifle") + " viên";
                case "pistol_k54": return " · " + inventory.Count("ammo_pistol") + " viên";
                case "rifle_mosin": return " · " + inventory.Count("ammo_sniper") + " viên";
                default: return "";
            }
        }

        private void DrawThrowCard(float width, Event ev, bool modal)
        {
            var card = ThrowCard(width);
            string dragId = DragId();
            bool fits = dragging && dragId != null && System.Array.IndexOf(Map01Inventory.Throwables, dragId) >= 0;
            var tint = GUI.color; if (dragging && !fits) GUI.color = new Color(1, 1, 1, .45f);
            HudPanel(card, fits && card.Contains(ev.mousePosition));
            var keyBox = new Rect(card.x + 14, card.y + 14, 32, 32);
            HudBorder(keyBox, HudGold, 2); GUI.Label(keyBox, "4", bagButton);
            GUI.Label(new Rect(card.x + 58, card.y + 12, 260, 34), "ĐỒ NÉM", bagSlotHead);
            string sel = inventory.Throwable;
            var pic = new Rect(card.x + 14, card.y + 56, 150, 66);
            HudFill(pic, new Color(.06f, .07f, .04f, .7f));
            HudIcon(new Rect(pic.x + 42, pic.y + 2, 66, 62), sel, inventory.Count(sel) <= 0);
            GUI.Label(new Rect(card.x + 178, card.y + 52, card.width - 190, 28), ForestInventory.Name(sel) + " ×" + inventory.Count(sel), bagName);
            // The three kinds, picked with a click (or key 4 in play).
            for (int k = 0; k < Map01Inventory.Throwables.Length; k++)
            {
                string t = Map01Inventory.Throwables[k];
                var box = new Rect(card.x + 176 + k * 60, card.y + 86, 54, 48);
                HudFill(box, new Color(.06f, .07f, .04f, .75f));
                if (t == sel) HudBorder(box, HudGold, 2);
                HudIcon(new Rect(box.x + 5, box.y + 2, 44, 44), t, inventory.Count(t) <= 0);
                GUI.Label(new Rect(box.x, box.yMax - 20, box.width - 4, 18), inventory.Count(t).ToString(), bagCount);
                if (!modal && !menuOpen && ev.type == EventType.MouseDown && ev.button == 0 && box.Contains(ev.mousePosition))
                {
                    if (inventory.Count(t) > 0) inventory.SelectThrowable(t); else mission.Say("Chưa có " + ForestInventory.Name(t).ToLowerInvariant() + ".", 2);
                    ev.Use();
                }
            }
            GUI.Label(new Rect(card.x + 14, card.yMax - 30, card.width - 28, 22), "4 chọn/đổi đồ ném · Giữ/thả chuột trái ném", bagHint);
            GUI.color = tint;
        }

        // ---- the bag ----------------------------------------------------------------------------------------------
        private void DrawBagGrid(float width, Event ev, bool modal)
        {
            var plate = BagPlate(width); HudPanel(plate);
            GUI.Label(new Rect(plate.x + 25, plate.y + 18, 200, 50), "TÚI ĐỒ", bagTitle);
            int used = Map01Inventory.BagSize - inventory.FreeCells;
            GUI.Label(new Rect(plate.x + 205, plate.y + 34, 300, 24), $"{used}/{Map01Inventory.BagSize} ô · kéo để sắp xếp", bagHint);
            var sort = SortButton(width);
            HudPanel(sort);
            GUI.Label(sort, "SẮP XẾP", bagButton);
            if (!modal && !menuOpen && ev.type == EventType.MouseDown && ev.button == 0 && sort.Contains(ev.mousePosition))
            {
                inventory.Sort(); bagPick = -1; slotPick = null; mission.Say("Đã sắp xếp theo loại và gộp chồng.", 2.5f); ev.Use();
            }
            HudFill(new Rect(plate.x + 25, plate.y + 76, plate.width - 50, 1), HudGold);
            GUI.Label(new Rect(plate.x + 25, plate.y + 88, plate.width - 50, 24), "Thả lên ô trống: đặt vào · lên món khác: đổi chỗ · lên cùng loại: gộp chồng · chuột phải: menu", bagHint);
            for (int i = 0; i < Map01Inventory.BagSize; i++)
            {
                var r = BagCellRect(width, i);
                var cell = inventory.Cell(i);
                bool beingDragged = dragging && dragCell == i;
                HudFill(r, new Color(.06f, .07f, .04f, .72f));
                HudBorder(r, new Color(.3f, .3f, .2f, .8f), 1);
                if (cell != null && !beingDragged)
                {
                    HudIcon(new Rect(r.x + 6, r.y + 6, r.width - 12, r.height - 12), cell.id);
                    int limit = inventory.StackLimit(cell.id);
                    bool full = limit > 1 && cell.count >= limit;
                    // A gun shows its condition where a stack shows its count.
                    if (Map01Inventory.HasCondition(cell.id)) GunConditionOverlay(r, inventory.FractionOf(Map01Inventory.Gun.InCell(i)));
                    else HudNumber(new Rect(r.xMax - (full ? 86 : 54), r.yMax - 27, full ? 82 : 50, 23), "×" + cell.count + (full ? " ĐẦY" : ""));
                    if (Map01Inventory.IsMissionItem(cell.id)) GUI.Label(new Rect(r.x + 5, r.y + 3, r.width, 16), "NHIỆM VỤ", bagTag);
                    else if (cell.id == inventory.Throwable) GUI.Label(new Rect(r.x + 5, r.y + 3, r.width, 16), "Ô 4", bagTag);
                }
                if (i == bagPick) HudBorder(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), HudGold, 3);
                if (dragging && r.Contains(ev.mousePosition))
                {
                    string act = DropActionOnCell(i);
                    if (act != null)
                    {
                        HudBorder(r, HudPaper, 2);
                        HudFill(new Rect(r.x, r.y + r.height / 2 - 12, r.width, 24), new Color(.02f, .03f, .02f, .8f));
                        GUI.Label(new Rect(r.x, r.y + r.height / 2 - 12, r.width, 24), act, bagGhost);
                    }
                }
                if (!modal && !menuOpen && ev.type == EventType.MouseDown && r.Contains(ev.mousePosition))
                {
                    bagPick = cell != null ? i : -1; slotPick = null;
                    if (ev.button == 0 && cell != null) { dragCell = i; dragSlot = null; dragFrom = ev.mousePosition; }
                    if (ev.button == 1 && cell != null) OpenMenu(i, null, ev.mousePosition);
                    ev.Use();
                }
            }
        }

        private void DrawBagDetail(float width, Event ev, bool modal)
        {
            var d = DetailPlate(width); HudPanel(d);
            string id = bagPick >= 0 ? inventory.Cell(bagPick)?.id : slotPick != null ? inventory.SlotItem(slotPick.Value) : null;
            var plate = BagPlate(width);
            GUI.Label(new Rect(plate.x + 25, plate.yMax - 72, plate.width - 50, 44),
                "Kéo thả: sắp xếp, trang bị, vứt · Chuột phải: menu · Tab / Esc Đóng\n1 Súng chính · 2 Súng ngắn · 3 Dao · 4 Đồ ném · Giữ/thả chuột trái: ném · H Hồi máu", bagHint);
            if (id == null) { GUI.Label(new Rect(d.x + 24, d.y + 50, d.width - 48, 50), "Chọn một món để xem chi tiết. Kéo món vào ô khác để sắp xếp.", hudSmall); return; }
            HudIcon(new Rect(d.x + 14, d.y + 14, 122, 122), id);
            GUI.Label(new Rect(d.x + 150, d.y + 10, d.width - 165, 34), ForestInventory.Name(id).ToUpperInvariant(), hudHeading);
            GUI.Label(new Rect(d.x + 150, d.y + 42, d.width - 165, 44), ForestInventory.Description(id), bagLine);
            var cell = bagPick >= 0 ? inventory.Cell(bagPick) : null;
            string stack = cell != null ? $"Chồng {cell.count}/{inventory.StackLimit(id)} · tổng {inventory.Count(id)}" : "Trong ô trang bị";
            Map01Inventory.Gun? gun = !Map01Inventory.HasCondition(id) ? (Map01Inventory.Gun?)null
                : cell != null ? Map01Inventory.Gun.InCell(bagPick) : slotPick != null ? Map01Inventory.Gun.InSlot(slotPick.Value) : (Map01Inventory.Gun?)null;
            if (gun != null)
            {
                float f = inventory.FractionOf(gun.Value), wear = inventory.WearPerShot(id), left = f * inventory.MaxCondition(id);
                stack = $"Độ bền {(f <= 0 ? "HỎNG" : Percent(f))}" + (wear > 0 && f > 0 ? $" · còn khoảng {Mathf.CeilToInt(left / wear - .001f)} phát" : "") + (cell != null ? "" : " · trong ô trang bị");
                GunConditionOverlay(new Rect(d.x + 14, d.y + 14, 122, 122), f);
            }
            GUI.Label(new Rect(d.x + 150, d.y + 86, d.width - 165, 20), stack, bagHint);
            float bx = d.x + 150;
            if (cell != null && Map01Inventory.SlotOf(id) is Slot s && s != Slot.Knife)
                bx = DetailButton(new Rect(bx, d.yMax - 44, 150, 34), "TRANG BỊ", () => { inventory.EquipFromCell(bagPick); bagPick = -1; slotPick = s; }, ev, modal);
            if (cell != null && System.Array.IndexOf(Map01Inventory.Throwables, id) >= 0)
                bx = DetailButton(new Rect(bx, d.yMax - 44, 150, 34), id == inventory.Throwable ? "ĐANG CHỌN" : "CHỌN NÉM", () => inventory.SelectThrowable(id), ev, modal);
            if (cell != null && cell.count >= 2)
                bx = DetailButton(new Rect(bx, d.yMax - 44, 130, 34), "TÁCH ĐÔI", () => inventory.Split(bagPick), ev, modal);
            if (slotPick != null && slotPick != Slot.Knife && id != null)
                bx = DetailButton(new Rect(bx, d.yMax - 44, 150, 34), "CẤT VÀO TÚI", () => { inventory.Unequip(slotPick.Value); slotPick = null; }, ev, modal);
            if (gun != null)
            {
                var g = gun.Value;
                bx = DetailButton(new Rect(bx, d.yMax - 44, 90, 34), "SỬA", () => inventory.FieldRepair(g), ev, modal);
                bx = DetailButton(new Rect(bx, d.yMax - 44, 70, 34), "RÃ", () => AskSalvage(g), ev, modal);
            }
            if (CanDrop(id) && !(slotPick == Slot.Knife))
                DetailButton(new Rect(bx, d.yMax - 44, 110, 34), "VỨT", () => AskDrop(bagPick, bagPick >= 0 ? null : slotPick), ev, modal, true);
        }

        private float DetailButton(Rect r, string label, System.Action act, Event ev, bool modal, bool warn = false)
        {
            HudFill(r, warn ? new Color(.2f, .06f, .04f, .85f) : new Color(.04f, .05f, .03f, .85f));
            HudBorder(r, warn ? new Color(.85f, .42f, .3f) : HudGold, 2);
            GUI.Label(r, label, warn ? new GUIStyle(bagButton) { normal = { textColor = new Color(.95f, .6f, .48f) } } : bagButton);
            if (!modal && !menuOpen && ev.type == EventType.MouseDown && ev.button == 0 && r.Contains(ev.mousePosition)) { act(); ev.Use(); }
            return r.xMax + 12;
        }

        // ---- dragging -----------------------------------------------------------------------------------------------
        private string DragId() => dragCell >= 0 ? inventory.Cell(dragCell)?.id : dragSlot != null ? inventory.SlotItem(dragSlot.Value) : null;
        private static bool CanDrop(string id) => id != null && id != "knife" && !Map01Inventory.IsMissionItem(id);

        /// <summary>What letting the dragged thing go over cell <paramref name="i"/> would do, as the cell shows it.</summary>
        private string DropActionOnCell(int i)
        {
            var target = inventory.Cell(i);
            if (dragCell >= 0)
            {
                if (dragCell == i) return null;
                var from = inventory.Cell(dragCell);
                if (from == null) return null;
                if (target == null) return "Đặt vào";
                return target.id == from.id && target.count < inventory.StackLimit(from.id) ? "Gộp" : "Đổi chỗ";
            }
            if (dragSlot != null)
            {
                if (target == null) return "Cất vào";
                return Map01Inventory.SlotOf(target.id) == dragSlot ? "Đổi súng" : null;
            }
            return null;
        }

        private void HandleDrag(float width, Event ev)
        {
            if (dragCell < 0 && dragSlot == null) return;
            if (ev.type == EventType.MouseDrag && !dragging && Vector2.Distance(ev.mousePosition, dragFrom) > 6f) { dragging = true; menuOpen = false; ev.Use(); }
            if (ev.rawType != EventType.MouseUp) return;
            if (dragging)
            {
                Vector2 m = ev.mousePosition;
                string id = DragId();
                bool done = false;
                for (int i = 0; i < Map01Inventory.BagSize && !done; i++)
                {
                    if (!BagCellRect(width, i).Contains(m)) continue;
                    if (DropActionOnCell(i) == null) break;
                    if (dragCell >= 0) inventory.Move(dragCell, i); else inventory.Unequip(dragSlot.Value, i);
                    bagPick = inventory.Cell(i) != null ? i : -1; slotPick = null; done = true;
                }
                foreach (Slot s in new[] { Slot.Primary, Slot.Pistol, Slot.Knife })
                {
                    if (done || !SlotCard(width, s).Contains(m)) continue;
                    if (dragCell >= 0 && Map01Inventory.SlotOf(id) == s && s != Slot.Knife) { inventory.EquipFromCell(dragCell); slotPick = s; bagPick = -1; }
                    else if (dragCell >= 0 || dragSlot != s) mission.Say(ForestInventory.Name(id) + " không vào được ô này.", 2.5f);
                    done = true;
                }
                if (!done && ThrowCard(width).Contains(m))
                {
                    if (System.Array.IndexOf(Map01Inventory.Throwables, id) >= 0) inventory.SelectThrowable(id);
                    else mission.Say(ForestInventory.Name(id) + " không phải đồ ném.", 2.5f);
                    done = true;
                }
                if (!done && BinRect(width).Contains(m)) { if (CanDrop(id)) AskDrop(dragCell, dragCell >= 0 ? null : dragSlot); else mission.Say(id == "knife" ? "Dao luôn mang theo, không vứt được." : "Đồ nhiệm vụ không vứt được.", 3); }
                ev.Use();
            }
            dragging = false; dragCell = -1; dragSlot = null;
        }

        private void DrawDragGhost(float width, Event ev)
        {
            string id = DragId();
            if (id == null) return;
            var at = ev.mousePosition;
            var r = new Rect(at.x - 44, at.y - 44, 88, 88);
            var old = GUI.color; GUI.color = new Color(1, 1, 1, .85f);
            HudFill(r, new Color(.05f, .06f, .04f, .6f));
            HudIcon(new Rect(r.x + 6, r.y + 6, 76, 76), id);
            GUI.color = old;
            string label = BinRect(width).Contains(at) ? (CanDrop(id) ? "Vứt bỏ" : "Không vứt được") : null;
            foreach (Slot s in new[] { Slot.Primary, Slot.Pistol })
                if (SlotCard(width, s).Contains(at)) label = Map01Inventory.SlotOf(id) == s ? "Trang bị" : "Sai loại";
            if (ThrowCard(width).Contains(at)) label = System.Array.IndexOf(Map01Inventory.Throwables, id) >= 0 ? "Chọn ném" : "Sai loại";
            if (label != null)
            {
                var tag = new Rect(at.x - 60, r.yMax + 2, 120, 22);
                HudFill(tag, new Color(.02f, .03f, .02f, .85f)); GUI.Label(tag, label, bagGhost);
            }
        }

        // ---- right-click menu --------------------------------------------------------------------------------------
        private void OpenMenu(int cell, Slot? slot, Vector2 at) { menuOpen = true; menuCell = cell; menuSlot = slot; menuAt = at; }

        private void DrawBagMenu(float width, Event ev)
        {
            string id = menuCell >= 0 ? inventory.Cell(menuCell)?.id : menuSlot != null ? inventory.SlotItem(menuSlot.Value) : null;
            if (id == null) { menuOpen = false; return; }
            var items = new System.Collections.Generic.List<(string label, System.Action act)>();
            if (menuCell >= 0 && Map01Inventory.SlotOf(id) is Slot s && s != Slot.Knife) items.Add(("Trang bị vào ô " + ((int)s + 1), () => inventory.EquipFromCell(menuCell)));
            if (System.Array.IndexOf(Map01Inventory.Throwables, id) >= 0) items.Add(("Chọn làm đồ ném", () => inventory.SelectThrowable(id)));
            if (menuCell >= 0 && inventory.Cell(menuCell).count >= 2) items.Add(("Tách đôi chồng", () => inventory.Split(menuCell)));
            if (menuSlot != null && menuSlot != Slot.Knife) items.Add(("Cất vào túi", () => inventory.Unequip(menuSlot.Value)));
            if (Map01Inventory.HasCondition(id))
            {
                var gun = menuCell >= 0 ? Map01Inventory.Gun.InCell(menuCell) : Map01Inventory.Gun.InSlot(menuSlot.Value);
                items.Add(("Sửa dã chiến (bộ sửa chữa)", () => inventory.FieldRepair(gun)));
                items.Add(("Rã lấy linh kiện", () => AskSalvage(gun)));
            }
            if (CanDrop(id)) items.Add(("Vứt xuống đất", () => AskDrop(menuCell, menuCell >= 0 ? null : menuSlot)));
            if (items.Count == 0) items.Add(("Không có thao tác", () => { }));
            var box = new Rect(menuAt.x + 6, menuAt.y + 6, 240, 18 + items.Count * 38);
            HudFill(box, new Color(.04f, .05f, .03f, .96f)); HudBorder(box, HudGold, 2);
            for (int k = 0; k < items.Count; k++)
            {
                var row = new Rect(box.x + 8, box.y + 9 + k * 38, box.width - 16, 34);
                if (row.Contains(ev.mousePosition)) HudFill(row, new Color(.2f, .17f, .08f, .9f));
                GUI.Label(new Rect(row.x + 10, row.y, row.width - 10, row.height), items[k].label, new GUIStyle(bagLine) { fontSize = 17, alignment = TextAnchor.MiddleLeft });
                if (ev.type == EventType.MouseDown && row.Contains(ev.mousePosition)) { var act = items[k].act; menuOpen = false; act(); ev.Use(); return; }
            }
            if (ev.type == EventType.MouseDown && !box.Contains(ev.mousePosition)) { menuOpen = false; ev.Use(); }
        }

        // ---- the drop question ----------------------------------------------------------------------------------------
        private void AskDrop(int cell, Slot? slot)
        {
            string id = cell >= 0 ? inventory.Cell(cell)?.id : slot != null ? inventory.SlotItem(slot.Value) : null;
            if (!CanDrop(id)) return;
            askDrop = true; askCell = cell; askSlot = cell >= 0 ? null : slot;
            askAmount = cell >= 0 ? inventory.Cell(cell).count : 1;
        }

        private void DrawDropQuestion(float width, Event ev)
        {
            string id = askCell >= 0 ? inventory.Cell(askCell)?.id : askSlot != null ? inventory.SlotItem(askSlot.Value) : null;
            if (id == null) { askDrop = false; return; }
            int have = askCell >= 0 ? inventory.Cell(askCell).count : 1;
            askAmount = Mathf.Clamp(askAmount, 1, have);
            var screen = new Rect(EquipPlate(width).x, 60, BagPlate(width).xMax - EquipPlate(width).x, 805);
            HudFill(screen, new Color(0, 0, 0, .5f));
            var box = new Rect(screen.center.x - 260, 330, 520, have > 1 ? 250 : 200);
            HudPanel(box);
            HudIcon(new Rect(box.x + 24, box.y + 30, 90, 90), id);
            GUI.Label(new Rect(box.x + 130, box.y + 26, box.width - 150, 34), "VỨT XUỐNG ĐẤT?", hudHeading);
            GUI.Label(new Rect(box.x + 130, box.y + 62, box.width - 150, 56), ForestInventory.Name(id) + (have > 1 ? "" : (askSlot != null ? " (đang ở ô " + ((int)askSlot.Value + 1) + ")" : "")) +
                ". Nằm dưới chân Nam, [E] để nhặt lại.", bagLine);
            if (have > 1)
            {
                var minus = new Rect(box.x + 130, box.y + 126, 44, 40); var plus = new Rect(box.x + 260, box.y + 126, 44, 40); var all = new Rect(box.x + 316, box.y + 126, 90, 40);
                HudPanel(minus); GUI.Label(minus, "−", bagButton); HudPanel(plus); GUI.Label(plus, "+", bagButton); HudPanel(all); GUI.Label(all, "TẤT CẢ", bagButton);
                GUI.Label(new Rect(minus.xMax, minus.y, plus.x - minus.xMax, 40), askAmount + " / " + have, bagButton);
                if (ev.type == EventType.MouseDown && minus.Contains(ev.mousePosition)) { askAmount--; ev.Use(); }
                if (ev.type == EventType.MouseDown && plus.Contains(ev.mousePosition)) { askAmount++; ev.Use(); }
                if (ev.type == EventType.MouseDown && all.Contains(ev.mousePosition)) { askAmount = have; ev.Use(); }
            }
            var yes = new Rect(box.x + 130, box.yMax - 56, 150, 38); var no = new Rect(box.x + 296, box.yMax - 56, 150, 38);
            HudFill(yes, new Color(.2f, .06f, .04f, .9f)); HudBorder(yes, new Color(.85f, .42f, .3f), 2); GUI.Label(yes, "VỨT", new GUIStyle(bagButton) { normal = { textColor = new Color(.95f, .6f, .48f) } });
            HudPanel(no); GUI.Label(no, "HỦY", bagButton);
            if (ev.type == EventType.MouseDown && yes.Contains(ev.mousePosition))
            {
                bool ok = askCell >= 0 ? inventory.Drop(askCell, askAmount) : inventory.DropSlot(askSlot.Value);
                if (ok) mission.Say("Đã vứt " + (askCell >= 0 ? askAmount + " " : "") + ForestInventory.Name(id).ToLowerInvariant() + " xuống đất — [E] để nhặt lại.", 3);
                askDrop = false; bagPick = -1; slotPick = null; ev.Use();
            }
            else if (ev.type == EventType.MouseDown && no.Contains(ev.mousePosition)) { askDrop = false; ev.Use(); }
            else if (ev.type == EventType.MouseDown) ev.Use();            // nothing behind the question takes the click
        }

        private static void Dashed(Rect r, Color c)
        {
            for (float x = r.x; x < r.xMax; x += 14) { HudFill(new Rect(x, r.y, Mathf.Min(8, r.xMax - x), 2), c); HudFill(new Rect(x, r.yMax - 2, Mathf.Min(8, r.xMax - x), 2), c); }
            for (float y = r.y; y < r.yMax; y += 14) { HudFill(new Rect(r.x, y, 2, Mathf.Min(8, r.yMax - y)), c); HudFill(new Rect(r.xMax - 2, y, 2, Mathf.Min(8, r.yMax - y)), c); }
        }
    }
}
