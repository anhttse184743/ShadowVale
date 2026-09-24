using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>Map 1's HUD: borderless health/weapon icons, objective plate, inventory, dialogue
    /// feedback, the objective compass/marker and the map overlay. Pure
    /// presentation — reads Map01Mission/Map01Quest/Map01Inventory/Map01PlayerInteraction/
    /// Map01ObjectiveGuide, owns nothing gameplay.</summary>
    public sealed partial class Map01Hud : MonoBehaviour
    {
        private Texture2D hudItems, hudPlate, hudWeapons, compassArrow, compassBadge;
        private GUIStyle hudTitle, hudHeading, hudBody, hudSmall, hudCount, hudKey, hudCenter, hudEquipped, hudGuide;
        private float hudScroll;
        private float objectiveBottom;
        private int selectedStack;
        private static readonly Color HudPaper = new Color(.9f, .85f, .68f);
        private static readonly Color HudGold = new Color(.87f, .68f, .32f);
        private static readonly Rect HudPlateUv = new Rect(0, 0, 1, 1);

        private Map01Mission mission;
        private Map01Minimap minimap;
        private Map01Quest quest;
        private Map01Inventory inventory;
        private Map01PlayerInteraction interaction;
        private Map01ObjectiveGuide guide;
        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            minimap = GetComponent<Map01Minimap>();
            if (minimap == null) minimap = gameObject.AddComponent<Map01Minimap>();
            quest = GetComponent<Map01Quest>();
            inventory = GetComponent<Map01Inventory>();
            interaction = GetComponent<Map01PlayerInteraction>();
            // Added at runtime rather than baked into Map 1.unity; the HUD is its only reader.
            // Deliberately not "GetComponent() ?? AddComponent()" — see WeaponHotbar.
            guide = GetComponent<Map01ObjectiveGuide>();
            if (guide == null) guide = gameObject.AddComponent<Map01ObjectiveGuide>();
        }

        private void HudStyles()
        {
            if (hudBody != null) return;
            hudItems = Resources.Load<Texture2D>("Hud/Items");
            hudWeapons = Resources.Load<Texture2D>("Hud/StartingWeapons");

            hudPlate = Resources.Load<Texture2D>("Hud/Panel");
            var bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var stencil = Resources.Load<Font>("Menu/Fonts/BlackOpsOne-Regular") ?? bodyFont;
            hudBody = new GUIStyle(GUI.skin.label) { font = bodyFont, fontSize = 22, wordWrap = true, normal = { textColor = HudPaper } };
            hudSmall = new GUIStyle(hudBody) { fontSize = 18 };
            hudEquipped = new GUIStyle(hudSmall) { fontSize = 12, wordWrap = false, alignment = TextAnchor.MiddleCenter };
            hudHeading = new GUIStyle(hudBody) { font = stencil, fontSize = 25 };
            hudTitle = new GUIStyle(hudHeading) { fontSize = 46, alignment = TextAnchor.MiddleCenter };
            hudCount = new GUIStyle(hudHeading) { fontSize = 22, alignment = TextAnchor.LowerRight };
            hudKey = new GUIStyle(hudHeading) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            hudCenter = new GUIStyle(hudHeading) { alignment = TextAnchor.MiddleCenter };
            hudGuide = new GUIStyle(hudHeading) { fontSize = 19, wordWrap = false, normal = { textColor = HudGold } };
            compassBadge = MakeTexture(64, (u, v) => {
                float d = new Vector2(u - .5f, v - .5f).magnitude * 2;
                float ring = Mathf.Clamp01(1 - Mathf.Abs(d - .9f) / .07f);
                return Color.Lerp(new Color(.09f, .1f, .065f, d < .97f ? .9f : 0), HudGold, ring);
            });
            // Notched dart pointing up (v = 1 is the top row as GUI draws it).
            compassArrow = MakeTexture(64, (u, v) => {
                var p = new Vector2(u - .5f, v - .5f);
                Vector2 tip = new Vector2(0, .36f), left = new Vector2(-.27f, -.22f), right = new Vector2(.27f, -.22f), notch = new Vector2(0, -.06f);
                return InTriangle(p, tip, left, notch) || InTriangle(p, tip, notch, right) ? new Color(1f, .84f, .38f, 1) : Color.clear;
            });
        }
        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
        }
        private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        /// <summary>4× supersampled so the procedural shapes have smooth edges.</summary>
        private static Texture2D MakeTexture(int size, System.Func<float, float, Color> shade)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var sum = Color.clear;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            sum += shade((x + (sx + .5f) / 4) / size, (y + (sy + .5f) / 4) / size);
                    texture.SetPixel(x, y, sum / 16);
                }
            texture.Apply();
            return texture;
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
            if (ForestInventory.IsWeapon(id) && hudWeapons != null) {
                // Use the text-free inventory previews from the approved art board.
                float left = id == "rifle_standard" ? 57 : 240;
                var weaponUv = new Rect(left / 1536f, 1 - 850f / 1024f, 148f / 1536f, 120f / 1024f);
                var tint = GUI.color;
                if (dim) GUI.color = new Color(.55f, .55f, .55f, .75f);
                GUI.DrawTextureWithTexCoords(new Rect(r.x, r.center.y - r.width * 120f / 148f / 2, r.width, r.width * 120f / 148f), hudWeapons, weaponUv);
                GUI.color = tint;
                return;
            }
            int index = ForestInventory.IconIndex(id);
            if (hudItems == null || index < 0) { GUI.Label(r, "?", hudCenter); return; }
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

        private void OnGUI() => DrawHud();

        private void DrawHud()
        {
            if (!mission.IsInitialized || ForestMenu.Visible) return;
            HudStyles();
            var matrix = GUI.matrix; var color = GUI.color; int depth = GUI.depth;
            float scale = HudScale, width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1)); GUI.color = Color.white; GUI.depth = -10;
            DrawScouting(width, height, scale); // Map01Hud.Scouting.cs — first, under every other plate.
            bool guiding = guide.HasTarget && !mission.Stopped && !mission.InventoryOpen && !mission.MapOpen;
            if (guiding) DrawTargetMarker(width, height, scale); // First, so every HUD plate sits on top of it.
            DrawObjective();
            if (guiding) DrawCompass(width);
            if (!mission.InventoryOpen && !mission.MapOpen) DrawCombatHud(width, height);
            if (!mission.MapOpen && mission.InventoryOpen) DrawInventory(width);
            DrawHudFeedback(width, height);
            if (!mission.InventoryOpen && !mission.MapOpen) {
                var frame = new Rect(28, 24, 285, 307); HudPanel(frame);
                minimap.Draw(new Rect(frame.x + 12, frame.y + 12, 261, 261), false, hudSmall);
                GUI.Label(new Rect(frame.x + 15, frame.yMax - 29, 255, 26), "M  MỞ BẢN ĐỒ", hudKey);
            }
            if (mission.MapOpen) DrawMap(width, height);
            if (mission.PlayerHealth <= 0 || quest.Stage == Map01Quest.CompleteStage) {
                HudPanel(new Rect(width / 2 - 300, 320, 600, 185));
                GUI.Label(new Rect(width / 2 - 275, 345, 550, 75), mission.PlayerHealth <= 0 ? "NAM ĐÃ GỤC NGÃ" : "HOÀN THÀNH MAP 1", hudCenter);
                GUI.Label(new Rect(width / 2 - 245, 437, 490, 50), "Enter Chơi lại  ·  F9 Tải bản lưu  ·  Esc Menu", hudSmall);
            }
            GUI.matrix = matrix; GUI.color = color; GUI.depth = depth;
        }
        private void DrawObjective()
        {
            var previousMatrix = GUI.matrix;
            float offset = mission.InventoryOpen || mission.MapOpen ? -158 : 160;
            GUI.matrix = previousMatrix * Matrix4x4.Translate(new Vector3(0, offset, 0));
            // Sized to the text: the longer objectives wrap to three lines and used to be cut off.
            string text = quest.ObjectiveText;
            float textHeight = hudBody.CalcHeight(new GUIContent(text), 328);
            bool guiding = guide.HasTarget && !mission.Stopped;
            float height = Mathf.Max(152, 258 + textHeight + (guiding ? 32 : 0) + 14 - 186);
            HudPanel(new Rect(28, 186, 373, height));
            GUI.Label(new Rect(48, 205, 332, 37), "NHIỆM VỤ", hudHeading);
            HudFill(new Rect(49, 246, 328, 1), new Color(.6f, .51f, .3f, .65f));
            GUI.Label(new Rect(49, 258, 328, textHeight), text, hudBody);
            if (guiding) {
                float y = 258 + textHeight + 6;
                HudDiamond(new Vector2(58, y + 12), 7);
                GUI.Label(new Rect(72, y, 312, 26), $"{guide.Label}  ·  {guide.Distance:0} m", hudGuide);
            }
            objectiveBottom = 186 + height + offset;
            GUI.matrix = previousMatrix;
        }
        /// <summary>Rotates GUI drawing around a point given in the current (scaled) GUI space and
        /// returns the matrix to restore. Never GUIUtility.RotateAroundPivot here: it takes the
        /// pivot in unscaled screen space, so under the HUD's scale (any screen but 1600×900)
        /// whatever it turns swings off its spot — the minimap arrow ended up outside the map.</summary>
        internal static Matrix4x4 RotateGui(Vector2 pivot, float degrees)
        {
            var previous = GUI.matrix;
            GUI.matrix = RotateAbout(previous, pivot, degrees);
            return previous;
        }
        public static Matrix4x4 RotateAbout(Matrix4x4 current, Vector2 pivot, float degrees) =>
            current * Matrix4x4.TRS(pivot, Quaternion.Euler(0, 0, degrees), Vector3.one) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
        private static void HudDiamond(Vector2 center, float radius)
        {
            var previous = RotateGui(center, 45);
            float side = radius * 1.42f;
            HudFill(new Rect(center.x - side / 2, center.y - side / 2, side, side), HudGold);
            GUI.matrix = previous;
        }
        /// <summary>Top-centre compass: the arrow turns with the camera toward the next stretch of
        /// the route (not straight through walls), next to the objective's name and walking distance.</summary>
        private void DrawCompass(float width)
        {
            var forward = Vector3.ProjectOnPlane(mission.gameCamera.transform.forward, Vector3.up);
            var toward = Vector3.ProjectOnPlane(guide.SteerPoint - mission.player.position, Vector3.up);
            float angle = toward.sqrMagnitude > .01f ? Vector3.SignedAngle(forward, toward, Vector3.up) : 0;
            float x = width / 2 - 156;
            var badge = new Rect(x, 26, 64, 64);
            GUI.DrawTexture(badge, compassBadge);
            var previous = RotateGui(badge.center, angle);
            GUI.DrawTexture(new Rect(badge.x + 10, badge.y + 10, 44, 44), compassArrow);
            GUI.matrix = previous;
            var plate = new Rect(x + 72, 32, 240, 54);
            HudPanel(plate);
            GUI.Label(new Rect(plate.x + 14, plate.y + 5, plate.width - 20, 24), guide.Label, hudGuide);
            GUI.Label(new Rect(plate.x + 14, plate.y + 27, plate.width - 20, 24), $"{guide.Distance:0} m", hudSmall);
        }
        /// <summary>A diamond floating over the objective itself while it is on screen.</summary>
        private void DrawTargetMarker(float width, float height, float scale)
        {
            var screen = mission.gameCamera.WorldToScreenPoint(guide.Target + Vector3.up * 2.4f);
            if (screen.z <= 0) return;
            var p = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
            if (p.x < 20 || p.x > width - 20 || p.y < 20 || p.y > height - 20) return;
            HudDiamond(p, 11);
            var tag = new Rect(p.x - 50, p.y + 14, 100, 24);
            HudFill(tag, new Color(.025f, .03f, .02f, .7f));
            GUI.Label(tag, $"{guide.Distance:0} m", hudKey);
        }
        private void DrawInventory(float width)
        {
            var panel = InventoryRect(width); HudPanel(panel);
            GUI.Label(new Rect(panel.x + 25, 75, panel.width - 50, 65), "TÚI ĐỒ", hudTitle);
            GUI.Label(new Rect(panel.x + 230, 144, 256, 42), "VẬT TƯ", hudCenter);
            HudFill(new Rect(panel.x + 75, 164, 150, 1), HudGold); HudFill(new Rect(panel.xMax - 225, 164, 150, 1), HudGold);
            var stacks = inventory.InventoryStacks();
            string selectedItem = inventory.SelectedItem;
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
                if (inventory.IsEquipped(item.Id)) GUI.Label(new Rect(rect.x + 5, rect.y + 4, cell - 10, 20), "ĐANG CẦM", hudEquipped);
                if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition)) {
                    selectedStack = i; selectedItem = item.Id;
                    ev.Use();
                }
            }
            GUI.EndGroup();
            inventory.SelectedItem = selectedItem;
            var detail = new Rect(panel.x + 19, 648, panel.width - 38, 155); HudPanel(detail);
            if (selectedItem != null) {
                HudIcon(new Rect(detail.x + 11, detail.y + 12, 126, 129), selectedItem);
                GUI.Label(new Rect(detail.x + 151, detail.y + 12, 499, 43), ForestInventory.Name(selectedItem).ToUpperInvariant(), hudHeading);
                HudFill(new Rect(detail.x + 154, detail.y + 57, 492, 1), HudGold);
                GUI.Label(new Rect(detail.x + 153, detail.y + 67, 490, 55), ForestInventory.Description(selectedItem), hudSmall);
                GUI.Label(new Rect(detail.x + 153, detail.y + 122, ForestInventory.IsWeapon(selectedItem) ? 310 : 490, 29), $"Tổng: {inventory.Count(selectedItem)}  ·  Tối đa {inventory.StackLimit(selectedItem)}/ô", hudSmall);
                if (ForestInventory.IsWeapon(selectedItem)) {
                    var equipRect = new Rect(detail.xMax - 173, detail.y + 116, 158, 34);
                    HudPanel(equipRect, inventory.IsEquipped(selectedItem));
                    if (GUI.Button(equipRect, inventory.IsEquipped(selectedItem) ? "ĐANG CẦM" : "TRANG BỊ", hudKey)) inventory.EquipItem(selectedItem);
                }
            } else GUI.Label(new Rect(detail.x + 24, detail.y + 42, detail.width - 48, 70), "Túi đồ trống. Nhặt vật tư trong màn chơi để bổ sung.", hudBody);
            GUI.Label(new Rect(panel.x + 27, 811, panel.width - 54, 50), "Tab / Esc Đóng  ·  Chọn vũ khí rồi bấm TRANG BỊ\n6 Súng  ·  7 Dao  ·  8 Tay không  ·  H Hồi máu  ·  Q Ném đá", hudSmall);
        }
        private void DrawHudFeedback(float width, float height)
        {
            float areaWidth = mission.InventoryOpen ? InventoryRect(width).x - 56 : Mathf.Min(770, width - 100);
            float x = mission.InventoryOpen ? 28 : (width - areaWidth) / 2;
            if (Time.time < mission.DialogueUntil) {
                var r = new Rect(x, height - 289, areaWidth, 86); HudPanel(r);
                GUI.Label(new Rect(r.x + 18, r.y + 12, r.width - 36, r.height - 20), mission.Dialogue, hudBody);
            }
            var nearby = interaction.Nearby;
            if (quest.HungInRange && !mission.Stopped && !mission.InventoryOpen)
                GUI.Label(new Rect(x + 18, height - 338, areaWidth - 36, 43), quest.HungPrompt, hudBody);
            else if (nearby != null && !nearby.used && !mission.Stopped && !mission.InventoryOpen)
                GUI.Label(new Rect(x + 18, height - 338, areaWidth - 36, 43), "[E] " + nearby.label, hudBody);
            if (inventory.IsCrafting) GUI.Label(new Rect(47, objectiveBottom + 10, 415, 40), $"Đang chế tạo… {inventory.CraftRemaining:0.0}s", hudBody);
            // Alerted guards are marked by the awareness eye (Map01Hud.Scouting).
            GUI.Label(new Rect(28, height - 26, width - 430, 24), "Tab Túi đồ  ·  H Hồi máu  ·  Q Ném đá  ·  B Chế tạo  ·  M Bản đồ  ·  Esc Menu", hudSmall);
        }

        private void DrawMap(float width, float height)
        {
            float size = Mathf.Min(650, height - 180);
            var frame = new Rect((width - size - 40) / 2, 65, size + 40, size + 100);
            HudPanel(frame);
            GUI.Label(new Rect(frame.x + 20, frame.y + 10, size, 35), "BẢN ĐỒ ĐỊA HÌNH", hudCenter);
            minimap.Draw(new Rect(frame.x + 20, frame.y + 50, size, size), true, hudSmall);
            GUI.Label(new Rect(frame.x + 20, frame.yMax - 43, size, 40), "Bạn ▲   Đồng đội ●   Mục tiêu ◇   Địch △   ·   M / Esc Đóng", hudSmall);
        }
        private Vector2 MapPosition(Vector3 p, Rect r) => new Vector2(r.x + Mathf.InverseLerp(mission.mapMin.x, mission.mapMax.x, p.x) * r.width, r.yMax - Mathf.InverseLerp(mission.mapMin.y, mission.mapMax.y, p.z) * r.height);
        private static void Panel(Rect rect) { var old = GUI.color; GUI.color = new Color(.035f, .07f, .06f, .94f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
    }
}
