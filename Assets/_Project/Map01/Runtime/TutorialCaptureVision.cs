using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01
{
    /// <summary>A private, temporary camera volume. Never edits a shared pipeline/profile.</summary>
    internal sealed class TutorialCaptureVision : IDisposable
    {
        private const int Layer = 30;
        private readonly UniversalAdditionalCameraData camera;
        private readonly bool postProcessing;
        private readonly CameraOverrideOption depth;
        private readonly LayerMask mask;
        private readonly Volume volume;
        private readonly VolumeProfile profile;
        private readonly DepthOfField blur;
        private readonly Vignette vignette;
        private readonly MotionBlur motion;
        private bool active;
        public float Amount { get; private set; }
        public TutorialCaptureVision(Camera target, VolumeProfile source)
        {
            if (source == null) throw new InvalidOperationException("Build TutorialCapture assets before playing concussion effects.");
            camera = target.GetUniversalAdditionalCameraData();
            postProcessing = camera.renderPostProcessing; depth = camera.requiresDepthOption; mask = camera.volumeLayerMask;
            var go = new GameObject("Tutorial concussion vision") { layer = Layer, hideFlags = HideFlags.DontSave };
            go.transform.SetParent(target.transform, false);
            volume = go.AddComponent<Volume>(); volume.enabled = false; volume.isGlobal = true; volume.priority = 2000;
            volume.sharedProfile = source; profile = volume.profile; profile.hideFlags = HideFlags.DontSave;
            profile.TryGet(out blur); profile.TryGet(out vignette); profile.TryGet(out motion);
        }
        public void Set(float amount, float shake)
        {
            Amount = Mathf.Clamp01(amount);
            if (Amount <= 0) return;
            if (!active) {
                active = true; camera.renderPostProcessing = true; camera.requiresDepthOption = CameraOverrideOption.On;
                camera.volumeLayerMask = mask.value | (1 << Layer); volume.weight = 1; volume.enabled = true;
            }
            blur.focalLength.value = Mathf.Lerp(1, 75, Amount); vignette.intensity.value = .45f * Amount;
            motion.intensity.value = .35f * Mathf.Clamp01(shake);
        }
        public void Dispose()
        {
            Amount = 0;
            if (volume != null) { volume.enabled = false; Object.Destroy(volume.gameObject); }
            if (profile != null) Object.Destroy(profile);
            if (active && camera != null) { camera.renderPostProcessing = postProcessing; camera.requiresDepthOption = depth; camera.volumeLayerMask = mask; }
        }
    }
}
