using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// Builds the graphics tiers and the film look (see <see cref="GameLook"/>, <see cref="GameOptions"/>). Idempotent:
    /// re-running rewrites the tier assets and the look profiles from the numbers below.
    ///
    /// Tiers: the existing "PC" quality level (PC_RPAsset) stays level 0 and is the "Cao" tier, so nothing that runs at
    /// the project default changes. "Thấp" and "Trung bình" are appended as levels 1 and 2 with their own URP asset and
    /// renderer; being quality levels, their shader variants are kept in the build.
    /// </summary>
    public static class GraphicsLookSetup
    {
        private const string Rendering = "Assets/_Project/Settings/Rendering/";
        private const string Looks = "Assets/_Project/Map01/Resources/Look/";

        [MenuItem("ShadowVale/Graphics/Build Tiers and Film Look")]
        public static void Run()
        {
            BuildTier("Thấp", "Tier_Low", lodBias: 1f, ssao: false, scale: .8f, upscale: UpscalingFilterSelection.FSR, shadowRes: 1024,
                shadowDistance: 30, cascades: 1, soft: false, lights: 2, lightShadows: false);
            BuildTier("Trung bình", "Tier_Medium", lodBias: 1.5f, ssao: true, scale: 1f, upscale: UpscalingFilterSelection.Auto, shadowRes: 2048,
                shadowDistance: 40, cascades: 2, soft: true, lights: 4, lightShadows: true);
            BuildLooks();
            AssetDatabase.SaveAssets();
            Debug.Log("[GraphicsLookSetup] tiers: " + string.Join(", ", QualitySettings.names));
        }

        private static void BuildTier(string level, string file, float lodBias, bool ssao, float scale, UpscalingFilterSelection upscale,
            int shadowRes, float shadowDistance, int cascades, bool soft, int lights, bool lightShadows)
        {
            string rendererPath = Rendering + file + "_Renderer.asset", assetPath = Rendering + file + "_RPAsset.asset";
            AssetDatabase.DeleteAsset(rendererPath); AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CopyAsset(Rendering + "PC_Renderer.asset", rendererPath);
            AssetDatabase.CopyAsset(Rendering + "PC_RPAsset.asset", assetPath);
            AssetDatabase.ImportAsset(rendererPath); AssetDatabase.ImportAsset(assetPath);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            foreach (var feature in renderer.rendererFeatures.Where(f => f is ScreenSpaceAmbientOcclusion)) feature.SetActive(ssao);
            EditorUtility.SetDirty(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            var so = new SerializedObject(pipeline);
            var list = so.FindProperty("m_RendererDataList"); list.arraySize = 1; list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            so.FindProperty("m_RenderScale").floatValue = scale;
            so.FindProperty("m_UpscalingFilter").intValue = (int)upscale;
            so.FindProperty("m_MainLightShadowmapResolution").intValue = shadowRes;
            so.FindProperty("m_ShadowDistance").floatValue = shadowDistance;
            so.FindProperty("m_ShadowCascadeCount").intValue = cascades;
            so.FindProperty("m_SoftShadowsSupported").boolValue = soft;
            so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = lights;
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = lightShadows;
            so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = lightShadows ? 1024 : 512;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = settings.FindProperty("m_QualitySettings");
            int index = -1;
            for (int i = 0; i < levels.arraySize; i++) if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == level) index = i;
            if (index < 0) { levels.InsertArrayElementAtIndex(levels.arraySize - 1); index = levels.arraySize - 1; } // copies the last level's values
            var entry = levels.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("name").stringValue = level;
            entry.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            entry.FindPropertyRelative("lodBias").floatValue = lodBias;
            entry.FindPropertyRelative("shadowDistance").floatValue = shadowDistance;
            entry.FindPropertyRelative("shadowCascades").intValue = cascades;
            settings.FindProperty("m_CurrentQuality").intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static VolumeProfile Profile(string name)
        {
            string path = Looks + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            if (!AssetDatabase.IsValidFolder(Looks.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/_Project/Map01/Resources", "Look");
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }
        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            component.name = typeof(T).Name; component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        /// <summary>
        /// A war film rather than a postcard: the authored scenes are lime grass, a clean blue sky and pale paths. The grade
        /// takes the greens to olive and the sky to an overcast grey-blue, pulls overall colour well down, adds contrast and a
        /// faint ochre cast, then fine grain and a soft vignette. Night, golden afternoon (Day 3), dawn (end of Night 3) and
        /// cutscenes layer on top with their own weights. The haze that goes with it is set by GameLook (fog).
        /// </summary>
        private static void BuildLooks()
        {
            var film = Profile("Look_Film");
            // Neutral, not ACES: the maps are lit for no tonemapper, and ACES sank the nights to black (measured 27 -> 3 mean luma).
            Add<Tonemapping>(film).mode.Override(TonemappingMode.Neutral);
            var grade = Add<ColorAdjustments>(film);
            grade.postExposure.Override(.15f); grade.contrast.Override(14f); grade.saturation.Override(-26f);
            grade.colorFilter.Override(new Color(1f, .96f, .88f));
            var balance = Add<WhiteBalance>(film); balance.temperature.Override(8f); balance.tint.Override(5f);
            var curves = Add<ColorCurves>(film);
            // Hue runs 0..1 from red: yellow ~.17, green ~.33, cyan ~.5, blue ~.66. .5 leaves a hue alone.
            curves.hueVsSat.Override(new TextureCurve(new[] {
                new Keyframe(0f, .5f), new Keyframe(.08f, .48f), new Keyframe(.16f, .38f), new Keyframe(.22f, .34f), new Keyframe(.33f, .28f),
                new Keyframe(.45f, .3f), new Keyframe(.58f, .22f), new Keyframe(.7f, .28f), new Keyframe(.82f, .5f) }, .5f, true, new Vector2(0, 1)));
            curves.hueVsHue.Override(new TextureCurve(new[] {
                new Keyframe(0f, .5f), new Keyframe(.2f, .5f), new Keyframe(.3f, .485f), new Keyframe(.4f, .49f), new Keyframe(.5f, .5f) }, .5f, true, new Vector2(0, 1)));
            var tones = Add<ShadowsMidtonesHighlights>(film);
            tones.shadows.Override(new Vector4(.95f, 1f, 1.04f, 0f)); tones.highlights.Override(new Vector4(1.04f, 1f, .9f, 0f));
            var split = Add<SplitToning>(film);
            split.shadows.Override(new Color(.36f, .44f, .46f)); split.highlights.Override(new Color(.64f, .55f, .4f)); split.balance.Override(-15f);
            var bloom = Add<Bloom>(film);
            bloom.threshold.Override(1f); bloom.intensity.Override(.3f); bloom.scatter.Override(.62f);
            var vignette = Add<Vignette>(film); vignette.intensity.Override(.26f); vignette.smoothness.Override(.45f);
            var grain = Add<FilmGrain>(film); grain.type.Override(FilmGrainLookup.Medium1); grain.intensity.Override(.2f); grain.response.Override(.75f);

            var night = Profile("Look_Night");
            var nightBalance = Add<WhiteBalance>(night); nightBalance.temperature.Override(-26f); nightBalance.tint.Override(3f);
            var nightGrade = Add<ColorAdjustments>(night);
            nightGrade.postExposure.Override(.45f); nightGrade.contrast.Override(6f); nightGrade.saturation.Override(-30f);
            var nightBloom = Add<Bloom>(night); nightBloom.threshold.Override(.85f); nightBloom.intensity.Override(.6f); nightBloom.scatter.Override(.68f);
            var nightVignette = Add<Vignette>(night); nightVignette.intensity.Override(.28f); nightVignette.smoothness.Override(.5f);

            var golden = Profile("Look_Golden");
            var goldenBalance = Add<WhiteBalance>(golden); goldenBalance.temperature.Override(18f); goldenBalance.tint.Override(3f);
            var goldenGrade = Add<ColorAdjustments>(golden); goldenGrade.colorFilter.Override(new Color(1f, .93f, .8f)); goldenGrade.saturation.Override(-6f);

            var dawn = Profile("Look_Dawn");
            var dawnBalance = Add<WhiteBalance>(dawn); dawnBalance.temperature.Override(22f); dawnBalance.tint.Override(6f);
            var dawnGrade = Add<ColorAdjustments>(dawn);
            dawnGrade.colorFilter.Override(new Color(1f, .86f, .72f)); dawnGrade.contrast.Override(18f); dawnGrade.saturation.Override(-16f);
            var dawnBloom = Add<Bloom>(dawn); dawnBloom.threshold.Override(.85f); dawnBloom.intensity.Override(.6f); dawnBloom.scatter.Override(.72f);

            var cinematic = Profile("Look_Cinematic");
            var cutVignette = Add<Vignette>(cinematic); cutVignette.intensity.Override(.34f); cutVignette.smoothness.Override(.5f);
            var cutGrade = Add<ColorAdjustments>(cinematic); cutGrade.contrast.Override(16f);

            // Medium and High only: a cheap Gaussian blur of the far background in cutscenes.
            var focus = Profile("Look_CinematicFocus");
            var dof = Add<DepthOfField>(focus);
            dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(18f); dof.gaussianEnd.Override(60f);
            dof.gaussianMaxRadius.Override(1f); dof.highQualitySampling.Override(false);

            foreach (var profile in new[] { film, night, golden, dawn, cinematic, focus }) EditorUtility.SetDirty(profile);
        }
    }
}
