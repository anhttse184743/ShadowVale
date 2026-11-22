using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>The options page of the title and pause menus. Values live in <see cref="GameOptions"/>.</summary>
    public sealed partial class ForestMenu
    {
        private bool settings;
        private const int SettingRows = 5;
        private static readonly string[] SettingNames = { "ÂM LƯỢNG", "ĐỘ NHẠY CHUỘT", "HIỂN THỊ", "ĐỘ PHÂN GIẢI", "ĐỒNG BỘ DỌC (VSYNC)" };

        /// <summary>The main-menu row "Cài đặt" sits on, so Esc from the page lands back on it.</summary>
        private int SettingsIndex => 3;
        private void OpenSettings() { settings = true; selected = 0; message = null; }
        private void CloseSettings() { settings = false; selected = SettingsIndex; }

        private string SettingValue(int row)
        {
            switch (row) {
                case 0: return Mathf.RoundToInt(GameOptions.Volume * 100) + "%";
                case 1: return GameOptions.Sensitivity.ToString("0.0") + "×";
                case 2: return GameOptions.Fullscreen ? "TOÀN MÀN HÌNH" : "CỬA SỔ";
                case 3: var size = GameOptions.Resolution; return size.x + " × " + size.y;
                default: return GameOptions.VSync ? "BẬT" : "TẮT";
            }
        }

        /// <summary>Steps one option left (-1) or right (+1); on/off options simply flip.</summary>
        private void ChangeSetting(int row, int step)
        {
            switch (row) {
                case 0: GameOptions.SetVolume(GameOptions.Volume + .1f * step); break;
                case 1: GameOptions.SetSensitivity(GameOptions.Sensitivity + .1f * step); break;
                case 2: GameOptions.SetFullscreen(!GameOptions.Fullscreen); break;
                case 3:
                    var sizes = GameOptions.Resolutions(); int at = Mathf.Max(0, sizes.IndexOf(GameOptions.Resolution));
                    GameOptions.SetResolution(sizes[(at + step + sizes.Count) % sizes.Count]); break;
                default: GameOptions.SetVSync(!GameOptions.VSync); break;
            }
        }

        private void SettingsKeys(Keyboard kb)
        {
            if (kb.downArrowKey.wasPressedThisFrame) selected = (selected + 1) % SettingRows;
            if (kb.upArrowKey.wasPressedThisFrame) selected = (selected + SettingRows - 1) % SettingRows;
            if (kb.leftArrowKey.wasPressedThisFrame) ChangeSetting(selected, -1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) ChangeSetting(selected, 1);
        }

        private void DrawSettings()
        {
            Fill(new Rect(35, 55, 480, 245), Ink);
            GUI.Label(new Rect(60, 85, 450, 80), "SHADOWVALE", heading);
            GUI.Label(new Rect(60, 170, 440, 45), "CÀI ĐẶT", heading);
            if (Button(new Rect(60, 230, 420, 55), "QUAY LẠI")) CloseSettings();
            Fill(new Rect(535, 50, 1025, 790), Ink); Frame(new Rect(535, 50, 1025, 790), Gold);
            GUI.Label(new Rect(575, 75, 900, 45), "TUỲ CHỌN TRÒ CHƠI", heading);
            for (int i = 0; i < SettingRows; i++) {
                var r = new Rect(575, 150 + i * 104, 945, 92);
                Fill(r, selected == i ? new Color(.31f, .33f, .18f) : new Color(.12f, .15f, .10f)); Frame(r, selected == i ? Gold : new Color(.3f, .32f, .23f));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) selected = i;
                GUI.Label(new Rect(r.x + 28, r.y + 26, 420, 45), SettingNames[i], heading);
                if (Button(new Rect(r.x + 480, r.y + 16, 70, 60), "◀")) { selected = i; ChangeSetting(i, -1); }
                var old = body.alignment; body.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(r.x + 560, r.y + 16, 280, 60), SettingValue(i), body);
                body.alignment = old;
                if (Button(new Rect(r.x + 850, r.y + 16, 70, 60), "▶")) { selected = i; ChangeSetting(i, 1); }
            }
            if (Button(new Rect(575, 750, 360, 60), "MẶC ĐỊNH")) GameOptions.ResetToDefaults();
            if (Button(new Rect(1235, 750, 285, 60), "QUAY LẠI")) CloseSettings();
            Fill(new Rect(535, 842, 1025, 52), Ink);
            GUI.Label(new Rect(575, 850, 980, 45), "↑ ↓ Chọn     ← → Thay đổi     Esc Quay lại", small);
        }
    }
}
