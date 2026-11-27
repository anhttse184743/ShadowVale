using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// 26/11: two Hùng fixes after the V119 swap (idempotent, safe to re-run).
    /// 1. The "Hung wounded" blend tree walks on the looping Hung_DiKhapKhieng limp again; the 1 s
    ///    non-looping sample clip inside Hung_V119_model_va_clip froze him after one step. Its threshold is
    ///    LimpBlendSpeed (the slower escort pace) so he walks the full limp, played slower.
    /// 2. The cot pose becomes a Humanoid copy of HungRest.fbx (old Hùng skeleton, avatar copied from
    ///    hung.fbx), so Map01HungRest can pose the live V119 Hùng instead of the old mesh.
    /// </summary>
    public static class HungDiVeVaNamGiuongSetup
    {
        const string Controller="Assets/_Project/Art/Characters/Animations/Controllers/AC_Hung.controller";
        const string Limp="Assets/_Project/Art/Characters/NPCs/Animations/Hung_DiKhapKhieng.fbx";
        const string OldHung="Assets/_Project/Art/Characters/NPCs/hung.fbx";
        const string RestSource="Assets/_Project/Art/Characters/Animations/ShelterSupplies/HungRest.fbx";
        const string RestHuman="Assets/_Project/Art/Characters/Animations/ShelterSupplies/HungRest_Humanoid.fbx";
        const string Presentation="Assets/Resources/OperationsAnimation/ShelterSupplies/Presentation.asset";

        [MenuItem("ShadowVale/Hung/Sua di ve va nam giuong")]
        public static void Run()
        {
            var log=new System.Text.StringBuilder();
            FixLimp(log);ImportRest(log);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/HungDiVe");File.WriteAllText("Logs/HungDiVe/setup.txt",log.ToString());
            Debug.Log("HUNG_DIVE_SETUP\n"+log);
        }

        static void FixLimp(System.Text.StringBuilder log)
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            var limp=AssetDatabase.LoadAllAssetsAtPath(Limp).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview",StringComparison.Ordinal));
            if(controller==null||limp==null)throw new Exception("Missing AC_Hung or Hung_DiKhapKhieng");
            var state=controller.layers[0].stateMachine.states.Select(s=>s.state).First(s=>s.name==Map01HungVisual.WoundedState);
            if(state.motion is not BlendTree tree)throw new Exception("Wounded state has no blend tree");
            var children=tree.children;bool changed=false;
            for(int i=0;i<children.Length;i++){
                if(children[i].threshold<.5f)continue;
                if(children[i].motion!=limp){log.AppendLine($"Wounded threshold {children[i].threshold}: {AssetDatabase.GetAssetPath(children[i].motion)} -> {Limp}");children[i].motion=limp;changed=true;}
                // 26/11: the escort slowed to EscortSpeed; the full limp is reached at LimpBlendSpeed.
                if(!Mathf.Approximately(children[i].threshold,Map01HungVisual.LimpBlendSpeed)){log.AppendLine($"Limp threshold {children[i].threshold} -> {Map01HungVisual.LimpBlendSpeed}");children[i].threshold=Map01HungVisual.LimpBlendSpeed;changed=true;}
            }
            if(changed){tree.children=children;EditorUtility.SetDirty(tree);EditorUtility.SetDirty(controller);}
            log.AppendLine($"Limp clip {limp.name} length {limp.length:F3}s loop {limp.isLooping}; changed {changed}");
        }

        static void ImportRest(System.Text.StringBuilder log)
        {
            if(AssetImporter.GetAtPath(RestHuman)==null&&!AssetDatabase.CopyAsset(RestSource,RestHuman))throw new Exception("Copy failed: "+RestHuman);
            var avatar=AssetDatabase.LoadAllAssetsAtPath(OldHung).OfType<Avatar>().FirstOrDefault();
            if(avatar==null||!avatar.isHuman)throw new Exception("hung.fbx has no Humanoid avatar");
            var importer=(ModelImporter)AssetImporter.GetAtPath(RestHuman);
            importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;importer.sourceAvatar=avatar;
            importer.importVisibility=false;importer.isReadable=false;importer.animationCompression=ModelImporterAnimationCompression.Off;
            var take=importer.defaultClipAnimations.First();
            take.name="Shelter_HungRest_Human";take.loopTime=false;take.firstFrame=1;take.lastFrame=376;
            // Keep the whole lying body in the pose; the cot placement does the yaw and height.
            take.lockRootRotation=take.lockRootHeightY=take.lockRootPositionXZ=true;
            take.keepOriginalOrientation=take.keepOriginalPositionY=take.keepOriginalPositionXZ=true;
            importer.clipAnimations=new[]{take};importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAllAssetsAtPath(RestHuman).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview",StringComparison.Ordinal));
            var data=AssetDatabase.LoadAssetAtPath<Map01SupplyPresentation>(Presentation);
            if(data==null)throw new Exception("Missing "+Presentation);
            data.hungRestHuman=clip;EditorUtility.SetDirty(data);
            log.AppendLine($"Rest clip {clip.name} humanMotion {clip.humanMotion} length {clip.length:F3}s; avatar {avatar.name} valid {avatar.isValid}");
        }
    }
}
