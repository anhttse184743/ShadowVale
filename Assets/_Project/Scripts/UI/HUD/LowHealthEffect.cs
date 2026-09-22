using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadowVale.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class LowHealthEffect : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField, Range(.01f, 1f)] private float lowThreshold = .3f;
        [SerializeField, Range(.01f, 1f)] private float criticalThreshold = .15f;
        [SerializeField, Range(0f, 1f)] private float maxOpacity = .8f;
        [SerializeField, Min(.1f)] private float pulseFrequency = 1.1f;
        [SerializeField, Min(.01f)] private float fadeSeconds = .3f;
        [SerializeField, Range(0f, 1f)] private float hitFlashOpacity = .45f;
        [SerializeField, Min(.01f)] private float hitFlashSeconds = .35f;

        private RawImage overlay;
        private Texture2D texture;
        private Volume colorVolume;
        private VolumeProfile colorProfile;
        private UniversalAdditionalCameraData cameraData;
        private bool originalPostProcessing;
        private int originalVolumeMask;
        private float previousHealth, flash, opacity, phase;

        public float Opacity => opacity;

        public void Bind(Health target)
        {
            health = target;
            previousHealth = health != null ? health.Current : 0f;
            flash = opacity = phase = 0f;
        }

        private void Awake()
        {
            var go = new GameObject("Low HP • blood edges", typeof(RectTransform), typeof(RawImage));
            go.layer = gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay = go.GetComponent<RawImage>();
            overlay.raycastTarget = false;
            overlay.color = Color.clear;
            texture = CreateBorder();
            overlay.texture = texture;
            var camera = Camera.main;
            if (camera != null)
            {
                cameraData = camera.GetUniversalAdditionalCameraData();
                originalPostProcessing = cameraData.renderPostProcessing;
                originalVolumeMask = cameraData.volumeLayerMask;
                cameraData.renderPostProcessing = true;
                cameraData.volumeLayerMask = originalVolumeMask | (1 << gameObject.layer);
            }
            var volumeObject = new GameObject("Low HP • desaturation");
            volumeObject.layer = gameObject.layer;
            volumeObject.transform.SetParent(transform, false);
            colorVolume = volumeObject.AddComponent<Volume>();
            colorVolume.isGlobal = true;
            colorVolume.priority = 1000f;
            colorVolume.weight = 0f;
            colorProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            colorVolume.sharedProfile = colorProfile;
            colorProfile.Add<ColorAdjustments>().saturation.Override(-100f);
        }

        private void Update()
        {
            if (health == null)
            {
                overlay.color = Color.clear;
                colorVolume.weight = 0f;
                return;
            }
            float current = health.Current;
            if (current < previousHealth) flash = hitFlashOpacity;
            if (current > previousHealth && previousHealth <= 0f)
                flash = opacity = phase = 0f;
            previousHealth = current;

            float dt = Time.deltaTime;
            flash = Mathf.MoveTowards(flash, 0f, dt * hitFlashOpacity / Mathf.Max(.01f, hitFlashSeconds));
            float hp = health.Normalized;
            // At 15% HP the scene is already 75% desaturated; death reaches monochrome.
            float gray = Mathf.Pow(Mathf.Clamp01(1f - hp / Mathf.Max(.01f, lowThreshold)), .42f);
            colorVolume.weight = Mathf.MoveTowards(colorVolume.weight, gray, dt / Mathf.Max(.01f, fadeSeconds));
            float target = hp <= lowThreshold
                ? Mathf.Lerp(.18f, maxOpacity, Mathf.Clamp01(1f - hp / Mathf.Max(.01f, lowThreshold))) : 0f;
            if (!health.IsDead && hp <= Mathf.Min(criticalThreshold, lowThreshold))
            {
                phase += dt * pulseFrequency * Mathf.PI * 2f;
                target *= .8f + .2f * Mathf.Sin(phase);
            }
            opacity = Mathf.MoveTowards(opacity, Mathf.Max(target, flash), dt / Mathf.Max(.01f, fadeSeconds));
            overlay.color = new Color(1f, 1f, 1f, opacity);
        }

        // Generated once: transparent centre and irregular, soft blood-red edges.
        private static Texture2D CreateBorder()
        {
            const int size = 256;
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "LowHealthBorder", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (size - 1f), v = y / (size - 1f);
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float noise = Mathf.PerlinNoise(u * 17f + 7f, v * 17f + 3f);
                float width = .095f + noise * .065f;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - edge / width));
                pixels[y * size + x] = new Color(.28f + noise * .36f, .004f, .008f, alpha);
            }
            result.SetPixels(pixels);
            result.Apply(false, true);
            return result;
        }

        private void OnDisable()
        {
            flash = opacity = phase = 0f;
            if (overlay != null) overlay.color = Color.clear;
            if (colorVolume != null) colorVolume.weight = 0f;
            if (health != null) previousHealth = health.Current;
        }

        private void OnDestroy()
        {
            if (overlay != null) Destroy(overlay.gameObject);
            if (texture != null) Destroy(texture);
            if (colorVolume != null) Destroy(colorVolume.gameObject);
            if (colorProfile != null) Destroy(colorProfile);
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = originalPostProcessing;
                cameraData.volumeLayerMask = originalVolumeMask;
            }
        }
    }
}
