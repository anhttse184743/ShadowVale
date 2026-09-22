using System.IO;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Hand-tuning for where a weapon sits in the fist. Weapons are spawned at runtime, so in the
    /// editor there is nothing to drag — this puts a temporary copy in the hand, lets you move it
    /// with the normal transform gizmo, and writes the result back into
    /// <see cref="WeaponGripConfig"/>, which the builders read. That is what makes a nudge survive
    /// the next rebuild.
    /// <para>
    /// Workflow: <b>Preview Weapon Grip</b> → move/rotate the highlighted weapon in the Scene view
    /// → <b>Save Weapon Grip</b> → <b>Clear Weapon Grip Preview</b>.
    /// </para>
    /// </summary>
    public static class WeaponGripTool
    {
        public const string ConfigPath =
            "Assets/_Project/ScriptableObjects/GameSettings/WeaponGripConfig.asset";

        private const string PreviewPrefix = "__GripPreview_";
        private const string WeaponFolder = "Assets/_Project/Prefabs/Items";

        [MenuItem("ShadowVale/Weapon Grip/Preview Weapon Grip")]
        public static void Preview()
        {
            Transform anchor = FindAnchor();
            if (anchor == null)
            {
                return;
            }

            Clear();
            WeaponGripConfig config = EnsureConfig();
            GameObject first = null;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { WeaponFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid));
                Weapon weapon = prefab != null ? prefab.GetComponent<Weapon>() : null;
                if (weapon == null)
                {
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = PreviewPrefix + weapon.Kind;
                instance.transform.SetParent(anchor, false);
                config.Apply(instance.transform, weapon.Kind);
                // Only one is visible at a time, or they overlap into an unreadable mess.
                instance.SetActive(first == null);
                first ??= instance;
            }

            if (first == null)
            {
                Debug.LogWarning("[Grip] No weapon prefabs found — run 'ShadowVale ▸ Build Weapon Prefabs'.");
                return;
            }

            Selection.activeGameObject = first;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("[Grip] Preview placed. Move/rotate it in the Scene view, then " +
                      "'ShadowVale ▸ Weapon Grip ▸ Save Weapon Grip'. Enable the other preview " +
                      "object in the Hierarchy to tune that weapon.");
        }

        [MenuItem("ShadowVale/Weapon Grip/Save Weapon Grip")]
        public static void Save()
        {
            Transform anchor = FindAnchor();
            if (anchor == null)
            {
                return;
            }

            WeaponGripConfig config = EnsureConfig();
            int saved = 0;

            foreach (Transform child in anchor)
            {
                if (!child.name.StartsWith(PreviewPrefix))
                {
                    continue;
                }

                Weapon weapon = child.GetComponent<Weapon>();
                if (weapon == null)
                {
                    continue;
                }

                config.SetOffset(weapon.Kind, child.localPosition, child.localEulerAngles);
                // Scaling the preview counts as tuning too, so it is saved with the rest.
                config.SetScale(weapon.Kind, child.localScale.x);
                Debug.Log($"[Grip] {weapon.Kind}: position {child.localPosition}, " +
                          $"rotation {child.localEulerAngles}, scale {child.localScale.x:F3}");
                saved++;
            }

            if (saved == 0)
            {
                Debug.LogWarning("[Grip] Nothing to save — run 'Preview Weapon Grip' first.");
                return;
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Grip] Saved {saved} offset(s) to {ConfigPath}. They now survive a rebuild.");
        }

        [MenuItem("ShadowVale/Weapon Grip/Clear Weapon Grip Preview")]
        public static void Clear()
        {
            Transform anchor = FindAnchor(quiet: true);
            if (anchor == null)
            {
                return;
            }

            for (int i = anchor.childCount - 1; i >= 0; i--)
            {
                Transform child = anchor.GetChild(i);
                if (child.name.StartsWith(PreviewPrefix))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        /// <summary>Loads the config, creating it with sensible defaults the first time.</summary>
        public static WeaponGripConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<WeaponGripConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            config = ScriptableObject.CreateInstance<WeaponGripConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Grip] Created {ConfigPath}");
            return config;
        }

        private static Transform FindAnchor(bool quiet = false)
        {
            var player = GameObject.Find("GREYBOX_Sandbox/Player");
            if (player != null)
            {
                foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "WeaponAnchor")
                    {
                        return t;
                    }
                }
            }

            if (!quiet)
            {
                Debug.LogWarning("[Grip] No Player/WeaponAnchor in the open scene — open " +
                                 "Map Test.unity, or run 'ShadowVale ▸ Rebuild Test Map (All)'.");
            }
            return null;
        }
    }
}
