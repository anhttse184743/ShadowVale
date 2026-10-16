using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>The stones' layer of the HUD. While one is being aimed: who would hear it land, and how to
    /// throw or put it away (the arc and the guards' hearing rings themselves are drawn in the world by
    /// Map01StoneThrow). Otherwise: a faint dot over stones lying within a few steps.</summary>
    public sealed partial class Map01Hud
    {
        private Map01StoneThrow stoneThrow;
        private Map01StoneSites stoneSites;
        private Texture2D stoneDot;

        /// <summary>How near stones have to lie before they are pointed out on screen.</summary>
        public const float StoneMarkerRange = 14f;

        /// <summary>
        /// How plainly the dot over a stone site shows, 0 to 1. Fist-sized stones are lost in the undergrowth
        /// from more than a couple of metres, so the ones near enough to walk to are marked — faint at the
        /// edge of the range, plain from a few steps. Nothing with a full bag, or once they are picked up.
        /// </summary>
        public float StoneMarker(ForestPoint point)
        {
            if (point == null || point.used || point.kind != ForestPointKind.Stones || inventory.StoneRoom <= 0) return 0f;
            if (mission.Stopped || mission.InventoryOpen || mission.MapOpen) return 0f;
            float distance = Vector3.Distance(mission.player.position, point.transform.position);
            return Mathf.Clamp01((StoneMarkerRange - distance) / 5f);
        }

        private void DrawStoneMarkers(float width, float height)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (stoneSites == null) stoneSites = GetComponent<Map01StoneSites>(); // Added by Map01Mission.Awake when the map has stone data.
            if (stoneSites == null) return;
            if (stoneDot == null)
                stoneDot = MakeTexture(32, (u, v) =>
                {
                    float d = new Vector2(u - .5f, v - .5f).magnitude * 2;
                    return new Color(1, 1, 1, d > 1 ? 0 : d > .7f ? .95f : .45f); // a ring round a paler middle
                });
            var old = GUI.color;
            for (int i = 0; i < stoneSites.Count; i++)
            {
                var point = stoneSites.Point(i);
                float plain = StoneMarker(point);
                if (plain <= 0) continue;
                var view = mission.gameCamera.WorldToViewportPoint(point.transform.position + Vector3.up * .35f);
                if (view.z <= 0 || view.x < 0 || view.x > 1 || view.y < 0 || view.y > 1) continue;
                GUI.color = new Color(.88f, .85f, .76f, plain * .9f);
                GUI.DrawTexture(new Rect(view.x * width - 7, (1 - view.y) * height - 7, 14, 14), stoneDot);
            }
            GUI.color = old;
        }

        private void DrawStoneAim(float width, float height)
        {
            if (stoneThrow == null) stoneThrow = GetComponent<Map01StoneThrow>(); // Added by Map01PlayerInteraction.Awake.
            if (stoneThrow == null || !stoneThrow.Aiming) return;
            var plate = new Rect(width / 2 - 380, height - 150, 760, 64);
            HudPanel(plate);
            string item = stoneThrow.Item, head;
            if (item == "smoke_bomb") head = $"NÉM BOM KHÓI — mây khói {Map01StoneThrow.SmokeRadius:0} m, {Map01StoneThrow.SmokeSeconds:0} giây, lính không nhìn xuyên qua";
            else if (item == "grenade_f1")
            {
                bool self = Vector3.Distance(mission.player.position, stoneThrow.Landing) <= Map01StoneThrow.WoundRadius;
                head = self ? $"NÉM LỰU ĐẠN F1 — CẨN THẬN: NAM ĐỨNG TRONG TẦM NỔ ({Map01StoneThrow.WoundRadius:0} m)!"
                    : "NÉM LỰU ĐẠN F1 — " + (stoneThrow.WouldHit > 0 ? $"{stoneThrow.WouldHit} lính trong tầm sát thương" : "chưa lính nào trong tầm") + $", tiếng nổ {Map01StoneThrow.GrenadeNoise:0} m";
            }
            else head = "NÉM ĐÁ — " + (stoneThrow.WouldHear > 0 ? $"{stoneThrow.WouldHear} lính sẽ nghe thấy và đi kiểm tra" : "chưa lính nào ở đủ gần để nghe");
            GUI.Label(new Rect(plate.x + 16, plate.y + 6, plate.width - 32, 26), head, hudGuide);
            string left = item == "stone" ? $"Còn {inventory.Stones}/{inventory.StoneCapacity} viên" : $"Còn {inventory.Count(item)} quả";
            GUI.Label(new Rect(plate.x + 16, plate.y + 34, plate.width - 32, 26), $"Thả Q để ném  ·  Chuột phải để cất  ·  Phím 4 đổi đồ ném  ·  {left}", hudSmall);
        }
    }
}
