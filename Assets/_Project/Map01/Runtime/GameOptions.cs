using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The player's options from the title and pause menus (volume, mouse, display). Kept in PlayerPrefs, not in a
    /// save slot, so every map and every save shares them. Only a value the player has changed is applied over the
    /// project's defaults.
    /// </summary>
    public static class GameOptions
    {
        private const string VolumeKey = "options.volume", SensitivityKey = "options.sensitivity", FullscreenKey = "options.fullscreen",
            WidthKey = "options.width", HeightKey = "options.height", VSyncKey = "options.vsync";
        public const float MinSensitivity = .3f, MaxSensitivity = 2.5f;
        /// <summary>The project's own setting (ProjectSettings/QualitySettings: off), restored by "Mặc định".</summary>
        private const int DefaultVSync = 0;

        public static float Volume => PlayerPrefs.GetFloat(VolumeKey, 1f);
        public static float Sensitivity => PlayerPrefs.GetFloat(SensitivityKey, 1f);
        public static bool Fullscreen => PlayerPrefs.HasKey(FullscreenKey) ? PlayerPrefs.GetInt(FullscreenKey) == 1 : Screen.fullScreenMode != FullScreenMode.Windowed;
        public static bool VSync => PlayerPrefs.HasKey(VSyncKey) ? PlayerPrefs.GetInt(VSyncKey) == 1 : QualitySettings.vSyncCount > 0;
        public static Vector2Int Resolution => PlayerPrefs.HasKey(WidthKey)
            ? new Vector2Int(PlayerPrefs.GetInt(WidthKey), PlayerPrefs.GetInt(HeightKey)) : new Vector2Int(Screen.width, Screen.height);

        /// <summary>Distinct sizes the display offers, smallest first; the current size when it offers none (the editor).</summary>
        public static List<Vector2Int> Resolutions()
        {
            var sizes = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Where(s => s.x >= 1024 && s.y >= 576)
                .Distinct().OrderBy(s => s.x * s.y).ToList();
            var current = Resolution;
            if (!sizes.Contains(current)) { sizes.Add(current); sizes = sizes.OrderBy(s => s.x * s.y).ToList(); }
            return sizes;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplySaved()
        {
            AudioListener.volume = Volume;
            ThirdPersonCamera.SensitivityScale = Sensitivity;
            if (PlayerPrefs.HasKey(VSyncKey)) QualitySettings.vSyncCount = VSync ? 1 : 0;
            if (!Application.isEditor && (PlayerPrefs.HasKey(WidthKey) || PlayerPrefs.HasKey(FullscreenKey))) ApplyDisplay();
        }

        public static void SetVolume(float value) { value = Mathf.Clamp01(Mathf.Round(value * 10) / 10); PlayerPrefs.SetFloat(VolumeKey, value); AudioListener.volume = value; PlayerPrefs.Save(); }
        public static void SetSensitivity(float value)
        {
            value = Mathf.Clamp(Mathf.Round(value * 10) / 10, MinSensitivity, MaxSensitivity);
            PlayerPrefs.SetFloat(SensitivityKey, value); ThirdPersonCamera.SensitivityScale = value; PlayerPrefs.Save();
        }
        public static void SetFullscreen(bool value) { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); PlayerPrefs.Save(); ApplyDisplay(); }
        public static void SetResolution(Vector2Int size) { PlayerPrefs.SetInt(WidthKey, size.x); PlayerPrefs.SetInt(HeightKey, size.y); PlayerPrefs.Save(); ApplyDisplay(); }
        public static void SetVSync(bool value) { PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0); QualitySettings.vSyncCount = value ? 1 : 0; PlayerPrefs.Save(); }

        public static void ResetToDefaults()
        {
            foreach (var key in new[] { VolumeKey, SensitivityKey, VSyncKey }) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            AudioListener.volume = 1f; ThirdPersonCamera.SensitivityScale = 1f; QualitySettings.vSyncCount = DefaultVSync;
        }

        private static void ApplyDisplay()
        {
            var size = Resolution;
            Screen.SetResolution(size.x, size.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
    }
}
