using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Planting the party's feet as they step ashore: shoe contacts, the hull, bank and stair surfaces, and the leg solve.
    /// </summary>
    public sealed partial class Map02Arrival
    {
        float ShoreSurface(Vector3 position) {
            var hit=WalkSurfaceBelow(position);
            return hit.collider!=null?hit.point.y:position.y;
        }
        void GroundStandingFeet(int index) {
            float sole=float.PositiveInfinity;
            for(int side=0;side<2;side++){
                var foot=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                var hit=WalkSurfaceBelow(foot.position);
                LevelWalkingFoot(index,side,hit.collider!=null?hit.normal:Vector3.up);
            }
            foreach(var sample in shoeContacts[index])if(sample.bone!=null)sole=Mathf.Min(sole,sample.bone.TransformPoint(sample.point).y);
            if(!float.IsInfinity(sole))actors[index].transform.position+=Vector3.up*(SpawnPoints[index].y-sole);
        }
        void PrepareBindFeet(int index) {
            var actor=actors[index];
            Matrix4x4 Bind(Transform bone) {
                foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                    int j=Array.IndexOf(skin.bones,bone);
                    if(j>=0 && j<skin.sharedMesh.bindposes.Length)
                        return skin.transform.localToWorldMatrix*skin.sharedMesh.bindposes[j].inverse;
                }
                throw new InvalidOperationException("Missing original foot bind pose for "+bone.name);
            }
            for(int side=0;side<2;side++) {
                var foot=actor.GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                var toes=actor.GetBoneTransform(side==0?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);
                var reference=Bind(foot).rotation;
                levelFeet[index,side]=Quaternion.Inverse(actor.transform.rotation)*reference;
                if(toes!=null)neutralToes[index,side]=Quaternion.Inverse(Bind(toes.parent).rotation)*Bind(toes).rotation;
                LevelWalkingFoot(index,side,Vector3.up);
            }
        }
        void PrepareShoeContacts(int index) {
            var feet=new[]{actors[index].GetBoneTransform(HumanBodyBones.LeftFoot),actors[index].GetBoneTransform(HumanBodyBones.RightFoot)};
            var toes=new[]{actors[index].GetBoneTransform(HumanBodyBones.LeftToes),actors[index].GetBoneTransform(HumanBodyBones.RightToes)};
            shoeContacts[index]=new List<(Transform,Vector3)>();
            var soles=new[]{float.PositiveInfinity,float.PositiveInfinity};
            foreach(var skin in actors[index].GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var mesh=new Mesh();skin.BakeMesh(mesh);
                foreach(var vertex in mesh.vertices){
                    var point=skin.transform.TransformPoint(vertex);
                    float left=Vector3.ProjectOnPlane(point-feet[0].position,Vector3.up).sqrMagnitude;
                    float right=Vector3.ProjectOnPlane(point-feet[1].position,Vector3.up).sqrMagnitude;
                    int side=left<right?0:1;
                    if(Mathf.Min(left,right)>.09f || point.y>feet[side].position.y+.03f)continue;
                    soles[side]=Mathf.Min(soles[side],point.y);
                    var bone=toes[side]!=null && (point-toes[side].position).sqrMagnitude<(point-feet[side].position).sqrMagnitude?toes[side]:feet[side];
                    shoeContacts[index].Add((bone,bone.InverseTransformPoint(point)));
                }
                Destroy(mesh);
            }
            for(int side=0;side<2;side++)ankleOffsets[index,side]=float.IsInfinity(soles[side])?.10f:Mathf.Clamp(feet[side].position.y-soles[side],.03f,.24f);
            if(shoeContacts[index].Count==0)for(int side=0;side<2;side++)shoeContacts[index].Add((feet[side],feet[side].InverseTransformPoint(feet[side].position-Vector3.up*ankleOffsets[index,side])));
            for(int side=0;side<2;side++) {
                var points=shoeContacts[index].Where(c=>c.bone==feet[side]||c.bone==toes[side])
                    .Select(c=>c.bone.TransformPoint(c.point)).ToArray();
                float lo=points.Min(p=>Vector3.Dot(p-feet[side].position,actors[index].transform.forward));
                float hi=points.Max(p=>Vector3.Dot(p-feet[side].position,actors[index].transform.forward));
                for(int end=0;end<2;end++) {
                    var region=points.Where(p=>end==0?Vector3.Dot(p-feet[side].position,actors[index].transform.forward)<Mathf.Lerp(lo,hi,.35f):
                        Vector3.Dot(p-feet[side].position,actors[index].transform.forward)>Mathf.Lerp(lo,hi,.65f)).ToArray();
                    float low=region.Min(p=>p.y);
                    var bottom=region.Where(p=>p.y<low+.008f).ToArray();
                    var point=bottom.Aggregate(Vector3.zero,(sum,p)=>sum+p)/bottom.Length;
                    var bone=end==1&&toes[side]!=null?toes[side]:feet[side];
                    solePlane[index,side,end]=(bone,bone.InverseTransformPoint(point));
                }
            }
        }
        void PlantShoreFeet(int index) {
            float cycles=(float)poses[index].Time/Mathf.Max(.1f,walk[index].length);
            for(int side=0;side<2;side++) {
                float phase=cycles+side*.5f;int cycle=Mathf.FloorToInt(phase);
                float contactPhase=phase-cycle;
                var foot=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                var hit=WalkSurfaceBelow(foot.position);
                // Swinging feet used to bypass FlattenFoot entirely, retaining
                // the source clip's excessive upward pitch. Level both ankles
                // throughout the cycle; the leg animation still provides lift.
                var normal=hit.collider!=null?hit.normal:Vector3.up;
                if(contactPhase>=.55f){LevelWalkingFoot(index,side,normal);continue;}
                if(plantedCycles[index,side]!=cycle) {
                    var point=foot.position;
                    point.y=(hit.collider!=null?hit.point.y:Passengers[index].position.y)+ankleOffsets[index,side];
                    plantedFeet[index,side]=point;plantedCycles[index,side]=cycle;
                    plantedNormals[index,side]=normal;
                }
                // Blend contact at heel strike/toe off instead of snapping across the swing pose.
                float weight=Mathf.Min(Mathf.SmoothStep(0,1,contactPhase/.07f),Mathf.SmoothStep(0,1,(.55f-contactPhase)/.07f));
                var target=Vector3.Lerp(foot.position,plantedFeet[index,side],weight);
                SolveLeg(actors[index],side==0,target);
                // IK may plant the shoe on the landing while the unsolved stride
                // is above sloping ground. Level against the final contact surface.
                var solvedSurface=WalkSurfaceBelow(foot.position);
                LevelWalkingFoot(index,side,solvedSurface.collider!=null?solvedSurface.normal:Vector3.Slerp(normal,plantedNormals[index,side],weight));
            }
        }
        /// <summary>The nearest ground under a point that is neither the boat nor one of the party; no collider when there is none.</summary>
        RaycastHit WalkSurfaceBelow(Vector3 point) {
            // The two who stay at the jetty are grounded every frame for the rest of the map: no lists here.
            RaycastHit nearest=default;
            int hits=Map01Probe.Raycast(point+Vector3.up*1.5f,Vector3.down,3);
            for(int i=0;i<hits;i++) {
                var hit=Map01Probe.Hits[i];
                if(!(hit.point.y>.5f && hit.normal.y>.5f) || hit.transform.IsChildOf(transform) || IsPassenger(hit.transform))continue;
                if(nearest.collider==null || hit.distance<nearest.distance)nearest=hit;
            }
            return nearest;
        }
        bool IsPassenger(Transform part) {
            foreach(var passenger in Passengers)if(part.IsChildOf(passenger))return true;
            return false;
        }
        void LevelWalkingFoot(int index,int side,Vector3 normal) {
            var foot=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
            var toes=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);
            if(toes!=null)toes.localRotation=neutralToes[index,side];
            foot.rotation=Quaternion.FromToRotation(Vector3.up,normal)*actors[index].transform.rotation*levelFeet[index,side];
        }
        public static Vector2 StairContact(float progress,int side) {
            float cycle=Mathf.Clamp(progress,0,.999999f)*StairX.Length;
            int active=(int)cycle;float fraction=cycle-active;
            int completed=active-1;if(completed%2!=side)completed--;
            var from=completed<0?new Vector2(0,InteriorFloor):new Vector2(StairX[completed],StairY[completed]);
            if(active%2!=side)return from;
            var to=new Vector2(StairX[active],StairY[active]);
            // Clear the riser before translating the shoe. A diagonal ankle
            // arc let the toe clip the next tread, especially on Hung's sandals.
            float advance=StepEase((fraction-.22f)/.56f);
            float clearance=Mathf.Max(from.y,to.y)+.14f;
            float height=fraction<.30f?Mathf.Lerp(from.y,clearance,StepEase(fraction/.30f)):
                fraction>.76f?Mathf.Lerp(clearance,to.y,StepEase((fraction-.76f)/.24f)):clearance;
            return new Vector2(Mathf.Lerp(from.x,to.x,advance),height);
        }
        static float StepEase(float value) {
            float p=Mathf.Clamp01(value);return p*p*p*(10+p*(-15+6*p));
        }
        static float StairSupportHeight(float progress,int side) {
            float cycle=Mathf.Clamp(progress,0,.999999f)*StairX.Length;
            int active=(int)cycle,completed=active-1;if(completed%2!=side)completed--;
            float from=completed<0?InteriorFloor:StairY[completed];
            return active%2==side?Mathf.Lerp(from,StairY[active],StepEase(cycle-active)):from;
        }
        void PlantStairFeet(int index,float progress) {
            float lane=Lane(index);
            var targets=new Vector3[2];float pelvisDrop=0;
            for(int side=0;side<2;side++) {
                var hips=actors[index].GetBoneTransform(HumanBodyBones.Hips);
                var upper=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                float lateral=Mathf.Sign(Vector3.Dot(upper.position-hips.position,DockRotation*Vector3.forward))*.13f;
                var point=StairContact(progress,side);var contact=new Vector3(point.x,point.y,lane+lateral);
                contact.y+=ankleOffsets[index,side];
                targets[side]=DockPoint(contact);
                var lower=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                var foot=actors[index].GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                float reach=(Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,foot.position))*.995f;
                float horizontal=Vector3.ProjectOnPlane(upper.position-targets[side],Vector3.up).magnitude;
                float allowed=targets[side].y+Mathf.Sqrt(Mathf.Max(.000001f,reach*reach-horizontal*horizontal));
                pelvisDrop=Mathf.Max(pelvisDrop,upper.position.y-allowed);
            }
            // During the turn-to-stair blend the outgoing pose can put the hips
            // above leg reach. Transfer weight down before solving both contacts.
            actors[index].GetBoneTransform(HumanBodyBones.Hips).position-=Vector3.up*(pelvisDrop+.0001f);
            for(int side=0;side<2;side++) {
                LevelWalkingFoot(index,side,Vector3.up);
                SolveLeg(actors[index],side==0,targets[side]);
            }
        }
        static void SolveLeg(Animator actor,bool left,Vector3 target) {
            var upper=actor.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
            var lower=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
            var foot=actor.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
            var origin=upper.position;float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,foot.position);
            var axis=(target-origin).normalized;float d=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.001f,(a+b)*.995f);
            var bend=Vector3.ProjectOnPlane(actor.transform.forward,axis).normalized;
            float along=(a*a+d*d-b*b)/(2*d);
            var knee=origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var ankleRotation=foot.rotation;
            upper.rotation=Quaternion.FromToRotation(lower.position-origin,knee-origin)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(foot.position-lower.position,origin+axis*d-lower.position)*lower.rotation;
            foot.rotation=ankleRotation;
        }
    }
}
