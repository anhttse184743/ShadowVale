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
        private float objectiveBottom;
        private int selectedStack;
        private static readonly Color HudPaper = new Color(.9f, .85f, .68f);
        private static readonly Color HudGold = new Color(.87f, .68f, .32f);
        private static readonly Rect HudPlateUv = new Rect(0, 0, 1, 1);

        private Map01Mission mission;
        private Map01Minimap minimap;
        private Map01Quest quest;
        private IMapObjectives objectives;
        private Map01Inventory inventory;
        private Map01PlayerInteraction interaction;
        private Map01ObjectiveGuide guide;
        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            minimap = GetComponent<Map01Minimap>();
            if (minimap == null) minimap = gameObject.AddComponent<Map01Minimap>();
            quest = GetComponent<Map01Quest>();
            objectives = GetComponents<MonoBehaviour>().OfType<IMapObjectives>().FirstOrDefault();
            inventory = GetComponent<Map01Inventory>();
            interaction = GetComponent<Map01PlayerInteraction>();
            // Added at runtime rather than baked into Map 1.unity; the HUD is its only reader.
            // Deliberately not "GetComponent() ?? AddComponent()" — see WeaponHotbar.
            guide = GetComponent<Map01ObjectiveGuide>();
            if (guide == null) guide = gameObject.AddComponent<Map01ObjectiveGuide>();
            HookPickups(); // Map01Hud.Pickup.cs: from the start, not from the first OnGUI
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
                                // Every other tile mirrored, so neighbouring tiles meet edge to matching edge
                                // and a large panel (the bench screen) shows no seams.
                                float u = .32f * tw / 256, v = .32f * th / 256;
                                bool flipU = ((int)(tx / 256)) % 2 == 1, flipV = ((int)(ty / 256)) % 2 == 1;
                                GUI.DrawTextureWithTexCoords(new Rect(target.x + tx, target.y + ty, tw, th), hudPlate,
                                    // (u runs left to right from the left edge; v is anchored at each tile's top edge.)
                                    new Rect(flipU ? .52f : .2f, flipV ? .2f + v : .52f - v, flipU ? -u : u, flipV ? -v : v));
                            }
                    } else GUI.DrawTextureWithTexCoords(target, hudPlate, uv, true);
                }
                HudFill(new Rect(r.x + 9, r.y + 9, r.width - 18, r.height - 18), new Color(.035f, .045f, .025f, .32f));
            } else { HudFill(r, new Color(.08f, .10f, .065f, .97f)); HudBorder(r, HudGold); }
            if (selected) HudBorder(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), HudGold, 3);
        }
        private readonly System.Collections.Generic.Dictionary<string, Texture2D> itemIcons = new System.Collections.Generic.Dictionary<string, Texture2D>();
        /// <summary>The icon an item has of its own, if any (null for the ones on the art board).</summary>
        public Texture2D ItemIcon(string id)
        {
            if (id == null) return null;
            if (!itemIcons.TryGetValue(id, out var icon)) itemIcons[id] = icon = Resources.Load<Texture2D>("Hud/ItemIcons/" + id);
            return icon;
        }
        private void HudIcon(Rect r, string id, bool dim = false)
        {
            if(id==Map01Story.KeyItem||id==Map01Story.CargoItem){var texture=id==Map01Story.KeyItem?Map01OperationHints.KeyIcon:Map01OperationHints.CargoIcon;if(texture!=null){GUI.DrawTexture(r,texture,ScaleMode.ScaleToFit);return;}}
            if ((id == "rifle_standard" || id == "knife") && hudWeapons != null) {
                // Use the text-free inventory previews from the approved art board.
                float left = id == "rifle_standard" ? 57 : 240;
                var weaponUv = new Rect(left / 1536f, 1 - 850f / 1024f, 148f / 1536f, 120f / 1024f);
                var tint = GUI.color;
                if (dim) GUI.color = new Color(.55f, .55f, .55f, .75f);
                // Fitted inside the rect both ways (a wide, low slot card must not spill over its own title).
                float ih = Mathf.Min(r.height, r.width * 120f / 148f), iw = ih * 148f / 120f;
                GUI.DrawTextureWithTexCoords(new Rect(r.center.x - iw / 2, r.center.y - ih / 2, iw, ih), hudWeapons, weaponUv);
                GUI.color = tint;
                return;
            }
            int index = ForestInventory.IconIndex(id);
            if (index < 0 && ItemIcon(id) is Texture2D own) {
                // Items added after the art board have an icon of their own, rendered from their model (Resources/Hud/ItemIcons).
                var tinted = GUI.color; if (dim) GUI.color = new Color(.55f, .55f, .55f, .75f);
                HudFill(r, new Color(.11f, .12f, .09f, .9f));
                GUI.DrawTexture(new Rect(r.x - r.width * .08f, r.y - r.height * .08f, r.width * 1.16f, r.height * 1.16f), own, ScaleMode.ScaleToFit);
                GUI.color = tinted;
                return;
            }
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
        /// <summary>The bag screen's two plates together (Map01Hud.Bag.cs); messages sit to their left while it is open.</summary>
        private Rect InventoryRect(float width) => new Rect(width - 1175, 60, 1132, 805);

        private void OnGUI() => DrawHud();

        private void DrawHud()
        {
            if (!mission.IsInitialized || mission.Cinematic || ForestMenu.Visible) return;
            HudStyles();
            HookPickups(); // Map01Hud.Pickup.cs
            var matrix = GUI.matrix; var color = GUI.color; int depth = GUI.depth;
            float scale = HudScale, width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1)); GUI.color = Color.white; GUI.depth = -10;
            DrawScouting(width, height, scale); // Map01Hud.Scouting.cs — first, under every other plate.
            DrawStoneMarkers(width, height); // Map01Hud.Stone.cs — dots in the world, under the plates too.
            bool guiding = guide.HasTarget && !mission.Stopped && !mission.InventoryOpen && !mission.MapOpen;
            if (guiding) DrawTargetMarker(width, height, scale); // First, so every HUD plate sits on top of it.
            if (!mission.BenchOpen) DrawObjective(); // The bench screen covers the screen; nothing peeks out at its edge.
            if (guiding) DrawCompass(width);
            if (!mission.InventoryOpen && !mission.MapOpen) DrawCombatHud(width, height);
            if (mission.BenchOpen) DrawBench(width, height); // Map01Hud.Craft.cs
            else if (!mission.MapOpen && mission.InventoryOpen) DrawInventory(width);
            DrawHudFeedback(width, height);
            if (!mission.InventoryOpen && !mission.MapOpen) DrawPickups(width);
            if (!mission.InventoryOpen && !mission.MapOpen) {
                var map = new Rect(40, 30, 261, 261);
                minimap.Draw(map, false, hudSmall); // Round, with its own rim.
                var hint = new Rect(map.center.x - 95, map.yMax + 9, 190, 27);
                HudFill(hint, new Color(.02f, .03f, .02f, .5f));
                GUI.Label(hint, "M  MỞ BẢN ĐỒ", hudKey);
            }
            DrawRescue(width); // Map01Hud.Rescue.cs
            DrawTakedownHint(width,height);
            DrawStoneAim(width, height); // Map01Hud.Stone.cs
            DrawScoutFailure(width); // Map01Hud.Scouting.cs
            if (mission.MapOpen) DrawMap(width, height);
            if ((mission.PlayerHealth <= 0 && GetComponent<TutorialDirector>() == null) || (objectives?.IsComplete ?? false)) {
                HudPanel(new Rect(width / 2 - 300, 320, 600, 185));
                GUI.Label(new Rect(width / 2 - 275, 345, 550, 75), mission.PlayerHealth <= 0 ? "NAM ĐÃ GỤC NGÃ" : "HOÀN THÀNH MAP 1", hudCenter);
                GUI.Label(new Rect(width / 2 - 245, 437, 490, 50), (objectives?.IsComplete ?? false) ? "Continuing to Map 2…  Enter: Continue" : "Enter Chơi lại  ·  F9 Tải bản lưu  ·  Esc Menu", hudSmall);
            }
            GUI.matrix = matrix; GUI.color = color; GUI.depth = depth;
        }
        private void DrawObjective()
        {
            var previousMatrix = GUI.matrix;
            float offset = mission.InventoryOpen || mission.MapOpen ? -158 : 160;
            GUI.matrix = previousMatrix * Matrix4x4.Translate(new Vector3(0, offset, 0));
            if (!ObjectiveShown) {
                // Tucked away with [J]: only its heading stays, as a reminder of the key.
                ShadowLabel(new Rect(48, 205, 332, 37), "NHIỆM VỤ  ·  J", hudHeading);
                objectiveBottom = 186 + 50 + offset;
                GUI.matrix = previousMatrix;
                return;
            }
            // Sized to the text: the longer objectives wrap to three lines and used to be cut off.
            string text = objectives?.ObjectiveText ?? "";
            float textHeight = hudBody.CalcHeight(new GUIContent(text), 328);
            bool guiding = guide.HasTarget && !mission.Stopped;
            // Point 3 (Map01C3Mission): the optional objectives once the order is given, and the camp's state nearby.
            var c3 = GetComponent<Map01C3Mission>();
            var optional = c3 != null && quest != null && quest.Stage == Map01Quest.BossStage ? c3.OptionalObjectives().ToList() : null;
            string c3Status = c3 != null ? c3.StatusLine() : null;
            float c3Height = (optional != null ? optional.Count * 22 + 8 : 0) + (c3Status != null ? 28 : 0);
            float height = Mathf.Max(152, 258 + textHeight + (guiding ? 32 : 0) + c3Height + 14 - 186);
            // See-through: a faint shade and shadowed lettering instead of the plate, so the
            // world stays visible behind the objective.
            HudFill(new Rect(28, 186, 373, height), new Color(.02f, .03f, .02f, .22f));
            ShadowLabel(new Rect(48, 205, 332, 37), "NHIỆM VỤ", hudHeading);
            HudFill(new Rect(49, 246, 328, 1), new Color(.6f, .51f, .3f, .65f));
            ShadowLabel(new Rect(49, 258, 328, textHeight), text, hudBody);
            if (guiding) {
                float y = 258 + textHeight + 6;
                HudDiamond(new Vector2(58, y + 12), 7);
                ShadowLabel(new Rect(72, y, 312, 26), $"{guide.Label}  ·  {guide.Distance:0} m", hudGuide);
            }
            float c3y = 258 + textHeight + (guiding ? 32 : 0) + 6;
            if (optional != null)
            {
                var old = GUI.color;
                foreach (var (what, done) in optional)
                {
                    GUI.color = done ? new Color(.72f, .86f, .55f, old.a) : old;
                    ShadowLabel(new Rect(49, c3y, 328, 22), "• " + what + (done ? " — xong" : ""), hudSmall);
                    c3y += 22;
                }
                GUI.color = old; c3y += 8;
            }
            if (c3Status != null) ShadowLabel(new Rect(49, c3y, 328, 26), c3Status, hudGuide);
            objectiveBottom = 186 + height + offset;
            GUI.matrix = previousMatrix;
        }

        /// <summary>[J] shows or hides the objective panel.</summary>
        public bool ObjectiveShown { get; private set; } = true;
        public void ToggleObjective() => ObjectiveShown = !ObjectiveShown;

        /// <summary>Text with a dark drop shadow, legible without a plate behind it.</summary>
        private static void ShadowLabel(Rect r, string text, GUIStyle style)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, .85f * old.a);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
            GUI.color = old;
            GUI.Label(r, text, style);
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
        private void DrawHudFeedback(float width, float height)
        {
            if (mission.BenchOpen) return; // The bench screen shows its own messages (Map01Hud.Craft.cs).
            float areaWidth = mission.InventoryOpen ? InventoryRect(width).x - 56 : Mathf.Min(770, width - 100);
            float x = mission.InventoryOpen ? 28 : (width - areaWidth) / 2;
            if (Time.time < mission.DialogueUntil && !string.IsNullOrEmpty(mission.Dialogue)) {
                var r = new Rect(x, height - 289, areaWidth, 86); HudPanel(r);
                GUI.Label(new Rect(r.x + 18, r.y + 12, r.width - 36, r.height - 20), mission.Dialogue, hudBody);
            }
            var nearby = interaction.Nearby;
            var storyPrompt = GetComponent<Map01Story>()?.Prompt;
            if (nearby != null && !nearby.used && nearby.kind == ForestPointKind.Workbench && !mission.Stopped && !mission.InventoryOpen)
                GUI.Label(new Rect(x + 18, height - 338, areaWidth - 36, 43), "[E] " + nearby.label, hudBody);
            else if (!string.IsNullOrEmpty(storyPrompt) && !mission.Stopped && !mission.InventoryOpen)
                GUI.Label(new Rect(x + 18, height - 338, areaWidth - 36, 43), storyPrompt, hudBody);
            else if (quest != null && quest.HungInRange && !mission.Stopped && !mission.InventoryOpen)
                GUI.Label(new Rect(x + 18, height - 338, areaWidth - 36, 43), quest.HungPrompt, hudBody);
            else if (nearby != null && !nearby.used && !mission.Stopped && !mission.InventoryOpen)
                GUI.Label(new Rect(x + 18, height - 338, areaWidth - 36, 43), "[E] " + nearby.label, hudBody);
            // The bench's progress while Nam is away from it: Map01Hud.Pickup.cs, above the pickup feed.
            // Alerted guards are marked by the awareness eye (Map01Hud.Scouting).
            GUI.Label(new Rect(28, height - 26, width - 430, 24), "Tab Túi đồ  ·  1 Súng chính  ·  2 Súng ngắn  ·  3 Dao  ·  4 Đồ ném  ·  Giữ/thả chuột trái Ném  ·  R Nạp đạn  ·  H Hồi máu  ·  M Bản đồ  ·  Esc Menu", hudSmall);
        }

        private void DrawMap(float width, float height)
        {
            float size = Mathf.Min(650, height - 200);
            var frame = new Rect((width - size - 40) / 2, 65, size + 40, size + 120);
            HudPanel(frame);
            GUI.Label(new Rect(frame.x + 20, frame.y + 10, size, 35), "BẢN ĐỒ ĐỊA HÌNH", hudCenter);
            minimap.Draw(new Rect(frame.x + 20, frame.y + 50, size, size), true, hudSmall);
            GUI.Label(new Rect(frame.x + 20, frame.yMax - 62, size, 56), "Nhà xanh: căn cứ A · Vùng tối: chưa khám phá\nBạn ▲  Đồng đội ●  Mục tiêu ◇  Địch △", hudSmall);
        }
        private Vector2 MapPosition(Vector3 p, Rect r) => new Vector2(r.x + Mathf.InverseLerp(mission.mapMin.x, mission.mapMax.x, p.x) * r.width, r.yMax - Mathf.InverseLerp(mission.mapMin.y, mission.mapMax.y, p.z) * r.height);
        private static void Panel(Rect rect) { var old = GUI.color; GUI.color = new Color(.035f, .07f, .06f, .94f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
    }
}
