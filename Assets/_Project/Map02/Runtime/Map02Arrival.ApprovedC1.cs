using System.Collections.Generic;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02Arrival
    {
        Map02ArrivalC1Data approvedC1;
        readonly GameObject[] c1Visuals=new GameObject[3];
        readonly Animator[] c1VisualActors=new Animator[3];
        readonly Dictionary<Renderer,bool>[] c1Hidden={new(),new(),new()};
        public bool UsesApprovedMisstep=>approvedC1!=null;
        public Animator MisstepActor(int index)=>c1VisualActors[index]!=null?c1VisualActors[index]:actors[index];
        void StartApprovedVisual(int index)
        {
            if(approvedC1==null||c1Visuals[index]!=null)return;
            foreach(var renderer in Passengers[index].GetComponentsInChildren<SkinnedMeshRenderer>(true)){if(renderer.GetComponentInParent<ShadowVale.Gameplay.Combat.Weapon>()!=null)continue;c1Hidden[index][renderer]=renderer.enabled;renderer.enabled=false;}
            var go=Instantiate(index==0?approvedC1.nam:approvedC1.hung,Passengers[index],false);c1Visuals[index]=go;c1VisualActors[index]=go.GetComponentInChildren<Animator>();c1VisualActors[index].enabled=false;
            if(index==0)Map01BoatCarry.For(actors[0]).UseVisualActor(c1VisualActors[0]);
        }
        void SampleApprovedVisuals()
        {
            if(approvedC1==null)return;
            float t=Mathf.Max(0,MisstepClock);
            foreach(int index in new[]{0,2})if(c1Visuals[index]!=null)(index==0?c1Nam:c1Hung).SampleAnimation(c1VisualActors[index].gameObject,t);
            if(c1Start>=0&&c1VisualActors[2]!=null)GroundApprovedKnee(c1VisualActors[2],t,C1Point(Vector3.zero).y);
            if(c1Visuals[0]!=null)TickPack(t);
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
        void RestoreApprovedVisual(int index)
        {
            if(index==0&&actors!=null&&actors[0]!=null)actors[0].GetComponent<Map01BoatCarry>()?.UseVisualActor(null);
            if(c1Visuals[index]!=null){c1Visuals[index].SetActive(false);Destroy(c1Visuals[index]);}c1Visuals[index]=null;c1VisualActors[index]=null;
            foreach(var pair in c1Hidden[index])if(pair.Key!=null)pair.Key.enabled=pair.Value;c1Hidden[index].Clear();
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
