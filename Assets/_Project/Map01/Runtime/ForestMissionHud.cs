using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public sealed partial class ForestMission
    {
        private Texture2D hudItems, hudFrame, hudPlate;
        private GUIStyle hudTitle, hudHeading, hudBody, hudSmall, hudCount, hudKey, hudCenter;
        private float hudScroll;
        private int selectedStack;
        private string dragItem;
        private bool dragging;
        private static readonly Color HudPaper = new Color(.9f, .85f, .68f);
        private static readonly Color HudGold = new Color(.87f, .68f, .32f);
        private static readonly Rect HudPlateUv = new Rect(0, 0, 1, 1);

        private void HudStyles()
        {
            if (hudBody != null) return;
            hudItems = Resources.Load<Texture2D>("Hud/Items");
            hudFrame = Resources.Load<Texture2D>("Hud/HealthFrame");
            hudPlate = Resources.Load<Texture2D>("Hud/Panel");
            var bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var stencil = Resources.Load<Font>("Menu/Fonts/BlackOpsOne-Regular") ?? bodyFont;
            hudBody = new GUIStyle(GUI.skin.label) { font = bodyFont, fontSize = 22, wordWrap = true, normal = { textColor = HudPaper } };
            hudSmall = new GUIStyle(hudBody) { fontSize = 18 };
            hudHeading = new GUIStyle(hudBody) { font = stencil, fontSize = 25 };
            hudTitle = new GUIStyle(hudHeading) { fontSize = 46, alignment = TextAnchor.MiddleCenter };
            hudCount = new GUIStyle(hudHeading) { fontSize = 22, alignment = TextAnchor.LowerRight };
            hudKey = new GUIStyle(hudHeading) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            hudCenter = new GUIStyle(hudHeading) { alignment = TextAnchor.MiddleCenter };
            // Retain the existing map overlay using the same fonts.
            titleStyle = hudHeading; textStyle = hudBody; smallStyle = hudSmall;
        }
        private static void HudFill(Rect rect, Color color)
        {
            var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        private static void HudBorder(Rect r, Color color, float line = 2)
        {
            HudFill(new Rect(r.x, r.y, r.width, line), color); HudFill(new Rect(r.x, r.yMax - line, r.width, line), color);
            HudFill(new Rect(r.x, r.y, line, r.height), color); HudFill(new Rect(r.xMax - line, r.y, line, r.height), color);
        }
        private void HudPanel(Rect r, bool selected = false)
        {
            if (hudPlate != null) {
                float edge = Mathf.Min(19, r.height * .13f);
                const float uEdge = .055f, vEdge = .055f;
                for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) {
                    var target = new Rect(x == 0 ? r.x : x == 1 ? r.x + edge : r.xMax - edge,
                        y == 0 ? r.y : y == 1 ? r.y + edge : r.yMax - edge,
                        x == 1 ? r.width - edge * 2 : edge, y == 1 ? r.height - edge * 2 : edge);
                    var uv = new Rect(x == 0 ? HudPlateUv.x : x == 1 ? HudPlateUv.x + uEdge : HudPlateUv.xMax - uEdge,
                        y == 0 ? HudPlateUv.yMax - vEdge : y == 1 ? HudPlateUv.y + vEdge : HudPlateUv.y,
                        x == 1 ? HudPlateUv.width - uEdge * 2 : uEdge, y == 1 ? HudPlateUv.height - vEdge * 2 : vEdge);
                    if (x == 1 && y == 1) {
                        // Tile a quiet central patch at consistent scale instead of stretching fabric grain.
                        for (float ty = 0; ty < target.height; ty += 256)
                            for (float tx = 0; tx < target.width; tx += 256) {
                                float tw = Mathf.Min(256, target.width - tx), th = Mathf.Min(256, target.height - ty);
                                GUI.DrawTextureWithTexCoords(new Rect(target.x + tx, target.y + ty, tw, th), hudPlate,
                                    new Rect(.2f, .2f, .32f * tw / 256, .32f * th / 256));
                            }
                    } else GUI.DrawTextureWithTexCoords(target, hudPlate, uv, true);
                }
                HudFill(new Rect(r.x + 9, r.y + 9, r.width - 18, r.height - 18), new Color(.035f, .045f, .025f, .32f));
            } else { HudFill(r, new Color(.08f, .10f, .065f, .97f)); HudBorder(r, HudGold); }
            if (selected) HudBorder(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), HudGold, 3);
        }
        private void HudIcon(Rect r, string id, bool dim = false)
        {
            int index = ForestInventory.IconIndex(id);
            if (hudItems == null || index < 0) { GUI.Label(r, "?", hudCenter); return; }
            // UVs select only artwork, excluding baked captions from the approved concept sheet.
            float x = 23 + index % 7 * 218.3f, y = index < 7 ? 179 : 614;
            var uv = new Rect(x / 1536f, 1 - (y + 218) / 1024f, 190f / 1536f, 218f / 1024f);
            var old = GUI.color; if (dim) GUI.color = new Color(.55f, .55f, .55f, .75f);
            GUI.DrawTextureWithTexCoords(r, hudItems, uv); GUI.color = old;
        }
        private void HudNumber(Rect r, string text)
        {
            HudFill(r, new Color(.025f, .03f, .02f, .8f)); GUI.Label(r, text, hudCount);
        }
        private float HudScale => Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        private Rect InventoryRect(float width) => new Rect(width - 748, 52, 716, 816);
        private Rect QuickRect(float width, float height) => new Rect(inventoryOpen ? Mathf.Max(25, (width - 748 - 540) / 2) : (width - 540) / 2, height - 153, 540, 124);
        private bool HudPointerBlocked()
        {
            if (Cursor.lockState == CursorLockMode.Locked || Mouse.current == null) return false;
            float scale = HudScale;
            var p = Mouse.current.position.ReadValue(); p = new Vector2(p.x / scale, (Screen.height - p.y) / scale);
            return QuickRect(Screen.width / scale, Screen.height / scale).Contains(p);
        }
        private void CancelHudDrag() { dragItem = null; dragging = false; }

        private void DrawHud()
        {
            if (!IsInitialized || ForestMenu.Visible) { CancelHudDrag(); return; }
            HudStyles();
            var matrix = GUI.matrix; var color = GUI.color; int depth = GUI.depth;
            float scale = HudScale, width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1)); GUI.color = Color.white; GUI.depth = -10;
            DrawHealth();
            DrawObjective();
            if (!mapOpen) {
                DrawQuickBar(width, height);
                if (inventoryOpen) DrawInventory(width);
            }
            DrawHudFeedback(width, height);
            if (mapOpen) DrawMap(width, height);
            if (hp <= 0 || stage == 4) {
                HudPanel(new Rect(width / 2 - 300, 320, 600, 185));
                GUI.Label(new Rect(width / 2 - 275, 345, 550, 75), hp <= 0 ? "NAM ĐÃ GỤC NGÃ" : "HOÀN THÀNH MAP 1", hudCenter);
                GUI.Label(new Rect(width / 2 - 245, 437, 490, 50), "Enter Chơi lại  ·  F9 Tải bản lưu  ·  Esc Menu", hudSmall);
            }
            FinishHudDrag(Event.current, width, height);
            GUI.matrix = matrix; GUI.color = color; GUI.depth = depth;
        }
        private void DrawHealth()
        {
            // Empty alpha aperture of the generated frame is filled live; text is never baked into art.
            var bar = new Rect(153, 58, 389, 27);
            HudFill(bar, new Color(.065f, .04f, .03f, .98f));
            var fill = new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(hp / Settings.playerHP), bar.height);
            HudFill(fill, new Color(.57f, .035f, .025f));
            HudFill(new Rect(fill.x, fill.y + 2, fill.width, 3), new Color(.9f, .19f, .10f, .4f));
            if (hudFrame != null) GUI.DrawTexture(new Rect(18, -18, 575, 192), hudFrame);
            else HudBorder(bar, HudGold);
            GUI.Label(new Rect(230, 100, 66, 25), "NAM", hudKey);
            GUI.Label(new Rect(308, 100, 138, 25), $"{hp:0} / {Settings.playerHP:0} HP", hudKey);
            HudPanel(new Rect(141, 136, 390, 38));
            GUI.Label(new Rect(158, 144, 355, 27), $"Sức bền {stamina:0}  ·  Đạn {Count("ammo_rifle")}  ·  {(Hidden ? "Ẩn nấp" : crouched ? "Đi khom" : "Sẵn sàng")}", hudSmall);
        }
        private void DrawObjective()
        {
            HudPanel(new Rect(28, 186, 373, 152));
            GUI.Label(new Rect(48, 205, 332, 37), "NHIỆM VỤ", hudHeading);
            HudFill(new Rect(49, 246, 328, 1), new Color(.6f, .51f, .3f, .65f));
            GUI.Label(new Rect(49, 258, 328, 69), Objectives[Mathf.Clamp(stage, 0, 4)], hudBody);
        }
        private void DrawInventory(float width)
        {
            var panel = InventoryRect(width); HudPanel(panel);
            GUI.Label(new Rect(panel.x + 25, 75, panel.width - 50, 65), "TÚI ĐỒ", hudTitle);
            GUI.Label(new Rect(panel.x + 230, 144, 256, 42), "VẬT TƯ", hudCenter);
            HudFill(new Rect(panel.x + 75, 164, 150, 1), HudGold); HudFill(new Rect(panel.xMax - 225, 164, 150, 1), HudGold);
            var stacks = InventoryStacks();
            if (stacks.Count == 0) { selectedItem = null; selectedStack = 0; }
            else {
                selectedStack = Mathf.Clamp(selectedStack, 0, stacks.Count - 1);
                selectedItem = stacks[selectedStack].Id;
            }
            const float step = 111, cell = 104;
            var viewport = new Rect(panel.x + 19, 195, 666, 437);
            float contentHeight = Mathf.Max(viewport.height, Mathf.Ceil(stacks.Count / 6f) * step - 7);
            float maxScroll = contentHeight - viewport.height;
            hudScroll = Mathf.Clamp(hudScroll, 0, maxScroll);
            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && viewport.Contains(ev.mousePosition)) {
                hudScroll = Mathf.Clamp(hudScroll + ev.delta.y * 35, 0, maxScroll); ev.Use();
            }
            var scrollTint = GUI.color; GUI.color = new Color(.72f, .65f, .42f);
            hudScroll = GUI.VerticalScrollbar(new Rect(panel.xMax - 26, 196, 14, 435), hudScroll, viewport.height, 0, contentHeight);
            GUI.color = scrollTint;
            GUI.BeginGroup(viewport);
            int total = Mathf.Max(24, Mathf.CeilToInt(stacks.Count / 6f) * 6);
            for (int i = 0; i < total; i++) {
                var rect = new Rect(i % 6 * step, i / 6 * step - hudScroll, cell, cell);
                if (rect.yMax < 0 || rect.y > viewport.height) continue;
                bool occupied = i < stacks.Count;
                bool chosen = occupied && i == selectedStack;
                HudPanel(rect, chosen);
                if (!occupied) continue;
                var item = stacks[i];
                HudIcon(new Rect(rect.x + 8, rect.y + 8, cell - 16, cell - 16), item.Id);
                if (chosen) HudBorder(new Rect(rect.x + 3, rect.y + 3, cell - 6, cell - 6), HudGold, 3);
                HudNumber(new Rect(rect.xMax - 59, rect.yMax - 32, 51, 25), "x" + item.Count);
                if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition)) {
                    selectedStack = i; selectedItem = item.Id;
                    if (ForestInventory.QuickUsable(item.Id)) dragItem = item.Id;
                    ev.Use();
                }
            }
            GUI.EndGroup();
            var detail = new Rect(panel.x + 19, 648, panel.width - 38, 155); HudPanel(detail);
            if (selectedItem != null) {
                HudIcon(new Rect(detail.x + 11, detail.y + 12, 126, 129), selectedItem);
                GUI.Label(new Rect(detail.x + 151, detail.y + 12, 499, 43), ForestInventory.Name(selectedItem).ToUpperInvariant(), hudHeading);
                HudFill(new Rect(detail.x + 154, detail.y + 57, 492, 1), HudGold);
                GUI.Label(new Rect(detail.x + 153, detail.y + 67, 490, 55), ForestInventory.Description(selectedItem), hudSmall);
                GUI.Label(new Rect(detail.x + 153, detail.y + 122, 490, 29), $"Tổng: {Count(selectedItem)}  ·  Tối đa {StackLimit(selectedItem)}/ô", hudSmall);
            } else GUI.Label(new Rect(detail.x + 24, detail.y + 42, detail.width - 48, 70), "Túi đồ trống. Nhặt vật tư trong màn chơi để bổ sung.", hudBody);
            GUI.Label(new Rect(panel.x + 27, 811, panel.width - 54, 50), "Tab / Esc Đóng  ·  Kéo đồ hoặc chọn + 1–5 để gán\nChuột phải ô nhanh: bỏ gán", hudSmall);
        }
        private Rect QuickCell(Rect bar, int i) => new Rect(bar.x + 10 + i * 105, bar.y + 11, 100, 102);
        private void DrawQuickBar(float width, float height)
        {
            var bar = QuickRect(width, height); HudPanel(bar);
            var label = new Rect(bar.center.x - 83, bar.y - 31, 166, 36); HudPanel(label);
            GUI.Label(label, "DÙNG NHANH", hudKey);
            for (int i = 0; i < 5; i++) {
                var r = QuickCell(bar, i); string id = quickSlots[i]; int count = string.IsNullOrEmpty(id) ? 0 : Count(id);
                HudPanel(r, i == activeQuickSlot);
                if (!string.IsNullOrEmpty(id)) {
                    HudIcon(new Rect(r.x + 9, r.y + 9, 82, 82), id, count == 0);
                    HudNumber(new Rect(r.xMax - 54, r.yMax - 30, 46, 24), "x" + count);
                } else GUI.Label(new Rect(r.x + 26, r.y + 31, 48, 44), "+", hudCenter);
                HudFill(new Rect(r.x + 7, r.y + 6, 24, 27), new Color(.045f, .05f, .03f, .95f));
                GUI.Label(new Rect(r.x + 7, r.y + 6, 24, 27), (i + 1).ToString(), hudKey);
                if (i == activeQuickSlot) HudBorder(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), HudGold);
                if (inventoryOpen && Event.current.type == EventType.MouseDown && Event.current.button == 1 && r.Contains(Event.current.mousePosition)) {
                    AssignQuickSlot(i, null); Event.current.Use();
                }
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) {
                    if (inventoryOpen) { if (selectedItem != null) AssignQuickSlot(i, selectedItem); }
                    else UseQuickSlot(i);
                }
            }
        }
        private void FinishHudDrag(Event ev, float width, float height)
        {
            if (!inventoryOpen || Stopped) { CancelHudDrag(); return; }
            if (dragItem != null && ev.type == EventType.MouseDrag) { dragging = true; ev.Use(); }
            if (dragItem != null && ev.type == EventType.MouseUp && ev.button == 0) {
                if (dragging) {
                    var bar = QuickRect(width, height);
                    for (int i = 0; i < 5; i++) if (QuickCell(bar, i).Contains(ev.mousePosition)) AssignQuickSlot(i, dragItem);
                }
                CancelHudDrag(); ev.Use();
            }
            if (dragging && dragItem != null) HudIcon(new Rect(ev.mousePosition.x - 30, ev.mousePosition.y - 30, 60, 60), dragItem);
        }
        private void DrawHudFeedback(float width, float height)
        {
            float areaWidth = inventoryOpen ? InventoryRect(width).x - 56 : Mathf.Min(770, width - 100);
            float x = inventoryOpen ? 28 : (width - areaWidth) / 2;
            if (Time.time < dialogueUntil) {
                var r = new Rect(x, height - 289, areaWidth, 86); HudPanel(r);
                GUI.Label(new Rect(r.x + 18, r.y + 12, r.width - 36, r.height - 20), dialogue, hudBody);
            }
            if (nearby != null && !nearby.used && !Stopped && !inventoryOpen)
                GUI.Label(new Rect(x + 18, height - 338, areaWidth - 36, 43), "[E] " + nearby.label, hudBody);
            if (crafting != null) GUI.Label(new Rect(47, 350, 415, 40), $"Đang chế tạo… {Mathf.Max(0, craftUntil - Time.time):0.0}s", hudBody);
            foreach (var guard in guards.Where(g => g.Alive)) {
                var screen = gameCamera.WorldToScreenPoint(guard.transform.position + Vector3.up * 2.4f);
                var point = new Vector2(screen.x / HudScale, (Screen.height - screen.y) / HudScale);
                if (screen.z > 0 && (!inventoryOpen || !InventoryRect(width).Contains(point)) && Vector3.Distance(player.position, guard.transform.position) < 24)
                    GUI.Label(new Rect(point.x - 60, point.y, 190, 40), guard.state + (guard.suspicion > .05f ? $" {Mathf.Min(100, guard.suspicion * 100):0}%" : ""), hudSmall);
            }
            GUI.Label(new Rect(28, height - 26, width - 56, 24), "Tab Túi đồ  ·  1–5 Dùng nhanh  ·  H Hồi máu  ·  Q Ném đá  ·  B Chế tạo  ·  M Bản đồ  ·  Esc Menu", hudSmall);
        }
    }
}
