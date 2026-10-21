using UnityEngine;

namespace ShadowVale.Map01
{
    public static class Map01RifleHud
    {
        static Texture2D m16;

        public static void DrawM16(Rect area, bool selected)
        {
            if (m16 == null) m16 = Resources.Load<Texture2D>("Hud/HotbarIcons/" + Map01M16.Id);
            if (m16 == null) m16 = Resources.Load<Texture2D>("Hud/ItemIcons/" + Map01M16.Id);
            if (m16 == null) return;

            var previous = GUI.color;
            GUI.color = selected ? Color.white : new Color(.59f, .61f, .59f, .8f);
            GUI.DrawTexture(area, m16, ScaleMode.ScaleToFit);
            GUI.color = previous;
        }
    }
}
