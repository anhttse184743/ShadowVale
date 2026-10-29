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
    /// Rowing the boat: the paddling arms and their grip, the seated passengers, and Hùng ridden injured.
    /// </summary>
    public sealed partial class Map01Extraction
    {
        private void PaddleArm(HumanBodyBones upperBone,HumanBodyBones lowerBone,HumanBodyBones handBone,Vector3 target,float weight,float side)
        {
            var upper=commanderActor.GetBoneTransform(upperBone);var lower=commanderActor.GetBoneTransform(lowerBone);var hand=commanderActor.GetBoneTransform(handBone);
            target=Vector3.Lerp(hand.position,target,weight);
            Vector3 origin=upper.position;float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,hand.position);
            Vector3 direction=(target-origin).normalized;float d=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.001f,(a+b)*.97f);
            target=origin+direction*d;
            Vector3 bend=Vector3.ProjectOnPlane(boat.right*side*.65f+boat.forward*.15f-boat.up*.45f,direction).normalized;
            float along=(a*a+d*d-b*b)/(2*d);float height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Vector3 elbow=origin+direction*along+bend*height;
            Quaternion wrist=hand.localRotation;
            upper.rotation=Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
            hand.localRotation=wrist;
            if(hand.childCount>0) {
                // The hand end defines the actual mesh finger axis, independent of FBX bone axes.
                Vector3 fingers=hand.GetChild(0).position-hand.position;
                Vector3 forearm=hand.position-lower.position;
                Vector3 shaft=boat.TransformDirection(new Vector3(.36f,-.26f,.05f)).normalized;
                Vector3 gripFacing=Vector3.ProjectOnPlane(forearm,shaft).normalized;
                if(gripFacing.sqrMagnitude>.1f) hand.rotation=Quaternion.Slerp(hand.rotation,Quaternion.FromToRotation(fingers,gripFacing)*hand.rotation,weight);
                paddleGrip?.Orient(hand,shaft,forearm,weight);
            }
        }
        private readonly Dictionary<Transform,Vector3> paddlePalmCenters=new();
        private Vector3 PaddleGrip(Transform hand)
        {
            if(paddleGrip!=null&&paddleGrip.TryGrip(hand,out var gripped))return gripped;
            if(!paddlePalmCenters.TryGetValue(hand,out var contact)) {
                // Hand_end is a rig endpoint, not the center of Hung's closed fist.
                // Measure the actual hand surface in bind space once, preserving the source rig.
                Vector3 sum=Vector3.zero;float total=0;
                foreach(var renderer in commanderActor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                    var mesh=renderer.sharedMesh;if(mesh==null || !mesh.isReadable)continue;
                    int bone=Array.IndexOf(renderer.bones,hand);if(bone<0)continue;
                    var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes[bone];
                    for(int i=0;i<weights.Length;i++) {
                        var w=weights[i];float influence=(w.boneIndex0==bone?w.weight0:0)+(w.boneIndex1==bone?w.weight1:0)
                            +(w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0);
                        if(influence<.7f)continue;
                        sum+=bind.MultiplyPoint3x4(vertices[i])*influence;total+=influence;
                    }
                }
                contact=total>0?sum/total:hand.InverseTransformPoint(hand.childCount>0?hand.GetChild(0).position:hand.position)*.8f;
                paddlePalmCenters[hand]=contact;
            }
            return hand.TransformPoint(contact);
        }
        public void HidePassengerGuns() {
            foreach(var actor in new[]{commanderActor,hungActor})if(actor!=null)
                foreach(var weapon in actor.GetComponentsInChildren<Weapon>(true))if(weapon.IsGun)weapon.gameObject.SetActive(false);
        }
        // Save restoration runs after the quest event has already prepared the seats.
        // It must not attach Hùng's standing controller or ground his skin through the hull.
        public bool RestoreSeatedPassenger()
        {
            if(!Prepared||quest==null||quest.Stage<Map01Quest.ExtractionStage||hungActor==null)return false;
            hungPose?.Dispose();hungPose=null;StopAgent(mission.hung);Seat(mission.hung,1.6f);
            var visual=mission.hung.GetComponentInChildren<Map01HungVisual>();if(visual!=null)visual.enabled=false;
            hungActor.transform.localPosition=Vector3.zero;hungActor.Rebind();
            hungPose=new PosePlayer(hungActor,hungClips);hungPose.Play(travel);HidePassengerGuns();ApplyInjuredPassenger();
            return true;
        }
        public AnimationClip InjuredTravel => hungClips?.FirstOrDefault(c=>c.name==travel.name.Replace("Nam_","Hung_"))??travel;
        public void ApplyInjuredPassenger(float weight=1) {
            if(hungActor==null || weight<=0)return;
            var hips=hungActor.GetBoneTransform(HumanBodyBones.Hips);
            var right=hungActor.GetBoneTransform(HumanBodyBones.RightHand);
            var left=hungActor.GetBoneTransform(HumanBodyBones.LeftHand);

            // Right arm rests across the abdomen. The left palm supports its forearm.
            // Solve with the original limb lengths; no bone scale/translation edits.
            SolveCoverArm(hungActor,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
                hips.position+hungActor.transform.forward*.20f-hungActor.transform.right*.08f+boat.up*.27f,right.rotation,1,weight);
            RelaxSupportedWrist(right,hungActor.GetBoneTransform(HumanBodyBones.RightLowerArm),weight);
            var forearm=hungActor.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var support=Vector3.Lerp(forearm.position,right.position,.70f)+hungActor.transform.forward*.035f-boat.up*.025f;
            SolveCoverArm(hungActor,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
                support,left.rotation,-1,weight);RelaxSupportedWrist(left,hungActor.GetBoneTransform(HumanBodyBones.LeftLowerArm),weight);
        }
        static void RelaxSupportedWrist(Transform hand,Transform forearm,float weight) {
            if(hand.childCount==0)return;
            var fingers=hand.GetChild(0).position-hand.position;var arm=hand.position-forearm.position;
            if(fingers.sqrMagnitude<.000001f||arm.sqrMagnitude<.000001f)return;
            var neutral=Quaternion.FromToRotation(fingers,arm)*hand.rotation;
            hand.rotation=Quaternion.Slerp(hand.rotation,neutral,weight);
        }
        private void UpdateRowing()
        {
            if(CurrentPhase==Phase.Approach) commanderPose.Play(travel);
            if(CurrentPhase==Phase.Boarding) {
                commander.localRotation=Quaternion.Slerp(Quaternion.Euler(0,270,0),Quaternion.identity,Mathf.SmoothStep(0,1,clock/hungTurnSeconds));
                commanderPose.Play(travel);oarHeld=clock>=hungTurnSeconds;
            }
            if(CurrentPhase==Phase.Seating) commanderPose.Play(rowStart);
            if(CurrentPhase==Phase.Departing) {
                commanderPose.Play(clock>16?rowStop:rowLoop);
                float stroke=Mathf.Floor(clock/2.4f);
                if(stroke!=lastStroke && clock<16) {lastStroke=stroke;motor.PlayOneShot(paddleAudio,speech!=null&&speech.HasSpeech?.045f:.18f);}
            }
        }
    }
}
