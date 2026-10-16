using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Nam's game model, exported from his Blender master (chuyen_dong, blender_scripts/export_nam_model.py):
    /// the rig with its finger bones, the body, the two hands, his webbing gear and the camo pack.
    /// <list type="bullet">
    /// <item>His fingers play straight from the clips: they are left out of the Humanoid avatar, so the clips'
    /// finger bones come through as plain (generic) curves on the same skeleton, exactly as posed in Blender.
    /// Mapped as Humanoid fingers they lost 15-35 deg of every middle and end phalanx: Humanoid works out its
    /// own curl axis from the hand's shape, which is not the axis the rig curls them about.</item>
    /// <item>Player.prefab's "Model" keeps its own GameObject and Animator (PlayerController and PlayerCombat
    /// point at them) but its contents become the new model; the WeaponAnchor moves to the new right hand
    /// with its seat unchanged.</item>
    /// </list>
    /// Run by <see cref="StoryAnimationSetup.Run"/> before the clips are imported, since they read this avatar.
    /// Menu <b>ShadowVale ▸ Characters ▸ Setup Nam Model</b>.
    /// </summary>
    public static class NamModelSetup
    {
        public const string ModelPath = "Assets/_Project/Art/Characters/Player/Player.fbx";
        public const string PrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string TextureFolder = "Assets/_Project/Art/Characters/Player/Textures/";
        private static readonly string[] NormalMaps = { "T_Nam_BaloRanRi_Normal.png" };
        private static readonly string[] HumanFingers = { "Thumb", "Index", "Middle", "Ring", "Little" };

        /// <summary>The model in the project is the one exported with his fingers.</summary>
        public static bool HasFingers
        {
            get
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                return model != null && model.GetComponentsInChildren<Transform>(true).Any(t => t.name == "RightHandIndex1");
            }
        }

        [MenuItem("ShadowVale/Characters/Setup Nam Model")]
        public static void Run()
        {
            PrepareTextures();
            SetUpAvatar();
            RebuildPrefabModel();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The pack's baked normal map must import as a normal map or its material reads it as colour; the
        /// model is then reimported so its materials pick up every texture it names.
        /// </summary>
        private static void PrepareTextures()
        {
            foreach (string name in NormalMaps)
            {
                if (!(AssetImporter.GetAtPath(TextureFolder + name) is TextureImporter importer)
                    || importer.textureType == TextureImporterType.NormalMap) continue;
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// Keeps the fingers out of the avatar (see above) and makes its reference pose the model's own rest pose. The
        /// model and every Nam clip come out of the same Blender rig, whose rest is a clean T-pose; the pose the
        /// avatar kept from the first model (arms 12-19 deg, thighs 21-23 deg and the hips 7.5 cm off that rig)
        /// made the clips read differently in game than in Blender.
        /// </summary>
        public static void SetUpAvatar()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (importer == null || model == null) { Debug.LogError($"[NamModel] {ModelPath} missing."); return; }
            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            var names = new HashSet<string>(all.Select(t => t.name));
            HumanDescription description = importer.humanDescription;

            List<HumanBone> human = description.human
                .Where(h => names.Contains(h.boneName) && !HumanFingers.Any(f => h.humanName.Contains(f))).ToList();
            int dropped = description.human.Length - human.Count;
            bool changed = dropped > 0;

            var stored = new Dictionary<string, SkeletonBone>();
            foreach (SkeletonBone bone in description.skeleton.Skip(1)) stored[bone.name] = bone;
            var skeleton = new List<SkeletonBone>
            {
                // the model root, stored under its instance name ("Player(Clone)")
                description.skeleton.Length > 0 ? description.skeleton[0] : Pose(all[0]),
            };
            var drift = new List<string>();
            foreach (Transform t in all.Skip(1))
            {
                if (stored.TryGetValue(t.name, out SkeletonBone kept))
                {
                    float degrees = Quaternion.Angle(kept.rotation, t.localRotation);
                    float metres = Vector3.Distance(kept.position, t.localPosition);
                    if (degrees > .1f || metres > .0005f) drift.Add($"{t.name} {degrees:F1}deg {metres * 100:F1}cm");
                }
                else changed = true;
                skeleton.Add(Pose(t));
            }
            changed |= drift.Count > 0 || skeleton.Count != description.skeleton.Length;
            // The model's own twist bones spread the hand's roll along the sleeve (ForearmTwist). Humanoid must then
            // leave all of that roll on the hand: its own share (0.8 from CharacterImportSetup, for rigs without such
            // bones) turns the forearm bone at the elbow as well, and the cuff ends up further round than the hand.
            if (names.Any(StoryAnimationSetup.IsForearmTwist) && !Mathf.Approximately(description.lowerArmTwist, 0f))
            {
                description.lowerArmTwist = 0f;
                changed = true;
            }
            if (drift.Count > 0)
                Debug.Log($"[NamModel] Reference pose reset to the Blender rest where the avatar had it elsewhere: {string.Join(", ", drift)}");
            if (!changed) return;

            description.human = human.ToArray();
            description.skeleton = skeleton.ToArray();
            importer.humanDescription = description;
            importer.SaveAndReimport();
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            Debug.Log($"[NamModel] Avatar: {dropped} bones dropped (fingers play as generic curves), {skeleton.Count} transforms in the reference pose; " +
                      $"valid={avatar != null && avatar.isValid}, human={avatar != null && avatar.isHuman}.");
        }

        private static SkeletonBone Pose(Transform t) => new SkeletonBone
        {
            name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale,
        };

        /// <summary>
        /// Swaps the contents of the prefab's "Model" for the current model: the old skeleton, body and the stray
        /// cameras and lights the first export carried go; the Animator, its controller and the WeaponAnchor stay.
        /// </summary>
        public static void RebuildPrefabModel()
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null) { Debug.LogError($"[NamModel] {ModelPath} missing."); return; }
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform model = root.GetComponentsInChildren<Animator>(true).Select(a => a.transform).FirstOrDefault(t => t.name == "Model");
                if (model == null) { Debug.LogError("[NamModel] Player.prefab has no Model with an Animator."); return; }

                Transform anchor = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "WeaponAnchor");
                Vector3 anchorPosition = Vector3.zero, anchorScale = Vector3.one;
                Quaternion anchorRotation = Quaternion.identity;
                if (anchor != null)
                {
                    anchorPosition = anchor.localPosition; anchorRotation = anchor.localRotation; anchorScale = anchor.localScale;
                    anchor.SetParent(root.transform, false);
                }
                for (int i = model.childCount - 1; i >= 0; i--) Object.DestroyImmediate(model.GetChild(i).gameObject);

                var fresh = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.scene);
                PrefabUtility.UnpackPrefabInstance(fresh, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                while (fresh.transform.childCount > 0) fresh.transform.GetChild(0).SetParent(model, false);
                Object.DestroyImmediate(fresh);

                Transform hand = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightHand");
                if (anchor != null)
                {
                    if (hand == null) { Debug.LogError("[NamModel] The new model has no RightHand for the WeaponAnchor."); return; }
                    anchor.SetParent(hand, false);
                    anchor.localPosition = anchorPosition; anchor.localRotation = anchorRotation; anchor.localScale = anchorScale;
                }
                // Culling bounds: the import gives each mesh a box around its T-pose, under whatever bone it picked.
                // The hands' box stays out at arm's length while the hands come in onto the gun, and the camera
                // culls them when it frames the grip. Every piece gets one box on the hips wide enough for any pose.
                Transform hips = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Hips");
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (hips == null) break;
                    skin.rootBone = hips;
                    skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 2.4f);
                }
                ConfigureForearmTwist(model);
                var animator = model.GetComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[NamModel] Player.prefab Model rebuilt: {model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length} skinned meshes, " +
                          $"{model.GetComponentsInChildren<Transform>(true).Length} transforms, WeaponAnchor {(anchor != null ? "kept" : "absent")}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Share of the hand's roll each forearm twist bone takes, elbow end first (add_twist_bones.py).</summary>
        private static readonly float[] TwistShares = { .4f, .8f };

        /// <summary>
        /// Middle of the roll each hand really uses, from its rest (palm down), in degrees. In Blender the support
        /// hand runs from +77 (turned in on the magazine) to -172 (under the handguard) and the firing hand from -59
        /// to +214 (winding the bandage); the drivers there count the roll about -45 and +80
        /// (twist_driver_range.py). Unity's import mirrors the rig, which turns the sign of a roll.
        /// </summary>
        private static float TwistCentre(string side) => side == "Left" ? 45f : -80f;

        /// <summary>
        /// Hands the model's forearm twist bones to <see cref="ForearmTwist"/>, with the rest pose their roll is
        /// counted from (the model has just been put in, so it stands in it). A model without them gets no driver.
        /// </summary>
        private static void ConfigureForearmTwist(Transform model)
        {
            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            var limbs = new List<ForearmTwist.Limb>();
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform hand = all.FirstOrDefault(t => t.name == side + "Hand");
                Transform[] bones = all.Where(t => t.name.StartsWith(side) && StoryAnimationSetup.IsForearmTwist(t.name))
                    .OrderBy(t => t.name, System.StringComparer.Ordinal).ToArray();
                if (hand == null || bones.Length == 0) continue;
                limbs.Add(new ForearmTwist.Limb
                {
                    hand = hand, handRest = hand.localRotation, centre = TwistCentre(side), bones = bones,
                    bonesRest = bones.Select(b => b.localRotation).ToArray(),
                    shares = bones.Select((b, i) => TwistShares[Mathf.Min(i, TwistShares.Length - 1)]).ToArray(),
                });
            }
            var driver = model.GetComponent<ForearmTwist>();
            if (limbs.Count == 0) { if (driver != null) Object.DestroyImmediate(driver); return; }
            if (driver == null) driver = model.gameObject.AddComponent<ForearmTwist>();
            driver.Configure(limbs.ToArray());
        }
    }
}
