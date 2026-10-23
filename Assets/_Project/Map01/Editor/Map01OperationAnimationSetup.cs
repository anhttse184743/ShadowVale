using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    public static class Map01OperationAnimationSetup
    {
        static void CompactConstantCurves(AnimationClip clip)
        {
            // Keep the 30 fps moving curves; two endpoint keys represent a truly stationary channel.
            foreach(var binding in AnimationUtility.GetCurveBindings(clip)){
                var curve=AnimationUtility.GetEditorCurve(clip,binding);if(curve==null||curve.length<3)continue;
                var keys=curve.keys;float first=keys[0].value;if(keys.Any(k=>Mathf.Abs(k.value-first)>.00001f))continue;
                var constant=new AnimationCurve(new Keyframe(keys[0].time,first),new Keyframe(keys[keys.Length-1].time,first)){preWrapMode=curve.preWrapMode,postWrapMode=curve.postWrapMode};
                AnimationUtility.SetEditorCurve(clip,binding,constant);
            }
        }
        static AvatarMask Fingers(GameObject model,string path)
        {
            var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if(mask==null){mask=new AvatarMask();AssetDatabase.CreateAsset(mask,path);}
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers,false);mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers,false);
            var nodes=model.GetComponentsInChildren<Transform>(true).Skip(1).ToArray();mask.transformCount=nodes.Length;
            for(int i=0;i<nodes.Length;i++){
                mask.SetTransformPath(i,AnimationUtility.CalculateTransformPath(nodes[i],model.transform));
                bool finger=nodes[i].name.Contains("Hand")&&new[]{"Thumb","Index","Middle","Ring","Pinky"}.Any(f=>nodes[i].name.Contains(f));
                mask.SetTransformActive(i,finger);
            }
            EditorUtility.SetDirty(mask);return mask;
        }
        public static void Import()
        {
            const string source="Assets/_Project/Art/Characters/Animations/Map01Operations/",output="Assets/Resources/OperationsAnimation/";
            Directory.CreateDirectory(output);AssetDatabase.Refresh();
            var avatar=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Player/Player.fbx").OfType<Avatar>().First(a=>a.isHuman);
            var mask=Fingers(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Player/Player.fbx"),source+"SupplyHands.mask");
            foreach(string name in new[]{"Nam_SupplyCollect","Nam_SupplyHandoff","Nam_RadioPanel"}){
                string path=source+name+".fbx";if(!File.Exists(path))continue;
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Human;
                importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;importer.sourceAvatar=avatar;importer.preserveHierarchy=true;importer.animationCompression=ModelImporterAnimationCompression.Off;
                var clipMask=mask;
                var take=importer.defaultClipAnimations.First();take.name=name;take.firstFrame=1;take.lastFrame=205;take.loopTime=name=="Nam_RadioPanel";take.lockRootRotation=true;take.lockRootPositionXZ=true;take.lockRootHeightY=true;take.heightFromFeet=true;
                take.maskType=ClipAnimationMaskType.CopyFromOther;take.maskSource=clipMask;take.ConfigureClipFromMask(clipMask);
                importer.clipAnimations=new[]{take};importer.SaveAndReimport();
                var imported=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));if(!imported.humanMotion)throw new Exception("Operation clip is not Humanoid: "+name);
                int fingers=AnimationUtility.GetCurveBindings(imported).Count(b=>b.type==typeof(Transform)&&b.path.Contains("RightHand")&&b.propertyName.StartsWith("m_LocalRotation"));
                if(fingers<45)throw new Exception("Supply grip lost its authored finger rotations: "+name+" curves="+fingers);
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(output+name+".anim");
                if(existing!=null){EditorUtility.CopySerialized(imported,existing);EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(Object.Instantiate(imported),output+name+".anim");
                existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(output+name+".anim");
                if(name=="Nam_SupplyCollect"){
                    // Humanoid retargeting can flip the wrist near a pronated pickup pose.
                    // Keep the authored arm channels on the identical original skeleton, like the fingers.
                    string rawPath=source+name+"_ArmSource.fbx";
                    File.Copy(path,rawPath,true);AssetDatabase.ImportAsset(rawPath,ImportAssetOptions.ForceUpdate);
                    var rawImporter=(ModelImporter)AssetImporter.GetAtPath(rawPath);
                    rawImporter.animationType=ModelImporterAnimationType.Generic;rawImporter.avatarSetup=ModelImporterAvatarSetup.NoAvatar;
                    rawImporter.preserveHierarchy=true;rawImporter.animationCompression=ModelImporterAnimationCompression.Off;
                    var rawTake=rawImporter.defaultClipAnimations.First();rawTake.firstFrame=1;rawTake.lastFrame=205;rawImporter.clipAnimations=new[]{rawTake};rawImporter.SaveAndReimport();
                    var rawClip=AssetDatabase.LoadAllAssetsAtPath(rawPath).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
                    EditorUtility.CopySerialized(rawClip,existing);existing.name=name;
                    var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));
                    var genericAvatar=AvatarBuilder.BuildGenericAvatar(model.GetComponentInChildren<Animator>().gameObject,"");genericAvatar.name="Nam Supply Collect Avatar";
                    string avatarPath=output+"Nam_SupplyCollectAvatar.asset";var savedAvatar=AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
                    if(savedAvatar!=null){EditorUtility.CopySerialized(genericAvatar,savedAvatar);EditorUtility.SetDirty(savedAvatar);UnityEngine.Object.DestroyImmediate(genericAvatar);}else AssetDatabase.CreateAsset(genericAvatar,avatarPath);
                    UnityEngine.Object.DestroyImmediate(model);
                    existing.EnsureQuaternionContinuity();
                }
                CompactConstantCurves(existing);EditorUtility.SetDirty(existing);
            }AssetDatabase.SaveAssets();
        }
    }
}
