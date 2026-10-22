using ShadowVale.Gameplay.Enemies.Boss;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The commander's grenades on the HUD (the ring on the ground is Map01GrenadeWarning). The grenade indicator
    /// shooters have used since Call of Duty 2: the grenade's icon on a circle round the crosshair, on the side it
    /// lies, a dart pointing at it and the seconds left on it; red while Nam stands inside its blast, amber while he
    /// is near. Inside, a banner over the crosshair as well. With the bag or the map open only the banner shows.
    /// </summary>
    public sealed partial class Map01Hud
    {
        /// <summary>How far beyond its blast a grenade is still pointed out.</summary>
        public const float GrenadeWarnBeyond = 8f;
        private const float GrenadeCircle = 150f;
        private static readonly Color GrenadeNear = new Color(1f, .7f, .25f, .95f);
        private Texture2D grenadeBadge, grenadeDart;
        private GUIStyle grenadeSeconds, grenadeBanner;

        /// <summary>The grenade that most concerns Nam: one whose blast he stands in before one he is only near,
        /// then the one going off soonest. Null when none is within <see cref="GrenadeWarnBeyond"/> m of its blast.</summary>
        public Grenade GrenadeThreat(out bool inside)
        {
            inside = false;
            Grenade worst = null;
            foreach (var grenade in Grenade.Live)
            {
                if (grenade == null || grenade.Exploded) continue;
                Vector3 off = grenade.Target - mission.player.position; off.y = 0;
                float d = off.magnitude;
                if (d > grenade.Radius + GrenadeWarnBeyond) continue;
                bool within = d <= grenade.Radius;
                if (worst == null || (within && !inside) || (within == inside && grenade.SecondsToBlast < worst.SecondsToBlast))
                { worst = grenade; inside = within; }
            }
            return worst;
        }

        private void DrawGrenadeWarning(float width, float height)
        {
            if (mission.Stopped || mission.player == null) return;
            var grenade = GrenadeThreat(out bool inside);
            if (grenade == null) return;
            grenadeSeconds ??= new GUIStyle(hudHeading) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            grenadeBanner ??= new GUIStyle(hudHeading) { fontSize = 24, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            grenadeBadge ??= MakeTexture(64, (u, v) =>
            {
                float d = new Vector2(u - .5f, v - .5f).magnitude * 2;
                float ring = Mathf.Clamp01(1 - Mathf.Abs(d - .88f) / .1f);
                return d > .99f ? Color.clear : Color.Lerp(new Color(.07f, .07f, .05f, .88f), Color.white, ring);
            });
            // The compass's notched dart, in white so it takes the warning's colour.
            grenadeDart ??= MakeTexture(64, (u, v) =>
            {
                var p = new Vector2(u - .5f, v - .5f);
                Vector2 tip = new Vector2(0, .36f), left = new Vector2(-.27f, -.22f), right = new Vector2(.27f, -.22f), notch = new Vector2(0, -.06f);
                return InTriangle(p, tip, left, notch) || InTriangle(p, tip, notch, right) ? Color.white : Color.clear;
            });

            float left = grenade.SecondsToBlast;
            float burnt = grenade.TotalSeconds > 0 ? Mathf.Clamp01(1f - left / grenade.TotalSeconds) : 1f;
            float beat = .6f + .4f * Mathf.Abs(Mathf.Sin(Time.time * Mathf.Lerp(5f, 16f, burnt)));
            Color colour = inside ? Map01GrenadeWarning.DangerRed : GrenadeNear;
            var old = GUI.color;
            var centre = new Vector2(width / 2, height / 2);

            if (!mission.InventoryOpen && !mission.MapOpen && !mission.BenchOpen && mission.gameCamera != null)
            {
                // Where it lies, turned into the camera's frame: straight ahead is up the screen.
                Vector3 off = grenade.Target - mission.player.position; off.y = 0;
                Vector3 ahead = mission.gameCamera.transform.forward; ahead.y = 0;
                float angle = off.sqrMagnitude < .25f ? 180f : Vector3.SignedAngle(ahead, off, Vector3.up);
                var toward = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), -Mathf.Cos(angle * Mathf.Deg2Rad));
                var at = centre + toward * GrenadeCircle;
                var badge = new Rect(at.x - 30, at.y - 30, 60, 60);
                GUI.color = new Color(colour.r, colour.g, colour.b, beat);
                GUI.DrawTexture(badge, grenadeBadge);
                var dartAt = at + toward * 44;
                var canvas = GUI.matrix;
                GUI.matrix = Map02Navigation.RotateUi(canvas, dartAt, angle);
                GUI.DrawTexture(new Rect(dartAt.x - 15, dartAt.y - 15, 30, 30), grenadeDart);
                GUI.matrix = canvas;
                GUI.color = old;
                HudIcon(new Rect(badge.x + 12, badge.y + 12, 36, 36), "grenade_f1");
                var seconds = new Rect(at.x - 34, badge.yMax + 2, 68, 26);
                HudFill(seconds, new Color(.03f, .03f, .02f, .8f));
                GUI.color = colour;
                GUI.Label(seconds, $"{left:0.0}s", grenadeSeconds);
                GUI.color = old;
            }

            if (!inside) return;
            var banner = new Rect(centre.x - 260, centre.y - GrenadeCircle - 104, 520, 44);
            HudFill(banner, new Color(.05f, .02f, .015f, .82f));
            HudBorder(banner, new Color(colour.r, colour.g, colour.b, beat), 2);
            GUI.color = new Color(1f, .86f, .8f, 1f);
            GUI.Label(banner, $"LỰU ĐẠN!  CHẠY RA KHỎI VÒNG ĐỎ · {left:0.0}s", grenadeBanner);
            GUI.color = old;
        }
    }
}
