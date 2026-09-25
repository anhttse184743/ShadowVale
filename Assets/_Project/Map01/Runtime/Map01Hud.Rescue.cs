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
            if (rescue.HungDown)
            {
                HudPanel(new Rect(width / 2 - 300, 320, 600, 185));
                GUI.Label(new Rect(width / 2 - 275, 345, 550, 75), "HÙNG ĐÃ HY SINH", hudCenter);
                GUI.Label(new Rect(width / 2 - 270, 437, 540, 50), "Enter Làm lại đoạn giải cứu  ·  F9 Tải bản lưu  ·  Esc Menu", hudSmall);
                return;
            }
            // Only once it matters: he has been hit, or the squad is fighting.
            if (!rescue.Exposed || mission.InventoryOpen || mission.MapOpen) return;
            if (rescue.HungHealth >= rescue.HungMaxHealth && !rescue.Squad.Any(g => g.Alive && g.Engaged)) return;
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
