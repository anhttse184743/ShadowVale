using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ShadowVale.ArtPreview
{
    /// <summary>Isolated art review. Never loads or saves a gameplay scene.</summary>
    public static class ResistanceGarrisonPreview
    {
        const string Root = "Assets/_Project/Art/Environment/ResistanceGarrison_v2";
        const string Output = "Docs/Previews/ResistanceGarrison_v2";
        static readonly string[] Names = { "A_ResistanceWorkshop", "C1_QuartermasterDepot", "C2_SignalDetachment", "C3_CommandGarrison" };
        [Serializable] sealed class MaterialList { public MaterialSpec[] items; }
        [Serializable] sealed class MaterialSpec { public string name, albedo, normal, mask; public float smoothness, metallic; }
        [Serializable] sealed class ShotList { public Shot[] items; }
        [Serializable] sealed class Shot { public string root, name; public float fov; public bool cutaway; }
        [Serializable] sealed class ModelRecord { public string name; public int renderers, triangles; }
        [Serializable] sealed class CaptureManifest
        {
            public string unityVersion, graphicsDevice, renderPipeline, capturedUtc;
            public int width = 1920, height = 1080;
            public ModelRecord[] models;
            public Shot[] captures;
        }

        [MenuItem("ShadowVale/Art Preview/Resistance Garrison v2")]
        public static void BuildAndRender()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var specs = JsonUtility.FromJson<MaterialList>(File.ReadAllText(Root + "/materials.json"));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new Exception("URP Lit required for this art preview.");
            var materials = new Dictionary<string, Material>();
            foreach (var spec in specs.items)
            {
                ConfigureTexture(Root + "/" + spec.normal, true, false);
                ConfigureTexture(Root + "/" + spec.mask, false, true);
                ConfigureTexture(Root + "/" + spec.albedo, false, false);
                var path = Root + "/Materials/" + spec.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader) { name = spec.name }; AssetDatabase.CreateAsset(mat, path); }
                mat.SetColor("_BaseColor", Color.white);
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + spec.albedo));
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + spec.normal));
                mat.SetFloat("_BumpScale", .55f);
                mat.SetFloat("_Metallic", spec.metallic);
                mat.SetFloat("_Smoothness", 1);
                mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + spec.mask));
                mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Cull", 0); // Real mesh leaves, canvas and sheet metal are two-sided.
                mat.doubleSidedGI = true; mat.enableInstancing = true;
                if (spec.name == "RG_LampGlass") { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(1, .48f, .12f) * .7f); }
                EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
                materials.Add(spec.name, mat);
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var models = new Dictionary<string, GameObject>();
            var records = new List<ModelRecord>();
            for (int i = 0; i < Names.Length; ++i)
            {
                string path = Root + "/Meshes/" + Names[i] + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new Exception("Missing FBX: " + path);
                if (importer.importCameras || importer.importLights || importer.importAnimation || importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
                {
                    importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    importer.SaveAndReimport();
                }
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                go.name = Names[i]; go.transform.position = new Vector3(i * 700, 0, 0);
                foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                    {
                        string key = m != null ? m.name.Replace(" (Instance)", "") : "";
                        if (!materials.TryGetValue(key, out var material)) throw new Exception("Unmapped material: " + key);
                        return material;
                    }).ToArray();
                    bool ground = renderer.name.StartsWith("Continuous terrain") || renderer.name.StartsWith("Trodden irregular") || renderer.name.StartsWith("Shallow wet");
                    renderer.shadowCastingMode = ground ? ShadowCastingMode.Off : ShadowCastingMode.TwoSided;
                    renderer.receiveShadows = true;
                }
                models.Add(go.name, go);
                int tris = 0;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null)
                        for (int s = 0; s < mf.sharedMesh.subMeshCount; ++s) tris += (int)mf.sharedMesh.GetIndexCount(s) / 3;
                records.Add(new ModelRecord { name = go.name, renderers = go.GetComponentsInChildren<Renderer>().Length, triangles = tris });
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.63f, .67f, .61f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.reflectionIntensity = .55f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .0055f; RenderSettings.fogColor = new Color(.61f, .66f, .62f);
            var skyPath = Root + "/Materials/RG_PreviewSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, skyPath); }
            sky.SetColor("_SkyTint", new Color(.58f, .65f, .7f)); sky.SetColor("_GroundColor", new Color(.28f, .27f, .21f));
            sky.SetFloat("_AtmosphereThickness", 1.2f); sky.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(sky); AssetDatabase.SaveAssetIfDirty(sky); RenderSettings.skybox = sky;
            var sun = new GameObject("Afternoon sun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.color = new Color(1, .91f, .77f); sun.intensity = 2.4f; sun.shadows = LightShadows.Soft;
            sun.shadowCustomResolution = 4096; sun.shadowBias = .018f; sun.shadowNormalBias = .16f;
            sun.transform.rotation = Quaternion.Euler(47, -38, 0); RenderSettings.sun = sun;
            var fill = new GameObject("Sky bounce").AddComponent<Light>(); fill.type = LightType.Directional;
            fill.color = new Color(.76f, .85f, 1); fill.intensity = .75f;
            fill.transform.rotation = Quaternion.Euler(58, 140, 0); fill.shadows = LightShadows.None;
            var camera = new GameObject("Full HD art camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.nearClipPlane = .05f; camera.farClipPlane = 250; camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.Skybox; camera.aspect = 16f / 9f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camera.GetUniversalAdditionalCameraData().antialiasingQuality = AntialiasingQuality.High;
            var profilePath = Root + "/Materials/RG_Tonemapping.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            if (!profile.TryGet<Tonemapping>(out var tone)) tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.ACES;
            if (!profile.TryGet<ColorAdjustments>(out var color)) color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = .15f; color.contrast.value = 7; color.saturation.value = -7;
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            var volume = new GameObject("Preview grading").AddComponent<Volume>(); volume.isGlobal = true; volume.sharedProfile = profile;
            DynamicGI.UpdateEnvironment();
            var ambient = new SphericalHarmonicsL2(); ambient.AddAmbientLight(new Color(.63f, .67f, .61f)); RenderSettings.ambientProbe = ambient;
            var shots = JsonUtility.FromJson<ShotList>(File.ReadAllText(Root + "/shots.json")).items;
            foreach (var shot in shots)
            {
                SelectShot(shot, models, camera);
                LightForRoot(models[shot.root], sun, fill);
                RenderSettings.ambientProbe = ambient;
                Capture(camera, Output + "/Unity_" + shot.name + ".png");
                Debug.Log("GARRISON CAPTURE " + shot.name + " cutaway=" + shot.cutaway);
            }
            foreach (var main in shots.Where(s => s.name.EndsWith("Overview") || s.name == "A_Workshop"))
            {
                SelectShot(main, models, camera);
                foreach (var model in models.Values)
                    foreach (var t in model.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("ROOF /"))) t.gameObject.SetActive(true);
                LightForRoot(models[main.root], sun, fill);
                string scenePath = main.name == "C3_Overview" ? "/ResistanceGarrison_Preview.unity" : "/Preview_" + main.root + ".unity";
                EditorSceneManager.SaveScene(scene, Root + scenePath);
            }
            File.WriteAllText(Output + "/unity_capture_manifest.json", JsonUtility.ToJson(new CaptureManifest
            {
                unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
                renderPipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "Built-in",
                capturedUtc = DateTime.UtcNow.ToString("O"), models = records.ToArray(), captures = shots
            }, true));
            Debug.Log("GARRISON UNITY PREVIEW COMPLETE");
        }

        static void ConfigureTexture(string path, bool normal, bool mask)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new Exception("Missing texture: " + path);
            var expected = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType == expected && importer.sRGBTexture == (!normal && !mask) && importer.maxTextureSize == 2048 &&
                importer.textureCompression == TextureImporterCompression.CompressedHQ && importer.anisoLevel == 8 && importer.mipmapEnabled) return;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !mask; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.anisoLevel = 8; importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        static void LightForRoot(GameObject root, Light sun, Light fill)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            var eye = all.First(t => t.name.EndsWith("Overview_Eye") || t.name == "SHOT_A_Workshop_Eye");
            var target = all.First(t => t.name.EndsWith("Overview_Target") || t.name == "SHOT_A_Workshop_Target");
            var front = eye.position - target.position; front.y = 0; front.Normalize();
            var side = Vector3.Cross(Vector3.up, front).normalized;
            var center = root.transform.position + Vector3.up;
            sun.transform.rotation = Quaternion.LookRotation(center - (center + front * 14 + side * 10 + Vector3.up * 20));
            fill.transform.rotation = Quaternion.LookRotation(center - (center + front * 4 - side * 9 + Vector3.up * 8));
        }
        static void SelectShot(Shot shot, Dictionary<string, GameObject> models, Camera camera)
        {
            foreach (var pair in models) pair.Value.SetActive(pair.Key == shot.root);
            var root = models[shot.root]; var children = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in children.Where(t => t.name.StartsWith("ROOF /"))) t.gameObject.SetActive(!shot.cutaway);
            var eye = children.First(t => t.name == "SHOT_" + shot.name + "_Eye");
            var target = children.First(t => t.name == "SHOT_" + shot.name + "_Target");
            camera.orthographic = false; camera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(shot.fov, 16f / 9f);
            camera.transform.SetPositionAndRotation(eye.position, Quaternion.LookRotation(target.position - eye.position));
        }
        static void Capture(Camera camera, string path)
        {
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                for (int i = 0; i < 4; ++i) camera.Render();
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); }
        }
    }
}
