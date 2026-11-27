using System;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02Arrival
    {
        Map02ArrivalC1Data approvedC1;
        readonly LandingPose[] c1Poses=new LandingPose[3];
        public bool UsesApprovedMisstep=>approvedC1!=null;
        public Animator MisstepActor(int index)=>actors[index];
        void StartApprovedPose(int index)
        {
            if(approvedC1==null||c1Poses[index]!=null)return;
            var source=Instantiate(index==0?approvedC1.nam:approvedC1.hung,Passengers[index],false);
            foreach(var renderer in source.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            c1Poses[index]=new LandingPose(source,actors[index]);
        }
        void SampleApprovedPoses()
        {
            if(approvedC1==null)return;
            float t=Mathf.Max(0,MisstepClock);
            c1Poses[2]?.Sample(c1Hung,t,C1Point(Vector3.zero).y);
            c1Poses[0]?.Sample(c1Nam,t);
            if(c1Poses[0]!=null)TickPack(t);
        }
        void EndApprovedPose(int index)
        {
            c1Poses[index]?.Dispose();
            c1Poses[index]=null;
        }
        sealed class LandingPose:IDisposable
        {
            readonly GameObject sourceRoot;
            readonly Animator source,target;
            readonly HumanPoseHandler sourceHandler,targetHandler;
            readonly Transform sourceHips,targetHips;
            HumanPose pose;

            public LandingPose(GameObject sourceRoot,Animator target)
            {
                this.sourceRoot=sourceRoot;
                this.target=target;
                source=sourceRoot.GetComponentInChildren<Animator>();
                source.enabled=false;
                sourceHandler=new HumanPoseHandler(source.avatar,source.transform);
                targetHandler=new HumanPoseHandler(target.avatar,target.transform);
                sourceHips=source.GetBoneTransform(HumanBodyBones.Hips);
                targetHips=target.GetBoneTransform(HumanBodyBones.Hips);
            }
            public void Sample(AnimationClip clip,float time,float? floor=null)
            {
                clip.SampleAnimation(source.gameObject,time);
                if(floor.HasValue)GroundApprovedKnee(source,time,floor.Value);

                // The authored tracks bind to the original rigs. Transfer their pose to the live cast.
                sourceHandler.GetHumanPose(ref pose);
                pose.bodyPosition=target.transform.InverseTransformPoint(pose.bodyPosition*source.humanScale)/target.humanScale;
                pose.bodyRotation=Quaternion.Inverse(target.transform.rotation)*pose.bodyRotation;
                targetHandler.SetHumanPose(ref pose);
                targetHips.position=sourceHips.position;

                float brace=0;
                if(floor.HasValue)
                {
                    brace=Ease(time,.55f,.85f)*(1-Ease(time,3.3f,4f));
                    var plantedHand=source.GetBoneTransform(HumanBodyBones.LeftHand).position;
                    FitMisstepTorso(target,true,plantedHand,brace,true);
                }
                KeepContacts(true,brace);
                KeepContacts(false);
                if(floor.HasValue)GroundApprovedKnee(target,time,floor.Value);
            }
            void KeepContacts(bool left,float brace=0)
            {
                var foot=left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot;
                var knee=left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg;
                var hand=left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand;
                var elbow=left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm;
                var targetFoot=target.GetBoneTransform(foot);
                var targetHand=target.GetBoneTransform(hand);
                var footRotation=targetFoot.rotation;
                var handRotation=targetHand.rotation;
                var handAt=source.GetBoneTransform(hand).position;
                if(brace>0)
                {
                    // Keep the brace at board height if Hùng's arm cannot reach the source point.
                    var shoulder=target.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                    var forearm=target.GetBoneTransform(elbow).position;
                    float reach=(Vector3.Distance(shoulder,forearm)+Vector3.Distance(forearm,targetHand.position))*.96f;
                    var offset=handAt-shoulder;
                    var planar=Vector3.ProjectOnPlane(offset,Vector3.up);
                    float range=Mathf.Sqrt(Mathf.Max(0,reach*reach-offset.y*offset.y));
                    handAt+=(Vector3.ClampMagnitude(planar,range)-planar)*brace;
                }
                KeepMisstepLeg(target,left,source.GetBoneTransform(foot).position,source.GetBoneTransform(knee).position);
                Map01ArmContact.Reach(target,left,handAt,source.GetBoneTransform(elbow).position,1);
                targetFoot.rotation=footRotation;
                targetHand.rotation=handRotation;
            }
            public void Dispose()
            {
                sourceHandler.Dispose();
                targetHandler.Dispose();
                Destroy(sourceRoot);
            }
        }
        // The reviewed take includes the lower stair. On the game's solid top landing only the
        // supporting leg needs adapting. Keep its ankle/orientation and the entire assisted upper body.
        public static void GroundApprovedKnee(Animator actor,float t,float floor)
        {
            float weight=Ease(t,.55f,.85f)*(1-Ease(t,3.25f,3.6f));if(weight<=0)return;
            var knee=actor.GetBoneTransform(HumanBodyBones.RightLowerLeg);var foot=actor.GetBoneTransform(HumanBodyBones.RightFoot);
            if(knee.position.y>=floor+.035f)return;
            var hint=knee.position;hint.y=Mathf.Lerp(hint.y,floor+.035f,weight);var at=foot.position;var rotation=foot.rotation;
            KeepMisstepLeg(actor,false,at,hint);foot.rotation=rotation;
        }
        // Legacy optional Humanoid fallback remains independent of the Day 1 duck/supper work.
        static void C1Reach(Animator body,bool left,Vector3 target,float weight)
        {
            if(weight<=0)return;
            var a=body.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var b=body.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var c=body.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);if(a==null||b==null||c==null)return;
            target=Vector3.Lerp(c.position,target,weight);float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position);
            var axis=target-a.position;float d=Mathf.Clamp(axis.magnitude,Mathf.Abs(l1-l2)+.001f,(l1+l2)*.995f);axis.Normalize();
            var bend=Vector3.ProjectOnPlane(b.position-a.position,axis);if(bend.sqrMagnitude<1e-6f)bend=Vector3.ProjectOnPlane(-body.transform.up,axis);bend.Normalize();
            float x=(l1*l1-l2*l2+d*d)/(2*d);var elbow=a.position+axis*x+bend*Mathf.Sqrt(Mathf.Max(0,l1*l1-x*x));var hand=c.rotation;
            a.rotation=Quaternion.FromToRotation(b.position-a.position,elbow-a.position)*a.rotation;b.rotation=Quaternion.FromToRotation(c.position-b.position,a.position+axis*d-b.position)*b.rotation;c.rotation=hand;
        }
    }
}
