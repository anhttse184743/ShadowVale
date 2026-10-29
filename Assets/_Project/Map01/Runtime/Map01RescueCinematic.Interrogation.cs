using System;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The interrogation blows in the intro: the punches, their contact on Hùng's body, and the thud.
    /// </summary>
    public sealed partial class Map01RescueCinematic
    {
        static float InterrogationRecoilTime(float age)=>age<.1f?.8f+.55f*Mathf.Clamp01(age/.1f):age<.3f?1.35f:Mathf.Lerp(1.35f,3,Mathf.Clamp01((age-.3f)/.5f));
        void RestoreInterrogationStanding(){
            if(interrogationStanding!=null)foreach(var b in interrogationStanding)b.bone.SetLocalPositionAndRotation(b.position,b.rotation);
        }
        static Vector3 PunchKnuckle(Animator actor,bool left=false){
            var knuckle=actor.GetBoneTransform(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
            return knuckle!=null?knuckle.position:actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand).position;
        }
        void CalibrateInterrogationContact(Vector3 direction){
            var actor=enemy.GetComponentInChildren<Animator>();var initial=enemy.transform.rotation;
            var hips=captiveVisual.actor.GetBoneTransform(HumanBodyBones.Hips).position;
            var chest=captiveVisual.actor.GetBoneTransform(HumanBodyBones.Chest).position;
            // Upper abdomen below the rib cage, above the belt and hip pouches.
            var belly=Vector3.Lerp(hips,chest,.38f)+Vector3.up*.15f+direction*.16f;
            leftBodyTarget=belly-mission.hung.right*.035f;rightBodyTarget=belly+mission.hung.right*.035f;
            float floor=interrogatorStart.y;
            Vector3 Measure(AnimationClip clip,bool left,Vector3 target,out float lean){
                lean=0;float best=float.PositiveInfinity;
                for(int angle=0;angle<=54;angle+=2){
                    enemy.transform.rotation=initial;interrogatorPose.Sample(clip,clip.length*(.7f/1.7f));LeanPunch(actor,angle);interrogatorPose.Ground(floor);
                    float error=Mathf.Abs(PunchKnuckle(actor,left).y-target.y);
                    if(error<best){best=error;lean=angle;}
                }
                interrogatorPose.Sample(clip,clip.length*(.7f/1.7f));LeanPunch(actor,lean);interrogatorPose.Ground(floor);
                return Quaternion.Inverse(initial)*(PunchKnuckle(actor,left)-enemy.transform.position);
            }
            Vector3 leftOffset=default,rightOffset=default;
            var samplePosition=interrogatorStart;
            // Ground contact belongs to the final stance, not the guard's
            // original post (the pier/navmesh heights can differ).
            for(int pass=0;pass<3;pass++){
                enemy.transform.position=samplePosition;floor=samplePosition.y;
                leftOffset=Measure(interrogationLeftPunch,true,leftBodyTarget,out leftLean);
                rightOffset=Measure(interrogationPunch,false,rightBodyTarget,out rightLean);
                float reach=(Vector3.ProjectOnPlane(leftOffset,Vector3.up).magnitude+Vector3.ProjectOnPlane(rightOffset,Vector3.up).magnitude)*.5f;
                var desired=belly+direction*reach;desired.y=samplePosition.y;
                canApproach=NavMesh.SamplePosition(desired,out var contact,.18f,NavMesh.AllAreas)&&!NavMesh.Raycast(interrogatorStart,contact.position,out _,NavMesh.AllAreas);
                interrogatorClose=canApproach?contact.position:interrogatorStart;samplePosition=interrogatorClose;
            }
            Quaternion Aim(Vector3 offset,Vector3 target){
                var to=Vector3.ProjectOnPlane(target-interrogatorClose,Vector3.up).normalized;
                return Quaternion.LookRotation(to)*Quaternion.AngleAxis(-Mathf.Atan2(offset.x,offset.z)*Mathf.Rad2Deg,Vector3.up);
            }
            leftFacing=Aim(leftOffset,leftBodyTarget);rightFacing=Aim(rightOffset,rightBodyTarget);interrogationFacing=leftFacing;
            enemy.transform.SetPositionAndRotation(interrogatorStart,initial);
        }
        void LeanPunch(Animator actor,float degrees){
            var spine=actor.GetBoneTransform(HumanBodyBones.Spine);
            if(spine!=null)spine.rotation=Quaternion.AngleAxis(degrees,enemy.transform.right)*spine.rotation;
        }
        void RenderInterrogation(){
            float walkIn=Mathf.SmoothStep(0,1,(Elapsed-IntroWalkAt)/1.35f);
            float walkOut=Mathf.SmoothStep(0,1,(Elapsed-IntroWalkBackAt)/1.25f);
            enemy.transform.position=Vector3.Lerp(interrogatorStart,interrogatorClose,walkIn*(1-walkOut));
            enemy.FaceImmediate(mission.hung.position);
            // The mocap punch crosses the torso. Lock its authored heading
            // at the stance anchor; re-aiming the root at Hung would turn the
            // whole arc away from the calibrated contact point.
            float stance=Mathf.SmoothStep(0,1,(Elapsed-IntroStrikeAt+.25f)/.25f)*(1-Mathf.SmoothStep(0,1,(Elapsed-IntroWalkBackAt)/.25f));
            var fightingFacing=Elapsed<IntroRightStrikeAt?leftFacing:Quaternion.Slerp(leftFacing,rightFacing,Mathf.SmoothStep(0,1,(Elapsed-IntroRightStrikeAt)/.4f));
            enemy.transform.rotation=Quaternion.Slerp(enemy.transform.rotation,fightingFacing,stance);
            var actor=enemy.GetComponentInChildren<Animator>();
            var held=executionAim??interrogationIdle;
            float sling=0;
            if(Elapsed<1.8f){
                sling=Mathf.SmoothStep(0,1,(Elapsed-.05f)/1.45f);
                interrogatorPose.SampleBlend(held,0,interrogationIdle,Elapsed,Mathf.SmoothStep(0,1,(sling-.45f)/.55f));RestoreInterrogationStanding();
            }else if(Elapsed<IntroWalkAt){
                interrogatorPose.Sample(interrogationIdle,Elapsed,true);RestoreInterrogationStanding();sling=1;
            }else if(Elapsed<IntroStrikeAt){
                float phase=Vector3.Distance(interrogatorStart,interrogatorClose)*walkIn/1.4f;
                float weight=Mathf.Min(Mathf.SmoothStep(0,1,(Elapsed-IntroWalkAt)/.16f),1-Mathf.SmoothStep(0,1,(Elapsed-IntroStrikeAt+.16f)/.16f));
                interrogatorPose.SampleBlend(interrogationIdle,0,interrogationWalk,phase,weight);sling=1;
            }else if(Elapsed<IntroWalkBackAt){
                bool left=Elapsed<IntroRightStrikeAt;
                var clip=left?interrogationLeftPunch:interrogationPunch;
                float begin=left?IntroStrikeAt:IntroRightStrikeAt;
                float planned=left?IntroHitAt:IntroRightHitAt;
                float actual=left?introImpactAt:introRightImpactAt;bool impacted=left?introImpact:introRightImpact;
                float age=Elapsed-begin-(impacted?Mathf.Max(0,actual-planned):0);
                float time=age<.45f?age*.4f/.45f:age<.6f?.4f+(age-.45f)*2:age<.68f?.7f+(age-.6f)*2.5f:.9f+(age-.68f)*(.8f/.52f);
                float nativeTime=Mathf.Clamp01(time/1.7f)*clip.length;
                float enter=Mathf.SmoothStep(0,1,age/.13f);
                float end=left?IntroRightStrikeAt:IntroWalkBackAt;
                float exit=Mathf.SmoothStep(0,1,(Elapsed-end+.18f)/.18f);
                if(exit>0)interrogatorPose.SampleBlend(clip,nativeTime,interrogationIdle,0,exit);
                else interrogatorPose.SampleBlend(interrogationIdle,0,clip,nativeTime,enter);
                LeanPunch(actor,(left?leftLean:rightLean)*enter*(1-exit));sling=1;
                // Measure the rendered, grounded fist, matching stance calibration.
                interrogatorPose.Ground(enemy.transform.position.y);
                if(impacted&&Elapsed-actual<.13f){
                    float error=Vector3.Distance(PunchKnuckle(actor,left),left?leftBodyTarget:rightBodyTarget);
                    if(left&&(leftContactError<0||error<leftContactError)){leftContactError=error;LeftBodyDelta=PunchKnuckle(actor,true)-leftBodyTarget;}
                    else if(!left&&(rightContactError<0||error<rightContactError)){rightContactError=error;RightBodyDelta=PunchKnuckle(actor,false)-rightBodyTarget;}
                    InterrogationContactError=Mathf.Max(leftContactError,rightContactError);
                }
            }else if(Elapsed<7.3f){
                float phase=Vector3.Distance(interrogatorStart,interrogatorClose)*walkOut/1.4f;
                float weight=Mathf.Min(Mathf.SmoothStep(0,1,(Elapsed-IntroWalkBackAt)/.15f),1-Mathf.SmoothStep(0,1,(Elapsed-7.15f)/.15f));
                interrogatorPose.SampleBlend(interrogationIdle,0,interrogationWalk,Mathf.Max(0,interrogationWalk.length-Mathf.Repeat(phase,interrogationWalk.length)),weight);sling=1;
            }else if(Elapsed<9.1f){
                float age=Elapsed-7.3f;
                sling=1-Mathf.SmoothStep(0,1,(age-.15f)/1.45f);
                interrogatorPose.SampleBlend(held,0,interrogationIdle,age,Mathf.SmoothStep(0,1,(sling-.45f)/.55f));RestoreInterrogationStanding();
            }else interrogatorPose.Sample(held,0);
            if(sling<.001f)actor.GetComponent<Map01EnemyGrip>()?.Apply();
            interrogationSling?.Sample(sling);
        }
        void PlayContactThud(){
            if(contactSource==null){contactSource=gameObject.AddComponent<AudioSource>();contactSource.playOnAwake=false;contactSource.spatialBlend=0;contactSource.volume=.26f;}
            if(contactThud==null){
                var data=new float[7200];uint seed=8128;
                for(int i=0;i<data.Length;i++){seed=1664525*seed+1013904223;float t=i/48000f;data[i]=(Mathf.Sin(t*2*Mathf.PI*95)*.6f+((seed>>8)/8388607.5f-1)*.13f)*Mathf.Exp(-t*38);}
                contactThud=AudioClip.Create("Authored cloth contact",data.Length,1,48000,false);contactThud.SetData(data,0);
            }
            contactSource.PlayOneShot(contactThud);
        }
    }
}
