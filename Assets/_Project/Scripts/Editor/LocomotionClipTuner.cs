using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Rewrites the locomotion clips so the elbows sit straighter than the Mixamo takes author
    /// them. The takes are correct for the rig they were made on — this is an art call for a
    /// character whose sleeves read badly at a sharp elbow, not a bug fix.
    /// <para>
    /// Humanoid clips store joints as muscle curves, so the whole edit is a constant offset on
    /// "Left/Right Forearm Stretch". The relationship is linear and was measured on this rig:
    /// <c>elbowDegrees = 80 - 80 * muscle</c>, so one degree is 1/80 of a muscle unit.
    /// </para>
    /// Source FBX clips are read-only, so tuned copies are written to their own folder and the
    /// animator is pointed at those. Attack clips are left alone — a punch needs its full bend.
    /// </summary>
    public static class LocomotionClipTuner
    {
        private const string OutputFolder = "Assets/_Project/Art/Characters/Animations/Generated";

        /// <summary>
        /// Degrees of elbow bend removed from every locomotion clip. 0 disables the whole step and
        /// the original clips are used. Raise it for straighter arms, lower it for a looser stance.
        /// </summary>
        public const float ElbowStraightenDegrees = 15f;

        /// <summary>
        /// Degrees added to wrist flexion. Muscle "Hand Down-Up", range +/-80 degrees.
        /// Found by sweeping both wrist muscles and keeping the pair that left the hand most in
        /// line with the forearm: it took the Idle take from 17.9 deg of deviation down to 0.6.
        /// </summary>
        public const float WristDownUpDegrees = 12.5f;

        /// <summary>
        /// Degrees added to sideways wrist deviation. Muscle "Hand In-Out", range +/-40 degrees.
        /// Negative pulls the hand back in line with the forearm instead of cocking it outward.
        /// </summary>
        public const float WristInOutDegrees = -12.5f;

        /// <summary>Muscle ranges in degrees, read from HumanTrait's default limits.</summary>
        private const float ForearmMuscleDegrees = 80f;
        private const float HandDownUpMuscleDegrees = 80f;
        private const float HandInOutMuscleDegrees = 40f;

        /// <summary>Roles whose arms get straightened. Attacks are deliberately absent.</summary>
        private static readonly string[] TunedRoles = { "Idle", "Walk", "Run", "Sneak" };

        /// <summary>
        /// Roles whose torso is re-aligned to the idle stance. Empty on purpose: the Mixamo gun
        /// and knife takes blade the chest about 18 degrees off the character's facing, and that
        /// bladed shooting stance is wanted. Add a role here only if its torso twist is genuinely
        /// unwanted — it also invalidates the weapon grip offsets, which are measured against
        /// whatever stance the clip ends up in.
        /// </summary>
        private static readonly string[] TorsoAlignRoles = System.Array.Empty<string>();

        /// <summary>Reference role the torso is aligned to.</summary>
        private const string TorsoReferenceRole = "Idle";

        private static readonly string[] TorsoMuscles =
        {
            "Spine Twist Left-Right",
            "Chest Twist Left-Right",
            "UpperChest Twist Left-Right",
            "Spine Left-Right",
            "Chest Left-Right",
            "UpperChest Left-Right",
        };

        /// <summary>Muscle name paired with the offset applied to it, in muscle units.</summary>
        private static Dictionary<string, float> BuildOffsets()
        {
            float elbow = ElbowStraightenDegrees / ForearmMuscleDegrees;
            float downUp = WristDownUpDegrees / HandDownUpMuscleDegrees;
            float inOut = WristInOutDegrees / HandInOutMuscleDegrees;

            return new Dictionary<string, float>
            {
                // Positive muscle means a straighter arm, so removing bend adds to the curve.
                { "Left Forearm Stretch", elbow },
                { "Right Forearm Stretch", elbow },
                { "Left Hand Down-Up", downUp },
                { "Right Hand Down-Up", downUp },
                { "Left Hand In-Out", inOut },
                { "Right Hand In-Out", inOut },
            };
        }

        /// <summary>
        /// Per-torso-muscle offset that shifts this clip's average onto the idle clip's average.
        /// Only the constant bias moves; the clip keeps its own swing.
        /// </summary>
        private static Dictionary<string, float> BuildTorsoOffsets(AnimationClip source)
        {
            var result = new Dictionary<string, float>();
            AnimationClip reference = CharacterClipLibrary.LoadClip(
                CharacterClipLibrary.ResolveRoleToPath(), TorsoReferenceRole);
            if (reference == null)
            {
                Debug.LogWarning("[ClipTuner] No idle clip to align the torso against.");
                return result;
            }

            foreach (string muscle in TorsoMuscles)
            {
                if (!TryAverage(reference, muscle, out float referenceAverage)
                    || !TryAverage(source, muscle, out float sourceAverage))
                {
                    continue;
                }
                result[muscle] = referenceAverage - sourceAverage;
            }
            return result;
        }

        private static bool TryAverage(AnimationClip clip, string propertyName, out float average)
        {
            average = 0f;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.propertyName != propertyName)
                {
                    continue;
                }
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve.length == 0)
                {
                    return false;
                }
                float sum = 0f;
                foreach (Keyframe key in curve.keys)
                {
                    sum += key.value;
                }
                average = sum / curve.length;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns a tuned copy for a role, or the original clip when tuning is off or the role is
        /// not one we touch.
        /// </summary>
        public static AnimationClip Tune(AnimationClip source, string role)
        {
            if (source == null)
            {
                return source;
            }

            bool armTuning = System.Array.IndexOf(TunedRoles, role) >= 0
                             && (Mathf.Abs(ElbowStraightenDegrees) > 0.01f
                                 || Mathf.Abs(WristDownUpDegrees) > 0.01f
                                 || Mathf.Abs(WristInOutDegrees) > 0.01f);
            bool torsoAlign = System.Array.IndexOf(TorsoAlignRoles, role) >= 0;
            if (!armTuning && !torsoAlign)
            {
                return source;
            }

            Directory.CreateDirectory(OutputFolder);
            string path = $"{OutputFolder}/{role}_Tuned.anim";

            var offsets = new Dictionary<string, float>();
            if (armTuning)
            {
                foreach (KeyValuePair<string, float> pair in BuildOffsets())
                {
                    offsets[pair.Key] = pair.Value;
                }
            }
            if (torsoAlign)
            {
                foreach (KeyValuePair<string, float> pair in BuildTorsoOffsets(source))
                {
                    offsets[pair.Key] = pair.Value;
                }
            }

            var tuned = new AnimationClip
            {
                name = $"{role}_Tuned",
                frameRate = source.frameRate,
            };

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            AnimationUtility.SetAnimationClipSettings(tuned, settings);

            var adjusted = new List<string>();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                if (offsets.TryGetValue(binding.propertyName, out float offset)
                    && Mathf.Abs(offset) > 0.0001f)
                {
                    Keyframe[] keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        // Clamped: past 1 the muscle is out of range and Unity folds it back.
                        keys[i].value = Mathf.Clamp(keys[i].value + offset, -1f, 1f);
                    }
                    curve.keys = keys;
                    adjusted.Add(binding.propertyName);
                }
                AnimationUtility.SetEditorCurve(tuned, binding, curve);
            }

            if (adjusted.Count == 0)
            {
                Debug.LogWarning($"[ClipTuner] {role}: no arm muscle curves matched — using the original.");
                return source;
            }

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(tuned, existing);
                Object.DestroyImmediate(tuned);
                tuned = existing;
                EditorUtility.SetDirty(tuned);
            }
            else
            {
                AssetDatabase.CreateAsset(tuned, path);
            }

            Debug.Log($"[ClipTuner] {role}: arms={armTuning} torsoAlign={torsoAlign} on " +
                      $"{adjusted.Count} curve(s) -> {path}, humanMotion={tuned.isHumanMotion}, " +
                      $"looping={tuned.isLooping}");
            return tuned;
        }
    }
}
