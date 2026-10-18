using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// Writes the beats the guards would walk (Map01PatrolBeats) into the open Map 1 scene as editable
    /// routes: one GameObject per area whose children are the points (Map01PatrolWaypoint: the stop; the
    /// transform's forward: where the guard looks). Move, add or delete points in the scene afterwards;
    /// the layout then uses those instead of the ring laid out at run time. Re-baking an area replaces it.
    /// </summary>
    public static class Map01PatrolBeatBaker
    {
        [MenuItem("ShadowVale/Map 1/Bake patrol beats (kho tiếp tế, trạm radio)")]
        public static void Bake()
        {
            var layout = Object.FindFirstObjectByType<Map01OperationsLayout>();
            if (layout == null) { EditorUtility.DisplayDialog("Patrol beats", "Open Map 1: no Map01OperationsLayout in the scene.", "OK"); return; }
            Undo.RecordObject(layout, "Bake patrol beats");
            layout.supplyBeat = Write(layout, "Patrol beat — supply", Map01PatrolBeats.SupplyBeat(Without(layout, true)));
            layout.radioBeat = Write(layout, "Patrol beat — radio", Map01PatrolBeats.RadioBeat(Without(layout, false)));
            EditorUtility.SetDirty(layout);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            Debug.Log($"[Map01] Patrol beats baked: supply {layout.supplyBeat?.childCount ?? 0} points, radio {layout.radioBeat?.childCount ?? 0} points. Edit them in the scene, then save.");
        }

        /// <summary>The layout with that area's hand-placed route ignored, so baking lays out a fresh ring.</summary>
        private static Map01OperationsLayout Without(Map01OperationsLayout layout, bool supply)
        {
            var old = supply ? layout.supplyBeat : layout.radioBeat;
            if (supply) layout.supplyBeat = null; else layout.radioBeat = null;
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            return layout;
        }

        private static Transform Write(Map01OperationsLayout layout, string name, Map01PatrolBeats.Beat beat)
        {
            if (beat.Count < 3) { Debug.LogWarning($"[Map01] {name}: no ring fits the NavMesh there; nothing baked."); return null; }
            var root = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Bake patrol beats");
            root.SetParent(layout.transform, true);
            for (int i = 0; i < beat.Count; i++)
            {
                var point = new GameObject($"Point {i}").transform;
                point.SetParent(root, false);
                point.SetPositionAndRotation(beat.Points[i], Quaternion.Euler(0, beat.Yaws[i], 0));
                point.gameObject.AddComponent<Map01PatrolWaypoint>().waitSeconds = beat.Stops[i];
            }
            return root;
        }
    }
}
