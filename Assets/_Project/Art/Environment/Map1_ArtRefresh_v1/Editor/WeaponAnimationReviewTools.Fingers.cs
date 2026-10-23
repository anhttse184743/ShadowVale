using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.EnvironmentReview
{
    public static partial class WeaponAnimationReviewTools
    {
        sealed class FingerJoint{public Transform bone;public Quaternion rest;public Vector3 axis;public bool right,thumb,index;public int joint;}
        static List<FingerJoint> fingerJoints;
        static Quaternion rightHandFrame,leftPistolFrame,leftRifleFrame;
        static void CalibrateFingers(Animator a)
        {
            fingerJoints=new List<FingerJoint>();
            foreach(bool right in new[]{true,false}){
                string side=right?"Right":"Left";var h=a.GetBoneTransform(right?HumanBodyBones.RightHand:HumanBodyBones.LeftHand);
                var middle=Find(a.transform,side+"HandMiddle1");var index=Find(a.transform,side+"HandIndex1");var pinky=Find(a.transform,side+"HandPinky1");var thumb=Find(a.transform,side+"HandThumb1");
                if(middle==null||index==null||pinky==null||thumb==null)throw new Exception("Missing original finger rig "+side);
                var forward=(middle.position-h.position).normalized;var palm=Vector3.Cross(forward,index.position-pinky.position).normalized;
                if(Vector3.Dot(palm,thumb.position-h.position)<0)palm=-palm;
                var localFrame=Quaternion.LookRotation(h.InverseTransformDirection(forward),h.InverseTransformDirection(palm));
                if(right)rightHandFrame=Quaternion.LookRotation(Vector3.forward,Vector3.left)*Quaternion.Inverse(localFrame);
                else {leftPistolFrame=Quaternion.LookRotation(Vector3.forward,Vector3.right)*Quaternion.Inverse(localFrame);leftRifleFrame=Quaternion.LookRotation(Vector3.forward,Vector3.up)*Quaternion.Inverse(localFrame);}
                foreach(string digit in new[]{"Thumb","Index","Middle","Ring","Pinky"})for(int joint=1;joint<=3;joint++){
                    var bone=Find(a.transform,side+"Hand"+digit+joint);if(bone==null)continue;
                    var next=Find(a.transform,side+"Hand"+digit+(joint+1));Vector3 along=next!=null?next.position-bone.position:bone.TransformDirection(Vector3.up);
                    Vector3 toward=digit=="Thumb"?Vector3.ProjectOnPlane(index.position-bone.position,along):palm;
                    var axis=Vector3.Cross(along,toward).normalized;
                    fingerJoints.Add(new FingerJoint{bone=bone,rest=bone.localRotation,axis=bone.InverseTransformDirection(axis),right=right,thumb=digit=="Thumb",index=digit=="Index",joint=joint});
                }
            }
        }
        static void PoseFingers(string item,string action,float u)
        {
            foreach(var f in fingerJoints){
                float close=f.joint==1?72:f.joint==2?88:58;
                if(f.thumb)close=f.joint==1?38:f.joint==2?42:30;
                if(f.right&&f.index&&(item=="pistol_k54"||item=="rifle_mosin"))close=action.StartsWith("fire")?f.joint==1?32:48:f.joint==1?8:12;
                if(!f.right&&item=="pistol_k54")close=f.thumb?30:f.joint==1?58:f.joint==2?78:50;
                if(item=="grenade_f1"||item=="smoke_bomb"){
                    close=f.thumb?35:f.joint==1?48:f.joint==2?65:44;
                    if(!f.right&&u<.31f)close=f.index?36:f.thumb?25:50;
                    if(f.right&&action.Contains("throw")){float open=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.52f,.59f,u));close=Mathf.Lerp(close,4,open);}
                }
                if(item=="rifle_mosin"&&action.StartsWith("reload")&&f.right&&u>.24f&&u<.76f)close=f.thumb?34:f.index?38:22;
                f.bone.localRotation=f.rest*Quaternion.AngleAxis(close,f.axis);
            }
        }
    }
}
