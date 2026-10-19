using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowVale.Editor
{
    [InitializeOnLoad]
    public static class RifleFireStabilitySetup
    {
        public const string ClipPath = StoryAnimationSetup.NamFolder + "Nam_Rifle_Fire_Stable.anim";
        static RifleFireStabilitySetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Tools/StabilizeRifleFire.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            Configure(AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorBuilder.ControllerPath));
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Tools/GunfireReports");
            File.WriteAllText("Tools/GunfireReports/stability-setup.txt", "Stable aim/recoil clip and conditional firing exits saved.");
        }

        [MenuItem("ShadowVale/Weapons/Stabilize Nam rifle fire")]
        static void Run()
        {
            Configure(AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorBuilder.ControllerPath));
            AssetDatabase.SaveAssets();
        }

        public static void Configure(AnimatorController controller)
        {
            var layer = controller.layers.First(l => l.name == PlayerAnimatorBuilder.UpperBodyLayer).stateMachine;
            AnimatorState State(string name) => layer.states.First(s => s.state.name == name).state;
            var fire = State("Attack_Rifle");
            var source = AssetDatabase.LoadAllAssetsAtPath(StoryAnimationSetup.NamFolder + "Nam_Rifle_AimIdle.fbx")
                .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, ClipPath); }
            clip.ClearCurves(); clip.frameRate = 60;
            // Start and end in the same raised aim pose, including Nam's generic finger bones.
            // The original take raises and lowers the whole weapon on every round.
            const float duration = .08f;
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                float value = AnimationUtility.GetEditorCurve(source, binding).Evaluate(source.length * .5f);
                float kick = binding.type == typeof(Animator) && binding.propertyName.EndsWith("Arm Front-Back") ? .012f
                    : binding.type == typeof(Animator) && binding.propertyName.EndsWith("Forearm Stretch") ? -.018f : 0;
                AnimationUtility.SetEditorCurve(clip, binding, kick == 0
                    ? AnimationCurve.Constant(0, duration, value)
                    : new AnimationCurve(new Keyframe(0, value), new Keyframe(.015f, value + kick),
                        new Keyframe(.05f, value), new Keyframe(duration, value)));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false; AnimationUtility.SetAnimationClipSettings(clip, settings);
            fire.motion = clip; fire.speed = 1;
            foreach (var enter in layer.anyStateTransitions.Where(t => t.destinationState == fire))
            { enter.canTransitionToSelf = true; enter.duration = .02f; }
            // Preserve immediate weapon-change exits; replace only the timed return to carry.
            foreach (var exit in fire.transitions.Where(t => t.hasExitTime).ToArray()) fire.RemoveTransition(exit);
            void Return(string name, bool aiming, bool? crouched)
            {
                var back = fire.AddTransition(State(name));
                back.hasExitTime = true; back.exitTime = 1; back.duration = .08f;
                back.AddCondition(aiming ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Aiming");
                if (crouched.HasValue) back.AddCondition(crouched.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Sneaking");
            }
            Return("Aim_Rifle", true, false);
            Return("AimCrouch_Rifle", true, true);
            Return("Hold_Rifle", false, null);
            EditorUtility.SetDirty(clip); EditorUtility.SetDirty(controller);
        }
    }
}
