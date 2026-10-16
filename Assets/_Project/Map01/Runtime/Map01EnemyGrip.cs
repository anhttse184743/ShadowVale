using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Avatar-specific rifle mount measured once in the existing aim reference pose.</summary>
    [DefaultExecutionOrder(145),DisallowMultipleComponent]
    public sealed class Map01EnemyGrip:MonoBehaviour
    {
        struct Mount{public Quaternion gun,leftWrist;public Vector3 rightPalm,leftPalm;}
        static readonly Dictionary<Avatar,Mount> mounts=new();
        Animator actor;Map01Rifle rifle;Map01EnemyController owner;Mount mount;Vector3 grip;
        public bool Calibrated{get;private set;}
        public float PalmError{get;private set;}
        public float WristBend{get;private set;}
        public static Map01EnemyGrip Bind(Map01EnemyController guard)
        {
            var actor=guard.GetComponentInChildren<Animator>();if(actor==null||!actor.isHuman)return null;
            var result=actor.GetComponent<Map01EnemyGrip>()??actor.gameObject.AddComponent<Map01EnemyGrip>();
            result.owner=guard;result.actor=actor;result.rifle=Map01Rifle.Attach(actor);result.Calibrate();return result;
        }
        static Vector3 Palm(Transform hand)
        {
            var tip=hand.Cast<Transform>().FirstOrDefault(t=>t.name.ToLowerInvariant().Contains("end"));
            return tip!=null?hand.InverseTransformPoint(tip.position)*.43f:Vector3.zero;
        }
        void Calibrate()
        {
            if(Calibrated||rifle==null||rifle.weapon==null)return;
            if(!mounts.TryGetValue(actor.avatar,out mount)){
                var clip=actor.runtimeAnimatorController!=null?actor.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="Enemy_Rifle_AimIdle"):null;
                if(clip==null)return;
                using(var pose=new Map01CinematicPose(actor)){
                    pose.Sample(clip,0);var right=actor.GetBoneTransform(HumanBodyBones.RightHand);
                    var left=actor.GetBoneTransform(HumanBodyBones.LeftHand);var lower=actor.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    mount=new Mount{gun=Quaternion.Inverse(right.rotation)*actor.transform.rotation*Quaternion.Euler(0,0,-90),
                        rightPalm=Palm(right),leftPalm=Palm(left),leftWrist=Quaternion.Inverse(lower.rotation)*left.rotation};
                }
                mounts[actor.avatar]=mount;
            }
            var handle=rifle.weapon.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name.StartsWith("bl_grip_low"));
            grip=handle!=null?rifle.weapon.transform.InverseTransformPoint(handle.bounds.center):Vector3.zero;Calibrated=true;
        }
        void LateUpdate()
        {
            if(!Calibrated||owner==null||!owner.Alive||!owner.WeaponPoseAvailable||rifle==null||rifle.weapon==null||!rifle.weapon.gameObject.activeInHierarchy)return;
            Apply();
        }
        public void Apply()
        {
            if(!Calibrated)return;var gun=rifle.weapon.transform;
            gun.localRotation=mount.gun;gun.localPosition=mount.rightPalm-mount.gun*Vector3.Scale(grip,gun.localScale);
            var left=actor.GetBoneTransform(HumanBodyBones.LeftHand);left.localRotation=mount.leftWrist;
            var elbow=actor.GetBoneTransform(HumanBodyBones.Chest).position-actor.transform.right*.4f-actor.transform.up*.3f;
            // Recalculate the palm offset as the forearm rotates, so the visible palm
            // reaches the foregrip rather than only placing the wrist at that point.
            for(int i=0;i<3;i++)Map01ArmContact.Reach(actor,true,rifle.support.position-left.TransformVector(mount.leftPalm),elbow,1);
            PalmError=Vector3.Distance(left.TransformPoint(mount.leftPalm),rifle.support.position);
            WristBend=Quaternion.Angle(mount.leftWrist,left.localRotation);
        }
    }
}
