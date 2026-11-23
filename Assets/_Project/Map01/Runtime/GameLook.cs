using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The film look over both maps: a base grade (Resources/Look/Look_Film) with night, golden-afternoon, dawn and
    /// cutscene layers blended on top, plus the tier's anti-aliasing on the gameplay camera. A map tells it the time of
    /// day with <see cref="SetTime"/>; without that the sun's strength decides night. Off in batch mode (the test runs),
    /// so screenshots and colour checks see the scenes as they were authored.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class GameLook : MonoBehaviour
    {
        private static GameLook instance;
        private static float mapNight, mapGolden, mapDawn;
        private static int mapFrame = -1000;
        private Volume film, night, golden, dawn, cinematic, focus;
        private float nightWeight, goldenWeight, dawnWeight, cutWeight;
        private Camera view;
        private bool searched;
        private Map01Mission mission;
        private Component map2;
        private int tier = -1;
        private WarAmbience war;
        // The scene's own fog, put back when the look is switched off or the scene changes.
        private bool authoredFog, captured, hazing;
        private Color authoredFogColor;
        private float authoredStart, authoredEnd;

        /// <summary>
        /// The haze of a country at war: dust and smoke hang in the air by day, so the far tree lines go grey-ochre within a
        /// couple of hundred metres instead of staying crisp to the horizon (Map 2 authored its fog from 240 m). Linear fog,
        /// the mode both maps already use, so no other shader variants are needed.
        /// </summary>
        private static readonly Color DayHaze = new Color(.6f, .59f, .5f), GoldenHaze = new Color(.66f, .55f, .4f),
            NightHaze = new Color(.1f, .125f, .16f), DawnHaze = new Color(.5f, .4f, .34f);

        /// <summary>Called by a map every frame it knows the time of day (each 0..1).</summary>
        public static void SetTime(float night, float golden, float dawn)
        {
            mapNight = Mathf.Clamp01(night); mapGolden = Mathf.Clamp01(golden); mapDawn = Mathf.Clamp01(dawn); mapFrame = Time.frameCount;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // Off for test runs (their colour checks see the scenes as authored) unless a run asks for it to film the look.
            if (Application.isBatchMode && System.Environment.GetEnvironmentVariable("SHADOWVALE_FILMLOOK") != "1" || instance != null) return;
            var go = new GameObject("ShadowVale Film Look");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameLook>();
        }

        private void Awake()
        {
            film = Layer("Look_Film", 150); night = Layer("Look_Night", 151); golden = Layer("Look_Golden", 152);
            dawn = Layer("Look_Dawn", 153); cinematic = Layer("Look_Cinematic", 154); focus = Layer("Look_CinematicFocus", 155);
            war = gameObject.AddComponent<WarAmbience>();
            SceneManager.sceneLoaded += OnScene;
        }
        private void OnDestroy() { SceneManager.sceneLoaded -= OnScene; if (instance == this) instance = null; }
        private void OnScene(Scene scene, LoadSceneMode mode) { view = null; mission = null; map2 = null; searched = false; tier = -1; mapFrame = -1000; captured = hazing = false; war.Clear(); }

        private Volume Layer(string profile, float priority)
        {
            var go = new GameObject(profile); go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = priority; volume.weight = 0;
            var source = Resources.Load<VolumeProfile>("Look/" + profile);
            if (source != null) volume.sharedProfile = source; else volume.enabled = false;
            return volume;
        }

        private static bool Gameplay(Scene scene) => scene.name == "Map 1" || scene.name == "Map 2" || scene.name.StartsWith("Map00");

        private void LateUpdate()
        {
            bool gameplay = Gameplay(SceneManager.GetActiveScene());
            bool look = gameplay && GameOptions.FilmLook;
            if (gameplay) BindCamera();

            if (gameplay && Time.frameCount - mapFrame < 30) { Blend(ref nightWeight, mapNight); Blend(ref goldenWeight, mapGolden); Blend(ref dawnWeight, mapDawn); }
            else { Blend(ref nightWeight, SunNight()); Blend(ref goldenWeight, 0); Blend(ref dawnWeight, 0); }
            Blend(ref cutWeight, Cinematic() ? 1 : 0, 1.6f);

            film.weight = look ? 1 : 0;
            night.weight = look ? nightWeight : 0;
            golden.weight = look ? goldenWeight : 0;
            dawn.weight = look ? dawnWeight : 0;
            cinematic.weight = look ? cutWeight : 0;
            focus.weight = look && GameOptions.Quality != GameOptions.LowTier ? cutWeight : 0;
            war.Tick(look, view, nightWeight, dawnWeight, cutWeight > .5f);
            if (gameplay) Haze(look);
        }

        private void Haze(bool look)
        {
            if (!captured)
            {
                captured = true; authoredFog = RenderSettings.fog; authoredFogColor = RenderSettings.fogColor;
                authoredStart = RenderSettings.fogStartDistance; authoredEnd = RenderSettings.fogEndDistance;
            }
            if (!look)
            {
                if (hazing) { RenderSettings.fog = authoredFog; RenderSettings.fogColor = authoredFogColor; RenderSettings.fogStartDistance = authoredStart; RenderSettings.fogEndDistance = authoredEnd; }
                hazing = false; return;
            }
            hazing = true;
            var color = Color.Lerp(Color.Lerp(DayHaze, GoldenHaze, goldenWeight), NightHaze, nightWeight);
            color = Color.Lerp(color, DawnHaze, dawnWeight);
            color = Color.Lerp(color, new Color(.58f, .36f, .2f), war.Flash * .55f);   // far shelling lights the haze
            float start = Mathf.Lerp(Mathf.Lerp(30f, 25f, nightWeight), 18f, dawnWeight), end = Mathf.Lerp(Mathf.Lerp(280f, 220f, nightWeight), 200f, dawnWeight);
            RenderSettings.fog = true; RenderSettings.fogColor = color;
            // Never hazier than the scene asked for: a map with closer fog keeps it.
            RenderSettings.fogStartDistance = authoredFog ? Mathf.Min(start, authoredStart) : start;
            RenderSettings.fogEndDistance = authoredFog ? Mathf.Min(end, authoredEnd) : end;
        }

        private static void Blend(ref float value, float target, float speed = .8f) =>
            value = Mathf.MoveTowards(value, target, speed * Time.unscaledDeltaTime);

        /// <summary>Night from the sun when the map does not say: full night below about a quarter of daylight.</summary>
        private static float SunNight()
        {
            var sun = RenderSettings.sun;
            return sun == null || !sun.isActiveAndEnabled ? 0 : Mathf.InverseLerp(.6f, .15f, sun.intensity);
        }

        private bool Cinematic()
        {
            if (!searched) { searched = true; mission = FindFirstObjectByType<Map01Mission>(); if (mission == null) map2 = MapTwoBridge.FindMission?.Invoke(); }
            if (mission != null) return mission.Cinematic;
            var host = MapTwoBridge.Host(map2);
            return host != null && host.Cinematic;
        }

        /// <summary>
        /// The camera needs URP post-processing on for both the look and the tier's anti-aliasing (FXAA / SMAA run in the
        /// post pass). Scripts that switch it for an effect and restore it afterwards find it on again the next frame.
        /// </summary>
        private void BindCamera()
        {
            if (view == null || !view.isActiveAndEnabled) { view = Camera.main; tier = -1; }
            if (view == null || !view.TryGetComponent(out UniversalAdditionalCameraData data)) return;
            data.renderPostProcessing = true;
            if (tier == GameOptions.Quality) return;
            tier = GameOptions.Quality;
            data.antialiasing = tier == GameOptions.LowTier ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = tier == GameOptions.HighTier ? AntialiasingQuality.High : AntialiasingQuality.Medium;
        }
    }
}
