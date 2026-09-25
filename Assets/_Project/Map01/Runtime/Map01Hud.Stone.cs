using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>While a stone is being aimed: who would hear it land, and how to throw or put it away.
    /// The arc and the guards' hearing rings themselves are drawn in the world by Map01StoneThrow.</summary>
    public sealed partial class Map01Hud
    {
        private Map01StoneThrow stoneThrow;

        private void DrawStoneAim(float width, float height)
        {
            if (stoneThrow == null) stoneThrow = GetComponent<Map01StoneThrow>(); // Added by Map01PlayerInteraction.Awake.
            if (stoneThrow == null || !stoneThrow.Aiming) return;
            var plate = new Rect(width / 2 - 300, height - 150, 600, 64);
            HudPanel(plate);
            string who = stoneThrow.WouldHear > 0 ? $"{stoneThrow.WouldHear} lính sẽ nghe thấy và đi kiểm tra" : "chưa lính nào ở đủ gần để nghe";
            GUI.Label(new Rect(plate.x + 16, plate.y + 6, plate.width - 32, 26), "NÉM ĐÁ — " + who, hudGuide);
            GUI.Label(new Rect(plate.x + 16, plate.y + 34, plate.width - 32, 26), $"Thả Q để ném  ·  Chuột phải để cất  ·  Còn {inventory.Count("stone")} viên", hudSmall);
        }
    }
}
