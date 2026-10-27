using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor {
public static class InterrogationMotionSetup {
    [MenuItem("ShadowVale/Rescue/Prepare native interrogation performance")]
    public static void Prepare(){
        const string target="Assets/Resources/Rescue/Story";
        Directory.CreateDirectory(target);
        Copy("Assets/_Project/Art/Characters/Animations/Clips/Punching.fbx","Enemy_Interrogation_Punch");
        Copy("Assets/_Project/Art/Characters/Animations/Clips/Standard Walk.fbx","Enemy_Interrogation_Walk");
        Copy("Assets/_Project/Art/Characters/Animations/Clips/Breathing Idle.fbx","Enemy_Interrogation_Idle");
        string leftPath=target+"/Enemy_Interrogation_Punch_Left.anim";
        if(AssetDatabase.LoadAssetAtPath<AnimationClip>(leftPath)==null){
            var original=AssetDatabase.LoadAssetAtPath<AnimationClip>(target+"/Enemy_Interrogation_Punch.anim");
            var left=UnityEngine.Object.Instantiate(original);left.name="Enemy_Interrogation_Punch_Left";
            var settings=AnimationUtility.GetAnimationClipSettings(left);settings.mirror=true;
            AnimationUtility.SetAnimationClipSettings(left,settings);AssetDatabase.CreateAsset(left,leftPath);
        }
        AssetDatabase.SaveAssets();
        void Copy(string path,string name){
            string output=target+"/"+name+".anim";
            if(AssetDatabase.LoadAssetAtPath<AnimationClip>(output)!=null)return;
            var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(x=>!x.name.StartsWith("__preview"));
            if(!clip.humanMotion)throw new InvalidOperationException("The interrogation requires a mapped Humanoid: "+path);
            var copy=UnityEngine.Object.Instantiate(clip);copy.name=name;AssetDatabase.CreateAsset(copy,output);
            Debug.Log(name+" copied from existing project clip, duration="+copy.length);
        }
    }
}}
