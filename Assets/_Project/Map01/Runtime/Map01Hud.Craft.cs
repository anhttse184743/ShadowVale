using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The workbench screen, opened with [E] at the bench in base A (Map01Mission.BenchOpen). Left, the
    /// recipes, each saying at a glance how many can be made and showing its materials as have/need; middle,
    /// the chosen recipe: its picture, what it does, and one row per material with a picture, need and have,
    /// enough or short and, when short, where more is found; a quantity and the craft button, which says why
    /// when it cannot be pressed. Right, the bag's materials (those the recipe uses outlined, with what will be
    /// left), the queue with its progress, and what has just been made. Hovering a material's picture shows it
    /// large. Laid out on the 1600 x 900 HUD grid, as the reviewed design "Bàn chế tạo Map 1".
    /// </summary>
    public sealed partial class Map01Hud
    {
        private GUIStyle benchTitle, benchName, benchHead, benchRowName, benchMatName, benchText, benchDim, benchDimRight,
            benchSmall, benchSmallDim, benchSmallBold, benchTab, benchButton, benchVerdict, benchStep, benchCount, benchCenterSmall;
        private string benchRecipe, benchGroup = "all";
        private int benchQty = 1;
        private readonly Dictionary<string, Texture2D> itemRenders = new Dictionary<string, Texture2D>();

        private static readonly Color BenchHave = new Color(.78f, .9f, .62f), BenchShort = new Color(.92f, .55f, .42f),
            BenchDim = new Color(.66f, .64f, .51f), BenchTile = new Color(.075f, .085f, .055f, .96f), BenchEdge = new Color(.33f, .31f, .21f);
        private static readonly (string id, string label)[] BenchGroups =
            { ("all", "Tất cả"), ("heal", "Hồi máu"), ("tool", "Dụng cụ"), ("throw", "Ném"), ("ammo", "Đạn") };
        /// <summary>The bench's materials, in the bag column's order.</summary>
        private GUIStyle benchMatLabel;
        public static readonly string[] BenchMaterials = { "cloth", "herb", "scrap_metal", "gun_oil", "smoke_module", "gunpowder", "blast_charge", "fuse_module" };

        /// <summary>The recipe the bench screen has picked (kept between openings), and its quantity.</summary>
        public string BenchRecipe => benchRecipe;
        public int BenchQuantity => benchQty;
        public void SelectBenchRecipe(string id, int quantity = 1) { benchRecipe = id; benchQty = Mathf.Max(1, quantity); }

        /// <summary>A large picture of an item for the bench (Resources/Hud/ItemRenders), null if it has none.</summary>
        public Texture2D ItemRender(string id)
        {
            if (id == null) return null;
            if (!itemRenders.TryGetValue(id, out var picture)) itemRenders[id] = picture = Resources.Load<Texture2D>("Hud/ItemRenders/" + id);
            return picture;
        }

        private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        private void BenchStyles()
        {
            if (benchTitle != null) return;
            benchTitle = new GUIStyle(hudHeading) { fontSize = 42, wordWrap = false };
            benchName = new GUIStyle(hudHeading) { fontSize = 30, wordWrap = true };
            benchHead = new GUIStyle(hudHeading) { fontSize = 19, wordWrap = false, normal = { textColor = HudGold } };
            benchRowName = new GUIStyle(hudHeading) { fontSize = 18, wordWrap = false };
            benchMatName = new GUIStyle(hudBody) { fontSize = 19, fontStyle = FontStyle.Bold, wordWrap = false };
            benchText = new GUIStyle(hudBody) { fontSize = 16, wordWrap = true, richText = true };
            benchDim = new GUIStyle(hudBody) { fontSize = 16, wordWrap = false, richText = true, normal = { textColor = BenchDim } };
            benchDimRight = new GUIStyle(benchDim) { alignment = TextAnchor.UpperRight };
            benchSmall = new GUIStyle(hudBody) { fontSize = 14, wordWrap = true, richText = true };
            benchSmallDim = new GUIStyle(benchSmall) { normal = { textColor = BenchDim } };
            benchSmallBold = new GUIStyle(hudBody) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = false, richText = true };
            benchTab = new GUIStyle(hudBody) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter };
            benchButton = new GUIStyle(hudHeading) { fontSize = 22, wordWrap = false, alignment = TextAnchor.MiddleCenter };
            benchVerdict = new GUIStyle(hudBody) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter };
            benchStep = new GUIStyle(hudBody) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter };
            benchCount = new GUIStyle(hudBody) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.LowerRight };
            benchCenterSmall = new GUIStyle(hudBody) { fontSize = 13, wordWrap = false, alignment = TextAnchor.UpperCenter, normal = { textColor = BenchDim } };
        }

        /// <summary>An item on a dark tile with a thin edge (gold when <paramref name="lit"/>).</summary>
        private void BenchTileIcon(Rect r, string id, bool lit = false)
        {
            HudFill(r, BenchTile);
            HudIcon(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), id);
            HudBorder(r, lit ? HudGold : BenchEdge, lit ? 2 : 1);
        }

        /// <summary>A flat button: dark fill, an edge, centred text. Returns true when clicked.</summary>
        private bool BenchButton(Rect r, string text, GUIStyle style, bool on = true, bool lit = false)
        {
            HudFill(r, new Color(.05f, .06f, .035f, .85f));
            HudBorder(r, lit ? HudGold : BenchEdge, lit ? 2 : 1);
            var old = GUI.color; if (!on) GUI.color = new Color(.6f, .6f, .6f, .7f);
            GUI.Label(r, text, style);
            GUI.color = old;
            var was = GUI.enabled; GUI.enabled = on;
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            GUI.enabled = was;
            return clicked;
        }

        private void DrawBench(float width, float height)
        {
            BenchStyles();
            var recipes = inventory.Recipes.ToList();
            if (recipes.Count == 0) return;
            float ox = (width - 1600) / 2, oy = (height - 900) / 2;
            Rect R(float x, float y, float w, float h) => new Rect(ox + x, oy + y, w, h);

            var shown = recipes.Where(r => benchGroup == "all" || ForestInventory.BenchGroup(r.output_item_id) == benchGroup).ToList();
            if (shown.Count == 0) { benchGroup = "all"; shown = recipes; }
            if (benchRecipe == null || shown.All(r => r.id != benchRecipe)) benchRecipe = shown[0].id;
            var recipe = recipes.First(r => r.id == benchRecipe);
            int most = inventory.CanMake(recipe);
            benchQty = Mathf.Clamp(benchQty, 1, Mathf.Max(1, most));
            int q = benchQty;
            var queue = inventory.Queue;
            string hover = null; Rect hoverFrom = default; bool hoverRow = false;
            var mouse = Event.current.mousePosition;

            HudFill(new Rect(0, 0, width, height), new Color(.02f, .025f, .015f, .74f));
            HudPanel(R(70, 46, 1460, 808));
            GUI.Label(R(106, 68, 420, 60), "BÀN CHẾ TẠO", benchTitle);
            GUI.Label(R(430, 90, 400, 26), "Căn cứ A · bàn họp", benchDim);
            GUI.Label(R(1004, 90, 500, 26), "[E] / Esc Đóng · Tab Túi đồ", benchDimRight);
            HudFill(R(96, 136, 1408, 1), HudGold);

            // Tabs.
            float tx = 96;
            foreach (var (id, label) in BenchGroups)
            {
                float w = benchTab.CalcSize(new GUIContent(label)).x + 22;
                if (BenchButton(R(tx, 150, w, 32), label, benchTab, true, benchGroup == id))
                {
                    benchGroup = id;
                    var first = recipes.FirstOrDefault(r => id == "all" || ForestInventory.BenchGroup(r.output_item_id) == id);
                    if (first != null && id != "all" && ForestInventory.BenchGroup(recipe.output_item_id) != id) { benchRecipe = first.id; benchQty = 1; }
                }
                tx += w + 6;
            }

            // Recipes.
            for (int k = 0; k < shown.Count; k++)
            {
                var r = shown[k];
                var row = R(96, 196 + k * 112, 372, 104);
                bool selected = r.id == benchRecipe;
                HudPanel(row, selected);
                BenchTileIcon(new Rect(row.x + 10, row.y + 13, 78, 78), r.output_item_id);
                if (r.output_count > 1) GUI.Label(new Rect(row.x + 12, row.y + 66, 72, 22), "×" + r.output_count, benchCount);
                GUI.Label(new Rect(row.x + 98, row.y + 11, 268, 26), ForestInventory.Name(r.output_item_id).ToUpperInvariant(), benchRowName);
                int n = inventory.CanMake(r);
                int missing = r.inputs.Count(i => inventory.Count(i.item_id) < i.count);
                GUI.Label(new Rect(row.x + 98, row.y + 38, 268, 22), n > 0
                    ? $"<color=#{Hex(BenchHave)}>Làm được ×{n}</color>" : $"<color=#{Hex(BenchShort)}>Thiếu {missing} loại vật liệu</color>", benchSmallBold);
                float mx = row.x + 98;
                foreach (var input in r.inputs)
                {
                    int have = inventory.Count(input.item_id);
                    HudIcon(new Rect(mx, row.y + 64, 22, 22), input.item_id);
                    string plain = $"{have}/{input.count}";
                    float tw = benchSmallBold.CalcSize(new GUIContent(plain)).x;
                    GUI.Label(new Rect(mx + 26, row.y + 66, tw + 6, 22), $"<color=#{Hex(have >= input.count ? BenchHave : BenchShort)}>{plain}</color>", benchSmallBold);
                    mx += 26 + tw + 16;
                }
                if (queue.Count > 0 && queue[0].recipe.id == r.id)
                {
                    HudFill(new Rect(row.x + 98, row.y + 92, 262, 4), new Color(.17f, .18f, .11f));
                    HudFill(new Rect(row.x + 98, row.y + 92, 262 * inventory.CraftProgress, 4), HudGold);
                }
                if (GUI.Button(row, GUIContent.none, GUIStyle.none) && !selected) { benchRecipe = r.id; benchQty = 1; }
            }

            // The chosen recipe.
            string output = recipe.output_item_id;
            var picture = R(500, 150, 210, 210);
            HudFill(picture, new Color(.06f, .07f, .045f, .96f)); HudBorder(picture, BenchEdge, 1);
            var render = ItemRender(output);
            if (render != null) GUI.DrawTexture(new Rect(picture.x + 6, picture.y + 6, picture.width - 12, picture.height - 12), render, ScaleMode.ScaleToFit);
            else HudIcon(new Rect(picture.x + 30, picture.y + 30, 150, 150), output);
            GUI.Label(R(732, 144, 440, 42), ForestInventory.Name(output).ToUpperInvariant(), benchName);
            GUI.Label(R(732, 190, 440, 24), $"Ra ×{recipe.output_count} mỗi lần · {recipe.craft_seconds:0} giây · Trong túi: <b><color=#{Hex(HudPaper)}>{inventory.Count(output)}</color></b>", benchDim);
            float cx = ox + 732;
            foreach (var fact in ForestInventory.BenchFacts(output).Append($"Tối đa {inventory.StackLimit(output)}/ô"))
            {
                float w = benchSmallBold.CalcSize(new GUIContent(fact)).x + 16;
                var chip = new Rect(cx, oy + 222, w, 28);
                HudBorder(chip, BenchEdge, 1);
                GUI.Label(new Rect(chip.x + 8, chip.y + 5, w, 22), fact.Contains("chưa có") ? $"<color=#{Hex(BenchShort)}>{fact}</color>" : fact, benchSmallBold);
                cx += w + 6;
            }
            GUI.Label(R(732, 262, 440, 96), ForestInventory.Description(output), benchText);

            GUI.Label(R(500, 374, 400, 28), "VẬT LIỆU CẦN" + (q > 1 ? $" · ×{q}" : ""), benchHead);
            HudFill(R(700, 390, 480, 1), new Color(HudGold.r, HudGold.g, HudGold.b, .55f));
            for (int k = 0; k < recipe.inputs.Length; k++)
            {
                var input = recipe.inputs[k];
                int need = input.count * q, have = inventory.Count(input.item_id);
                bool ok = have >= need;
                var row = R(500, 408 + k * 114, 664, 106);
                HudPanel(row);
                var tile = new Rect(row.x + 10, row.y + 10, 86, 86);
                bool over = tile.Contains(mouse);
                BenchTileIcon(tile, input.item_id, over);
                if (over) { hover = input.item_id; hoverFrom = tile; hoverRow = true; }
                GUI.Label(new Rect(row.x + 112, row.y + 14, 380, 26), ForestInventory.Name(input.item_id), benchMatName);
                GUI.Label(new Rect(row.x + 112, row.y + 42, 380, 24),
                    $"Cần <b><color=#{Hex(HudPaper)}>{need}</color></b> · Có <b><color=#{Hex(ok ? BenchHave : BenchShort)}>{have}</color></b>", benchDim);
                GUI.Label(new Rect(row.x + 112, row.y + 68, 430, 24), ok
                    ? $"<color=#{Hex(BenchDim)}>Sau khi làm còn {have - need}</color>"
                    : $"<color=#{Hex(BenchShort)}>Thiếu {need - have} · Lấy ở: {ForestInventory.Source(input.item_id)}</color>", benchSmall);
                if (need <= 8)
                    for (int p = 0; p < need; p++)
                    {
                        var pip = new Rect(row.xMax - 126 - (need - p) * 19, row.y + 28, 14, 14);
                        if (p < have) HudFill(pip, BenchHave); else HudBorder(pip, BenchShort, 2);
                    }
                var verdict = new Rect(row.xMax - 112, row.y + 20, 98, 34);
                HudBorder(verdict, ok ? BenchHave : BenchShort, 1);
                var old = GUI.color; GUI.color = ok ? BenchHave : BenchShort;
                GUI.Label(verdict, ok ? "ĐỦ" : "THIẾU " + (need - have), benchVerdict);
                GUI.color = old;
            }

            // Quantity, the button, and why it cannot be pressed.
            if (BenchButton(R(500, 772, 42, 42), "-", benchStep, q > 1)) benchQty = q - 1;
            HudFill(R(548, 772, 64, 42), new Color(.05f, .06f, .035f, .85f)); HudBorder(R(548, 772, 64, 42), BenchEdge, 1);
            GUI.Label(R(548, 772, 64, 42), q.ToString(), benchStep);
            if (BenchButton(R(618, 772, 42, 42), "+", benchStep, q < most)) benchQty = q + 1;
            if (BenchButton(R(666, 772, 120, 42), $"TỐI ĐA ×{Mathf.Max(1, most)}", benchVerdict, most > 1)) benchQty = Mathf.Max(1, most);

            string reason = !inventory.NearWorkbench() ? "Đến bàn chế tạo ở căn cứ A để chế tạo"
                : most < q ? "Thiếu " + string.Join(", ", recipe.inputs.Where(i => inventory.Count(i.item_id) < i.count * q)
                    .Select(i => (i.count * q - inventory.Count(i.item_id)) + " " + ForestInventory.Name(i.item_id).ToLowerInvariant()))
                : null;
            bool ready = reason == null;
            var craft = R(840, 756, 340, 70);
            HudPanel(craft, ready);
            var tint = GUI.color;
            GUI.color = ready ? HudGold : new Color(.5f, .48f, .38f);
            GUI.Label(craft, (queue.Count > 0 ? "XẾP HÀNG" : "CHẾ TẠO") + $" ×{q} · {recipe.craft_seconds * q:0}s", benchButton);
            GUI.color = tint;
            var enabled = GUI.enabled; GUI.enabled = ready;
            if (GUI.Button(craft, GUIContent.none, GUIStyle.none) && inventory.TryCraft(recipe.id, q)) benchQty = 1;
            GUI.enabled = enabled;
            GUI.Label(R(500, 826, 680, 24), ready
                ? $"<color=#{Hex(BenchDim)}>Tổng: {Map01Inventory.Inputs(recipe, q)}{(queue.Count > 0 ? " · xếp sau món đang làm" : "")}</color>"
                : $"<color=#{Hex(BenchShort)}>{reason}</color>", benchSmall);

            // The bag's materials.
            GUI.Label(R(1212, 146, 300, 28), "VẬT LIỆU TRONG TÚI", benchHead);
            for (int k = 0; k < BenchMaterials.Length; k++)
            {
                string id = BenchMaterials[k];
                var tile = R(1212 + k % 4 * 78, 184 + k / 4 * 116, 70, 70);   // four across: the F1's two parts joined the six
                var use = recipe.inputs.FirstOrDefault(i => i.item_id == id);
                bool over = tile.Contains(mouse);
                BenchTileIcon(tile, id, use != null || over);
                if (over) { hover = id; hoverFrom = tile; hoverRow = false; }
                GUI.Label(new Rect(tile.x, tile.y, tile.width - 4, tile.height - 2), "×" + inventory.Count(id), benchCount);
                if (use != null)
                {
                    int left = inventory.Count(id) - use.count * q;
                    var tag = new Rect(tile.x + 3, tile.y + 3, 54, 20);
                    HudFill(tag, new Color(.03f, .035f, .02f, .85f));
                    GUI.Label(new Rect(tag.x + 4, tag.y + 1, 60, 20), left >= 0 ? $"<color=#{Hex(HudGold)}>→ {left}</color>" : $"<color=#{Hex(BenchShort)}>thiếu</color>", benchSmallBold);
                }
                GUI.Label(new Rect(tile.x - 4, tile.yMax + 2, tile.width + 8, 36), ForestInventory.Name(id), benchMatLabel ??= new GUIStyle(benchCenterSmall) { wordWrap = true, fontSize = 12 });
            }

            // The queue.
            GUI.Label(R(1212, 438, 300, 28), "ĐANG CHẾ TẠO", benchHead);
            if (queue.Count == 0)
                GUI.Label(R(1212, 474, 300, 64), "Trống. Món đang làm hiện ở đây; rời bàn vẫn làm tiếp, xong sẽ báo ở mép phải màn hình.", benchSmallDim);
            for (int k = 0; k < Mathf.Min(2, queue.Count); k++)
            {
                var job = queue[k];
                var row = R(1212, 474 + k * 74, 300, 66);
                HudPanel(row, k == 0);
                BenchTileIcon(new Rect(row.x + 9, row.y + 9, 48, 48), job.recipe.output_item_id);
                GUI.Label(new Rect(row.x + 66, row.y + 8, 228, 22), $"{ForestInventory.Name(job.recipe.output_item_id)} ×{job.recipe.output_count}", benchSmallBold);
                GUI.Label(new Rect(row.x + 66, row.y + 30, 228, 20), k == 0 ? $"còn {inventory.CraftRemaining:0.0} giây" : "chờ", benchSmallDim);
                HudFill(new Rect(row.x + 66, row.y + 52, 222, 5), new Color(.17f, .18f, .11f));
                if (k == 0) HudFill(new Rect(row.x + 66, row.y + 52, 222 * inventory.CraftProgress, 5), HudGold);
            }
            if (queue.Count > 2) GUI.Label(R(1212, 624, 300, 22), $"+{queue.Count - 2} món chờ", benchSmallDim);

            // Just made: the pickup feed, here while the bench is open.
            var made = pickups.Where(p => Age(p) < PickupShown + PickupFade).Take(2).ToList();
            if (made.Count > 0)
            {
                GUI.Label(R(1212, 660, 300, 28), "VỪA XONG", benchHead);
                for (int k = 0; k < made.Count; k++)
                {
                    var p = made[k];
                    float alpha = Age(p) < PickupShown ? 1 : Mathf.Clamp01(1 - (Age(p) - PickupShown) / PickupFade);
                    var row = R(1212, 696 + k * 52, 300, 46);
                    HudFill(row, new Color(.035f, .045f, .03f, .82f * alpha));
                    HudFill(new Rect(row.x, row.y, 3, row.height), new Color(HudGold.r, HudGold.g, HudGold.b, alpha));
                    var old = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
                    HudIcon(new Rect(row.x + 8, row.y + 3, 40, 40), p.id);
                    GUI.Label(new Rect(row.x + 58, row.y + 12, 240, 24), $"<color=#{Hex(HudGold)}><b>+{p.count}</b></color>   {ForestInventory.Name(p.id)}", benchText);
                    GUI.color = old;
                }
            }

            // A material's picture, large, beside what is hovered — never over the rows it explains.
            if (hover != null)
            {
                float hx = hoverRow ? ox + 1196 : ox + 856;
                float hy = Mathf.Clamp(hoverFrom.y - 120, oy + 120, oy + 900 - 430);
                var card = new Rect(hx, hy, 330, 392);
                HudPanel(card);
                var pic = new Rect(card.x + 16, card.y + 16, 298, 190);
                HudFill(pic, new Color(.06f, .07f, .045f, .96f));
                var large = ItemRender(hover);
                if (large != null) GUI.DrawTexture(pic, large, ScaleMode.ScaleToFit); else HudIcon(new Rect(pic.center.x - 80, pic.y + 15, 160, 160), hover);
                GUI.Label(new Rect(card.x + 16, card.y + 214, 298, 30), ForestInventory.Name(hover).ToUpperInvariant(), benchRowName);
                GUI.Label(new Rect(card.x + 16, card.y + 244, 298, 66), ForestInventory.Description(hover), benchSmall);
                GUI.Label(new Rect(card.x + 16, card.y + 312, 298, 44), $"<color=#{Hex(HudGold)}>Lấy ở: {ForestInventory.Source(hover)}</color>", benchSmall);
                GUI.Label(new Rect(card.x + 16, card.y + 356, 298, 22), "Trong túi: " + inventory.Count(hover), benchSmall);
            }
        }
    }
}
