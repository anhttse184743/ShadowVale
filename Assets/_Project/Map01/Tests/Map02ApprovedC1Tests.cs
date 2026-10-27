using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02ApprovedC1Tests
    {
        [Serializable] sealed class Poses{public Frame[] frames;}
        [Serializable] sealed class Frame{public int frame;public float time;public Bones Nam,Hung;}
        [Serializable] sealed class Bones{public float[] Hips,Head,LeftHand,RightHand,LeftForeArm,LeftArm,RightLeg,LeftFoot,RightFoot;}
        [Serializable] sealed class Travel{public float[] nam,hung;}
        static Vector3 V(float[] p)=>new(p[0],p[1],p[2]);
        static Vector3 Convert(Vector3 p)=>new(p.y,p.z,-p.x);
        [Test]public void DetachedBackpackRetainsWorldSizeAndSitsNearTheGameplayTorso()
        {
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");var go=Object.Instantiate(prefab);Mesh mesh=null;
            try
            {
                var skin=go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Nam_BaloRanRi");var actor=go.GetComponentInChildren<Animator>();
                var data=Resources.Load<Map02ArrivalC1Data>("Map02ArrivalC1/Approved");Assert.NotNull(data.backpackMesh);Assert.IsTrue(data.backpackMesh.isReadable,"The isolated prop must retain CPU data in Play Mode.");
                mesh=Map02Arrival.BakeC1Prop(skin,out var centre,data.backpackMesh);var chest=actor.GetBoneTransform(HumanBodyBones.UpperChest)??actor.GetBoneTransform(HumanBodyBones.Chest);
                Assert.That(mesh.bounds.size.magnitude,Is.InRange(.15f,1.5f),"The pack must keep character-scale geometry, not millimetres or metres of import scaling.");
                Assert.Less(Vector3.Distance(centre,chest.position),.55f,"The detached pack retains the actual skinned pivot near the torso.");
            }
            finally{if(mesh!=null)Object.DestroyImmediate(mesh);Object.DestroyImmediate(go);}
        }
        [Test]public void SupportingKneeFitsSolidLandingWithoutMovingTheApprovedHandsOrAnkle()
        {
            var data=Resources.Load<Map02ArrivalC1Data>("Map02ArrivalC1/Approved");var go=Object.Instantiate(data.hung);var actor=go.GetComponentInChildren<Animator>();
            try
            {
                // The actor's source origin starts one stair (21 cm) below the landing.
                go.transform.position=Vector3.down*.21f;
                for(int frame=27;frame<=98;frame++)
                {
                    float t=(frame-1)/30f;data.hungMotion.SampleAnimation(actor.gameObject,t);
                    var hand=actor.GetBoneTransform(HumanBodyBones.LeftHand);var ankle=actor.GetBoneTransform(HumanBodyBones.RightFoot);var knee=actor.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                    var handAt=hand.position;var ankleAt=ankle.position;var footRotation=ankle.rotation;
                    Map02Arrival.GroundApprovedKnee(actor,t,0);
                    Assert.Less(Vector3.Distance(handAt,hand.position),.0001f,"The assisted upper body is untouched.");
                    Assert.Less(Vector3.Distance(ankleAt,ankle.position),.001f,"The authored ankle stays planted.");
                    Assert.Less(Quaternion.Angle(footRotation,ankle.rotation),.01f);
                    Assert.GreaterOrEqual(knee.position.y,.015f,"The knee volume stays above the solid top landing, frame "+frame);
                }
            }
            finally{Object.DestroyImmediate(go);}
        }
        [Test]public void ApprovedMeshesAndDirectTracksMatchTheBlenderSource()
        {
            var data=Resources.Load<Map02ArrivalC1Data>("Map02ArrivalC1/Approved");Assert.NotNull(data);Assert.AreEqual("15723d4da3281b245f401c11446c7ee006ec79889436096adf5fc9b7a8234ae0",data.sourceSha256);
            var poses=JsonUtility.FromJson<Poses>(data.reviewPoses.text);var travel=JsonUtility.FromJson<Travel>(data.travel.text);
            for(int index=0;index<2;index++)
            {
                var holder=new GameObject("C1 test origin");var go=Object.Instantiate(index==0?data.nam:data.hung,holder.transform,false);var a=go.GetComponentInChildren<Animator>();var clip=index==0?data.namMotion:data.hungMotion;
                try
                {
                    Assert.IsTrue(a.isHuman);Assert.IsTrue(a.avatar.isValid);Assert.IsFalse(clip.humanMotion,"Direct transform tracks must retain the approved twists/fingers.");Assert.IsFalse(a.enabled);
                    Assert.AreEqual(index==0?62:28,go.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="root"||t.IsChildOf(go.GetComponentsInChildren<Transform>(true).Single(b=>b.name=="root"))));
                    foreach(var f in poses.frames)
                    {
                        var tr=index==0?travel.nam:travel.hung;int k=(f.frame-1)*2;var start=index==0?new Vector3(.78f,-.42f,0):new Vector3(0,.1f,-.21f);
                        holder.transform.SetPositionAndRotation(Convert(start+new Vector3(tr[k],tr[k+1],0)),index==0?Quaternion.identity:Quaternion.LookRotation(Vector3.left));clip.SampleAnimation(a.gameObject,f.time);
                        var b=index==0?f.Nam:f.Hung;
                        foreach(var pair in new[]{(HumanBodyBones.Hips,b.Hips),(HumanBodyBones.Head,b.Head),(HumanBodyBones.LeftHand,b.LeftHand),(HumanBodyBones.RightHand,b.RightHand),(HumanBodyBones.LeftFoot,b.LeftFoot),(HumanBodyBones.RightFoot,b.RightFoot)})
                            Assert.Less(Vector3.Distance(a.GetBoneTransform(pair.Item1).position,Convert(V(pair.Item2))),.008f,$"C1 actor {index}, frame {f.frame}, bone {pair.Item1} must match Blender within 8 mm.");
                    }
                }
                finally{Object.DestroyImmediate(holder);}
            }
        }
    }
}
