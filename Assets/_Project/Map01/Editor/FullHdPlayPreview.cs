using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static class FullHdPlayPreview
    {
        private const string PendingKey = "ShadowVale.FullHdPreview.Pending";
        private static double captureAt;
        static FullHdPlayPreview()
        {
            EditorApplication.delayCall += () => {
                const string request = "Logs/FullHDPreview.request";
                if (!File.Exists(request)) return;
                File.Delete(request); Play();
            };
            EditorApplication.playModeStateChanged += state => {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
                SessionState.EraseBool(PendingKey);
                captureAt = EditorApplication.timeSinceStartup + 3;
                EditorApplication.update += CaptureStatus;
            };
        }

        [MenuItem("ShadowVale/Preview/Chơi Full HD 1920 x 1080")]
        public static void Play()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var groupClass = group.GetType();
            int selected = -1, count = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null);
            for (int i = 0; i < count; i++) {
                var existing = groupClass.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((int)sizeType.GetProperty("width").GetValue(existing) == 1920 && (int)sizeType.GetProperty("height").GetValue(existing) == 1080) { selected = i; break; }
            }
            if (selected < 0) {
                var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
                var size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { Enum.Parse(modeType, "FixedResolution"), (object)1920, 1080, "Full HD" }, null);
                groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { size }); selected = count;
            }
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, selected);
            view.maximized = true; view.Show(); view.Focus(); view.Repaint();
            if (!EditorApplication.isPlaying) {
                SessionState.SetBool(PendingKey, true);
                EditorApplication.isPaused = false; EditorApplication.EnterPlaymode();
            }
        }

        private static void CaptureStatus()
        {
            if (EditorApplication.timeSinceStartup < captureAt) return;
            EditorApplication.update -= CaptureStatus;
            if (!EditorApplication.isPlaying) return;
            Directory.CreateDirectory("Logs/FullHDPreview");
            File.WriteAllText("Logs/FullHDPreview/status.txt", $"Playing: true\nRender resolution: {Screen.width} x {Screen.height}\nQuality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}\n");
            ScreenCapture.CaptureScreenshot("Logs/FullHDPreview/preview.png");
        }
    }
}
