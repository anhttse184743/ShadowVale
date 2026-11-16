using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Editor
{
    /// <summary>Imports the approved AK jump and adds only its own state and transitions.</summary>
    public static class NamPubgRifleSetup
    {
        const string ClipPath = "Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_Jump.fbx";
        const string ControllerPath = "Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller";
        const string StateName = "Jump_Rifle";
        const string TransitionName = "A1 rifle jump";

        [MenuItem("ShadowVale/Characters/Setup PUBG Rifle Jump")]
        public static void Run()
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(NamModelSetup.ModelPath).OfType<Avatar>().FirstOrDefault();
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(StoryAnimationSetup.NamFingerMaskPath);
            var importer = AssetImporter.GetAtPath(ClipPath) as ModelImporter;
            if (avatar == null || !avatar.isValid || !avatar.isHuman || mask == null || importer == null)
                throw new InvalidOperationException("A1 requires the existing Nam humanoid avatar, finger mask and FBX.");
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true; importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            var take = importer.defaultClipAnimations.FirstOrDefault() ?? throw new InvalidOperationException("A1 FBX has no take.");
            take.name = "Nam_Rifle_Jump"; take.loopTime = false; take.loopPose = false;
            take.lockRootRotation = true; take.keepOriginalOrientation = true;
            take.lockRootHeightY = true; take.keepOriginalPositionY = false; take.heightFromFeet = true;
            take.lockRootPositionXZ = true; take.keepOriginalPositionXZ = true;
            take.maskType = ClipAnimationMaskType.CopyFromOther; take.maskSource = mask; take.ConfigureClipFromMask(mask);
            importer.clipAnimations = new[] { take };
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (clip == null || controller == null) throw new InvalidOperationException("A1 clip or controller missing.");
            var machine = controller.layers[0].stateMachine;
            AnimatorState Find(string name) => machine.states.Select(s => s.state).Single(s => s.name == name);
            var original = Find("Jump");
            var rifleLoco = Find("Locomotion_Rifle");
            var existing = machine.states.Select(s => s.state).Where(s => s.name == StateName).ToArray();
            if (existing.Length > 1) throw new InvalidOperationException("Duplicate A1 states.");
            var jump = existing.FirstOrDefault() ?? machine.AddState(StateName);
            jump.motion = clip; jump.speed = original.speed; jump.writeDefaultValues = original.writeDefaultValues;
            foreach (var t in jump.transitions.ToArray()) jump.RemoveTransition(t);
            foreach (var old in original.transitions)
            {
                if (old.destinationState == null) throw new InvalidOperationException("Unexpected Jump exit.");
                // Land straight back into rifle locomotion: Jump → Locomotion would blend through the bare-handed pose for a frame
                // before Locomotion → Locomotion_Rifle (Locomotion_Rifle hands back to Locomotion by itself if the rifle is gone).
                var exit = jump.AddTransition(old.destinationState.name == "Locomotion" ? rifleLoco : old.destinationState);
                exit.hasExitTime = old.hasExitTime; exit.exitTime = old.exitTime;
                exit.hasFixedDuration = old.hasFixedDuration; exit.duration = old.duration; exit.offset = old.offset;
                foreach (var c in old.conditions) exit.AddCondition(c.mode, c.threshold, c.parameter);
            }
            foreach (string sourceName in new[] { "Locomotion", "Locomotion_Rifle" })
            {
                var source = Find(sourceName);
                var matches = source.transitions.Where(t => t.name == TransitionName).ToArray();
                foreach (var t in matches) source.RemoveTransition(t);
                var enter = source.AddTransition(jump);
                enter.name = TransitionName; enter.hasExitTime = false; enter.hasFixedDuration = true;
                enter.duration = .08f; enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.If, 0, "Jump");
                enter.AddCondition(AnimatorConditionMode.Equals, (int)WeaponKind.Rifle, "Weapon");
                source.transitions = new[] { enter }.Concat(source.transitions.Where(t => t != enter)).ToArray();
            }
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            Debug.Log("[A1] Imported and connected " + clip.name);
        }
    }
}
