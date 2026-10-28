using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static class Map02ScoutPoseValidation
    {
        const string Request = "Temp/Map02ScoutPose.request";
        static Map02ScoutPoseValidation() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            GameObject root = null;
            Directory.CreateDirectory("Logs/Map02Chapter/TamPose");
            try
            {
                root = new GameObject("Scout pose validation");
                var actor = root.AddComponent<Map02ChapterActor>();
                var source = Resources.Load<GameObject>("Characters/Map02Roles/Tam");
                var model = Object.Instantiate(source, root.transform);
                actor.Bind(model.transform);
                var original = model.GetComponentsInChildren<Renderer>();
                actor.SetInjured(true);
                var pose = root.GetComponent<Map02ScoutInjuredVisual>();
                if (pose == null || !pose.Injured || original.Any(r => r.enabled)) throw new Exception("Standing visual was not replaced.");
                var shown = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                if (shown.Length == 0 || shown.Any(r => r.sharedMaterials.Any(m => m == null))) throw new Exception("Missing pose mesh or materials.");
                var bounds = shown[0].bounds;
                foreach (var r in shown) bounds.Encapsulate(r.bounds);
                if (bounds.size.y > 1.3f || bounds.size.y < .3f || bounds.size.z < .7f) throw new Exception("Unexpected injured pose orientation/scale: " + bounds);
                actor.Follow(root.transform, 1.35f);
                if (pose.Injured || original.Any(r => !r.enabled)) throw new Exception("Escort did not restore standing visual.");
                actor.SetInjured(true); actor.SetInjured(false);
                if (pose.Injured || original.Any(r => !r.enabled)) throw new Exception("Repeated restore failed.");
                File.WriteAllText("Logs/Map02Chapter/TamPose/validation.txt", "PASS: imported approved pose, original materials, lying bounds=" + bounds + "; escort and repeated state restore.");
            }
            catch (Exception e) { File.WriteAllText("Logs/Map02Chapter/TamPose/validation.txt", "FAIL: " + e); Debug.LogException(e); }
            finally { if (root != null) Object.DestroyImmediate(root); }
        }
    }
}
