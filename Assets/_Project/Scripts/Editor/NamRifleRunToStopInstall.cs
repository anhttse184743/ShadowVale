using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowVale.Editor
{
    // Claude 14/11: installs the director-approved AK run-to-stop clip (T012c round 20) into the game.
    // Idempotent: copies the FBX, imports it like the B1C2 sandbox clips, and adds one state
    // "Rifle_RunToStop" to AC_Player's base layer that hands back to Locomotion_Rifle.
    // PlayerController crossfades into it when the player releases movement after sprinting with the rifle.
    public static class NamRifleRunToStopInstall
    {
        public const string StateName = "Rifle_RunToStop";
        const string Source = @"D:\DO_AN_GAME\chuyen_dong\xuat_thu_unity\PUBG_B1C2\Nam_Rifle_RunToStop_New.fbx";
        const string ClipPath = "Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_RunToStop.fbx";
        const string Controller = "Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller";
        const string Backup = "Docs/Reviews/T012c_VaoGame/AC_Player_truoc.controller";
        const string Result = "Tools/NamRifleRunToStopInstall.result.txt";

        [MenuItem("ShadowVale/Characters/Install Nam Rifle Run To Stop")]
        public static void Run()
        {
            try
            {
                var clip = ImportClip();
                InstallState(clip);
                File.WriteAllText(Result, "PASS: " + ClipPath + " -> state " + StateName + "\n");
            }
            catch (Exception e) { File.WriteAllText(Result, "FAIL: " + e + "\n"); throw; }
        }

        static AnimationClip ImportClip()
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(NamModelSetup.ModelPath).OfType<Avatar>().FirstOrDefault();
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(StoryAnimationSetup.NamFingerMaskPath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman || mask == null) throw new InvalidOperationException("Needs the Nam avatar and finger mask.");
            if (!File.Exists(Source)) throw new FileNotFoundException("Approved clip missing", Source);
            if (!File.Exists(ClipPath) || !File.ReadAllBytes(ClipPath).SequenceEqual(File.ReadAllBytes(Source)))
            {
                File.Copy(Source, ClipPath, true);
                AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = AssetImporter.GetAtPath(ClipPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("No FBX importer: " + ClipPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true; importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            var take = importer.defaultClipAnimations.FirstOrDefault();
            if (take == null) throw new InvalidOperationException("No animation take");
            take.name = "Nam_Rifle_RunToStop"; take.loopTime = false; take.loopPose = false;
            take.lockRootRotation = true; take.keepOriginalOrientation = true;
            take.lockRootHeightY = true; take.keepOriginalPositionY = false; take.heightFromFeet = true;
            take.lockRootPositionXZ = true; take.keepOriginalPositionXZ = true;
            take.maskType = ClipAnimationMaskType.CopyFromOther; take.maskSource = mask; take.ConfigureClipFromMask(mask);
            importer.clipAnimations = new[] { take };
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null || !clip.isHumanMotion) throw new InvalidOperationException("Humanoid import failed");
            return clip;
        }

        static void InstallState(AnimationClip clip)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Backup));
            if (!File.Exists(Backup)) File.Copy(Controller, Backup);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (controller == null) throw new InvalidOperationException("AC_Player missing");
            var root = controller.layers[0].stateMachine;
            var target = root.states.Select(s => s.state).FirstOrDefault(s => s.name == "Locomotion_Rifle");
            if (target == null) throw new InvalidOperationException("Locomotion_Rifle state missing");
            var state = root.states.Select(s => s.state).FirstOrDefault(s => s.name == StateName);
            if (state == null) state = root.AddState(StateName, new Vector3(600, 420, 0));
            state.motion = clip; state.writeDefaultValues = target.writeDefaultValues; state.speed = 1f;
            foreach (var t in state.transitions.ToArray()) state.RemoveTransition(t);
            // Hand back near the end; the blend hides the small hold difference between the clip's last pose and the idle.
            var done = state.AddTransition(target);
            done.hasExitTime = true; done.exitTime = .9f; done.hasFixedDuration = true; done.duration = .25f;
            // Moving again cancels the stop: PlayerController crossfades back on movement input
            // (Speed cannot do it — it is still high while braking).
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }
}
