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
        static readonly Dictionary<Avatar,Mount> patrolMounts=new();
        Mount patrolMount; bool hasPatrol;
        public static void ResetPatrolCalibration() { patrolMounts.Clear(); }
        Animator actor;Map01Rifle rifle;Map01EnemyController owner;Mount mount;Vector3 grip;
        Map02ChapterEnemy chapterOwner;
        public bool Calibrated{get;private set;}
        public float PalmError{get;private set;}
        public float WristBend{get;private set;}
        public static Map01EnemyGrip BindPreview(Animator actor)
        {
            var result=actor.gameObject.AddComponent<Map01EnemyGrip>();
            result.actor=actor;result.rifle=Map01Rifle.Attach(actor);result.Calibrate();return result;
        }
        public static Map01EnemyGrip Bind(Map01EnemyController guard)
        {
            var actor=guard.GetComponentInChildren<Animator>();if(actor==null||!actor.isHuman)return null;
            var result=actor.GetComponent<Map01EnemyGrip>()??actor.gameObject.AddComponent<Map01EnemyGrip>();
            result.owner=guard;result.actor=actor;result.rifle=Map01Rifle.Attach(actor);result.Calibrate();return result;
        }
        public static Map01EnemyGrip Bind(Map02ChapterEnemy guard)
        {
            var actor=guard.GetComponentInChildren<Animator>();if(actor==null||!actor.isHuman)return null;
            var result=actor.GetComponent<Map01EnemyGrip>()??actor.gameObject.AddComponent<Map01EnemyGrip>();
            result.chapterOwner=guard;result.actor=actor;result.rifle=Map01Rifle.Attach(actor);result.Calibrate();return result;
        }
        static Vector3 Palm(Transform hand)
        {
            var tip=hand.Cast<Transform>().FirstOrDefault(t=>t.name.ToLowerInvariant().Contains("end"));
            return tip!=null?hand.InverseTransformPoint(tip.position)*.43f:Vector3.zero;
        }
        void Calibrate()
        {
            if(Calibrated||rifle==null||rifle.weapon==null)return;
            var handle=rifle.weapon.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name.StartsWith("bl_grip_low"));
            grip=handle!=null?rifle.weapon.transform.InverseTransformPoint(handle.bounds.center):Vector3.zero;
            if(!mounts.TryGetValue(actor.avatar,out mount)){
                var clip=actor.runtimeAnimatorController!=null?actor.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name.StartsWith("Nam_Rifle_AimIdle")||c.name.StartsWith("Enemy_Rifle_AimIdle")):null;
                if(clip==null)return;
                using(var pose=new Map01CinematicPose(actor)){
                    pose.Sample(clip,0);var right=actor.GetBoneTransform(HumanBodyBones.RightHand);
                    var left=actor.GetBoneTransform(HumanBodyBones.LeftHand);var lower=actor.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    mount=new Mount{gun=Quaternion.Inverse(right.rotation)*actor.transform.rotation*Quaternion.Euler(0,0,-90),
                        rightPalm=Palm(right),leftPalm=Palm(left),leftWrist=Quaternion.Inverse(lower.rotation)*left.rotation};
                }
                mounts[actor.avatar]=mount;
            }
            var patrol=actor.runtimeAnimatorController!=null?actor.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name.StartsWith("Enemy_Patrol_Walk")):null;
            if(patrol!=null)
            {
                if(!patrolMounts.TryGetValue(actor.avatar,out patrolMount))
                {
                    using(var pose=new Map01CinematicPose(actor,skinGrounding:false))
                    {
                        pose.Sample(patrol,0);
                        var right=actor.GetBoneTransform(HumanBodyBones.RightHand);var left=actor.GetBoneTransform(HumanBodyBones.LeftHand);var lower=actor.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                        var rightPalm=Palm(right);var leftPalm=Palm(left);
                        var handDirection=left.TransformPoint(leftPalm)-right.TransformPoint(rightPalm);
                        var localDirection=new Vector3(-.025f,.01f,.32f)-grip;
                        var gunRotation=Quaternion.LookRotation(handDirection,actor.transform.up)*Quaternion.Inverse(Quaternion.LookRotation(localDirection,Vector3.left));
                        patrolMount=new Mount{gun=Quaternion.Inverse(right.rotation)*gunRotation,rightPalm=rightPalm,leftPalm=leftPalm,leftWrist=Quaternion.Inverse(lower.rotation)*left.rotation};
                    }
                    patrolMounts[actor.avatar]=patrolMount;
                }
                hasPatrol=true;
            }
            Calibrated=true;
        }
        void LateUpdate()
        {
            if(chapterOwner!=null)
            {
                if(Calibrated&&!chapterOwner.Dead&&rifle!=null&&rifle.weapon!=null&&rifle.weapon.gameObject.activeInHierarchy)
                    Apply(!chapterOwner.Alerted&&actor.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
                return;
            }
            if(!Calibrated||owner==null||!owner.Alive||!owner.WeaponPoseAvailable||rifle==null||rifle.weapon==null||!rifle.weapon.gameObject.activeInHierarchy)return;
            Apply(!owner.Engaged && actor.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
        }
        public void Apply(bool patrol=false)
        {
            if(!Calibrated)return;
            var aimMount=mount;
            if(patrol&&hasPatrol)mount=patrolMount;
            var gun=rifle.weapon.transform;
            gun.localRotation=mount.gun;gun.localPosition=mount.rightPalm-mount.gun*Vector3.Scale(grip,gun.localScale);
            var left=actor.GetBoneTransform(HumanBodyBones.LeftHand);left.localRotation=mount.leftWrist;
            var elbow=actor.GetBoneTransform(HumanBodyBones.Chest).position-actor.transform.right*(patrol?.27f:.4f)-actor.transform.up*(patrol?.26f:.3f);
            // Recalculate the palm offset as the forearm rotates, so the visible palm
            // reaches the foregrip rather than only placing the wrist at that point.
            var support=patrol?gun.TransformPoint(new Vector3(-.025f,.01f,.32f)):rifle.support.position;
            for(int i=0;i<3;i++)Map01ArmContact.Reach(actor,true,support-left.TransformVector(mount.leftPalm),elbow,1);
            PalmError=Vector3.Distance(left.TransformPoint(mount.leftPalm),support);
            WristBend=Quaternion.Angle(mount.leftWrist,left.localRotation);
            mount=aimMount;
        }
    }
}
