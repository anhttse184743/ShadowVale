using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ShadowVale.Map01;

namespace ShadowVale.Editor
{
    [InitializeOnLoad]
    public static class EnemyPatrolPoseSetup
    {
        public const string Path = "Assets/_Project/Art/Characters/Animations/Generated/Enemy_Patrol_Walk_LowReady.anim";
        static EnemyPatrolPoseSetup() { EditorApplication.update += Poll; }
        static void Poll()
        {
            const string request="Tools/EnemyPatrolPose.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(request);
            try { Build(); File.WriteAllText("Tools/BossAnimationPreview.request","patrol"); }
            catch(Exception e) { File.WriteAllText("Tools/EnemyPatrolPose.result.txt",e.ToString()); Debug.LogException(e); }
        }
        static void Build()
        {
            var source=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Animations/Enemy/Enemy_Patrol_Walk.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
            var clip=UnityEngine.Object.Instantiate(source);
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Map01/Resources/Characters/EnemyCommanderVisual.prefab"));
            int[] torso=Enumerable.Range(0,HumanTrait.MuscleCount).Where(i=>new[]{"Spine ","Chest ","UpperChest ","Neck ","Head "}.Any(HumanTrait.MuscleName[i].StartsWith)).ToArray();
            try
            {
                model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var actor=model.GetComponent<Animator>();
                using var pose=new Map01CinematicPose(actor,skinGrounding:false);
                using var handler=new HumanPoseHandler(actor.avatar,actor.transform);
                var indices=Enumerable.Range(0,HumanTrait.MuscleCount).Where(i=>new[]{"Left Arm","Right Arm","Left Forearm","Right Forearm","Left Shoulder","Right Shoulder","Left Hand","Right Hand"}.Any(HumanTrait.MuscleName[i].StartsWith)).ToArray();
                var curves=indices.ToDictionary(i=>i,i=>new AnimationCurve());
                float[] carry=null;
                for(int frame=0;frame<=60;frame++)
                {
                    float t=source.length*frame/60f;
                    pose.Sample(source,t);
                    var chest=actor.GetBoneTransform(HumanBodyBones.Chest).position;
                    var human=new HumanPose(); handler.GetHumanPose(ref human);
                    foreach(int i in torso)human.muscles[i]=0;
                    handler.SetHumanPose(ref human);
                    chest=actor.GetBoneTransform(HumanBodyBones.Chest).position;
                    float scale=actor.humanScale;
                    var shoulders=(actor.GetBoneTransform(HumanBodyBones.LeftUpperArm).position+actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position)*.5f;
                    var bodyRight=Vector3.ProjectOnPlane(actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position-actor.GetBoneTransform(HumanBodyBones.LeftUpperArm).position,Vector3.up).normalized;
                    var bodyForward=Vector3.Cross(bodyRight,Vector3.up).normalized;
                    if(carry==null)foreach(bool left in new[]{false,true})
                    {
                        var upper=actor.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
                        var lower=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                        var hand=actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                        float upperLength=Vector3.Distance(upper.position,lower.position),foreLength=Vector3.Distance(lower.position,hand.position);
                        var elbow=upper.position+(bodyRight*(left?-.15f:.15f)-Vector3.up*.98f+bodyForward*.08f).normalized*upperLength;
                        var foreDirection=left?bodyRight*.12f-Vector3.up*.35f+bodyForward*.93f:-bodyRight*.80f+bodyForward*.60f;
                        var target=elbow+foreDirection.normalized*foreLength;
                        Solve(handler,actor,ref human,left,target,elbow);
                        File.AppendAllText("Tools/EnemyPatrolPose.targets.txt",(left?"Left":"Right")+" target="+target+" solved="+actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand).position+"\n");
                    }
                    if(carry==null)carry=(float[])human.muscles.Clone();
                    foreach(int i in indices)curves[i].AddKey(t,carry[i]);
                }
                foreach(int i in indices)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),HumanTrait.MuscleName[i]),curves[i]);
                foreach(int i in torso)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),HumanTrait.MuscleName[i]),AnimationCurve.Constant(0,source.length,0));
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(Path);
            if(existing==null) { AssetDatabase.CreateAsset(clip,Path);existing=clip; }
            else { EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip); }
            existing.name="Enemy_Patrol_Walk";EditorUtility.SetDirty(existing);
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyAnimatorBuilder.ControllerPath);
            foreach(var layer in controller.layers)Replace(layer.stateMachine,existing);
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
            Map01EnemyGrip.ResetPatrolCalibration();
            var oldBindings=AnimationUtility.GetCurveBindings(source).Where(b=>!indicesForArm(b.propertyName)&&!torso.Any(i=>HumanTrait.MuscleName[i]==b.propertyName));
            bool unchanged=oldBindings.All(b=>{
                var a=AnimationUtility.GetEditorCurve(source,b);var z=AnimationUtility.GetEditorCurve(existing,b);
                return z!=null&&a.keys.SequenceEqual(z.keys);
            });
            File.WriteAllText("Tools/EnemyPatrolPose.result.txt","Built upright patrol. Original leg/root curves unchanged="+unchanged);
        }
        static bool indicesForArm(string name)=>new[]{"Left Arm","Right Arm","Left Forearm","Right Forearm","Left Shoulder","Right Shoulder","Left Hand","Right Hand"}.Any(name.StartsWith);
        static void Replace(AnimatorStateMachine sm,AnimationClip clip)
        {
            foreach(var s in sm.states)ReplaceMotion(s.state.motion,clip);
            foreach(var child in sm.stateMachines)Replace(child.stateMachine,clip);
        }
        static void ReplaceMotion(Motion motion,AnimationClip clip)
        {
            if(motion is BlendTree tree)
            {
                var children=tree.children;
                for(int i=0;i<children.Length;i++)
                    if(children[i].motion is AnimationClip c&&c.name.StartsWith("Enemy_Patrol_Walk"))children[i].motion=clip;
                    else ReplaceMotion(children[i].motion,clip);
                tree.children=children;EditorUtility.SetDirty(tree);
            }
        }
        static void Solve(HumanPoseHandler handler,Animator actor,ref HumanPose human,bool left,Vector3 target,Vector3 elbowTarget)
        {
            string side=left?"Left ":"Right ";
            var arm=Enumerable.Range(0,HumanTrait.MuscleCount).Where(i=>new[]{"Arm","Forearm","Shoulder"}.Any(p=>HumanTrait.MuscleName[i].StartsWith(side+p))).ToArray();
            var wrist=Enumerable.Range(0,HumanTrait.MuscleCount).Where(i=>HumanTrait.MuscleName[i].StartsWith(side+"Hand")).ToArray();
            var hand=actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            var lower=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var tip=hand.Cast<Transform>().First();
            float Cost()=>Vector3.SqrMagnitude(hand.position-target)+.75f*Vector3.SqrMagnitude(lower.position-elbowTarget);
            foreach(int i in arm.Concat(wrist))human.muscles[i]=0;
            handler.SetHumanPose(ref human);
            for(int sweep=0;sweep<85;sweep++)
                foreach(int i in arm.Concat(wrist))
                {
                    bool isWrist=wrist.Contains(i);
                    float CostNow()=>isWrist?Vector3.Angle(hand.position-lower.position,tip.position-hand.position):Cost();
                    float v=human.muscles[i],best=v,score=CostNow(),step=sweep<25?.12f:sweep<55?.035f:.006f;
                    foreach(float delta in new[]{-step,step})
                    {
                        human.muscles[i]=Mathf.Clamp(v+delta,-1,1);handler.SetHumanPose(ref human);
                        float cost=CostNow();if(cost<score){score=cost;best=human.muscles[i];}
                    }
                    human.muscles[i]=best;handler.SetHumanPose(ref human);
                }
        }
    }
}
