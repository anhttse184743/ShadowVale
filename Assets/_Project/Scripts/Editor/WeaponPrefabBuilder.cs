using System.IO;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Turns the raw weapon models in <c>Art/Items</c> into usable weapon prefabs. The imported
    /// meshes come in at arbitrary scale, orientation and pivot, so each one is measured, scaled to
    /// a real-world length, rotated to point down +Z and shifted so the grip sits at the prefab
    /// origin — which is what lets it be parented straight onto a hand bone.
    /// Idempotent: prefabs are rebuilt from the source models on every run.
    /// Menu <b>ShadowVale ▸ Build Weapon Prefabs</b>.
    /// <para>
    /// The grip fractions below are eyeballed. Nudge the model child inside the prefab to line it
    /// up with the hand — the rest of the pipeline reads the prefab, not these numbers.
    /// </para>
    /// </summary>
    public static class WeaponPrefabBuilder
    {
        private const string OutputFolder = "Assets/_Project/Prefabs/Items";

        private readonly struct Spec
        {
            public readonly string ModelPath;
            public readonly string PrefabName;
            public readonly WeaponKind Kind;

            /// <summary>Real-world length in metres along the weapon's long axis.</summary>
            public readonly float TargetLength;

            /// <summary>Where the hand grips, as a fraction from the back end to the muzzle/tip.</summary>
            public readonly float GripFraction;

            /// <summary>
            /// Height of the grip as a fraction from the lowest point up. A rifle is held by a
            /// pistol grip well below the receiver, so this is small; a knife's handle is on the
            /// blade axis, so it is centred.
            /// </summary>
            public readonly float GripHeightFraction;

            /// <summary>
            /// True when the model's long axis points the wrong way after alignment, so the
            /// muzzle/tip ends up behind the character. Both current models need it: the AK's
            /// "r_barrelEnd" mesh sits at the low end of its long axis while "l_stockWood" sits at
            /// the high end, and the knife's handle is at the high end of its own.
            /// </summary>
            public readonly bool FlipForward;

            public readonly float Damage;
            public readonly float Range;
            public readonly float Cooldown;
            public readonly bool Automatic;

            public Spec(string modelPath, string prefabName, WeaponKind kind, float targetLength,
                float gripFraction, float gripHeightFraction, bool flipForward, float damage,
                float range, float cooldown, bool automatic)
            {
                GripHeightFraction = gripHeightFraction;
                FlipForward = flipForward;
                ModelPath = modelPath;
                PrefabName = prefabName;
                Kind = kind;
                TargetLength = targetLength;
                GripFraction = gripFraction;
                Damage = damage;
                Range = range;
                Cooldown = cooldown;
                Automatic = automatic;
            }
        }

        private static readonly Spec[] Specs =
        {
            new("Assets/_Project/Art/Items/ak-47.glb", "W_AK47", WeaponKind.Rifle,
                targetLength: 0.88f, gripFraction: 0.32f, gripHeightFraction: 0.28f,
                flipForward: true,
                damage: 30f, range: 120f, cooldown: 0.1f, automatic: true),

            new("Assets/_Project/Art/Items/ka-bar_knife.glb", "W_Knife", WeaponKind.Knife,
                targetLength: 0.30f, gripFraction: 0.20f, gripHeightFraction: 0.5f,
                flipForward: true,
                damage: 45f, range: 2f, cooldown: 0.75f, automatic: false),
        };

        [MenuItem("ShadowVale/Build Weapon Prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(OutputFolder);
            int built = 0;

            foreach (Spec spec in Specs)
            {
                if (BuildOne(spec))
                {
                    built++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Weapons] Built {built}/{Specs.Length} weapon prefab(s) in {OutputFolder}");
        }

        private static bool BuildOne(Spec spec)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
            if (model == null)
            {
                Debug.LogError($"[Weapons] {spec.ModelPath} did not import as a model. " +
                               "A .glb needs the glTFast package (com.unity.cloud.gltfast).");
                return false;
            }

            var root = new GameObject(spec.PrefabName);

            GameObject instance = Object.Instantiate(model);
            instance.name = "Model";
            instance.transform.SetParent(root.transform, false);
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            if (!Normalize(instance, spec, out Bounds finalBounds))
            {
                Object.DestroyImmediate(root);
                return false;
            }

            // Muzzle/tip marker at the front, for effects and debug lines.
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, finalBounds.max.z);

            var weapon = root.AddComponent<Weapon>();
            ApplyStats(weapon, spec, muzzle.transform);

            string path = $"{OutputFolder}/{spec.PrefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[Weapons] {path} — length {spec.TargetLength} m from {Path.GetFileName(spec.ModelPath)}");
            return true;
        }

        /// <summary>
        /// Scales the model to <see cref="Spec.TargetLength"/>, rotates its long axis onto +Z and
        /// moves the grip point to the local origin. Returns the resulting bounds.
        /// </summary>
        private static bool Normalize(GameObject instance, Spec spec, out Bounds bounds)
        {
            bounds = default;
            if (!TryGetBounds(instance, out Bounds raw))
            {
                Debug.LogError($"[Weapons] No renderers in {spec.ModelPath}");
                return false;
            }

            // Longest axis is the barrel/blade; turn it to face +Z.
            Vector3 size = raw.size;
            if (size.x >= size.y && size.x >= size.z)
            {
                instance.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            }
            else if (size.y >= size.x && size.y >= size.z)
            {
                instance.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (longest <= 0.0001f)
            {
                Debug.LogError($"[Weapons] Degenerate bounds on {spec.ModelPath}");
                return false;
            }

            if (spec.FlipForward)
            {
                // Composed on the left, so the spin happens in the prefab's frame: front and back
                // swap while the weapon stays upright.
                instance.transform.localRotation =
                    Quaternion.Euler(180f, 0f, 0f) * instance.transform.localRotation;
            }

            instance.transform.localScale = Vector3.one * (spec.TargetLength / longest);

            // Re-measure after rotating and scaling, then slide the grip onto the origin.
            if (!TryGetBounds(instance, out Bounds scaled))
            {
                return false;
            }

            // The grip is a point on the weapon, not its centre: along the barrel it sits at
            // GripFraction, and vertically at GripHeightFraction up from the lowest point (for a
            // rifle that is the pistol grip, below the receiver).
            float gripZ = scaled.min.z + scaled.size.z * spec.GripFraction;
            float gripY = scaled.min.y + scaled.size.y * spec.GripHeightFraction;
            var offset = new Vector3(-scaled.center.x, -gripY, -gripZ);
            instance.transform.localPosition += offset;

            TryGetBounds(instance, out bounds);
            return true;
        }

        /// <summary>
        /// Combined renderer bounds. The instance sits at the identity under its parent, so world
        /// bounds are the prefab-local bounds we want.
        /// </summary>
        private static bool TryGetBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return true;
        }

        private static void ApplyStats(Weapon weapon, Spec spec, Transform muzzle)
        {
            var so = new SerializedObject(weapon);
            so.FindProperty("kind").enumValueIndex = (int)spec.Kind;
            so.FindProperty("damage").floatValue = spec.Damage;
            so.FindProperty("range").floatValue = spec.Range;
            so.FindProperty("cooldown").floatValue = spec.Cooldown;
            so.FindProperty("automatic").boolValue = spec.Automatic;
            so.FindProperty("muzzle").objectReferenceValue = muzzle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
