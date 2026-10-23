using System;
using System.IO;
using System.Linq;
using ShadowVale.Map01;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ShadowVale.Editor
{
    /// <summary>Reuses only the neck strike and collapse of the existing paired clips.</summary>
    public static class QuickTakedownSetup
    {
        public const string Folder="Assets/_Project/Art/Characters/Animations/KnifeCarry/";
        public static AnimationClip ShortClip(string name)=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+(name=="Nam_Takedown"?"Nam_Takedown_Neck":"Enemy_Takedown_Neck_Victim")+".anim");
        [MenuItem("ShadowVale/Characters/Update single neck takedown")]
        public static void Build()
        {
            Bake("Assets/_Project/Art/Characters/Animations/Nam/Nam_Takedown.fbx","Nam_Takedown_Neck");
            Bake("Assets/_Project/Art/Characters/Animations/Enemy/Enemy_Takedown_Victim.fbx","Enemy_Takedown_Neck_Victim");
            Wire(PlayerAnimatorBuilder.ControllerPath,"Act_Takedown",ShortClip("Nam_Takedown"));
            Wire(EnemyAnimatorBuilder.ControllerPath,"TakedownVictim",ShortClip("Enemy_Takedown_Victim"));
            AssetDatabase.SaveAssets();
            Debug.Log("Single neck takedown pair baked at 30 fps, 2.2 seconds. Original FBXs retained.");
        }
        static void Bake(string sourcePath,string name)
        {
            var source=AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
            var clip=new AnimationClip{name=name,frameRate=30,wrapMode=WrapMode.ClampForever};
            foreach(var binding in AnimationUtility.GetCurveBindings(source)){
                var original=AnimationUtility.GetEditorCurve(source,binding);
                var keys=new Keyframe[67];
                for(int frame=0;frame<keys.Length;frame++){
                    float t=frame/30f;
                    // Interpolate the initial and neck-ready poses directly. Sampling the
                    // intervening source frames would replay the unwanted hip stab.
                    float value=t<Map01NamActions.TakedownWindupSeconds
                        ?Mathf.Lerp(original.Evaluate(0),original.Evaluate((Map01NamActions.TakedownReadyFrame-1)/30f),Mathf.SmoothStep(0,1,t/Map01NamActions.TakedownWindupSeconds))
                        :original.Evaluate((Map01NamActions.TakedownSourceFrame(t)-1)/30f);
                    keys[frame]=new Keyframe(t,value);
                }
                var curve=new AnimationCurve(keys);
                for(int i=0;i<curve.length;i++){
                    AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
                }
                AnimationUtility.SetEditorCurve(clip,binding,curve);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(source);settings.startTime=0;settings.stopTime=Map01NamActions.TakedownSeconds;
            settings.loopTime=false;settings.loopBlend=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
            string path=Folder+name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(old==null)AssetDatabase.CreateAsset(clip,path);else{EditorUtility.CopySerialized(clip,old);Object.DestroyImmediate(clip);EditorUtility.SetDirty(old);}
        }
        static void Wire(string path,string name,AnimationClip clip)
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            var state=controller.layers[0].stateMachine.states.Select(s=>s.state).Single(s=>s.name==name);
            state.motion=clip;state.speed=1;state.speedParameterActive=false;EditorUtility.SetDirty(controller);
        }
    }
}
