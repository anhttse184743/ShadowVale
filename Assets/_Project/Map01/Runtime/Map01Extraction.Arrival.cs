using System;
using System.Collections.Generic;
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
    /// The hand-off to Map 2: going on to the next map, and the hand and paddle contacts Map02Arrival
    /// borrows from the boat ride.
    /// </summary>
    public sealed partial class Map01Extraction
    {
        public void ContinueToMap2() {
            if(transitionStarted || mission==null || quest.Stage!=Map01Quest.CompleteStage)return;
            if(!Prepare())return;
            transitionStarted=true;
            namPose?.Dispose();hungPose?.Dispose();commanderPose?.Dispose();
            namPose=hungPose=commanderPose=null;
            var arrival=boat.gameObject.AddComponent<Map02Arrival>();
            arrival.TakeParty(this,mission);
            transform.SetParent(boat,true);enabled=false;
            boat.SetParent(null,true);DontDestroyOnLoad(boat.gameObject);
            mission.gameCamera.transform.SetParent(boat,true);
            mission.ReleaseForNextMap();
            arrival.StartCoroutine(arrival.LoadVillage());
        }
        public void ArrivalRestHands(Animator actor,float time,float leftWeight=1,float rightWeight=1) {
            var hips=actor.GetBoneTransform(HumanBodyBones.Hips);
            for(int side=-1;side<=1;side+=2) {
                var upper=side<0?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm;
                var lower=side<0?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm;
                var handId=side<0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand;
                var hand=actor.GetBoneTransform(handId);var local=hand.localRotation;
                var target=hips.position+actor.transform.right*(side*.21f)+actor.transform.up*.12f
                    +actor.transform.forward*(.12f+side*.025f*Mathf.Sin(time*5));
                SolveCoverArm(actor,upper,lower,handId,target,hand.rotation,side,side<0?leftWeight:rightWeight);hand.localRotation=local;
            }
        }
        public void ArrivalStowContacts(float leftWeight,float rightWeight) {
            var left=commanderActor.GetBoneTransform(HumanBodyBones.LeftHand);
            var right=commanderActor.GetBoneTransform(HumanBodyBones.RightHand);
            PaddleArm(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
                oar.position-(PaddleGrip(left)-left.position),leftWeight,-1);
            PaddleArm(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
                oar.TransformPoint(new Vector3(0,0,.45f))-(PaddleGrip(right)-right.position),rightWeight,1);
        }
        // Share the tested hand/prop contacts with the arrival, without mission or firing callbacks.
        public void ArrivalPaddleContacts(float time,float weight) {
            float phase=time*Mathf.PI*2/2.4f;
            float reach=.30f+.10f*Mathf.Cos(phase),lift=Mathf.Max(0,-Mathf.Sin(phase))*.035f;
            PaddleArm(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
                boat.TransformPoint(new Vector3(.12f,.99f+lift,-1.6f+reach)),weight,-1);
            PaddleArm(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
                boat.TransformPoint(new Vector3(.48f,.73f+lift,-1.55f+reach)),weight,1);
            SetPaddleGrip(weight,weight);
            var a=PaddleGrip(commanderActor.GetBoneTransform(HumanBodyBones.LeftHand));
            var b=PaddleGrip(commanderActor.GetBoneTransform(HumanBodyBones.RightHand));
            oar.SetPositionAndRotation(a,Quaternion.LookRotation((b-a).normalized,boat.forward));
        }
    }
}
