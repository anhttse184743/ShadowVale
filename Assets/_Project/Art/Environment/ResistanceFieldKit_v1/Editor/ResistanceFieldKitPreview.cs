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
    /// <summary>Imports only the new field-kit assets and renders a separate preview scene.</summary>
    public static class ResistanceFieldKitPreview
    {
        const string Root = "Assets/_Project/Art/Environment/ResistanceFieldKit_v1";
        const string Output = "Docs/Previews/ResistanceFieldKit_v1";
        static readonly string[] Models = { "A_BaseWorkshop", "C1_SupplyOutpost", "C2_RadioOutpost", "C3_CommandOutpost" };

        [Serializable] sealed class MaterialList { public MaterialEntry[] items; }
        [Serializable] sealed class MaterialEntry
        {
            public string name, albedo, normal;
            public float smoothness, metallic;
        }
        [Serializable] sealed class CaptureRecord
        {
            public string model, image;
            public int renderers, triangles;
            public Vector3 boundsSize;
        }
        [Serializable] sealed class CaptureManifest
        {
            public string unityVersion, graphicsDevice, renderPipeline, capturedUtc;
            public CaptureRecord[] captures;
        }

        [MenuItem("ShadowVale/Art Preview/Resistance Field Kit v1")]
        public static void BuildAndRender()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var materialData = JsonUtility.FromJson<MaterialList>(File.ReadAllText(Root + "/materials.json"));
            var materials = new Dictionary<string, Material>();
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            var shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            if (shader == null) throw new Exception("Preview material shader unavailable.");
            foreach (var spec in materialData.items)
            {
                var normalPath = Root + "/" + spec.normal;
                var textureImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (textureImporter == null) throw new Exception("Missing normal map: " + normalPath);
                if (textureImporter.textureType != TextureImporterType.NormalMap)
                {
                    textureImporter.textureType = TextureImporterType.NormalMap;
                    textureImporter.SaveAndReimport();
                }
                var path = Root + "/Materials/" + spec.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = spec.name };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetTexture(urp ? "_BaseMap" : "_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + spec.albedo));
                material.SetColor(urp ? "_BaseColor" : "_Color", Color.white);
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                material.SetFloat("_BumpScale", .5f);
                material.SetFloat("_Metallic", spec.metallic);
                material.SetFloat(urp ? "_Smoothness" : "_Glossiness", spec.smoothness);
                material.EnableKeyword("_NORMALMAP");
                material.enableInstancing = true;
                if (spec.name.Contains("LanternAmber"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", new Color(.65f, .25f, .035f) * 1.2f);
                }
                EditorUtility.SetDirty(material);
                materials.Add(spec.name, material);
            }

            // A new scene only. Build settings, gameplay scenes and shared materials are untouched.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instances = new List<GameObject>();
            for (int i = 0; i < Models.Length; ++i)
            {
                var path = Root + "/Meshes/" + Models[i] + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new Exception("Missing Blender FBX: " + path);
                importer.importCameras = false;
                importer.importLights = false;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) throw new Exception("FBX import failed: " + path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                instance.name = Models[i];
                instance.transform.position = new Vector3(i * 22, 0, 0);
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                {
                    var replacements = renderer.sharedMaterials.Select(m =>
                    {
                        string key = m != null ? m.name.Replace(" (Instance)", "") : "";
                        if (!materials.TryGetValue(key, out var replacement))
                            throw new Exception("Unmapped FBX material '" + key + "' on " + renderer.name);
                        return replacement;
                    }).ToArray();
                    renderer.sharedMaterials = replacements;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                instances.Add(instance);
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.62f, .65f, .61f);
            RenderSettings.ambientSkyColor = new Color(.55f, .61f, .68f);
            RenderSettings.ambientEquatorColor = new Color(.36f, .38f, .31f);
            RenderSettings.ambientGroundColor = new Color(.19f, .16f, .12f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.reflectionIntensity = .35f;
            RenderSettings.fog = false;
            var skyPath = Root + "/Materials/RFK_PreviewSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural")) { name = "RFK_PreviewSky" };
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetColor("_SkyTint", new Color(.58f, .64f, .69f));
            sky.SetColor("_GroundColor", new Color(.23f, .22f, .19f));
            sky.SetFloat("_AtmosphereThickness", .85f);
            sky.SetFloat("_Exposure", 1.0f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            var sun = new GameObject("Preview Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(43, -32, 0);
            sun.intensity = 1.6f;
            sun.color = new Color(1f, .91f, .76f);
            sun.shadows = LightShadows.Soft;
            sun.shadowCustomResolution = 2048;
            sun.shadowBias = .025f;
            sun.shadowNormalBias = .25f;
            RenderSettings.sun = sun;
            var fill = new GameObject("Preview Sky Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(60, 145, 0);
            fill.intensity = .38f;
            fill.color = new Color(.72f, .82f, 1f);
            fill.shadows = LightShadows.None;
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.34f, .38f, .39f);
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 160;
            camera.allowHDR = true;
            if (urp)
            {
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                var volume = new GameObject("Preview tone mapping").AddComponent<Volume>();
                volume.isGlobal = true;
                var profilePath = Root + "/Materials/RFK_PreviewVolume.asset";
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile, profilePath);
                }
                if (!profile.TryGet<Tonemapping>(out var tonemap)) tonemap = profile.Add<Tonemapping>(true);
                tonemap.mode.value = TonemappingMode.ACES;
                if (!profile.TryGet<ColorAdjustments>(out var colors)) colors = profile.Add<ColorAdjustments>(true);
                colors.postExposure.value = .45f;
                colors.contrast.value = 4;
                colors.saturation.value = -3;
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
                volume.sharedProfile = profile;
            }
            DynamicGI.UpdateEnvironment();
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.62f, .65f, .61f));
            RenderSettings.ambientProbe = ambient;
            var captures = new List<CaptureRecord>();
            for (int i = 0; i < instances.Count; ++i)
            {
                var instance = instances[i];
                foreach (var other in instances) other.SetActive(other == instance);
                var origin = instance.transform.position;
                var wall = instance.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith("Repaired plank wall"));
                var back = wall != null ? wall.position - origin : Vector3.forward;
                back.y = 0;
                back.Normalize();
                bool workshop = i == 0;
                var target = origin + Vector3.up * (workshop ? .90f : 1.05f) + back * .25f;
                var position = origin - back * (workshop ? 8.2f : 12.6f) + Vector3.right * (workshop ? 5.8f : 9.0f) + Vector3.up * (workshop ? 4.8f : 8.3f);
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
                camera.fieldOfView = workshop ? 47 : 46;
                sun.transform.rotation = Quaternion.LookRotation(target - (origin - back * 6 + new Vector3(-4, 9, 0)));
                sun.intensity = 2.0f;
                fill.transform.rotation = Quaternion.LookRotation(target - (origin - back * 3 + new Vector3(6, 5, 0)));
                fill.intensity = .85f;
                Frame(camera, instance.GetComponentsInChildren<Renderer>());
                string image = Output + "/Unity_" + Models[i] + ".png";
                Capture(camera, image);
                var renderers = instance.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                int triangles = instance.GetComponentsInChildren<MeshFilter>().Where(m => m.sharedMesh != null).Sum(m => (int)m.sharedMesh.GetIndexCount(0) / 3);
                captures.Add(new CaptureRecord { model = Models[i], image = image, renderers = renderers.Length, triangles = triangles, boundsSize = bounds.size });
                Debug.Log("FIELDKIT CAPTURE " + Models[i] + " bounds=" + bounds.size + " renderers=" + renderers.Length + " image=" + image);
            }
            foreach (var other in instances) other.SetActive(true);
            // Return the saved scene's camera to the workbench, leaving all four previews inspectable.
            var baseRoot = instances[0].transform;
            var baseWall = baseRoot.GetComponentsInChildren<Transform>().First(t => t.name == "Repaired plank wall");
            var baseBack = baseWall.position - baseRoot.position; baseBack.y = 0; baseBack.Normalize();
            camera.transform.position = baseRoot.position - baseBack * 8.2f + new Vector3(5.8f, 4.8f, 0);
            camera.transform.LookAt(baseRoot.position + Vector3.up * .9f + baseBack * .25f);
            camera.fieldOfView = 47;
            Frame(camera, instances[0].GetComponentsInChildren<Renderer>());
            sun.transform.rotation = Quaternion.LookRotation(Vector3.up - (-baseBack * 6 + new Vector3(-4, 9, 0)));
            fill.transform.rotation = Quaternion.LookRotation(Vector3.up - (-baseBack * 3 + new Vector3(6, 5, 0)));
            foreach (var other in instances) other.SetActive(other == instances[0]);
            var bench = baseRoot.GetComponentsInChildren<Transform>().First(t => t.name == "RepairWorkbench");
            var baseRenderers = baseRoot.GetComponentsInChildren<Renderer>();
            foreach (var renderer in baseRenderers) renderer.enabled = renderer.transform.IsChildOf(bench) || renderer.name == "Cutaway earth foundation";
            camera.transform.position = bench.position - baseBack * 2.5f + new Vector3(1.7f, 1.7f, 0);
            camera.transform.LookAt(bench.position + Vector3.up * .63f);
            Frame(camera, bench.GetComponentsInChildren<Renderer>(), 1.22f);
            Capture(camera, Output + "/Unity_WorkbenchDetail.png");
            var chests = baseRoot.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("AmmoChest_open") || t.name.StartsWith("MedicalChest_open") || t.name.StartsWith("ToolChest_open")).ToArray();
            foreach (var renderer in baseRenderers) renderer.enabled = chests.Any(t => renderer.transform.IsChildOf(t)) || renderer.name == "Cutaway earth foundation";
            var chestCenter = chests.Aggregate(Vector3.zero, (sum, t) => sum + t.position) / chests.Length;
            camera.transform.position = chestCenter - baseBack * 3 + new Vector3(1.1f, 2.8f, 0);
            camera.transform.LookAt(chestCenter + Vector3.up * .25f);
            Frame(camera, chests.SelectMany(t => t.GetComponentsInChildren<Renderer>()).ToArray(), 1.15f);
            Capture(camera, Output + "/Unity_ChestDetail.png");
            foreach (var renderer in baseRenderers) renderer.enabled = true;
            foreach (var other in instances) other.SetActive(true);
            camera.transform.position = baseRoot.position - baseBack * 8.2f + new Vector3(5.8f, 4.8f, 0);
            camera.transform.LookAt(baseRoot.position + Vector3.up * .9f + baseBack * .25f);
            Frame(camera, instances[0].GetComponentsInChildren<Renderer>());
            EditorSceneManager.SaveScene(scene, Root + "/ResistanceFieldKit_Preview.unity");
            foreach (var material in materials.Values) AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.SaveAssetIfDirty(sky);
            File.WriteAllText(Output + "/unity_capture_manifest.json", JsonUtility.ToJson(new CaptureManifest
            {
                unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                renderPipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "Built-in",
                capturedUtc = DateTime.UtcNow.ToString("O"),
                captures = captures.ToArray()
            }, true));
            Debug.Log("FIELDKIT UNITY PREVIEW COMPLETE");
        }

        static void Frame(Camera camera, Renderer[] renderers, float margin = 1.09f)
        {
            camera.orthographic = true;
            camera.aspect = 1920f / 1200f;
            var lo = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var hi = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var renderer in renderers)
            {
                var b = renderer.bounds;
                for (int x = 0; x < 2; ++x)
                for (int y = 0; y < 2; ++y)
                for (int z = 0; z < 2; ++z)
                {
                    var p = camera.transform.InverseTransformPoint(new Vector3(x == 0 ? b.min.x : b.max.x, y == 0 ? b.min.y : b.max.y, z == 0 ? b.min.z : b.max.z));
                    lo = Vector2.Min(lo, new Vector2(p.x, p.y));
                    hi = Vector2.Max(hi, new Vector2(p.x, p.y));
                }
            }
            var mid = (lo + hi) * .5f;
            camera.transform.position += camera.transform.right * mid.x + camera.transform.up * mid.y;
            camera.orthographicSize = Mathf.Max((hi.y - lo.y) * .5f, (hi.x - lo.x) / (2 * camera.aspect)) * margin;
        }

        static void Capture(Camera camera, string path)
        {
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(1920, 1200, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(1920, 1200, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                for (int frame = 0; frame < 3; ++frame) camera.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(image);
            }
        }
    }
}
