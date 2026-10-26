using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    /// <summary>C1 at the landing (2026-10-25): on the natural arrival Hùng misses the last stair and goes down on his knee and
    /// hand, Nam sets his pack down first and helps him up, picks the pack up again, and both walk on to their places.</summary>
    public sealed class Map02ArrivalMisstepTests:ForestSceneTestBase
    {
        const string Folder="Logs/Map02Presentation_20261022";
        [UnityTest,Timeout(300000)]public IEnumerator HungMissesTheLastStairAndNamHelpsHimUp()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();
            yield return Check(false);yield return new ExitPlayMode();
        }
        [UnityTest,Timeout(300000)]public IEnumerator SkippingWhileThePackIsDownRestoresTheWornPack()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();
            yield return Check(true);yield return new ExitPlayMode();
        }
        [UnityTest,Timeout(300000),Explicit("Record the natural C1 handoff in Unity for visual review.")]
        public IEnumerator RecordMisstepContactReview()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();
            yield return Check(false,true);yield return new ExitPlayMode();
        }
        static IEnumerator Check(bool skipWhileDown,bool record=false)
        {
            // Construct closures only after EnterPlayMode's domain reload, as in the existing Map 2 fixtures.
            yield return null;
            var mission=Object.FindFirstObjectByType<Map01Mission>();mission.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ExtractionStage);
            var extraction=mission.GetComponentInChildren<Map01Extraction>();extraction.autoContinueToMap2=false;
            var body=mission.player.GetComponent<CharacterController>();body.enabled=false;mission.player.position=extraction.approach;body.enabled=true;
            yield return WaitGameSeconds(.3f);extraction.Skip();extraction.ContinueToMap2();
            float until=Time.realtimeSinceStartup+200;
            while(SceneManager.GetActiveScene().name!="Map 2"&&Time.realtimeSinceStartup<until)yield return null;
            var arrival=Object.FindFirstObjectByType<Map02Arrival>();Assert.NotNull(arrival);
            while(arrival.CurrentPhase==Map02Arrival.Phase.Loading&&Time.realtimeSinceStartup<until)yield return null;
            Directory.CreateDirectory(Folder);var log=new List<string>();
            while(!arrival.MisstepPlayed&&arrival.IsPlaying&&Time.realtimeSinceStartup<until)yield return null;
            Assert.IsTrue(arrival.MisstepPlayed,"C1 plays on the natural landing.");
            Assert.IsTrue(arrival.UsesApprovedMisstep,"The v4 content must be used, not the optional old Humanoid fallback.");
            var hung=arrival.MisstepActor(2);var nam=arrival.MisstepActor(0);
            var capture=arrival.gameObject.AddComponent<ExtractionCapturePump>();
            var continuity=arrival.gameObject.AddComponent<ExtractionCapturePump>();float lastClock=-1,maxSpineRate=0,maxSpineAt=0;Quaternion lastSpine=Quaternion.identity;System.Action sample=null;
            sample=()=>
            {
                float t=arrival.MisstepClock;nam=arrival.MisstepActor(0);var spine=nam.GetBoneTransform(HumanBodyBones.Spine).localRotation;
                if(t>2.75f&&t<4.95f&&lastClock>=2.75f&&t>lastClock)
                {float rate=Quaternion.Angle(lastSpine,spine)/(t-lastClock);if(rate>maxSpineRate){maxSpineRate=rate;maxSpineAt=t;}}
                lastClock=t;lastSpine=spine;if(arrival.IsPlaying)continuity.draw=sample;
            };continuity.draw=sample;
            using var film=record?new Map02ArrivalMisstepFilm():null;
            if(film!=null)
            {
                var filmPump=arrival.gameObject.AddComponent<ExtractionCapturePump>();System.Action draw=null;
                draw=()=>{if(film.Shoot(arrival))filmPump.draw=draw;};filmPump.draw=draw;
            }
            float bank=Map02Arrival.MisstepPoint(Vector3.zero).y;
            string P(Vector3 v)=>v.ToString("F3");
            foreach(var(at,name)in new[]{(.5f,"c1-trip"),(1.6f,"c1-down"),(2.9f,"c1-kneel"),(4.2f,"c1-up"),(5.3f,"c1-shoulder"),(6.6f,"c1-pack"),(8.4f,"c1-on")})
            {
                while(arrival.MisstepClock<at&&arrival.IsPlaying&&Time.realtimeSinceStartup<until)yield return null;
                // The coroutine resumes before LateUpdate; snapshot through the existing late capture pump so the
                // numbers and screenshot describe the pose after the arrival's contact corrections, not the raw take.
                ContactFrame frame=null;
                capture.draw=()=>{frame=ContactFrame.Read(arrival,hung,nam);ScreenCapture.CaptureScreenshot(Folder+"/"+(skipWhileDown?"skip-":"")+name+".png");};
                while(frame==null&&Time.realtimeSinceStartup<until)yield return null;
                Assert.NotNull(frame,"A rendered arrival frame must be sampled after LateUpdate.");
                float c=frame.clock;
                Assert.IsTrue(frame.framed,"Both Nam and Hùng stay inside the landing camera as they get up and walk on.");
                log.Add($"{name} t={c:F2} bank={bank:F3} hungHips={P(frame.hungHips)} hungRKnee={P(frame.hungKnee)} hungLHand={P(frame.hungHand)} namHips={P(frame.namHips)} namRHand={P(frame.namHand)} namLHand={P(frame.namLeftHand)} pack={(frame.hasPack?P(frame.pack):"worn")} worn={frame.worn} packBounds={P(arrival.MisstepPackBounds.center)}/{P(arrival.MisstepPackBounds.size)} hungShoulder={P(frame.hungShoulder)} armReach={frame.armReach:F3} floorTarget={P(frame.floorTarget)} floorReach={Vector3.Distance(frame.hungShoulder,frame.floorTarget):F3} rifle={frame.rifleVisible}");
                File.WriteAllLines(Folder+(skipWhileDown?"/c1-skip.txt":"/c1-misstep.txt"),log);
                if(name=="c1-trip"||name=="c1-on")
                {
                    Assert.IsTrue(frame.worn,"The approved cast visibly wears the pack before removal and after pickup.");
                    var current=arrival.MisstepActor(0);var chest=current.GetBoneTransform(HumanBodyBones.UpperChest)??current.GetBoneTransform(HumanBodyBones.Chest);
                    var right=current.GetBoneTransform(HumanBodyBones.RightUpperArm).position-current.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                    var up=current.GetBoneTransform(HumanBodyBones.Head).position-current.GetBoneTransform(HumanBodyBones.Hips).position;
                    Assert.Less(Vector3.Dot(arrival.MisstepPackBounds.center-chest.position,Vector3.Cross(right,up).normalized),-.03f,"The pack belongs behind the approved torso, regardless of chest bone roll.");
                    Assert.Less(Vector3.Distance(arrival.MisstepPackBounds.center,chest.position),.55f,"No detached floating pack.");
                    Assert.Greater(arrival.MisstepPackBounds.size.magnitude,.15f,"The worn pack contains visible mesh geometry.");
                }
                if(name=="c1-down")
                {
                    Assert.Less(Mathf.Abs(frame.hungKnee.y-(bank+.035f)),.02f,"His supporting knee joint stays 3.5 cm above the boards for the visible knee volume.");
                    Assert.Less(Mathf.Abs(frame.hungHand.y-bank),.14f,"His left hand is on the boards.");
                    Assert.IsTrue(frame.hasPack,"Nam has the pack off his shoulder.");Assert.IsFalse(frame.worn);
                    Assert.IsFalse(frame.rifleVisible,"All gun renderers must be hidden while Nam's hands are free for the pack and Hùng.");
                }
                if(name=="c1-kneel")
                {
                    Assert.IsTrue(frame.hasPack,"The pack remains on the ground while Nam helps him.");
                    Assert.Less(frame.pack.y-bank,.35f,"The pack is set down on the ground first.");
                    Assert.Less(Mathf.Abs(arrival.MisstepPackBounds.min.y-bank),.015f,"The actual backpack mesh rests on the boards, not just its pivot.");
                    Assert.Less(frame.namHips.y-bank,.75f,"Nam is down on one knee beside him.");
                    Assert.Less(Vector3.Distance(frame.namHand,frame.helpArm),Vector3.Distance(frame.namHand,frame.hurtArm),"The approved supporting gesture addresses Hùng's left arm, away from his injured right arm. Exact authored hand placement is checked against Blender in Map02ApprovedC1Tests.");
                    if(skipWhileDown)
                    {
                        LogAssert.Expect(LogType.Log,"Map 2 arrival complete. Party on dry bank; inventory, health and magazine retained.");
                        arrival.Skip();yield return null;
                        Assert.IsNull(arrival.MisstepPack,"Skipping removes the temporary carried pack.");
                        Assert.IsTrue(arrival.WornPack.enabled,"Skipping restores the original backpack renderer.");
                        Assert.AreEqual(Map02Arrival.Phase.Gameplay,arrival.CurrentPhase);
                        Assert.AreEqual(1,arrival.Passengers[0].GetComponentsInChildren<Animator>(true).Length,"Skipping must remove the temporary C1 cast.");
                        yield break;
                    }
                }
                if(name=="c1-up")Assert.Greater(frame.hungHips.y-bank,.55f,"Getting up together.");
            }
            while(arrival.MisstepClock<8.2f&&arrival.IsPlaying&&Time.realtimeSinceStartup<until)yield return null;
            Assert.IsNull(arrival.MisstepPack,"The pack is back on his back.");Assert.IsTrue(arrival.WornPackVisible,"The approved C1 cast must visibly wear the pack before handing back to the gameplay mesh.");
            LogAssert.Expect(LogType.Log,"Map 2 arrival complete. Party on dry bank; inventory, health and magazine retained.");
            while(arrival.IsPlaying&&Time.realtimeSinceStartup<until)yield return null;
            File.WriteAllLines(Folder+"/c1-misstep.txt",log);
            File.WriteAllText(Folder+"/c1-continuity.txt",$"Nam assisting spine peak angular speed: {maxSpineRate:F2} degrees/second at {maxSpineAt:F3} s (2.75–4.95 s). Limit 500; a broken endpoint solve exceeded 970–1030 degrees/second.");
            Assert.Less(maxSpineRate,500,"Nam's assisting spine must not snap to a maximum bend when the reach endpoint misses.");
            Assert.AreEqual(Map02Arrival.Phase.Gameplay,arrival.CurrentPhase);
            Assert.AreEqual(1,arrival.Passengers[0].GetComponentsInChildren<Animator>(true).Length,"The gameplay model must be restored after C1.");
            for(int i=0;i<3;i++)Assert.Less(Vector3.ProjectOnPlane(arrival.Passengers[i].position-arrival.SpawnPoints[i],Vector3.up).magnitude,.1f,"Everyone ends at their place on the bank, actor "+i);
        }
        sealed class ContactFrame
        {
            public float clock,armReach;public bool hasPack,worn,rifleVisible,framed;
            public Vector3 hungHips,hungKnee,hungHand,hungShoulder,namHips,namHand,namLeftHand,helpArm,hurtArm,pack,floorTarget;
            public static ContactFrame Read(Map02Arrival arrival,Animator hung,Animator nam)
            {
                hung=arrival.MisstepActor(2);nam=arrival.MisstepActor(0);
                Vector3 B(Animator a,HumanBodyBones b)=>a.GetBoneTransform(b).position;
                var upper=B(hung,HumanBodyBones.LeftUpperArm);var lower=B(hung,HumanBodyBones.LeftLowerArm);var hand=B(hung,HumanBodyBones.LeftHand);
                var rifle=arrival.Passengers[0].GetComponentInChildren<Map01Rifle>(true);
                var camera=Camera.main;bool framed=camera!=null;
                foreach(var actor in new[]{nam,hung})foreach(var point in new[]{B(actor,HumanBodyBones.Head)+Vector3.up*.3f,B(actor,HumanBodyBones.LeftFoot),B(actor,HumanBodyBones.RightFoot),B(actor,HumanBodyBones.LeftHand),B(actor,HumanBodyBones.RightHand)})
                {if(camera==null)continue;var viewport=camera.WorldToViewportPoint(point);framed&=viewport.z>camera.nearClipPlane&&viewport.x>.02f&&viewport.x<.98f&&viewport.y>.09f&&viewport.y<.91f;}
                return new ContactFrame{clock=arrival.MisstepClock,hungHips=B(hung,HumanBodyBones.Hips),hungKnee=B(hung,HumanBodyBones.RightLowerLeg),hungHand=hand,hungShoulder=upper,armReach=Vector3.Distance(upper,lower)+Vector3.Distance(lower,hand),
                    namHips=B(nam,HumanBodyBones.Hips),namHand=B(nam,HumanBodyBones.RightHand),namLeftHand=B(nam,HumanBodyBones.LeftHand),helpArm=Vector3.Lerp(upper,lower,.6f),hurtArm=Vector3.Lerp(B(hung,HumanBodyBones.RightUpperArm),B(hung,HumanBodyBones.RightLowerArm),.6f),hasPack=arrival.MisstepPack!=null,pack=arrival.MisstepPack!=null?arrival.MisstepPack.position:Vector3.zero,worn=arrival.WornPackVisible,
                    floorTarget=Map02Arrival.MisstepPoint(new Vector3(.22f,-.78f,.03f))+Vector3.up*.05f,rifleVisible=System.Linq.Enumerable.Any(arrival.Passengers[0].GetComponentsInChildren<ShadowVale.Gameplay.Combat.Weapon>(true),w=>w.IsGun&&System.Linq.Enumerable.Any(w.GetComponentsInChildren<Renderer>(true),r=>r.enabled&&!r.forceRenderingOff&&r.gameObject.activeInHierarchy)),framed=framed};
            }
        }
    }
}
