using System;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Debug=UnityEngine.Debug;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// Hùng's sole is found from his skin every frame. The search skips the parts of him that cannot
    /// be lowest: it must still give exactly what a pass over every sample gives, in any pose.
    /// </summary>
    public sealed class SkinContactTests
    {
        private GameObject hung;
        private Animator actor;
        private object contact;
        private Func<float> culled,every;
        private PropertyInfo samples,evaluated;
        private Transform[] bones;
        private Quaternion[] rest;

        [SetUp]
        public void SetUp()
        {
            var prefab=Resources.Load<Map01HungVisual>("Characters/HungVisual");
            Assert.IsNotNull(prefab,"Hùng's visual prefab is missing.");
            hung=Object.Instantiate(prefab.gameObject);
            actor=hung.GetComponent<Map01HungVisual>().actor;
            // Internal to the game assembly, like the rest of the rescue's plumbing.
            var type=typeof(Map01Mission).Assembly.GetType("ShadowVale.Map01.Map01SkinContact",true);
            contact=Activator.CreateInstance(type,new object[]{actor}); // An Animator alone converts to the bool overload.
            culled=(Func<float>)Delegate.CreateDelegate(typeof(Func<float>),contact,type.GetMethod("LowestY"));
            every=(Func<float>)Delegate.CreateDelegate(typeof(Func<float>),contact,type.GetMethod("LowestYUnculled"));
            samples=type.GetProperty("Samples");evaluated=type.GetProperty("Evaluated");
            bones=actor.GetComponentsInChildren<Transform>();
            rest=Array.ConvertAll(bones,b=>b.localRotation);
        }

        [TearDown]
        public void TearDown() { if(hung!=null)Object.DestroyImmediate(hung); }

        private int Samples=>(int)samples.GetValue(contact);
        private int Evaluated=>(int)evaluated.GetValue(contact);

        /// <summary>
        /// The samples are baked from the models (ShadowVale > Rescue > Bake character ground contacts).
        /// A model exported again has new meshes: until the bake is run again its contact is not found.
        /// </summary>
        [Test]
        public void BakedSamplesMatchTheModelsInUse()
        {
            var data=Resources.Load<Map01SkinContactData>("Rescue/SkinContactData");
            Assert.IsNotNull(data,"Rescue/SkinContactData is missing.");
            var nam=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
            Assert.IsNotNull(nam,"Nam's prefab is missing.");
            foreach(var model in new[]{nam,hung})
                foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var baked=Array.Find(data.skins,s=>s.mesh==skin.sharedMesh);
                    Assert.IsNotNull(baked,$"{model.name}/{skin.name}: no baked samples for its mesh. Bake the ground contacts again.");
                    Assert.AreEqual(skin.sharedMesh.vertexCount,baked.vertices.Length,$"{model.name}/{skin.name}: the baked samples are from another version of the mesh.");
                }
        }

        [Test]
        public void SkippingBodyPartsNeverChangesTheContactHeight()
        {
            Assert.IsFalse(float.IsInfinity(every()),"No skin samples for Hùng: the contact data no longer matches his meshes.");
            Assert.AreEqual(every(),culled(),"Standing as authored.");
            var random=new System.Random(20261009);
            float Next(float low,float high)=>low+(high-low)*(float)random.NextDouble();
            Quaternion Turn(float degrees)=>Quaternion.Euler(Next(-degrees,degrees),Next(-degrees,degrees),Next(-degrees,degrees));
            for(int pose=0;pose<60;pose++)
            {
                // From a slight shift of weight to limbs anywhere, the whole body tipped any way up,
                // at any height and size: whichever part ends up lowest has to be found.
                float bend=pose<10?8:pose<30?60:180;
                for(int i=1;i<bones.Length;i++)bones[i].localRotation=rest[i]*Turn(bend);
                hung.transform.SetPositionAndRotation(new Vector3(Next(-300,300),Next(-40,120),Next(-300,300)),pose%3==0?Quaternion.identity:Turn(180));
                hung.transform.localScale=Vector3.one*Next(.4f,2.5f);
                Assert.AreEqual(every(),culled(),$"Pose {pose}, limbs bent up to {bend} degrees.");
            }
        }

        [Test]
        public void OnHisFeetOrKneesMostOfTheBodyIsSkipped()
        {
            Report("as authored");
            Assert.Less(Evaluated*4,Samples,"Standing, only what is near the ground should be evaluated.");
            // The poses he actually spends the mission in: every clip of his controller, held on his
            // knees, getting up, limping home, talking at the base, shot down.
            var hips=actor.GetBoneTransform(HumanBodyBones.Hips);
            var head=actor.GetBoneTransform(HumanBodyBones.Head);
            float standing=head.position.y-hips.position.y;int posed=0;
            foreach(var clip in actor.runtimeAnimatorController.animationClips)
                foreach(float moment in new[]{.1f,.5f,.9f})
                {
                    clip.SampleAnimation(actor.gameObject,clip.length*moment);
                    if(Mathf.Abs(head.position.y-hips.position.y-standing)>.02f)posed++;
                    Assert.AreEqual(every(),culled(),$"{clip.name} at {moment:P0}.");
                    if(moment==.5f)Report(clip.name);
                }
            Assert.Greater(posed,0,"None of his clips moved him: these poses were not really tried.");
        }

        private void Report(string pose)
        {
            for(int i=0;i<20;i++){culled();every();} // Warm the JIT and the caches.
            const int Rounds=200;
            var watch=Stopwatch.StartNew();
            for(int i=0;i<Rounds;i++)every();
            double full=watch.Elapsed.TotalMilliseconds/Rounds;
            watch.Restart();
            for(int i=0;i<Rounds;i++)culled();
            double fast=watch.Elapsed.TotalMilliseconds/Rounds;
            Debug.Log(FormattableString.Invariant($"[SkinContact] {pose}: {Evaluated} of {Samples} samples; every sample {full:0.000} ms, culled {fast:0.000} ms per call."));
        }
    }
}
