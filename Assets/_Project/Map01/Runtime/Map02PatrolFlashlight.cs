using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Approved shoulder-height grip. Only patrol actors receive this component.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class Map02PatrolFlashlight:MonoBehaviour
    {
        Animator actor;Map02ChapterEnemy unit;Transform torch,emitter;Map01Rifle rifle;
        Quaternion leftWrist;Vector3 rifleGrip;Vector3 direction;
        public Transform Emitter=>emitter;
        public void SetDirection(Vector3 value){direction=value;}
        public static Map02PatrolFlashlight Bind(Map02ChapterEnemy unit)
        {
            var existing=unit.GetComponent<Map02PatrolFlashlight>();if(existing!=null)return existing;
            var prefab=Resources.Load<GameObject>("Equipment/Map02PatrolFlashlight");
            var actor=unit.GetComponentInChildren<Animator>();if(prefab==null||actor==null||!actor.isHuman)return null;
            var result=unit.gameObject.AddComponent<Map02PatrolFlashlight>();result.unit=unit;result.actor=actor;
            result.torch=Instantiate(prefab,actor.transform).transform;result.emitter=result.torch.Find("Emitter");
            result.rifle=actor.GetComponent<Map01Rifle>();
            var clip=actor.runtimeAnimatorController.animationClips.First(c=>c.name.StartsWith("Enemy_Rifle_AimIdle")||c.name.StartsWith("Nam_Rifle_AimIdle"));
            using(var pose=new Map01CinematicPose(actor,skinGrounding:false))
            {pose.Sample(clip,0);result.leftWrist=actor.GetBoneTransform(HumanBodyBones.LeftHand).localRotation;}
            if(result.rifle?.weapon!=null)
            {
                var handle=result.rifle.weapon.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name.StartsWith("bl_grip_low"));
                if(handle!=null)result.rifleGrip=result.rifle.weapon.transform.InverseTransformPoint(handle.bounds.center);
            }
            return result;
        }
        static Vector3 Palm(Transform hand,float fraction)
        {var end=hand.Cast<Transform>().FirstOrDefault(t=>t.name.ToLowerInvariant().Contains("end"));return end!=null?Vector3.Lerp(hand.position,end.position,fraction):hand.position;}
        void LateUpdate()
        {
            if(torch==null||actor==null)return;
            torch.gameObject.SetActive(!unit.Dead);if(unit.Dead)return;
            var root=actor.transform;var forward=root.forward;var right=root.right;
            var chest=actor.GetBoneTransform(HumanBodyBones.Chest);var head=actor.GetBoneTransform(HumanBodyBones.Head);
            if(unit.Alerted)
            {
                // Free both hands for the rifle while the torch remains on the shoulder strap.
                torch.SetPositionAndRotation(chest.position-right*.16f+forward*.16f,Quaternion.LookRotation(forward));return;
            }
            var hand=actor.GetBoneTransform(HumanBodyBones.LeftHand);hand.localRotation=leftWrist;
            Map01ArmContact.Reach(actor,true,head.position-right*.25f+forward*.32f-Vector3.up*.12f,
                chest.position-right*.36f+forward*.12f-Vector3.up*.3f,1);
            var lower=actor.GetBoneTransform(HumanBodyBones.LeftLowerArm);var axis=(hand.position-lower.position).normalized;
            lower.rotation=Quaternion.AngleAxis(-30,axis)*lower.rotation;
            hand.rotation=Quaternion.AngleAxis(-60,axis)*hand.rotation;
            torch.SetPositionAndRotation(Palm(hand,.68f)+forward*.035f,Quaternion.LookRotation(direction.sqrMagnitude>.01f?direction:forward));
            // The other hand keeps the rifle lowered, as in the approved preview.
            var rightHand=actor.GetBoneTransform(HumanBodyBones.RightHand);
            Map01ArmContact.Reach(actor,false,chest.position+right*.25f-Vector3.up*.33f+forward*.12f,
                chest.position+right*.4f-Vector3.up*.2f,1);
            if(rifle?.weapon!=null)
            {var gun=rifle.weapon.transform;gun.rotation=Quaternion.LookRotation(forward*.35f-Vector3.up,-right);gun.position+=Palm(rightHand,.43f)-gun.TransformPoint(rifleGrip);}
        }
    }
    [DefaultExecutionOrder(250)]
    public sealed class Map02PatrolLightSync:MonoBehaviour
    {
        public Map02ChapterDirector owner;
        void LateUpdate(){if(owner!=null&&owner.enabled)owner.SyncNightPatrolPresentation();}
    }
}
