using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ShadowVale.Editor
{
    // Only knife carry changes. Source FBXs, attacks, takedowns and rifle states stay intact.
    public static class KnifeLocomotionSetup
    {
        const string Folder="Assets/_Project/Art/Characters/Animations/KnifeCarry";
        const string Nam="Assets/_Project/Art/Characters/Animations/Nam/";
        [MenuItem("ShadowVale/Characters/Polish standing knife wrists")]
        public static void PolishStanding()
        {
            var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));
            try{
                foreach(var behaviour in player.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                player.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var actor=player.GetComponentInChildren<Animator>();actor.Rebind();actor.Update(0);
                using(var pose=new HumanPoseHandler(actor.avatar,actor.transform)){
                    Bake(actor,pose,"Nam_Rifle_Idle","Nam_Knife_Idle",false,false);
                    Bake(actor,pose,"Nam_Rifle_Walk_F","Nam_Knife_Walk",false,true);
                    Bake(actor,pose,"Nam_Rifle_Run","Nam_Knife_Run",false,true);
                }
                AssetDatabase.SaveAssets();
            }finally{Object.DestroyImmediate(player);}
        }
        [MenuItem("ShadowVale/Characters/Polish sneaking knife wrists")]
        public static void PolishSneaking()
        {
            var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));
            try{
                foreach(var behaviour in player.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                player.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var actor=player.GetComponentInChildren<Animator>();actor.Rebind();actor.Update(0);
                using(var pose=new HumanPoseHandler(actor.avatar,actor.transform)){
                    Bake(actor,pose,"Nam_Crouch_Idle","Nam_Knife_Sneak_Idle",true,false);
                    Bake(actor,pose,"Nam_Crouch_Walk_F","Nam_Knife_Sneak_Walk",true,true);
                }
                AssetDatabase.SaveAssets();
            }finally{Object.DestroyImmediate(player);}
        }
        [MenuItem("ShadowVale/Characters/Update knife walk and sneak")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));
            try{
                foreach(var behaviour in player.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                player.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var actor=player.GetComponentInChildren<Animator>();actor.Rebind();actor.Update(0);
                using(var pose=new HumanPoseHandler(actor.avatar,actor.transform)){
                    Bake(actor,pose,"Nam_Rifle_Idle","Nam_Knife_Idle",false,false);
                    Bake(actor,pose,"Nam_Rifle_Walk_F","Nam_Knife_Walk",false,true);
                    Bake(actor,pose,"Nam_Rifle_Run","Nam_Knife_Run",false,true);
                    Bake(actor,pose,"Nam_Crouch_Idle","Nam_Knife_Sneak_Idle",true,false);
                    Bake(actor,pose,"Nam_Crouch_Walk_F","Nam_Knife_Sneak_Walk",true,true);
                }
                Wire();AssetDatabase.SaveAssets();
            }finally{Object.DestroyImmediate(player);}
        }
        static void Bake(Animator actor,HumanPoseHandler handler,string sourceName,string name,bool crouch,bool moving)
        {
            var source=AssetDatabase.LoadAllAssetsAtPath(Nam+sourceName+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
            var result=new AnimationClip{name=name,frameRate=30};
            foreach(var binding in AnimationUtility.GetCurveBindings(source))
                AnimationUtility.SetEditorCurve(result,binding,AnimationUtility.GetEditorCurve(source,binding));
            var armIds=Enumerable.Range(0,HumanTrait.MuscleCount).Where(i=>{
                var n=HumanTrait.MuscleName[i];return ((n.StartsWith("Left ")||n.StartsWith("Right "))&&(n.Contains("Arm")||n.Contains("Shoulder")||n.Contains("Forearm")||n.Contains("Hand")))
                    ||(!crouch&&(n.StartsWith("Spine")||n.StartsWith("Chest")||n.StartsWith("UpperChest")||n.StartsWith("Neck")||n.StartsWith("Head")));
            }).ToArray();
            var keys=armIds.ToDictionary(i=>i,i=>new System.Collections.Generic.List<Keyframe>());
            // A finger direction alone leaves wrist roll undefined. Carry the neutral
            // local rotations through the arm solve instead of twisting a rifle wrist
            // until its fingers happen to point along a world-space direction.
            var torso=new[]{HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest,HumanBodyBones.Neck,HumanBodyBones.Head};
            var neutralBones=new[]{HumanBodyBones.LeftShoulder,HumanBodyBones.RightShoulder,
                HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftLowerArm,
                HumanBodyBones.RightLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightHand}.Concat(crouch?Array.Empty<HumanBodyBones>():torso);
            var neutral=new System.Collections.Generic.Dictionary<HumanBodyBones,Quaternion>();
            {
                var reference=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Animations/Generated/Idle_Tuned.anim");
                reference.SampleAnimation(actor.gameObject,reference.length*.5f);
                foreach(var id in neutralBones){var bone=actor.GetBoneTransform(id);if(bone!=null)neutral[id]=bone.localRotation;}
            }
            int count=Mathf.CeilToInt(source.length*30);var human=new HumanPose();
            for(int frame=0;frame<=count;frame++){
                float t=source.length*frame/count;source.SampleAnimation(actor.gameObject,t);
                foreach(var pair in neutral){
                    var bone=actor.GetBoneTransform(pair.Key);
                    bone.localRotation=torso.Contains(pair.Key)?Quaternion.Slerp(pair.Value,bone.localRotation,.35f):pair.Value;
                }
                // Authored Nam legs/hips are retained. Compact, relaxed arms follow the gait,
                // without a frozen stabbing frame or a sideways sneak take.
                float wave=moving?Mathf.Sin(2*Mathf.PI*frame/count):Mathf.Sin(2*Mathf.PI*frame/count)*.15f;
                var hips=actor.GetBoneTransform(HumanBodyBones.Hips);float hipY=actor.transform.InverseTransformPoint(hips.position).y;
                PoseArm(actor,true,new Vector3(.28f,hipY+(crouch?.18f:.04f),crouch?.28f:.20f+wave*.035f));
                PoseArm(actor,false,new Vector3(-.28f,hipY+(crouch?.10f:.015f),crouch?.22f+wave*.04f:.06f-wave*.16f));
                handler.GetHumanPose(ref human);
                foreach(int i in armIds)keys[i].Add(new Keyframe(t,human.muscles[i]));
            }
            foreach(int i in armIds){
                var k=keys[i];k[k.Count-1]=new Keyframe(source.length,k[0].value);
                var curve=new AnimationCurve(k.ToArray());
                for(int j=0;j<curve.length;j++){
                    AnimationUtility.SetKeyLeftTangentMode(curve,j,AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve,j,AnimationUtility.TangentMode.ClampedAuto);
                }
                AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve("",typeof(Animator),HumanTrait.MuscleName[i]),curve);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(source);settings.loopTime=true;settings.loopBlend=true;
            AnimationUtility.SetAnimationClipSettings(result,settings);
            var path=Folder+"/"+name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(old==null)AssetDatabase.CreateAsset(result,path);else{EditorUtility.CopySerialized(result,old);Object.DestroyImmediate(result);EditorUtility.SetDirty(old);}
        }
        static void PoseArm(Animator a,bool right,Vector3 goal)
        {
            var upper=a.GetBoneTransform(right?HumanBodyBones.RightUpperArm:HumanBodyBones.LeftUpperArm);
            var lower=a.GetBoneTransform(right?HumanBodyBones.RightLowerArm:HumanBodyBones.LeftLowerArm);
            var hand=a.GetBoneTransform(right?HumanBodyBones.RightHand:HumanBodyBones.LeftHand);
            var target=a.transform.TransformPoint(goal);var origin=upper.position;var axis=(target-origin).normalized;
            float x=Vector3.Distance(origin,lower.position),y=Vector3.Distance(lower.position,hand.position);
            float distance=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(x-y)+.001f,(x+y)*.96f);
            target=origin+axis*distance;float along=(x*x+distance*distance-y*y)/(2*distance);
            var bend=Vector3.ProjectOnPlane(a.transform.right*(right?1:-1)*.7f-a.transform.up*.5f,axis).normalized;
            var elbow=origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,x*x-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;

        }
        static AnimationClip Clip(string n)=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/Nam_Knife_"+n+".anim");
        static AnimatorState Find(AnimatorStateMachine sm,string n)=>sm.states.Select(c=>c.state).FirstOrDefault(s=>s.name==n);
        static void Link(AnimatorState from,AnimatorState to,params (AnimatorConditionMode mode,float value,string parameter)[] conditions)
        {
            foreach(var old in from.transitions.Where(t=>t.name=="Knife carry / "+to.name).ToArray())from.RemoveTransition(old);
            var t=from.AddTransition(to);t.name="Knife carry / "+to.name;t.hasExitTime=false;t.duration=.18f;
            foreach(var c in conditions)t.AddCondition(c.mode,c.value,c.parameter);
        }
        static void Wire()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorBuilder.ControllerPath);var root=controller.layers[0].stateMachine;
            var normal=Find(root,"Locomotion_Knife")??root.AddState("Locomotion_Knife");
            var sneak=Find(root,"Sneak_Knife")??root.AddState("Sneak_Knife");
            foreach(var state in new[]{normal,sneak}){
                if(state.motion!=null&&AssetDatabase.GetAssetPath(state.motion)==PlayerAnimatorBuilder.ControllerPath)Object.DestroyImmediate(state.motion,true);
                state.writeDefaultValues=false;state.speedParameterActive=false;state.speed=1;
            }
            var walk=new BlendTree{name="Knife carry walk",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false};
            walk.AddChild(Clip("Idle"),0);walk.AddChild(Clip("Walk"),2.5f);walk.AddChild(Clip("Run"),6);
            var children=walk.children;children[1].timeScale=2.5f/1.94f;children[2].timeScale=6/4.34f;walk.children=children;
            AssetDatabase.AddObjectToAsset(walk,controller);normal.motion=walk;
            var crouch=new BlendTree{name="Knife carry sneak",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false};
            crouch.AddChild(Clip("Sneak_Idle"),0);crouch.AddChild(Clip("Sneak_Walk"),1.2f);
            children=crouch.children;children[1].timeScale=1.2f/2.05f;crouch.children=children;
            AssetDatabase.AddObjectToAsset(crouch,controller);sneak.motion=crouch;
            foreach(string name in new[]{"Locomotion","Sneak","Locomotion_Rifle","Sneak_Rifle"}){
                var from=Find(root,name);if(from==null)continue;
                // Place knife selection ahead of generic Sneak transitions.
                Link(from,normal,(AnimatorConditionMode.Equals,1,"Weapon"),(AnimatorConditionMode.IfNot,0,"Sneaking"));
                Link(from,sneak,(AnimatorConditionMode.Equals,1,"Weapon"),(AnimatorConditionMode.If,0,"Sneaking"));
                var mine=from.transitions.Where(t=>t.name.StartsWith("Knife carry / ")).ToArray();
                from.transitions=mine.Concat(from.transitions.Except(mine)).ToArray();
            }
            Link(normal,sneak,(AnimatorConditionMode.If,0,"Sneaking"));Link(sneak,normal,(AnimatorConditionMode.IfNot,0,"Sneaking"));
            Link(normal,Find(root,"Locomotion"),(AnimatorConditionMode.NotEqual,1,"Weapon"));
            Link(sneak,Find(root,"Sneak"),(AnimatorConditionMode.NotEqual,1,"Weapon"));
            foreach(var s in new[]{normal,sneak}){
                Link(s,Find(root,"Locomotion_Rifle"),(AnimatorConditionMode.Equals,2,"Weapon"),(AnimatorConditionMode.IfNot,0,"Sneaking"));
                Link(s,Find(root,"Sneak_Rifle"),(AnimatorConditionMode.Equals,2,"Weapon"),(AnimatorConditionMode.If,0,"Sneaking"));
                var direct=s.transitions.Where(t=>t.destinationState!=null&&t.destinationState.name.EndsWith("_Rifle")).ToArray();
                s.transitions=direct.Concat(s.transitions.Except(direct)).ToArray();
            }
            var jump=Find(root,"Jump");if(jump!=null)foreach(var s in new[]{normal,sneak})Link(s,jump,(AnimatorConditionMode.If,0,"Jump"));
            var hold=Find(controller.layers[1].stateMachine,"Hold_Knife");if(hold!=null){hold.motion=Clip("Idle");hold.speed=1;hold.cycleOffset=0;}
            EditorUtility.SetDirty(controller);
        }
    }
}
