using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>The rescue's layer of the HUD: Hùng's health while he is in the line of fire, and
    /// the panel when he falls.</summary>
    public sealed partial class Map01Hud
    {
        private Map01Rescue rescue;

        private void DrawRescue(float width)
        {
            if (rescue == null) rescue = GetComponent<Map01Rescue>(); // Added by Map01Quest.Awake.
            if (rescue == null) return;
            if (mission.InventoryOpen || mission.MapOpen) return;
            if (quest.Stage == Map01Quest.RescueStage && !rescue.Failed) {
                var info = new Rect(28, objectiveBottom + 12, 370, 65);
                HudPanel(info);
                GUI.Label(new Rect(info.x+12,info.y+8,345,24), $"LÍNH CÒN LẠI: {rescue.Remaining}/4", hudKey);
                GUI.Label(new Rect(info.x+12,info.y+34,345,25), rescue.OverseerAlive ? "Lính giám sát còn sống — giữ im lặng" : "Đã hạ lính giám sát — có thể giao chiến", hudSmall);
                return;
            }
            if (rescue.HungDown)
            {
                HudPanel(new Rect(width / 2 - 300, 320, 600, 185));
                GUI.Label(new Rect(width / 2 - 275, 345, 550, 75), "HÙNG ĐÃ HY SINH", hudCenter);
                GUI.Label(new Rect(width / 2 - 270, 415, 540, 30), rescue.FailureReason ?? "Giải cứu thất bại.", hudSmall);
                GUI.Label(new Rect(width / 2 - 270, 453, 540, 50), "Enter Thử lại checkpoint  ·  F9 Tải bản lưu  ·  Esc Menu", hudSmall);
                return;
            }
            // Only once it matters: he has been hit, or the squad is fighting.
            if (!rescue.Exposed || mission.InventoryOpen || mission.MapOpen) return;
            if (quest.Stage != Map01Quest.EscortStage) return;
            var plate = new Rect(28, objectiveBottom + 12, 370, 50);
            HudPanel(plate);
            GUI.Label(new Rect(plate.x + 12, plate.y + 12, 80, 26), "HÙNG", hudKey);
            var bar = new Rect(plate.x + 92, plate.y + 18, plate.width - 110, 14);
            HudFill(bar, new Color(0, 0, 0, .6f));
            float left = rescue.HungHealth / rescue.HungMaxHealth;
            bool justHit = Time.time - rescue.HungHitAt < .25f;
            HudFill(new Rect(bar.x, bar.y, bar.width * left, bar.height), justHit ? Color.white : Color.Lerp(Warning, new Color(.45f, .75f, .35f), left));
        }
    }
}
