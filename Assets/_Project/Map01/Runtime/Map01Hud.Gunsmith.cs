using System.Linq;
using UnityEngine;
using ShadowVale.Gameplay.Skills;
using Gun = ShadowVale.Map01.Map01Inventory.Gun;
using Slot = ShadowVale.Map01.Map01Inventory.Slot;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Gun condition on the HUD, and the bench screen's two tabs for it (the approved designs "Độ bền, sửa chữa, rã súng"
    /// and "Rớt đồ, hòm, tiếp tế căn cứ", 17/10/2026):
    /// <list type="bullet">
    /// <item>A bar under every gun, in the bag, in its slot and on the combat HUD: green, amber under 50%, red under 25%,
    /// HỎNG at 0 (it does not fire until repaired).</item>
    /// <item>SỬA SÚNG: the guns Nam carries; the picked one's condition and how many shots it has left, a bench repair
    /// to 100% for weapon parts (and a gun oil when half is gone), a field repair with a repair kit up to the
    /// Engineering cap, and taking it apart for parts, which asks a second press.</item>
    /// <item>KHO CĂN CỨ (also the supply shelf in base A, once Hùng is home): the logistics points, what each operation
    /// sent, what the store issues and for how much, and the captured guns he could hand in for points.</item>
    /// </list>
    /// Laid out on the bench screen's 1600 x 900 grid: the guns or the points on the left, the work in the middle,
    /// materials or hand-ins on the right.
    /// </summary>
    public sealed partial class Map01Hud
    {
        private const string GunsTab = "guns", StoreTab = "store";
        private int benchGun;
        private string armedKey; private float armedAt = -10f;
        private void Disarm() { armedKey = null; armedAt = -10f; }
        private bool askSalvage; private Gun salvageAsk;
        private GUIStyle bagCondition;

        private static readonly Color ConditionGood = new Color(.72f, .86f, .55f), ConditionWorn = new Color(.93f, .72f, .35f),
            ConditionBad = new Color(.92f, .42f, .3f), ConditionBroken = new Color(.85f, .22f, .18f);

        /// <summary>The bench screen at the base store (the supply shelf's [E]).</summary>
        public void OpenStore() { mission.SetBenchOpen(true); benchGroup = StoreTab; }
        /// <summary>The bench screen at its gun tab.</summary>
        public void OpenGunsmith() { mission.SetBenchOpen(true); benchGroup = GunsTab; }
        /// <summary>The bench tab showing: a recipe group, "guns" or "store".</summary>
        public string BenchTab => benchGroup;

        public static Color ConditionColor(float f) =>
            f <= 0 ? ConditionBroken : f < Map01Inventory.BadBelow ? ConditionBad : f < Map01Inventory.WornBelow ? ConditionWorn : ConditionGood;
        private static string Percent(float f) => (f * 100f).ToString("0") + "%";
        private static void ConditionBar(Rect r, float f)
        {
            HudFill(r, new Color(.09f, .09f, .06f, .92f));
            if (f > 0) HudFill(new Rect(r.x, r.y, Mathf.Max(2f, r.width * Mathf.Clamp01(f)), r.height), ConditionColor(f));
        }
        private static string ConditionWord(float f) =>
            f <= 0 ? "HỎNG — không bắn được" : f < Map01Inventory.BadBelow ? "Mòn nặng — dễ hỏng giữa trận" : f < Map01Inventory.WornBelow ? "Đã mòn" : "Còn tốt";

        /// <summary>A two-press button's first press: armed for three seconds.</summary>
        private bool Armed(string key) => armedKey == key && Time.unscaledTime - armedAt < 3f;
        private void Arm(string key) { armedKey = key; armedAt = Time.unscaledTime; }

        /// <summary>The condition bar and percentage drawn over a gun's picture in the bag or its slot.</summary>
        private void GunConditionOverlay(Rect picture, float f)
        {
            bagCondition ??= new GUIStyle(hudHeading) { fontSize = 14, wordWrap = false, alignment = TextAnchor.LowerRight };
            ConditionBar(new Rect(picture.x + 6, picture.yMax - 9, picture.width - 12, 5), f);
            var old = GUI.color; GUI.color = ConditionColor(f);
            GUI.Label(new Rect(picture.x + 4, picture.yMax - 32, picture.width - 10, 22), f <= 0 ? "HỎNG" : Percent(f), bagCondition);
            GUI.color = old;
        }

        private string WhereIs(Gun gun)
        {
            if (gun.Slot == Slot.Primary) return "Ô 1 · súng chính" + (inventory.IsEquipped(inventory.IdOf(gun)) ? " · đang cầm" : "");
            if (gun.Slot == Slot.Pistol) return "Ô 2 · súng ngắn";
            return "Trong túi · ô " + (gun.Cell + 1);
        }
        private static string Key(Gun gun, string id) => gun + "/" + id;

        // ---- SỬA SÚNG -----------------------------------------------------------------------------------------------

        private void DrawGunsmith(float ox, float oy, Vector2 mouse)
        {
            Rect R(float x, float y, float w, float h) => new Rect(ox + x, oy + y, w, h);
            var guns = inventory.Guns().ToList();
            GUI.Label(R(96, 196, 372, 28), "SÚNG ĐANG MANG", benchHead);
            if (guns.Count == 0)
            {
                GUI.Label(R(96, 232, 372, 120), "Không mang khẩu súng nào. Lính địch gục để lại khẩu AK cạnh xác (đã mòn); hòm trại C1 có K-54, C3 có Mosin.", benchSmallDim);
                DrawRepairSupplies(ox, oy);
                return;
            }
            benchGun = Mathf.Clamp(benchGun, 0, guns.Count - 1);
            for (int k = 0; k < Mathf.Min(guns.Count, 6); k++)
            {
                var g = guns[k]; string id = inventory.IdOf(g); float f = inventory.FractionOf(g);
                var row = R(96, 232 + k * 96, 372, 88);
                HudPanel(row, k == benchGun);
                BenchTileIcon(new Rect(row.x + 10, row.y + 9, 70, 70), id);
                GUI.Label(new Rect(row.x + 92, row.y + 8, 272, 26), ForestInventory.Name(id).ToUpperInvariant(), benchRowName);
                GUI.Label(new Rect(row.x + 92, row.y + 34, 272, 22), WhereIs(g), benchDim);
                ConditionBar(new Rect(row.x + 92, row.y + 66, 196, 8), f);
                GUI.Label(new Rect(row.x + 296, row.y + 58, 70, 24), $"<color=#{Hex(ConditionColor(f))}>{(f <= 0 ? "HỎNG" : Percent(f))}</color>", benchSmallBold);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) { benchGun = k; Disarm(); }
            }
            if (guns.Count > 6) GUI.Label(R(96, 812, 372, 22), $"+{guns.Count - 6} khẩu nữa trong túi", benchSmallDim);

            var gun = guns[benchGun];
            string gid = inventory.IdOf(gun);
            float fr = inventory.FractionOf(gun), max = inventory.MaxCondition(gid), now = fr * max, wear = inventory.WearPerShot(gid);

            // The gun: its picture, its condition, how long it lasts.
            var picture = R(500, 196, 210, 150);
            HudFill(picture, new Color(.06f, .07f, .045f, .96f)); HudBorder(picture, BenchEdge, 1);
            var render = ItemRender(gid);
            if (render != null) GUI.DrawTexture(new Rect(picture.x + 6, picture.y + 6, picture.width - 12, picture.height - 12), render, ScaleMode.ScaleToFit);
            else HudIcon(new Rect(picture.x + 40, picture.y + 10, 130, 130), gid);
            GUI.Label(R(732, 190, 440, 42), ForestInventory.Name(gid).ToUpperInvariant(), benchName);
            GUI.Label(R(732, 232, 440, 24), $"<color=#{Hex(ConditionColor(fr))}><b>{ConditionWord(fr)}</b></color> · {WhereIs(gun)}", benchDim);
            ConditionBar(R(732, 264, 440, 14), fr);
            GUI.Label(R(732, 284, 440, 24), $"Độ bền <b><color=#{Hex(HudPaper)}>{Percent(fr)}</color></b> ({now:0}/{max:0} điểm)", benchDim);
            GUI.Label(R(732, 310, 440, 24), wear > 0
                ? $"Mỗi phát −{wear:0.##} điểm · còn khoảng {Mathf.CeilToInt(now / wear - .001f)} phát tới khi hỏng"
                : "Không mòn khi dùng", benchDim);

            // Bench repair to 100%.
            var (parts, oil) = inventory.BenchCost(gun);
            string benchWhy = inventory.BenchRepairRefusal(gun);
            var card = R(500, 360, 664, 140);
            HudPanel(card);
            GUI.Label(new Rect(card.x + 18, card.y + 12, 460, 26), "SỬA Ở BÀN · LÊN 100%", benchHead);
            if (fr >= .9999f) GUI.Label(new Rect(card.x + 18, card.y + 44, 460, 24), "Súng đầy độ bền, chưa cần sửa.", benchDim);
            else
            {
                float cx = card.x + 18;
                foreach (var (item, need) in new[] { ("weapon_parts", parts), ("gun_oil", oil) })
                {
                    if (need <= 0) continue;
                    int have = inventory.Count(item);
                    BenchTileIcon(new Rect(cx, card.y + 44, 52, 52), item, have >= need);
                    GUI.Label(new Rect(cx + 60, card.y + 46, 170, 22), ForestInventory.Name(item), benchSmallBold);
                    GUI.Label(new Rect(cx + 60, card.y + 70, 170, 22), $"Cần {need} · Có <color=#{Hex(have >= need ? BenchHave : BenchShort)}>{have}</color>", benchSmall);
                    cx += 240;
                }
            }
            GUI.Label(new Rect(card.x + 18, card.y + 106, 470, 24), benchWhy == null
                ? $"<color=#{Hex(BenchDim)}>Mỗi ¼ độ bền thiếu: 1 linh kiện · thiếu từ ½: thêm 1 dầu súng · tính cho Kỹ thuật</color>"
                : $"<color=#{Hex(BenchShort)}>{benchWhy}</color>", benchSmall);
            if (BenchButton(new Rect(card.xMax - 170, card.y + 46, 152, 48), "SỬA ĐẦY", benchVerdict, benchWhy == null, benchWhy == null))
                inventory.BenchRepair(gun);

            // Field repair with a kit.
            float gain = inventory.FieldGain(gun), cap = inventory.FieldCap;
            string fieldWhy = inventory.FieldRepairRefusal(gun);
            card = R(500, 512, 664, 140);
            HudPanel(card);
            GUI.Label(new Rect(card.x + 18, card.y + 12, 460, 26), "SỬA DÃ CHIẾN · BỘ SỬA CHỮA", benchHead);
            BenchTileIcon(new Rect(card.x + 18, card.y + 44, 52, 52), "repair_kit", inventory.Count("repair_kit") > 0);
            GUI.Label(new Rect(card.x + 78, card.y + 46, 400, 22), $"Có {inventory.Count("repair_kit")} bộ · mỗi bộ +{gain / max * 100f:0}% → {Percent(Mathf.Min(cap, fr + gain / max))}", benchSmallBold);
            GUI.Label(new Rect(card.x + 78, card.y + 70, 400, 22), $"Dùng ở đâu cũng được, tối đa {Percent(cap)} (Kỹ thuật nấc {mission.Skills.Level(SkillKind.Engineering)})", benchSmall);
            GUI.Label(new Rect(card.x + 18, card.y + 106, 470, 24), fieldWhy == null
                ? $"<color=#{Hex(BenchDim)}>Trong trận: Tab → chuột phải vào súng → Sửa dã chiến</color>"
                : $"<color=#{Hex(BenchShort)}>{fieldWhy}</color>", benchSmall);
            if (BenchButton(new Rect(card.xMax - 170, card.y + 46, 152, 48), "DÙNG BỘ SỬA", benchVerdict, fieldWhy == null))
                inventory.FieldRepair(gun);

            // Salvage.
            var yields = inventory.SalvageYield(gun);
            string salvageWhy = inventory.SalvageRefusal(gun);
            card = R(500, 664, 664, 150);
            HudPanel(card);
            GUI.Label(new Rect(card.x + 18, card.y + 12, 460, 26), "RÃ LẤY LINH KIỆN", benchHead);
            float yx = card.x + 18;
            foreach (var y in yields)
            {
                BenchTileIcon(new Rect(yx, card.y + 44, 52, 52), y.item);
                GUI.Label(new Rect(yx, card.y + 76, 48, 20), "×" + y.count, benchCount);
                GUI.Label(new Rect(yx - 6, card.y + 98, 64, 20), ForestInventory.Name(y.item), benchCenterSmall);
                yx += 70;
            }
            GUI.Label(new Rect(card.x + 18, card.y + 120, 470, 24),
                $"<color=#{Hex(BenchDim)}>Súng mất hẳn. Còn từ 50%: thêm 1 linh kiện · Kỹ thuật nấc {Map01Loot.Load().repair.salvageBonusLevel}: thêm 1 kim loại vụn</color>", benchSmall);
            string key = Key(gun, gid);
            bool armed = Armed(key);
            if (BenchButton(new Rect(card.xMax - 170, card.y + 46, 152, 48), armed ? "BẤM LẦN NỮA" : "RÃ", benchVerdict, salvageWhy == null, armed))
            {
                if (armed) { inventory.Salvage(gun); Disarm(); benchGun = 0; }
                else { Arm(key); mission.Say($"Rã {ForestInventory.Name(gid).ToLowerInvariant()}? Súng mất hẳn. Bấm RÃ lần nữa để xác nhận.", 3); }
            }

            DrawRepairSupplies(ox, oy);
        }

        /// <summary>Right column of the gun tab: what repairs and salvage use, and what Engineering changes.</summary>
        private void DrawRepairSupplies(float ox, float oy)
        {
            Rect R(float x, float y, float w, float h) => new Rect(ox + x, oy + y, w, h);
            GUI.Label(R(1212, 196, 300, 28), "VẬT TƯ SỬA CHỮA", benchHead);
            string[] supplies = { "weapon_parts", "gun_oil", "repair_kit", "scrap_metal", "scope_lens" };
            for (int k = 0; k < supplies.Length; k++)
            {
                var tile = R(1212 + k % 4 * 78, 232 + k / 4 * 112, 70, 70);
                BenchTileIcon(tile, supplies[k]);
                GUI.Label(new Rect(tile.x, tile.y, tile.width - 4, tile.height - 2), "×" + inventory.Count(supplies[k]), benchCount);
                GUI.Label(new Rect(tile.x - 4, tile.yMax + 2, tile.width + 8, 36), ForestInventory.Name(supplies[k]), benchMatLabel ??= new GUIStyle(benchCenterSmall) { wordWrap = true, fontSize = 12 });
            }
            var skills = mission.Skills;
            int level = skills.Level(SkillKind.Engineering);
            var repair = Map01Loot.Load().repair;
            GUI.Label(R(1212, 470, 300, 28), $"KỸ THUẬT · NẤC {level}", benchHead);
            GUI.Label(R(1212, 504, 296, 250),
                $"Bộ sửa chữa: +{repair.fieldFraction * 100f:0}% + {skills.RepairBonusAt(level) * 100f:0}% thưởng, tối đa {Percent(skills.FieldRepairCap)}.\n" +
                $"Nấc {repair.salvageBonusLevel}: rã súng thêm 1 kim loại vụn {(level >= repair.salvageBonusLevel ? "✓" : "")}\n" +
                $"Nấc {repair.benchDiscountLevel}: sửa ở bàn bớt 1 linh kiện {(level >= repair.benchDiscountLevel ? "✓" : "")}\n" +
                "Luyện: mỗi lần sửa một khẩu tới 100%.\n\n" +
                $"<color=#{Hex(HudGold)}>Súng thu được còn có thể nộp ở KHO CĂN CỨ để đổi điểm hậu cần.</color>", benchSmall);
        }

        // ---- KHO CĂN CỨ -------------------------------------------------------------------------------------------

        private void DrawStore(float ox, float oy)
        {
            Rect R(float x, float y, float w, float h) => new Rect(ox + x, oy + y, w, h);
            var logistics = Map01Logistics.For(mission.MapOne);
            bool inBase = logistics.InBase();

            // Points, and where they came from.
            GUI.Label(R(96, 196, 372, 28), "ĐIỂM HẬU CẦN", benchHead);
            GUI.Label(R(96, 226, 372, 70), logistics.Credit.ToString(), new GUIStyle(benchTitle) { fontSize = 60, normal = { textColor = HudGold } });
            GUI.Label(R(96, 300, 372, 24), inBase ? "Lĩnh hàng ngay tại đây." : "Chỉ lĩnh được ở căn cứ A (bàn chế tạo, kệ tiếp tế).", inBase ? benchDim : benchSmall);
            GUI.Label(R(96, 340, 372, 28), "CẤP TRÊN GỬI KHI XONG", benchHead);
            for (int i = 0; i < logistics.Operations; i++)
            {
                bool done = logistics.Granted(i);
                var row = R(96, 374 + i * 52, 372, 46);
                HudPanel(row, done);
                GUI.Label(new Rect(row.x + 14, row.y + 11, 260, 24), (done ? "✓ " : "○ ") + logistics.OperationName(i), done ? benchSmallBold : benchSmallDim);
                GUI.Label(new Rect(row.xMax - 96, row.y + 11, 84, 24), $"<color=#{Hex(done ? BenchHave : BenchDim)}>+{logistics.OperationCredit(i)}</color>", benchSmallBold);
            }
            GUI.Label(R(96, 594, 372, 120),
                $"Súng thu được đã nộp: {logistics.HandedIn}.\nMỗi khẩu nộp được giá trị của nó, ít đi khi mòn (0%: còn {Map01Loot.Load().logistics.turnInFloor * 100f:0}%).\n" +
                "Kho không tự đầy lại: đạn và thuốc chủ yếu lấy từ địch và hòm trại.", benchSmallDim);

            // What the store issues.
            GUI.Label(R(500, 196, 400, 28), "CẤP PHÁT", benchHead);
            var stock = logistics.Stock.ToList();
            for (int k = 0; k < stock.Count && k < 8; k++)
            {
                var offer = stock[k];
                string why = logistics.IssueRefusal(offer);
                var row = R(500, 232 + k * 72, 664, 64);
                HudPanel(row);
                BenchTileIcon(new Rect(row.x + 8, row.y + 6, 52, 52), offer.item);
                GUI.Label(new Rect(row.x + 72, row.y + 8, 330, 24), $"{ForestInventory.Name(offer.item)} ×{offer.count}", benchMatName);
                GUI.Label(new Rect(row.x + 72, row.y + 34, 330, 22), $"Trong túi: {inventory.Count(offer.item)}", benchSmallDim);
                GUI.Label(new Rect(row.x + 400, row.y + 18, 110, 28), $"<color=#{Hex(logistics.Credit >= offer.price ? HudGold : BenchShort)}>{offer.price} điểm</color>", benchSmallBold);
                if (BenchButton(new Rect(row.xMax - 132, row.y + 10, 120, 44), "LĨNH", benchVerdict, why == null, why == null))
                    logistics.Issue(offer);
            }

            // Captured guns to hand in.
            GUI.Label(R(1212, 196, 300, 28), "NỘP SÚNG THU ĐƯỢC", benchHead);
            var spare = inventory.Guns().Where(g => !inventory.IsEquipped(inventory.IdOf(g)) || g.Slot == null).ToList();
            if (spare.Count == 0)
                GUI.Label(R(1212, 232, 296, 90), "Không có khẩu nào để nộp. Khẩu đang cầm trên tay không nộp được.", benchSmallDim);
            for (int k = 0; k < spare.Count && k < 6; k++)
            {
                var g = spare[k]; string id = inventory.IdOf(g); float f = inventory.FractionOf(g);
                var row = R(1212, 232 + k * 94, 296, 86);
                HudPanel(row);
                BenchTileIcon(new Rect(row.x + 8, row.y + 8, 56, 56), id);
                GUI.Label(new Rect(row.x + 72, row.y + 6, 220, 22), ForestInventory.Name(id), benchSmallBold);
                ConditionBar(new Rect(row.x + 72, row.y + 32, 120, 6), f);
                GUI.Label(new Rect(row.x + 198, row.y + 24, 90, 22), $"<color=#{Hex(ConditionColor(f))}>{(f <= 0 ? "HỎNG" : Percent(f))}</color>", benchSmallBold);
                GUI.Label(new Rect(row.x + 72, row.y + 52, 120, 22), $"<color=#{Hex(HudGold)}>+{inventory.TurnInValue(g)} điểm</color>", benchSmallBold);
                string key = "hand/" + Key(g, id);
                bool armed = Armed(key);
                if (BenchButton(new Rect(row.xMax - 100, row.y + 46, 90, 32), armed ? "CHẮC?" : "NỘP", benchVerdict, inBase, armed))
                {
                    if (armed) { logistics.HandIn(g); Disarm(); }
                    else Arm(key);
                }
            }
        }

        // ---- the salvage question in the bag ----------------------------------------------------------------------

        private void AskSalvage(Gun gun) { askSalvage = true; salvageAsk = gun; menuOpen = false; }

        private void DrawSalvageQuestion(float width, Event ev)
        {
            string id = inventory.IdOf(salvageAsk);
            if (!Map01Inventory.HasCondition(id)) { askSalvage = false; return; }
            var yields = inventory.SalvageYield(salvageAsk);
            var screen = new Rect(EquipPlate(width).x, 60, BagPlate(width).xMax - EquipPlate(width).x, 805);
            HudFill(screen, new Color(0, 0, 0, .5f));
            var box = new Rect(screen.center.x - 280, 320, 560, 230);
            HudPanel(box);
            HudIcon(new Rect(box.x + 24, box.y + 30, 90, 90), id);
            GUI.Label(new Rect(box.x + 130, box.y + 26, box.width - 150, 34), "RÃ LẤY LINH KIỆN?", hudHeading);
            GUI.Label(new Rect(box.x + 130, box.y + 62, box.width - 150, 80),
                $"{ForestInventory.Name(id)} · độ bền {Percent(inventory.FractionOf(salvageAsk))}. Súng mất hẳn, đổi lấy: " +
                string.Join(", ", yields.Select(y => y.count + " " + ForestInventory.Name(y.item).ToLowerInvariant())) + ".", bagLine);
            var yes = new Rect(box.x + 130, box.yMax - 56, 150, 38); var no = new Rect(box.x + 296, box.yMax - 56, 150, 38);
            HudFill(yes, new Color(.2f, .06f, .04f, .9f)); HudBorder(yes, new Color(.85f, .42f, .3f), 2); GUI.Label(yes, "RÃ", new GUIStyle(bagButton) { normal = { textColor = new Color(.95f, .6f, .48f) } });
            HudPanel(no); GUI.Label(no, "HỦY", bagButton);
            if (ev.type == EventType.MouseDown && yes.Contains(ev.mousePosition)) { inventory.Salvage(salvageAsk); askSalvage = false; bagPick = -1; slotPick = null; ev.Use(); }
            else if (ev.type == EventType.MouseDown && no.Contains(ev.mousePosition)) { askSalvage = false; ev.Use(); }
            else if (ev.type == EventType.MouseDown) ev.Use();
        }
    }
}
