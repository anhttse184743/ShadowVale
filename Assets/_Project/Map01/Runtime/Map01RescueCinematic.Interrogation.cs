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
            enemy.transform.position=Vector3.Lerp(interrogatorStart,interrogatorClose,walkIn);
            enemy.FaceImmediate(mission.hung.position);
            // The mocap punch crosses the torso. Lock its authored heading
            // at the stance anchor; re-aiming the root at Hung would turn the
            // whole arc away from the calibrated contact point.
            float stance=Mathf.SmoothStep(0,1,(Elapsed-IntroStrikeAt+.25f)/.25f);
            var fightingFacing=Quaternion.LookRotation(Vector3.ProjectOnPlane(mission.hung.position-enemy.transform.position,Vector3.up).normalized);
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
            }else {
                float age=Elapsed-IntroStrikeAt;
                float cycle=Mathf.Repeat(age,PunchPeriod);
                float enter=Mathf.SmoothStep(0,1,age/.22f);
                float weight=enter;
                bool face=Mathf.FloorToInt(age/PunchPeriod)%2==1;
                // Anticipation, fast extension, a brief impact, then recovery.
                float thrust=cycle<.20f?0:cycle<.40f?1-Mathf.Pow(1-(cycle-.20f)/.20f,3):cycle<.44f?1:1-Mathf.SmoothStep(0,1,(cycle-.44f)/.28f);
                float nativeTime=interrogationPunch.length*((.30f+.40f*thrust)/1.7f);
                interrogatorPose.Sample(interrogationPunch,nativeTime);
                float high=face?Mathf.Sin(Mathf.PI*cycle/PunchPeriod):0;
                RestoreInterrogationStanding();BendInterrogatorKnees(actor,.30f*weight);LeanPunch(actor,35f*weight);
                var spine=actor.GetBoneTransform(HumanBodyBones.Spine);
                float wind=cycle<.20f?-8*Mathf.Sin(Mathf.PI*cycle/.40f):10*thrust;
                if(spine!=null)spine.rotation=Quaternion.AngleAxis(wind*weight,Vector3.up)*spine.rotation;
                interrogatorPose.Ground(enemy.transform.position.y);
                // One arm holds the near shoulder; the other has a short thrust
                // and a slower recovery. Contact follows the moving captive.
                var victim=captiveVisual.actor;
                var shoulderA=victim.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                var shoulderB=victim.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                var holdingOrigin=actor.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                var shoulder=Vector3.Distance(holdingOrigin,shoulderA)<Vector3.Distance(holdingOrigin,shoulderB)?shoulderA:shoulderB;
                var belly=Vector3.Lerp(victim.GetBoneTransform(HumanBodyBones.Hips).position,victim.GetBoneTransform(HumanBodyBones.Chest).position,.38f)+Vector3.up*.15f;
                var toward=(enemy.transform.position-mission.hung.position).normalized;
                var hit=belly+toward*.22f; // Clothing and belly surface, outside the skeleton centreline.
                if(face)hit=victim.GetBoneTransform(HumanBodyBones.Head).position+toward*.14f-Vector3.up*.055f;
                var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
                var fistOffset=PunchKnuckle(actor)-hand.position;
                var chamber=actor.GetBoneTransform(HumanBodyBones.Chest).position+enemy.transform.right*.24f+enemy.transform.forward*(cycle<.20f?.04f:.12f)-Vector3.up*(face?.025f:.20f);
                for(int pass=0;pass<3;pass++){
                    fistOffset=PunchKnuckle(actor)-hand.position;
                    Map01ArmContact.Reach(actor,false,Vector3.Lerp(chamber,hit-fistOffset,thrust),actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position+enemy.transform.right*.30f-Vector3.up*.25f,weight);
                }
                var hold=shoulder+toward*.04f+Vector3.up*.025f;
                Map01ArmContact.Reach(actor,true,hold,actor.GetBoneTransform(HumanBodyBones.LeftUpperArm).position-enemy.transform.right*.30f,weight);
                if(weight>.95f)ShoulderContactError=Vector3.Distance(actor.GetBoneTransform(HumanBodyBones.LeftHand).position,hold);
                if(weight>.95f&&thrust>.75f){
                    float error=Vector3.Distance(PunchKnuckle(actor),hit);
                    if(face)FaceContactError=FaceContactError<0?error:Mathf.Min(FaceContactError,error);else rightContactError=rightContactError<0?error:Mathf.Min(rightContactError,error);
                    leftContactError=ShoulderContactError;InterrogationContactError=error;
                }
                sling=1;
            }
            if(sling<.001f)actor.GetComponent<Map01EnemyGrip>()?.Apply();
            interrogationSling?.Sample(sling);
        }
        void BendInterrogatorKnees(Animator actor,float depth){
            var hips=actor.GetBoneTransform(HumanBodyBones.Hips);
            var feet=new[]{actor.GetBoneTransform(HumanBodyBones.LeftFoot).position,actor.GetBoneTransform(HumanBodyBones.RightFoot).position};
            var rotations=new[]{actor.GetBoneTransform(HumanBodyBones.LeftFoot).rotation,actor.GetBoneTransform(HumanBodyBones.RightFoot).rotation};
            hips.position-=Vector3.up*depth;
            for(int i=0;i<2;i++){
                var upper=actor.GetBoneTransform(i==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                var lower=actor.GetBoneTransform(i==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                var foot=actor.GetBoneTransform(i==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,foot.position);
                var origin=upper.position;var delta=feet[i]-origin;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.005f,a+b-.001f);
                var axis=delta.normalized;var bend=Vector3.ProjectOnPlane(enemy.transform.forward,axis).normalized;
                float along=(a*a+d*d-b*b)/(2*d);
                var knee=origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
                upper.rotation=Quaternion.FromToRotation(lower.position-origin,knee-origin)*upper.rotation;
                lower.rotation=Quaternion.FromToRotation(foot.position-lower.position,feet[i]-lower.position)*lower.rotation;
                foot.rotation=rotations[i];
            }
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
