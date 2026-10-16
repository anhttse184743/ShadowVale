using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadowVale.Map01.Editor
{
    public static class Map01MinimapSurveySetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Maps/Map 1.unity";
        private const string ImagePath = "Assets/_Project/Art/Environment/Map01Detailed/Map01Survey.png";
        private const string ResourceFolder = "Assets/_Project/Map01/Resources/Maps";

        [MenuItem("ShadowVale/Map 1/Bake minimap survey")]
        public static void Bake()
        {
            if (Application.isPlaying || EditorSceneManager.GetActiveScene().path != ScenePath)
                throw new System.InvalidOperationException("Open Map 1 outside Play mode before surveying its boundaries.");
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var walls = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c => c.name == "Boundary").ToArray();
            if (walls.Length != 4) throw new System.InvalidOperationException("The map survey requires the four physical Boundary walls.");
            var westEast = walls.Where(c => c.bounds.size.x < c.bounds.size.z).OrderBy(c => c.bounds.center.x).ToArray();
            var southNorth = walls.Where(c => c.bounds.size.x > c.bounds.size.z).OrderBy(c => c.bounds.center.z).ToArray();
            if (westEast.Length != 2 || southNorth.Length != 2) throw new System.InvalidOperationException("Boundary walls must enclose an X/Z rectangle.");
            var bounds = Rect.MinMaxRect(westEast[0].bounds.max.x, southNorth[0].bounds.max.z,
                westEast[1].bounds.min.x, southNorth[1].bounds.min.z);
            if (Vector2.Distance(bounds.min, mission.mapMin) > .01f || Vector2.Distance(bounds.max, mission.mapMax) > .01f)
                throw new System.InvalidOperationException("Mission map bounds differ from the physical boundaries; align them before surveying.");
            var shelter = GameObject.Find("ShelterShell");
            if (shelter == null) throw new System.InvalidOperationException("Safe camp A was not found.");

            var hidden = new Dictionary<Renderer, bool>();
            void Hide(Transform root) {
                if (root == null) return;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) {
                    if (!hidden.ContainsKey(renderer)) hidden.Add(renderer, renderer.enabled);
                    renderer.enabled = false;
                }
            }
            Hide(mission.player); Hide(mission.hung);
            foreach (var enemy in Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None)) Hide(enemy.transform);
            var pipeline = GraphicsSettings.currentRenderPipeline;
            var drawerProperty = pipeline != null ? pipeline.GetType().GetProperty("gpuResidentDrawerMode") : null;
            var oldDrawer = drawerProperty?.GetValue(pipeline);
            bool oldFog = RenderSettings.fog;
            bool oldAsync = ShaderUtil.allowAsyncCompilation;
            var go = new GameObject("Editor minimap boundary survey");
            var camera = go.AddComponent<Camera>();
            int width = 1100, height = Mathf.RoundToInt(width * bounds.height / bounds.width);
            var rt = RenderTexture.GetTemporary(width, height, 24);
            var active = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                if (oldDrawer != null) drawerProperty.SetValue(pipeline, System.Enum.ToObject(drawerProperty.PropertyType, 0));
                RenderSettings.fog = false;
                ShaderUtil.allowAsyncCompilation = false;
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = bounds.height / 2;
                camera.aspect = bounds.width / bounds.height; camera.nearClipPlane = .1f; camera.farClipPlane = 1200;
                camera.useOcclusionCulling = false;
                camera.transform.SetPositionAndRotation(new Vector3(bounds.center.x, 600, bounds.center.y), Quaternion.Euler(90, 0, 0));
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .14f, .1f);
                camera.cullingMask = ~LayerMask.GetMask("Player", "Enemy", "UI"); camera.allowHDR = false; camera.allowMSAA = false;
                var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false; data.volumeLayerMask = 0;
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                float darkest=1,lightest=0;
                for(int y=0;y<height;y+=31)for(int x=0;x<width;x+=31) {
                    float value=texture.GetPixel(x,y).grayscale;darkest=Mathf.Min(darkest,value);lightest=Mathf.Max(lightest,value);
                }
                if(lightest-darkest<.04f)throw new System.InvalidOperationException("The scene survey rendered blank; keep the previous chart and retry after shader compilation.");
                File.WriteAllBytes(ImagePath, texture.EncodeToPNG());
            }
            finally
            {
                foreach (var entry in hidden) if (entry.Key != null) entry.Key.enabled = entry.Value;
                if (oldDrawer != null) drawerProperty.SetValue(pipeline, oldDrawer);
                RenderSettings.fog = oldFog;
                ShaderUtil.allowAsyncCompilation = oldAsync;
                camera.targetTexture = null; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt);
                if (texture != null) Object.DestroyImmediate(texture); Object.DestroyImmediate(go);
            }
            AssetDatabase.ImportAsset(ImagePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ImagePath);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true; importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            Directory.CreateDirectory(ResourceFolder); AssetDatabase.Refresh();
            string assetPath = ResourceFolder + "/Map01Survey.asset";
            var survey = AssetDatabase.LoadAssetAtPath<Map01MapSurvey>(assetPath);
            if (survey == null) { survey = ScriptableObject.CreateInstance<Map01MapSurvey>(); AssetDatabase.CreateAsset(survey, assetPath); }
            survey.chart = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath); survey.bounds = bounds; survey.safeCamp = shelter.transform.position;
            EditorUtility.SetDirty(survey); AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/MinimapDiscovery");
            File.WriteAllText("Logs/MinimapDiscovery/survey.txt", $"Physical boundaries: {bounds}\nSurvey: {width} x {height}\nSafe camp A: {survey.safeCamp}\nActors hidden: {hidden.Count}\n");
        }
    }
}
