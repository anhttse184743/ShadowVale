using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Configures the imported Mixamo character so its clips actually retarget: the model FBX
    /// becomes Humanoid and generates the avatar, every clip FBX copies that avatar, and each clip
    /// is renamed to its role (see <see cref="CharacterClipLibrary"/>) and looped where it should
    /// cycle. Idempotent — a reimport only happens when a setting is actually wrong.
    /// Menu <b>ShadowVale ▸ Setup Character Import</b>.
    /// </summary>
    public static class CharacterImportSetup
    {
        /// <summary>
        /// The rig the Mixamo clips were authored on. It stays in the project even when it is not
        /// the visible character, because every clip copies its avatar to be interpreted — that
        /// avatar has to describe the skeleton stored inside the clip files.
        /// </summary>
        private const string ClipSourceRigPath = "Assets/_Project/Art/Characters/Player/Y Bot.fbx";

        /// <summary>
        /// The character actually shown in game. It needs its own Humanoid avatar; Unity retargets
        /// the clips onto it through muscle space, so its skeleton need not match the source rig.
        /// </summary>
        private const string PlayerFolder = "Assets/_Project/Art/Characters/Player";

        /// <summary>
        /// Finds the character model instead of hardcoding its filename. Unity's AssetDatabase is
        /// case sensitive even on Windows, so re-exporting as "Player.fbx" instead of "player.fbx"
        /// silently resolved to null and left the built prefab pointing at a deleted mesh — which
        /// shows up as a mangled character rather than an error.
        /// Anything in the Player folder that is not the clip source rig is the character.
        /// </summary>
        public static string ResolvePlayerModelPath()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { PlayerFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(path, ClipSourceRigPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                return path;
            }

            Debug.LogError($"[CharacterImport] No character model found in {PlayerFolder} " +
                           $"(anything other than {ClipSourceRigPath}).");
            return null;
        }

        /// <summary>
        /// How much wrist roll is carried by the forearm instead of the wrist joint (Unity's
        /// "Forearm Twist" muscle setting; 0 = all at the wrist, 1 = all at the elbow, default
        /// 0.5). This rig has no dedicated twist bones, so all the roll lands on a single joint
        /// and the sleeve pinches. Pushing it up spreads the deformation along the forearm.
        /// A mitigation, not a cure — the real fix is twist bones and better weights in the DCC.
        /// </summary>
        private const float ForeArmTwist = 0.8f;

        /// <summary>Same idea for the shoulder-to-elbow roll.</summary>
        private const float ArmTwist = 0.5f;

        /// <summary>
        /// Bind-pose elbow bend, in degrees, above which the arm is straightened. This rig ships
        /// with about 11.4 degrees of bend baked into its rest pose — in the horizontal plane, so
        /// the T-pose still looks straight from the front. Unity builds the avatar's reference
        /// pose from that, so every retargeted clip inherits the bend.
        /// </summary>
        private const float ElbowStraightenThreshold = 1.5f;

        /// <summary>Unity's own default, restored on rigs that must stay untouched.</summary>
        private const float DefaultTwist = 0.5f;

        [MenuItem("ShadowVale/Setup Character Import")]
        public static void Run()
        {
            // The source rig keeps Unity's defaults: its avatar is what every clip is decoded
            // through, so changing its twist would silently re-bake all the animation.
            Avatar avatar = ConfigureModel(ClipSourceRigPath, tuneTwist: false);
            if (avatar == null)
            {
                Debug.LogError($"[CharacterImport] No avatar generated from {ClipSourceRigPath}. " +
                               "Is the FBX rigged? Check the Rig tab.");
                return;
            }

            string playerModelPath = ResolvePlayerModelPath();
            if (playerModelPath != null)
            {
                StraightenArms(playerModelPath);
            }
            Avatar playerAvatar = playerModelPath == null
                ? null
                : ConfigureModel(playerModelPath, tuneTwist: true);
            if (playerAvatar == null)
            {
                Debug.LogWarning($"[CharacterImport] {playerModelPath ?? "(none)"} has no Humanoid " +
                                 "avatar — the map falls back to the source rig as the character.");
            }
            else
            {
                Debug.Log($"[CharacterImport] Player model avatar '{playerAvatar.name}' " +
                          $"valid={playerAvatar.isValid}, human={playerAvatar.isHuman}.");
            }

            Dictionary<string, string> resolved = CharacterClipLibrary.ResolveRoleToPath();
            var pathToRole = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> pair in resolved)
            {
                pathToRole[pair.Value] = pair.Key;
            }

            int configured = 0;
            foreach (string raw in Directory.GetFiles(CharacterClipLibrary.ClipsFolder, "*.fbx"))
            {
                string path = raw.Replace('\\', '/');
                pathToRole.TryGetValue(path, out string role);
                if (ConfigureClip(path, avatar, role))
                {
                    configured++;
                }
            }

            AssetDatabase.SaveAssets();

            List<string> missing = CharacterClipLibrary.MissingRoles(resolved);
            Debug.Log($"[CharacterImport] Avatar '{avatar.name}' ready; {configured} clip(s) configured. " +
                      $"Roles found: {string.Join(", ", resolved.Keys)}.");
            if (missing.Count > 0)
            {
                Debug.LogWarning($"[CharacterImport] No clip for: {string.Join(", ", missing)}. " +
                                 $"Drop the matching Mixamo FBX into {CharacterClipLibrary.ClipsFolder} " +
                                 "and run this menu item again.");
            }
        }

        /// <summary>
        /// Rewrites the avatar's reference pose so the elbows and wrists are straight — the
        /// scripted equivalent of the Avatar window's "Enforce T-Pose", limited to the arm chain.
        /// Both joints ship pre-bent on this rig (elbow ~11-13 deg, wrist ~18 deg), and Unity
        /// treats whatever it finds as "neutral", so the bend rides along in every clip.
        /// Idempotent: an arm already within <see cref="ElbowStraightenThreshold"/> is left alone.
        /// </summary>
        private static void StraightenArms(string modelPath)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (importer == null || prefab == null)
            {
                return;
            }

            var probe = Object.Instantiate(prefab);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            probe.transform.localScale = Vector3.one;
            var animator = probe.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Object.DestroyImmediate(probe);
                return;
            }

            HumanDescription description = importer.humanDescription;
            SkeletonBone[] skeleton = description.skeleton;
            bool changed = false;

            var sides = new[]
            {
                (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
                (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            };

            foreach ((HumanBodyBones upperBone, HumanBodyBones foreBone, HumanBodyBones handBone) in sides)
            {
                Transform upper = animator.GetBoneTransform(upperBone);
                Transform fore = animator.GetBoneTransform(foreBone);
                Transform hand = animator.GetBoneTransform(handBone);
                if (upper == null || fore == null || hand == null || fore.parent == null)
                {
                    continue;
                }

                Vector3 upperDir = (fore.position - upper.position).normalized;
                Vector3 foreDir = (hand.position - fore.position).normalized;
                float bend = Vector3.Angle(upperDir, foreDir);
                if (bend < ElbowStraightenThreshold)
                {
                    continue;
                }

                // Rotate the forearm so it continues the upper arm's line, expressed back in the
                // forearm's own parent space — which is what the skeleton array stores.
                Quaternion delta = Quaternion.FromToRotation(foreDir, upperDir);
                Quaternion parentWorld = fore.parent.rotation;
                Quaternion corrected =
                    Quaternion.Inverse(parentWorld) * delta * parentWorld * fore.localRotation;

                changed |= WriteSkeletonRotation(skeleton, fore.name, corrected, bend, "elbow");

                // Same treatment one joint further out. The hand has no humanoid child bone, so
                // its direction comes from the leaf marker the exporter left behind.
                Transform handTip = hand.childCount > 0 ? hand.GetChild(0) : null;
                if (handTip == null)
                {
                    continue;
                }

                Vector3 foreToHand = (hand.position - fore.position).normalized;
                Vector3 handDir = (handTip.position - hand.position).normalized;
                float wristBend = Vector3.Angle(foreToHand, handDir);
                if (wristBend < ElbowStraightenThreshold)
                {
                    continue;
                }

                Quaternion wristDelta = Quaternion.FromToRotation(handDir, foreToHand);
                Quaternion handParentWorld = hand.parent.rotation;
                Quaternion wristCorrected = Quaternion.Inverse(handParentWorld) * wristDelta
                                            * handParentWorld * hand.localRotation;
                changed |= WriteSkeletonRotation(skeleton, hand.name, wristCorrected, wristBend, "wrist");
            }

            Object.DestroyImmediate(probe);

            if (!changed)
            {
                return;
            }

            description.skeleton = skeleton;
            importer.humanDescription = description;
            importer.SaveAndReimport();
        }

        private static bool WriteSkeletonRotation(SkeletonBone[] skeleton, string boneName,
            Quaternion rotation, float removedDegrees, string label)
        {
            for (int i = 0; i < skeleton.Length; i++)
            {
                if (skeleton[i].name != boneName)
                {
                    continue;
                }
                skeleton[i].rotation = rotation;
                Debug.Log($"[CharacterImport] Straightened {label} {boneName}: " +
                          $"{removedDegrees:F1} deg removed from the avatar reference pose.");
                return true;
            }
            return false;
        }

        /// <summary>Makes a model Humanoid with its own avatar, and returns that avatar.</summary>
        private static Avatar ConfigureModel(string modelPath, bool tuneTwist)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[CharacterImport] Not a model asset: {modelPath}");
                return null;
            }

            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }
            // Blender exports its scene camera and light into the FBX; they would become stray
            // GameObjects inside the character prefab.
            if (importer.importCameras)
            {
                importer.importCameras = false;
                dirty = true;
            }
            if (importer.importLights)
            {
                importer.importLights = false;
                dirty = true;
            }

            // A character FBX is mesh and skeleton only; clips live in the Clips folder.
            if (importer.importAnimation)
            {
                importer.importAnimation = false;
                dirty = true;
            }

            // Always written, not just when tuning: a rig that should keep Unity's defaults has
            // to be restored to them, or a value left over from an earlier run silently sticks.
            float wantForeArm = tuneTwist ? ForeArmTwist : DefaultTwist;
            float wantArm = tuneTwist ? ArmTwist : DefaultTwist;
            HumanDescription description = importer.humanDescription;
            if (!Mathf.Approximately(description.lowerArmTwist, wantForeArm)
                || !Mathf.Approximately(description.upperArmTwist, wantArm))
            {
                description.lowerArmTwist = wantForeArm;
                description.upperArmTwist = wantArm;
                importer.humanDescription = description;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log($"[CharacterImport] {modelPath} -> Humanoid, avatar from this model, " +
                          $"forearm twist {ForeArmTwist}.");
            }

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is Avatar a)
                {
                    return a;
                }
            }
            return null;
        }

        /// <summary>Points one clip FBX at the shared avatar, then names and loops its clip.</summary>
        private static bool ConfigureClip(string path, Avatar avatar, string role)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                dirty = true;
            }
            if (importer.sourceAvatar != avatar)
            {
                importer.sourceAvatar = avatar;
                dirty = true;
            }
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                dirty = true;
            }
            // Mixamo names every take "mixamo.com" — rename it to the role so the Animator
            // references stay readable, and loop the ones we cycle.
            if (!string.IsNullOrEmpty(role))
            {
                CharacterClipLibrary.Role spec = CharacterClipLibrary.FindRole(role);
                ModelImporterClipAnimation[] current = importer.clipAnimations;
                bool alreadyNamed = current.Length > 0
                                    && current[0].name == spec.Name
                                    && current[0].loopTime == spec.Loop;
                if (!alreadyNamed)
                {
                    // defaultClipAnimations carries the correct frame range straight from the FBX.
                    ModelImporterClipAnimation[] clips =
                        current.Length > 0 ? current : importer.defaultClipAnimations;
                    if (clips.Length > 0)
                    {
                        clips[0].name = spec.Name;
                        clips[0].loopTime = spec.Loop;
                        importer.clipAnimations = clips;
                        dirty = true;
                    }
                }
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log($"[CharacterImport] {path} -> Humanoid" +
                          (string.IsNullOrEmpty(role) ? "" : $", clip '{role}'"));
            }
            return true;
        }
    }
}
