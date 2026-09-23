using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>The stealth/scouting layer of the HUD: how aware each nearby guard is, the
    /// binocular view with its logging progress, and the banner when a scouting run fails.</summary>
    public sealed partial class Map01Hud
    {
        private Map01Scouting scouting;
        private Texture2D binocularMask, eyeFill, eyeOutline;
        private static readonly Color Warning = new Color(.9f, .22f, .12f);

        /// <summary>Drawn before the other HUD plates, so they stay readable over the mask.</summary>
        private void DrawScouting(float width, float height, float scale)
        {
            if (scouting == null) scouting = GetComponent<Map01Scouting>(); // Added by Map01Quest.Awake.
            if (scouting == null || mission.Stopped || mission.InventoryOpen || mission.MapOpen) return;
            if (scouting.Binoculars) DrawBinoculars(width, height);
            DrawAwareness(width, height, scale);
            bool failedRecently = Time.time - scouting.FailedAt < 4f;
            if (failedRecently)
            {
                var banner = new Rect(width / 2 - 260, 104, 520, 46);
                HudFill(banner, new Color(.35f, .05f, .03f, .88f)); HudBorder(banner, Warning);
                GUI.Label(banner, "BỊ PHÁT HIỆN — TRINH SÁT THẤT BẠI", hudKey);
            }
            else if (quest.Stage == Map01Quest.ScoutStage && !scouting.Binoculars)
                GUI.Label(new Rect(width / 2 - 220, height - 70, 440, 30), "Giữ [F] dùng ống nhòm  ·  [C] đi khom  ·  nấp bụi để khó bị thấy", hudKey);
        }

        /// <summary>
        /// An eye over each guard who has noticed something: it fills from the bottom, yellow to
        /// red, as his suspicion builds — at least half full while he hunts down a noise — and
        /// once he has spotted Nam it is solid red and throbs.
        /// </summary>
        private void DrawAwareness(float width, float height, float scale)
        {
            if (eyeFill == null) { eyeFill = MakeEye(false); eyeOutline = MakeEye(true); }
            foreach (var enemy in mission.Enemies)
            {
                if (!enemy.Alive || (!enemy.Alerted && enemy.Suspicion < .02f)) continue;
                if (Vector3.Distance(mission.player.position, enemy.transform.position) > 40) continue;
                var screen = mission.gameCamera.WorldToScreenPoint(enemy.transform.position + Vector3.up * 2.6f);
                if (screen.z <= 0) continue;
                var p = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
                if (p.x < 0 || p.x > width || p.y < 0 || p.y > height) continue;
                float level = enemy.Engaged ? 1f : enemy.Alerted ? Mathf.Max(.5f, enemy.Suspicion) : enemy.Suspicion;
                float size = enemy.Engaged ? 50 * (1 + .12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7))) : 44;
                var eye = new Rect(p.x - size / 2, p.y - size / 4, size, size / 2);
                var old = GUI.color;
                GUI.color = new Color(0, 0, 0, .6f);
                GUI.DrawTexture(new Rect(eye.x - 2, eye.y - 2, eye.width + 4, eye.height + 4), eyeFill);
                GUI.color = enemy.Engaged ? Warning : Color.Lerp(new Color(1f, .8f, .3f), Warning, level * level);
                GUI.DrawTextureWithTexCoords(new Rect(eye.x, eye.yMax - eye.height * level, eye.width, eye.height * level), eyeFill, new Rect(0, 0, 1, level));
                GUI.color = new Color(1f, .95f, .82f);
                GUI.DrawTexture(eye, eyeOutline);
                GUI.color = old;
            }
        }

        /// <summary>A 2:1 almond eye: the solid shape, or its outline with iris and pupil.</summary>
        private static Texture2D MakeEye(bool outline)
        {
            const int w = 96, h = 48;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float px = x + .5f - w / 2f, py = y + .5f - h / 2f;
                    // Distance inside the lid edge, in pixels (positive = inside the eye).
                    float lid = 21f * (1 - px * px / (46f * 46f)) - Mathf.Abs(py);
                    float r = Mathf.Sqrt(px * px + py * py);
                    float a;
                    if (!outline) a = Mathf.Clamp01(lid + .5f);
                    else
                    {
                        float edge = Mathf.Clamp01(lid + .5f) * Mathf.Clamp01(4.5f - lid);
                        float iris = Mathf.Clamp01(r - 10.5f) * Mathf.Clamp01(14.5f - r);
                        float pupil = Mathf.Clamp01(7f - r);
                        a = Mathf.Max(edge, Mathf.Max(iris, pupil)) * Mathf.Clamp01(lid + .5f);
                    }
                    pixels[y * w + x] = new Color(1, 1, 1, a);
                }
            texture.SetPixels(pixels); texture.Apply();
            return texture;
        }

        private void DrawBinoculars(float width, float height)
        {
            if (binocularMask == null) binocularMask = MakeBinocularMask();
            // The mask is 2:1; fill whatever the screen shape leaves around it.
            float maskWidth = height * 2, x = (width - maskWidth) / 2;
            GUI.DrawTexture(new Rect(x, 0, maskWidth, height), binocularMask);
            if (x > 0) { HudFill(new Rect(0, 0, x, height), Color.black); HudFill(new Rect(width - x, 0, x, height), Color.black); }

            var plate = new Rect(width / 2 - 250, height - 150, 500, 64);
            HudPanel(plate);
            if (scouting.Sighted != null)
            {
                GUI.Label(new Rect(plate.x + 16, plate.y + 6, plate.width - 32, 26), $"GHI CHÉP VỊ TRÍ DOANH TRẠI  {scouting.RecordProgress * 100:0}%", hudGuide);
                var bar = new Rect(plate.x + 18, plate.y + 38, plate.width - 36, 12);
                HudFill(bar, new Color(0, 0, 0, .6f));
                HudFill(new Rect(bar.x, bar.y, bar.width * scouting.RecordProgress, bar.height), HudGold);
            }
            else if (!scouting.InScanRange)
            {
                // Too far from every camp still to log: say so plainly, the binoculars scan nothing.
                var old = GUI.color;
                GUI.color = new Color(1f, .55f, .35f);
                GUI.Label(new Rect(plate.x + 16, plate.y + 4, plate.width - 32, 28), "CHƯA TỚI PHẠM VI QUÉT", hudGuide);
                GUI.color = old;
                GUI.Label(new Rect(plate.x + 16, plate.y + 32, plate.width - 32, 28),
                    $"Còn ngoài {scouting.ScanRange:0} m — tiến sâu vào khu vực nghi ngờ.", hudSmall);
            }
            else GUI.Label(new Rect(plate.x + 16, plate.y + 8, plate.width - 32, 50),
                "Đã trong phạm vi quét — hướng ống nhòm vào doanh trại, không để cây hay nhà che khuất.", hudSmall);
        }

        /// <summary>Black with two soft-edged round lenses, 2:1.</summary>
        private static Texture2D MakeBinocularMask()
        {
            const int w = 512, h = 256;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Lens centres at a third and two thirds across, radius 0.46 of the height.
                    float px = (x + .5f) / h, py = (y + .5f) / h;
                    float d = Mathf.Min(new Vector2(px - .72f, py - .5f).magnitude, new Vector2(px - 1.28f, py - .5f).magnitude) / .46f;
                    pixels[y * w + x] = new Color(0, 0, 0, Mathf.SmoothStep(0, .94f, (d - .9f) / .12f));
                }
            texture.SetPixels(pixels); texture.Apply();
            return texture;
        }
    }
}
