using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ShadowVale.Editor
{
    // Changes the two stone states only. The base layer remains responsible for
    // crouching and walking; this take supplies the crouched torso and arms.
    public static class CrouchStoneAnimationSetup
    {
        const string Folder="Assets/_Project/Art/Characters/Animations/StoneThrow";
        const string PathToClip=Folder+"/Nam_Crouch_Throw.anim";
        [MenuItem("ShadowVale/Characters/Polish crouched stone throw")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));
            try {
                foreach(var b in root.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var actor=root.GetComponentInChildren<Animator>();actor.Rebind();actor.Update(0);
                var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Animations/KnifeCarry/Nam_Knife_Sneak_Idle.anim");
                var clip=new AnimationClip{name="Nam_Crouch_Throw",frameRate=30};
                // Start with the original crouched body, rather than retargeting a
                // standing throw over crouched legs with a different shoulder frame.
                foreach(var binding in AnimationUtility.GetCurveBindings(idle))
                    AnimationUtility.SetEditorCurve(clip,binding,AnimationUtility.GetEditorCurve(idle,binding));
                var ids=Enumerable.Range(0,HumanTrait.MuscleCount).Where(i=>{
                    var n=HumanTrait.MuscleName[i];return n.StartsWith("Spine")||n.StartsWith("Chest")||n.StartsWith("UpperChest")||n.StartsWith("Head")||n.StartsWith("Neck")||
                        (n.StartsWith("Left ")||n.StartsWith("Right "))&&(n.Contains("Arm")||n.Contains("Shoulder")||n.Contains("Forearm")||n.Contains("Hand")||n.Contains("Stretched")||n.Contains("Spread"));
                }).ToArray();
                var curves=ids.ToDictionary(i=>i,i=>new List<Keyframe>());
                using(var handler=new HumanPoseHandler(actor.avatar,actor.transform)) {
                    var human=new HumanPose();
                    var neutral=new Dictionary<HumanBodyBones,Quaternion>();
                    var relaxed=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Animations/Generated/Idle_Tuned.anim");
                    relaxed.SampleAnimation(actor.gameObject,relaxed.length*.5f);
                    foreach(var bone in new[]{HumanBodyBones.LeftShoulder,HumanBodyBones.RightShoulder,HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightHand})
                        neutral[bone]=actor.GetBoneTransform(bone).localRotation;
                    for(int frame=0;frame<=60;frame++) {
                        float phase=frame/60f,time=frame/30f;
                        idle.SampleAnimation(actor.gameObject,0);
                        foreach(var n in neutral)actor.GetBoneTransform(n.Key).localRotation=n.Value;
                        var chest=actor.GetBoneTransform(HumanBodyBones.Chest)??actor.GetBoneTransform(HumanBodyBones.Spine);
                        float wind=Mathf.SmoothStep(0,1,phase/.42f),release=Mathf.SmoothStep(0,1,(phase-.42f)/.16f),recover=Mathf.SmoothStep(0,1,(phase-.65f)/.35f);
                        float turn=(wind-release)*(1-recover);
                        chest.localRotation*=Quaternion.Euler(0,turn*-7,0);
                        var shoulder=actor.transform.InverseTransformPoint(actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position);
                        float y=shoulder.y;
                        var rest=new Vector3(.25f,y-.30f,.27f);
                        var back=new Vector3(.33f,y+.08f,-.18f);
                        var front=new Vector3(.23f,y+.06f,.57f);
                        var follow=new Vector3(.24f,y-.17f,.53f);
                        var goal=phase<.42f?Vector3.Lerp(rest,back,wind):phase<.58f?Vector3.Lerp(back,front,release):Vector3.Lerp(front,follow,Mathf.SmoothStep(0,1,(phase-.58f)/.12f));
                        goal=Vector3.Lerp(goal,rest,recover);
                        Arm(actor,true,goal,new Vector3(.9f,-.15f,-.25f));
                        // The left arm balances the throw without crossing the right
                        // wrist or retaining the knife's two-handed carry pose.
                        Arm(actor,false,new Vector3(-.27f,y-.36f,.25f+release*.035f*(1-recover)),new Vector3(-.8f,-.4f,0));
                        handler.GetHumanPose(ref human);
                        foreach(var i in ids)curves[i].Add(new Keyframe(time,human.muscles[i]));
                    }
                }
                foreach(var pair in curves)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),HumanTrait.MuscleName[pair.Key]),new AnimationCurve(pair.Value.ToArray()));
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;settings.loopBlend=false;settings.stopTime=2;AnimationUtility.SetAnimationClipSettings(clip,settings);
                var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(PathToClip);
                if(old==null){AssetDatabase.CreateAsset(clip,PathToClip);old=clip;}else{EditorUtility.CopySerialized(clip,old);EditorUtility.SetDirty(old);Object.DestroyImmediate(clip);}
                var held=new AnimationClip{name="Nam_Crouch_ThrowAim",frameRate=30};
                foreach(var binding in AnimationUtility.GetCurveBindings(old)) {
                    float value=AnimationUtility.GetEditorCurve(old,binding).Evaluate(.84f);
                    AnimationUtility.SetEditorCurve(held,binding,AnimationCurve.Constant(0,2,value));
                }
                var heldSettings=AnimationUtility.GetAnimationClipSettings(held);heldSettings.loopTime=false;heldSettings.stopTime=2;AnimationUtility.SetAnimationClipSettings(held,heldSettings);
                string heldPath=Folder+"/Nam_Crouch_ThrowAim.anim";
                var storedHold=AssetDatabase.LoadAssetAtPath<AnimationClip>(heldPath);
                if(storedHold==null){AssetDatabase.CreateAsset(held,heldPath);storedHold=held;}
                else{EditorUtility.CopySerialized(held,storedHold);EditorUtility.SetDirty(storedHold);Object.DestroyImmediate(held);}
                Wire(old,storedHold);AssetDatabase.SaveAssets();Debug.Log("CROUCH_STONE_READY");
            }finally{Object.DestroyImmediate(root);}
        }
        static void Arm(Animator actor,bool right,Vector3 goal,Vector3 pole)
        {
            var upper=actor.GetBoneTransform(right?HumanBodyBones.RightUpperArm:HumanBodyBones.LeftUpperArm);
            var lower=actor.GetBoneTransform(right?HumanBodyBones.RightLowerArm:HumanBodyBones.LeftLowerArm);
            var hand=actor.GetBoneTransform(right?HumanBodyBones.RightHand:HumanBodyBones.LeftHand);
            var origin=upper.position;var target=actor.transform.TransformPoint(goal);var axis=(target-origin).normalized;
            float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,hand.position),d=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.001f,(a+b)*.94f);
            float along=(a*a+d*d-b*b)/(2*d);var bend=Vector3.ProjectOnPlane(actor.transform.TransformDirection(pole),axis).normalized;
            var elbow=origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,origin+axis*d-lower.position)*lower.rotation;
        }
        static void Wire(AnimationClip clip,AnimationClip heldClip)
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorBuilder.ControllerPath);
            var sm=controller.layers.Single(l=>l.name=="ActionsUpper").stateMachine;
            var normalAim=sm.states.Single(s=>s.state.name=="Act_ThrowAim").state;
            var normalThrow=sm.states.Single(s=>s.state.name=="Act_Throw").state;
            var none=sm.defaultState;
            AnimatorState State(string name,bool held) {
                var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==name)??sm.AddState(name);
                foreach(var t in state.transitions.ToArray())state.RemoveTransition(t);
                state.motion=held?heldClip:clip;state.writeDefaultValues=false;state.speed=held?0:1.2f;state.cycleOffset=0;
                return state;
            }
            var aim=State("Act_ThrowAimCrouch",true);var throwing=State("Act_ThrowCrouch",false);
            foreach(var t in sm.anyStateTransitions.Where(t=>t.destinationState==aim||t.destinationState==throwing).ToArray())sm.RemoveAnyStateTransition(t);
            // Rebuild just the routing for the existing stone actions.
            foreach(var t in sm.anyStateTransitions.Where(t=>t.destinationState==normalAim||t.destinationState==normalThrow)) {
                var conditions=t.conditions.Where(c=>c.parameter!="Sneaking").ToList();
                conditions.Add(new AnimatorCondition{mode=AnimatorConditionMode.IfNot,parameter="Sneaking"});t.conditions=conditions.ToArray();
                // Direct throws and held throws must start at the same wind-up.
                if(t.destinationState==normalThrow)t.offset=.42f;
            }
            foreach(var pair in new[]{(state:aim,id:2),(state:throwing,id:3)}) {
                var t=sm.AddAnyStateTransition(pair.state);t.hasExitTime=false;t.duration=pair.id==2?.2f:.06f;t.canTransitionToSelf=false;t.offset=pair.id==3?.42f:0;
                t.AddCondition(AnimatorConditionMode.If,0,"DoAction");t.AddCondition(AnimatorConditionMode.Equals,pair.id,"Action");t.AddCondition(AnimatorConditionMode.If,0,"Sneaking");
            }
            // Aim -> throw stays continuous, including changes of stance while holding Q.
            foreach(var t in normalAim.transitions.Where(t=>t.destinationState==normalThrow).ToArray())normalAim.RemoveTransition(t);
            void Link(AnimatorState from,AnimatorState to,params (AnimatorConditionMode mode,string key,float value)[] conditions) {
                var t=from.AddTransition(to);t.hasExitTime=false;t.duration=.12f;
                if(to==throwing||to==normalThrow)t.offset=.42f;
                foreach(var c in conditions)t.AddCondition(c.mode,c.value,c.key);
            }
            Link(aim,none,(AnimatorConditionMode.IfNot,"ActionHold",0),(AnimatorConditionMode.NotEqual,"Action",3));
            Link(aim,throwing,(AnimatorConditionMode.If,"DoAction",0),(AnimatorConditionMode.Equals,"Action",3),(AnimatorConditionMode.If,"Sneaking",0));
            Link(normalAim,normalThrow,(AnimatorConditionMode.If,"DoAction",0),(AnimatorConditionMode.Equals,"Action",3),(AnimatorConditionMode.IfNot,"Sneaking",0));
            foreach(var t in normalAim.transitions.Where(t=>t.name=="Stone stance").ToArray())normalAim.RemoveTransition(t);
            Link(normalAim,aim,(AnimatorConditionMode.If,"ActionHold",0),(AnimatorConditionMode.If,"Sneaking",0));normalAim.transitions.Last().name="Stone stance";
            Link(aim,normalAim,(AnimatorConditionMode.If,"ActionHold",0),(AnimatorConditionMode.IfNot,"Sneaking",0));
            var back=throwing.AddTransition(none);back.hasExitTime=true;back.exitTime=1;back.duration=.12f;
            EditorUtility.SetDirty(controller);
        }
    }
}
