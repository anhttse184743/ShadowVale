using ShadowVale.Gameplay.Skills;
using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed partial class Map01Hud
    {
        private bool skillsTab;
        private int pinnedSkill = -1, pinnedLevel;
        private GUIStyle skillValueStyle;
        private static readonly Color SkillGreen = new(.44f, .68f, .32f);
        private static readonly string[] SkillNames = { "Shooting", "Stealth", "Engineering" };
        private static readonly string[] SkillLabels = { "Bắn súng", "Lén lút", "Kỹ thuật" };
        private const string MissionSkillNotice = "Cấp độ tích lũy được sẽ không được ghi nhận nếu nhiệm vụ thất bại";

        private void DrawInventoryTabs(Rect panel)
        {
            for (int i = 0; i < 2; i++)
            {
                var tab = new Rect(panel.x + 24 + i * 334, 144, 326, 42);
                HudPanel(tab, skillsTab == (i == 1));
                if (GUI.Button(tab, i == 0 ? "KHO ĐỒ" : "KĨ NĂNG", hudKey))
                {
                    skillsTab = i == 1; pinnedSkill = -1;
                }
            }
        }

        private void DrawSkills(Rect panel)
        {
            var skills = mission.Skills;
            if (skills == null) return;
            int hoverSkill = -1, hoverLevel = 0;
            Rect anchor = default, pinnedAnchor = default;
            for (int i = 0; i < 3; i++)
            {
                var kind = (SkillKind)i;
                int level = skills.Level(kind), max = skills.MaxLevel(kind);
                var row = new Rect(panel.x + 20, 205 + i * 174, panel.width - 40, 156);
                HudPanel(row);
                GUI.Label(new Rect(row.x + 18, row.y + 14, 205, 35), SkillNames[i], hudBody);
                GUI.Label(new Rect(row.x + 18, row.y + 52, 205, 30), SkillLabels[i], hudSmall);
                GUI.Label(new Rect(row.x + 18, row.y + 87, 205, 30), $"Cấp {level}/{max}", hudSmall);
                float progress = level == max ? 1 : (skills.Xp(kind) - skills.Threshold(kind, level)) /
                    (float)(skills.Threshold(kind, level + 1) - skills.Threshold(kind, level));
                for (int n = 1; n <= max; n++)
                {
                    var hit = new Rect(row.x + 234 + (n - 1) * 102, row.y + 25, 90, 65);
                    var bar = new Rect(hit.x, hit.y + 9, hit.width, 23);
                    HudFill(bar, new Color(.025f, .04f, .02f));
                    float fill = n <= level ? 1 : n == level + 1 ? Mathf.Clamp01(progress) : 0;
                    if (fill > 0) HudFill(new Rect(bar.x + 3, bar.y + 3, (bar.width - 6) * fill, bar.height - 6), SkillGreen);
                    HudBorder(bar, HudGold);
                    GUI.Label(new Rect(hit.x, hit.y + 34, hit.width, 27), n.ToString(), hudKey);
                    GUI.SetNextControlName($"skill-{i}-{n}");
                    if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
                    {
                        if (pinnedSkill == i && pinnedLevel == n) pinnedSkill = -1;
                        else { pinnedSkill = i; pinnedLevel = n; }
                    }
                    if (hit.Contains(Event.current.mousePosition) || GUI.GetNameOfFocusedControl() == $"skill-{i}-{n}")
                    { hoverSkill = i; hoverLevel = n; anchor = hit; }
                    if (pinnedSkill == i && pinnedLevel == n) pinnedAnchor = hit;
                }
                string xp = level == max ? "TỐI ĐA" : $"{skills.Xp(kind) - skills.Threshold(kind, level)}/{skills.Threshold(kind, level + 1) - skills.Threshold(kind, level)} XP tới nấc {level + 1}";
                GUI.Label(new Rect(row.x + 234, row.y + 104, 420, 30), xp, hudSmall);
            }
            GUI.Label(new Rect(panel.x + 30, 745, panel.width - 60, 50), "Trỏ vào từng nấc để xem thông số · Bấm để ghim\nTab / Esc Đóng", hudSmall);
            if (hoverSkill >= 0) DrawSkillPopout(panel, anchor, hoverSkill, hoverLevel);
            else if (pinnedSkill >= 0) DrawSkillPopout(panel, pinnedAnchor, pinnedSkill, pinnedLevel);
        }

        private void DrawSkillPopout(Rect panel, Rect anchor, int index, int level)
        {
            var kind = (SkillKind)index; var skills = mission.Skills;
            const float popWidth = 626;
            float popHeight = kind == SkillKind.Stealth ? 354 : kind == SkillKind.Shooting ? 306 : 282;
            float y = anchor.y - popHeight - 10;
            if (y < panel.y + 5) y = anchor.yMax + 10;
            var pop = new Rect(Mathf.Clamp(anchor.center.x - popWidth / 2, panel.x + 10, panel.xMax - popWidth - 10),
                Mathf.Clamp(y, panel.y + 5, panel.yMax - popHeight - 5), popWidth, popHeight);
            // An opaque backing prevents rows beneath the popout bleeding through the plate.
            HudFill(pop, new Color(.06f, .075f, .045f, 1)); HudPanel(pop); HudBorder(pop, HudGold, 2);
            HudIcon(new Rect(pop.x + 16, pop.y + 10, 34, 34), index == 0 ? "rifle_standard" : index == 1 ? "knife" : "scrap_metal");
            GUI.Label(new Rect(pop.x + 58, pop.y + 12, pop.width - 76, 30), $"{SkillNames[index]} · Nấc {level - 1} → {level}", hudBody);
            GUI.Label(new Rect(pop.x + 18, pop.y + 48, pop.width - 36, 26),
                skills.Level(kind) >= level ? "ĐÃ ĐẠT" : "XEM TRƯỚC · CHƯA MỞ", hudSmall);
            float before = kind == SkillKind.Engineering ? skills.RepairBonusAt(level - 1) : skills.Multiplier(kind, level - 1);
            float after = kind == SkillKind.Engineering ? skills.RepairBonusAt(level) : skills.Multiplier(kind, level);
            if (kind == SkillKind.Shooting)
            {
                DrawSkillBenefit(new Rect(pop.x + 18, pop.y + 84, 290, 70), "Độ giật", $"Giảm {(1f - after / before) * 100f:0.##}%");
                float reloadBefore = skills.ReloadMultiplierAt(level - 1), reloadAfter = skills.ReloadMultiplierAt(level);
                DrawSkillBenefit(new Rect(pop.x + 326, pop.y + 84, 280, 70), "Tốc độ thay đạn", $"Tăng {(1f - reloadAfter / reloadBefore) * 100f:0.##}%");
                GUI.Label(new Rect(pop.x + 18, pop.y + 165, pop.width - 36, 58), MissionSkillNotice, hudSmall);
            }
            else if (kind == SkillKind.Stealth)
            {
                DrawSkillBenefit(new Rect(pop.x + 18, pop.y + 84, 580, 75), "Tầm ám sát", $"Tăng {(after / before - 1f) * 100f:0.##}%");
                GUI.Label(new Rect(pop.x + 18, pop.y + 165, pop.width - 36, 40), "Dùng vũ khí cận chiến để tiếp cận địch từ phía sau lưng", hudSmall);
                GUI.Label(new Rect(pop.x + 18, pop.y + 211, pop.width - 36, 58), MissionSkillNotice, hudSmall);
            }
            else
            {
                DrawSkillMetric(new Rect(pop.x + 18, pop.y + 84, 290, 75), "Bộ sửa chữa cộng thêm", before * 100, after * 100, "%");
                DrawSkillMetric(new Rect(pop.x + 326, pop.y + 84, 280, 75), "Sửa dã chiến tối đa", skills.FieldRepairCapAt(level - 1) * 100, skills.FieldRepairCapAt(level) * 100, "%");
                var repair = Map01Loot.Load().repair;
                string extra = level == repair.salvageBonusLevel ? " Mở: rã súng thêm 1 kim loại vụn."
                    : level == repair.benchDiscountLevel ? " Mở: sửa ở bàn bớt 1 linh kiện." : "";
                GUI.Label(new Rect(pop.x + 18, pop.y + 165, pop.width - 36, 44), "Luyện bằng cách sửa súng tới đầy 100% (ở bàn chế tạo, hoặc bộ sửa chữa khi đủ trần)." + extra, hudSmall);
            }
            GUI.Label(new Rect(pop.x + 18, pop.y + popHeight - 69, pop.width - 36, 55), SkillCondition(kind, level), hudSmall);
        }

        private string SkillCondition(SkillKind kind, int level)
        {
            var skills = mission.Skills;
            int previous = skills.Threshold(kind, level - 1), required = skills.Threshold(kind, level) - previous;
            int completed = Mathf.Clamp(skills.Xp(kind) - previous, 0, required);
            string action = kind == SkillKind.Shooting ? $"Diệt thêm {required} địch bằng súng"
                : kind == SkillKind.Stealth ? $"Ám sát thêm {required} địch bằng dao"
                : $"Sửa thêm {required} vũ khí tới 100% độ bền";
            return $"Điều kiện: {action}.\nTiến độ: {completed}/{required}.";
        }

        private void DrawSkillMetric(Rect rect, string label, float before, float after, string unit, string format = "0.##")
        {
            DrawSkillBenefit(rect, label, $"{before.ToString(format)}{unit} → {after.ToString(format)}{unit}");
        }

        private void DrawSkillBenefit(Rect rect, string label, string value)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 28), label, hudSmall);
            if (skillValueStyle == null) skillValueStyle = new GUIStyle(hudBody) { normal = { textColor = SkillGreen } };
            GUI.Label(new Rect(rect.x, rect.y + 33, rect.width, 35), value, skillValueStyle);
        }
    }
}
