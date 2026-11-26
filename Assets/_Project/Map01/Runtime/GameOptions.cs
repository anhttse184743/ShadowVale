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
            WidthKey = "options.width", HeightKey = "options.height", VSyncKey = "options.vsync", QualityKey = "options.quality", LookKey = "options.filmlook";
        /// <summary>Quality levels (ProjectSettings/QualitySettings, built by GraphicsLookSetup): "PC" stays level 0 and is the high tier.</summary>
        public const int HighTier = 0, LowTier = 1, MediumTier = 2;
        /// <summary>Menu order, low to high.</summary>
        public static readonly int[] Tiers = { LowTier, MediumTier, HighTier };
        public const float MinSensitivity = .3f, MaxSensitivity = 2.5f;
        /// <summary>The project's own setting (ProjectSettings/QualitySettings: off), restored by "Mặc định".</summary>
        private const int DefaultVSync = 0;

        public static float Volume => PlayerPrefs.GetFloat(VolumeKey, 1f);
        public static float Sensitivity => PlayerPrefs.GetFloat(SensitivityKey, 1f);
        public static bool Fullscreen => PlayerPrefs.HasKey(FullscreenKey) ? PlayerPrefs.GetInt(FullscreenKey) == 1 : Screen.fullScreenMode != FullScreenMode.Windowed;
        public static bool VSync => PlayerPrefs.HasKey(VSyncKey) ? PlayerPrefs.GetInt(VSyncKey) == 1 : QualitySettings.vSyncCount > 0;
        /// <summary>The active quality level; levels the project does not have (an older settings file) read as high.</summary>
        public static int Quality => QualitySettings.GetQualityLevel();
        public static string QualityName => Quality == LowTier ? "THẤP" : Quality == MediumTier ? "TRUNG BÌNH" : "CAO";
        /// <summary>The film grade and its night / dawn / cutscene layers (<see cref="GameLook"/>).</summary>
        public static bool FilmLook => PlayerPrefs.GetInt(LookKey, 1) == 1;
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
            // A test run keeps the project's default level whatever the player picked in the editor.
            if (PlayerPrefs.HasKey(QualityKey) && !Application.isBatchMode) ApplyQuality(PlayerPrefs.GetInt(QualityKey));
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

        /// <summary>Steps the tier through <see cref="Tiers"/> (-1 lower, +1 higher), wrapping round.</summary>
        public static void StepQuality(int step)
        {
            int at = System.Array.IndexOf(Tiers, Quality); if (at < 0) at = Tiers.Length - 1;
            int level = Tiers[(at + step + Tiers.Length) % Tiers.Length];
            PlayerPrefs.SetInt(QualityKey, level); PlayerPrefs.Save(); ApplyQuality(level);
        }
        public static void SetFilmLook(bool value) { PlayerPrefs.SetInt(LookKey, value ? 1 : 0); PlayerPrefs.Save(); }
        private static void ApplyQuality(int level)
        {
            if (level < 0 || level >= QualitySettings.names.Length) return;
            int vsync = QualitySettings.vSyncCount;   // a level carries its own VSync; keep the player's
            QualitySettings.SetQualityLevel(level, true);
            QualitySettings.vSyncCount = vsync;
        }

        public static void ResetToDefaults()
        {
            foreach (var key in new[] { VolumeKey, SensitivityKey, VSyncKey, QualityKey, LookKey }) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            AudioListener.volume = 1f; ThirdPersonCamera.SensitivityScale = 1f; ApplyQuality(HighTier); QualitySettings.vSyncCount = DefaultVSync;
        }

        private static void ApplyDisplay()
        {
            var size = Resolution;
            Screen.SetResolution(size.x, size.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
    }
}
