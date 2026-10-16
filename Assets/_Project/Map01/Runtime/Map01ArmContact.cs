using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Two-bone reach with an explicit elbow plane and a neutral wrist.</summary>
    public static class Map01ArmContact
    {
        public static void Reach(Animator actor,bool left,Vector3 target,Vector3 elbowHint,float weight)
        {
            var upper=actor.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            var lower=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            if(upper==null||lower==null||hand==null||weight<=0)return;
            var wrist=hand.localRotation;var origin=upper.position;
            float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,hand.position);
            var delta=target-origin;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.005f,(a+b)*.97f);
            var axis=delta.sqrMagnitude>.00001f?delta.normalized:actor.transform.forward;
            var bend=Vector3.ProjectOnPlane(elbowHint-origin,axis);
            if(bend.sqrMagnitude<.00001f)bend=Vector3.ProjectOnPlane(-actor.transform.up,axis);
            bend.Normalize();float along=(a*a+d*d-b*b)/(2*d);
            var elbow=origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation,weight);
            lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.FromToRotation(hand.position-lower.position,origin+axis*d-lower.position)*lower.rotation,weight);
            hand.localRotation=wrist;
        }
        public static void Radio(Animator actor,float weight)
        {
            if(actor==null||!actor.isHuman||weight<=0)return;
            var head=actor.GetBoneTransform(HumanBodyBones.Head);var chest=actor.GetBoneTransform(HumanBodyBones.Chest);
            if(head==null||chest==null)return;var root=actor.transform;
            Reach(actor,true,head.position-root.right*.14f+root.forward*.17f+root.up*.015f,
                chest.position-root.right*.43f+root.forward*.17f-root.up*.12f,weight);
        }
    }
}
