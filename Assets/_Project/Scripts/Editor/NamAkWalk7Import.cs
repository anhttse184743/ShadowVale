using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Editor
{
    // Claude 14/11 (T033): sandbox only. Imports the 7 AK walk directions made from the approved forward walk.
    // Does not touch the player controller, override controller or prefab.
    public static class NamAkWalk7Import
    {
        const string Source = @"D:\DO_AN_GAME\chuyen_dong\xuat_thu_unity\DiAK_7Huong";
        public const string Dest = "Assets/_Project/Map01/Tests/Sandbox/AkWalk7";
        public static readonly string[] Dirs = { "FR", "R", "BR", "B", "BL", "L", "FL" };

        // Claude 14/11: director approved T033. Imports the 7 clips beside the approved forward walk and points the
        // player's existing override controller at them (idempotent; backs the override up once).
        const string GameDest = "Assets/_Project/Art/Characters/NamAoBaBa_20261111/Animations";
        const string Override = GameDest + "/Nam_ApprovedWalk_Override.overrideController";
        [MenuItem("ShadowVale/Characters/Install Nam AK Walk 7 Directions")]
        public static void InstallToGame()
        {
            Directory.CreateDirectory("Docs/Reviews/T033_VaoGame");
            string backup = "Docs/Reviews/T033_VaoGame/Nam_ApprovedWalk_Override_truoc.overrideController";
            if (!File.Exists(backup)) File.Copy(Override, backup);
            ImportTo(GameDest);
            var mapping = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Override);
            if (mapping == null) throw new InvalidOperationException("Override controller missing");
            var pairs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            mapping.GetOverrides(pairs);
            int done = 0;
            for (int i = 0; i < pairs.Count; i++)
            {
                var key = pairs[i].Key; if (key == null) continue;
                foreach (var d in Dirs)
                    if (key.name == "Nam_Rifle_WalkLow_" + d)
                    {
                        var clip = AssetDatabase.LoadAllAssetsAtPath(GameDest + "/Nam_Rifle_Walk_Approved01_" + d + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                        pairs[i] = new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(key, clip); done++;
                    }
            }
            if (done != 7) throw new InvalidOperationException("Expected 7 direction slots, found " + done);
            mapping.ApplyOverrides(pairs); EditorUtility.SetDirty(mapping); AssetDatabase.SaveAssets();
            File.WriteAllText("Tools/NamAkWalk7Install.result.txt", "PASS 7 directions installed into " + Override + "\n");
        }

        public static void Run() => ImportTo(Dest);

        static void ImportTo(string Dest)
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(NamModelSetup.ModelPath).OfType<Avatar>().FirstOrDefault();
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(StoryAnimationSetup.NamFingerMaskPath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman || mask == null) throw new InvalidOperationException("Needs the Nam avatar and finger mask.");
            Directory.CreateDirectory(Dest);
            foreach (var d in Dirs)
            {
                string src = Path.Combine(Source, "Nam_Rifle_Walk_Approved01_" + d + ".fbx");
                if (!File.Exists(src)) throw new FileNotFoundException("Missing", src);
                // Copy the old clip's import settings so root handling and loop match what the blend tree expects.
                var old = AssetImporter.GetAtPath("Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_WalkLow_" + d + ".fbx") as ModelImporter;
                if (old == null) throw new InvalidOperationException("Old clip missing for " + d);
                string path = Dest + "/Nam_Rifle_Walk_Approved01_" + d + ".fbx";
                File.Copy(src, path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                imp.animationType = ModelImporterAnimationType.Human; imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; imp.sourceAvatar = avatar;
                imp.importCameras = false; imp.importLights = false; imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.importAnimation = true; imp.resampleCurves = true; imp.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
                var oldTake = old.clipAnimations.Length > 0 ? old.clipAnimations[0] : old.defaultClipAnimations[0];
                var take = imp.defaultClipAnimations.First();
                take.name = "Nam_Rifle_Walk_Approved01_" + d; take.loopTime = true; take.loopPose = oldTake.loopPose;
                take.lockRootRotation = oldTake.lockRootRotation; take.keepOriginalOrientation = oldTake.keepOriginalOrientation;
                take.lockRootHeightY = oldTake.lockRootHeightY; take.keepOriginalPositionY = oldTake.keepOriginalPositionY; take.heightFromFeet = oldTake.heightFromFeet;
                take.lockRootPositionXZ = oldTake.lockRootPositionXZ; take.keepOriginalPositionXZ = oldTake.keepOriginalPositionXZ;
                take.rotationOffset = oldTake.rotationOffset;
                take.maskType = ClipAnimationMaskType.CopyFromOther; take.maskSource = mask; take.ConfigureClipFromMask(mask);
                imp.clipAnimations = new[] { take };
                imp.SaveAndReimport();
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip == null || !clip.isHumanMotion) throw new InvalidOperationException("Import failed " + path);
            }
            File.WriteAllText("Tools/NamAkWalk7Import.result.txt", "PASS 7 clips in " + Dest + "\n");
        }
    }
}
