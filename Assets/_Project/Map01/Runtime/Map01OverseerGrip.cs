using System;
using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Reference-pose weapon mount and a foregrip contact for the stationary rescue guard.</summary>
    [DefaultExecutionOrder(150),DisallowMultipleComponent]
    public sealed class Map01OverseerGrip:MonoBehaviour
    {
        public Vector3 localGunEuler=new Vector3(295.40f,207.84f,160.54f);
        [Range(0,1)] public float supportWeight=1;
        public bool Calibrated{get;private set;}
        public float PalmError{get;private set;}
        private Animator actor;private Map01Rifle rifle;private Map01EnemyController owner;
        private Vector3 palm;private Vector3 fingerAxis;private Vector3 rightPalm,grip;
        public void Bind(Map01EnemyController guard){owner=guard;actor=GetComponent<Animator>();rifle=GetComponent<Map01Rifle>();}
        private void LateUpdate()
        {
            if(owner==null||!owner.Alive||actor==null||!actor.isHuman||rifle==null||rifle.weapon==null||!rifle.weapon.gameObject.activeInHierarchy)return;
            var left=actor.GetBoneTransform(HumanBodyBones.LeftHand);var right=actor.GetBoneTransform(HumanBodyBones.RightHand);
            if(!Calibrated){
                // Stable standing reference offset, also valid when loading a search/aim state.
                // Only the local hand mount is changed; animation still controls world aim.
                var model=rifle.weapon.transform.Find("Model");
                if(model!=null){var roll=Quaternion.AngleAxis(-90,Vector3.forward);model.localPosition=roll*model.localPosition;model.localRotation=roll*model.localRotation;}
                var tip=left.Cast<Transform>().FirstOrDefault(t=>t.name.IndexOf("end",StringComparison.OrdinalIgnoreCase)>=0);
                palm=tip!=null?left.InverseTransformPoint(tip.position)*.45f:Vector3.zero;
                fingerAxis=tip!=null?left.InverseTransformDirection(tip.position-left.position).normalized:Vector3.right;
                var rightTip=right.Cast<Transform>().FirstOrDefault(t=>t.name.IndexOf("end",StringComparison.OrdinalIgnoreCase)>=0);
                rightPalm=rightTip!=null?right.InverseTransformPoint(rightTip.position)*.45f:Vector3.zero;
                var handle=rifle.weapon.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name.StartsWith("bl_grip_low"));
                grip=handle!=null?rifle.weapon.transform.InverseTransformPoint(handle.bounds.center):new Vector3(.01f,-.01f,0);
                Calibrated=true;
            }
            rifle.weapon.transform.localRotation=Quaternion.Euler(localGunEuler);
            rifle.weapon.transform.localPosition=rightPalm-rifle.weapon.transform.localRotation*Vector3.Scale(grip,rifle.weapon.transform.localScale);
            var lower=actor.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var upper=actor.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var neutral=lower.rotation;
            var wrist=Quaternion.FromToRotation(neutral*fingerAxis,rifle.weapon.transform.up)*neutral;
            var target=rifle.support.position-wrist*Vector3.Scale(palm,left.lossyScale);
            var origin=upper.position;float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,left.position);
            var axis=(target-origin).normalized;float d=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.001f,(a+b)*.97f);
            target=origin+axis*d;
            var bend=Vector3.ProjectOnPlane(-actor.transform.right*.6f-actor.transform.up*.7f,axis).normalized;
            float along=(a*a+d*d-b*b)/(2*d);var elbow=origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation,supportWeight);
            lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.FromToRotation(left.position-lower.position,target-lower.position)*lower.rotation,supportWeight);
            left.rotation=Quaternion.Slerp(left.rotation,wrist,supportWeight);
            PalmError=Vector3.Distance(left.TransformPoint(palm),rifle.support.position);
        }
    }
}
