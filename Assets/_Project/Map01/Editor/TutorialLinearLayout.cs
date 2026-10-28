using System;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    /// <summary>Places tutorial objects on the existing map; never reshapes terrain or moves scenery.</summary>
    public static class TutorialLinearLayout
    {
        public static Vector3 Forward(TutorialSceneSetup.Layout l) => Vector3.ProjectOnPlane(l.routeEnd - l.routeStart, Vector3.up).normalized;
        public static float Along(Vector3 p, TutorialSceneSetup.Layout l) => Vector3.Dot(p - l.routeStart, Forward(l));
        public static float Across(Vector3 p, TutorialSceneSetup.Layout l) => Vector3.Dot(p - l.routeStart, Vector3.Cross(Vector3.up, Forward(l)));
        public static float Length(TutorialSceneSetup.Layout l) => Vector3.ProjectOnPlane(l.routeEnd - l.routeStart, Vector3.up).magnitude;

        public static void EnsureKnifeBush(GameObject root)
        {
            var anchor = root.transform.Find("Knife bush");
            var point = anchor.GetComponent<ForestPoint>(); if (point == null) point = anchor.gameObject.AddComponent<ForestPoint>();
            point.id = "tutorial_knife_bush"; point.label = "Bụi cây ẩn nấp"; point.kind = ForestPointKind.Hide; point.radius = 1.55f;
            if (anchor.childCount != 0) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/ForestV2/Prefabs/CoverShrub_A.prefab");
            if (prefab == null) throw new InvalidOperationException("The tutorial hiding bush prefab is missing.");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
            visual.transform.localPosition = Vector3.zero; visual.transform.localScale = Vector3.one;
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        }

        public static void ConformRoot(GameObject root, TutorialSceneSetup.Layout l)
        {
            foreach (var target in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Lesson ") || t.name == "Knife bush" || t.name == "Knife guard" || t.name == "Shooting guard" || t.name.StartsWith("Tutorial squad ") || t.name.StartsWith("Capture patrol ") || t.name == "Capture end" || t.name == "Dry branch trap" || t.name == "Supply cache")) {
                target.position = TutorialSceneSetup.Ground(target.position) + (target.name == "Supply cache" ? Vector3.up * .5f : Vector3.zero);
            }
            EnsureKnifeBush(root);
        }

        public static void ExcludeOutsideCorridor(Transform root, TutorialSceneSetup.Layout l)
        {
            var forward = Forward(l); var right = Vector3.Cross(Vector3.up, forward);
            float end = Length(l) + 6;
            void Volume(Vector3 center, Vector3 size) {
                var go = new GameObject("Tutorial nav exclusion", typeof(NavMeshModifierVolume)); go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(new Vector3(center.x, 15, center.z), Quaternion.LookRotation(forward));
                var volume = go.GetComponent<NavMeshModifierVolume>(); volume.size = size; volume.area = 1;
            }
            foreach (float side in new[] { -1f, 1f }) Volume(l.routeStart + forward * (end / 2) + right * side * (l.routeHalfWidth + 125), new Vector3(250, 80, 320));
            Volume(l.routeStart - forward * 127, new Vector3(320, 80, 250));
            Volume(l.routeStart + forward * (end + 125), new Vector3(320, 80, 250));
        }
    }
}
