using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>Nhịp 3, 5 and 6 staging: the scene clock is driven by hand, so a 70 s scene runs in a frame.</summary>
    public sealed class Map02FinaleTests:ForestSceneTestBase
    {
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static Map02Mission mission;static Map02ChapterDirector chapter;
        static IEnumerator Open()
        {
            float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){mission=Object.FindFirstObjectByType<Map02Mission>();if(mission?.Chapter!=null&&mission.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(mission?.Chapter);chapter=mission.Chapter;chapter.enabled=false;
        }
        static void Call(string method,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(method,Private).Invoke(chapter,args);
        static float Clock=>(float)typeof(Map02ChapterDirector).GetField("sceneClock",Private).GetValue(chapter);
        static void Load(string id)
        {
            var snapshot=mission.Capture();var s=new Map02ChapterState();
            foreach(var t in Map02ChapterDefinition.Load().tasks){s.Complete(t.id);if(t.id==id)break;}
            snapshot.chapter=s;mission.Restore(snapshot);
        }
        // The scene clock is synthetic; real audio cannot advance in this tight loop.
        // Keep authored line durations/gates, and release only the audio completion wait.
        static void Frame(float dt=.1f){DialogueVoice.StopAll();Call("TickScene",dt);Call("TickTank",dt,false);Call("TickEffects");}
        static void Until(string scene,float time){while(chapter.CurrentScene?.id==scene&&Clock<time)Frame();}
        static void Place(Vector3 p){var cc=mission.Player.GetComponent<CharacterController>();cc.enabled=false;mission.Player.position=p;cc.enabled=true;Physics.SyncTransforms();}
        // Without bound art there is nothing to see either way; with art, it must match the story.
        static bool HungVisible=>chapter.Actor("Hùng").visual!=null&&chapter.Actor("Hùng").visual.gameObject.activeInHierarchy;
        static bool HungArt=>chapter.Actor("Hùng").visual!=null;
        static void AssertEnding(string when)
        {
            Assert.AreEqual("DAWN",chapter.CurrentScene?.id,when);var s=chapter.State;
            Assert.IsTrue(s.hungDead&&s.tankDestroyed&&s.radioTransferred&&s.artilleryFired,when);
            Call("TickEffects");Call("TickTank",0f,true);
            Assert.AreEqual(0,chapter.MilitiaCount,when);Assert.AreEqual(0,chapter.EffectGrey,when);Assert.AreEqual(0,chapter.EffectBlack,when);Assert.AreEqual(0,chapter.EffectRed,when);
            Assert.AreEqual(22000,chapter.ListenerCutoff,when);Assert.AreEqual("destroyed",chapter.TankStage,when);
            Assert.IsTrue(chapter.RadioProp!=null&&chapter.RadioProp.activeSelf&&chapter.RadioProp.transform.IsChildOf(mission.Player),when+": the radio stays with Nam");
            Assert.AreEqual(1f,Time.timeScale,when);
        }

        [UnityTest,Timeout(240000)]public IEnumerator SacrificeAndCounterAttackFollowTheScriptClock()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return SacrificeInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator SacrificeInPlayMode()
        {
            yield return Open();
            try
            {
                Load("N3_08");Assert.AreEqual("N3_09",chapter.CurrentScene?.id);
                Until("N3_09",Map02ChapterDirector.ShotAt-.2f);Assert.IsFalse(chapter.State.hungDead);if(HungArt)Assert.IsTrue(HungVisible,"Hùng is alive until the shot at 8 s.");Assert.AreEqual("blocking",chapter.TankStage);
                Until("N3_09",Map02ChapterDirector.ShotAt+.3f);Assert.AreNotEqual("destroyed",chapter.TankStage,"One second between his fall and the blast.");
                Until("N3_09",Map02ChapterDirector.BlastAt+.4f);Assert.AreEqual("destroyed",chapter.TankStage);
                Assert.LessOrEqual(chapter.ListenerCutoff,10.5f,"Deaf right after the blast.");Assert.Greater(chapter.EffectGrey,0);
                Until("N3_09",Map02ChapterDirector.EchoAt-.5f);Assert.IsTrue(chapter.WaitingForLine);Assert.AreEqual("m2v3_sacrifice_02",chapter.CurrentLineId);
                Until("N3_09",Map02ChapterDirector.EchoAt+.3f);Assert.IsFalse(chapter.WaitingForLine,"The promise echoes after the blast.");
                Until("N3_09",Map02ChapterDirector.FireOrderAt-.5f);Assert.IsFalse(chapter.CurrentLineId=="m2v3_sacrifice_03"&&!chapter.WaitingForLine);
                Until("N3_09",Map02ChapterDirector.FireOrderAt+.3f);Assert.AreEqual("m2v3_sacrifice_03",chapter.CurrentLineId);Assert.IsFalse(chapter.WaitingForLine);
                Until("N3_09",Map02ChapterDirector.MilitiaAt+4);Assert.AreEqual(7,chapter.MilitiaCount,"The militia pour down the road.");
                Until("N3_09",Map02ChapterDirector.FadeAt+2);Assert.Greater(chapter.EffectBlack,0);
                Until("N3_09",90);AssertEnding("played through");
                Assert.IsFalse(HungVisible,"Hùng does not come back after the sacrifice.");
            }
            finally{chapter.Cancel();chapter.enabled=true;}
        }

        [UnityTest,Timeout(240000)]public IEnumerator SkippingTheSacrificeAtAnyMomentLandsOnTheSameDawn()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return SkipInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator SkipInPlayMode()
        {
            yield return Open();
            try
            {
                foreach(var at in new[]{1f,8.5f,20f,45f})
                {
                    Load("N3_08");Until("N3_09",at);Call("EndScene");AssertEnding("skipped at "+at+" s");
                    Assert.IsFalse(HungVisible,"skipped at "+at+" s");
                }
                // A load after the sacrifice keeps Hùng gone and the radio with Nam.
                Load("N3_09");Call("EndScene");Load("DAWN");Call("TickEffects");Assert.IsFalse(HungVisible);Assert.IsTrue(chapter.RadioProp!=null&&chapter.RadioProp.activeSelf);
            }
            finally{chapter.Cancel();chapter.enabled=true;}
        }

        [UnityTest,Timeout(240000)]public IEnumerator TankShotAndFarewellAreStagedWithoutCloseUps()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return WoundInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator WoundInPlayMode()
        {
            yield return Open();
            var rig=Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None).First(c=>c.GetComponent<Camera>()?.enabled==true);
            try
            {
                Assert.AreEqual(0,rig.ShakeAmplitude,"Nothing shakes unless a scene asks.");
                Load("N3_05B");Call("Finish",Map02ChapterDefinition.Load().Find("N3_05C"));Assert.AreEqual("N3_WOUND",chapter.CurrentScene?.id);
                Frame();var gap=Vector3.ProjectOnPlane(chapter.Tank.transform.position-chapter.Sites["tankBreach"].position,Vector3.up).magnitude;
                Assert.Greater(gap,10,"The tank drives in from outside the village.");Assert.AreEqual(0,chapter.EffectRed,"Nobody is hit while it drives in.");
                for(int i=0;i<140&&chapter.EffectRed==0;i++)Frame();
                Assert.Less(Vector3.ProjectOnPlane(chapter.Tank.transform.position-chapter.Sites["tankBreach"].position,Vector3.up).magnitude,1,"It stands at the breach when it fires.");
                Assert.Greater(chapter.EffectRed,0,"Red, blurred view after the hit.");Assert.Less(chapter.ListenerCutoff,2000,"Sound sinks.");Assert.Greater(rig.ShakeAmplitude,0);
                Call("EndScene");Call("TickEffects");Assert.Greater(chapter.EffectRed,0,"Still hurt while crawling to cover.");
                Load("N3_CRAWL");Call("TickEffects");Assert.Less(chapter.EffectRed,.1f,"In cover: only the faint pulse of the bleeding shoulder.");Assert.AreEqual(22000,chapter.ListenerCutoff);
                // The farewell: Hùng beside Nam, between him and the tank; the radio changes hands before Hùng goes.
                Load("N3_06");Place(chapter.Sites["ditch"].position+Vector3.forward*2);Call("Update");Assert.AreEqual("N3_07",chapter.CurrentScene?.id);
                var nam=mission.Player.position;var hung=chapter.Actor("Hùng").transform.position;
                Assert.Less(Vector3.Distance(nam,hung),2.5f);Assert.Greater(Vector3.Dot(hung-nam,chapter.Sites["tankDock"].position-nam),0);
                Assert.IsTrue(chapter.RadioProp==null||!chapter.RadioProp.activeSelf);
                for(int i=0;i<4000&&chapter.CurrentLineId!="m2v3_farewell_09"&&chapter.CurrentScene!=null;i++)Frame();
                Assert.AreEqual("m2v3_farewell_09",chapter.CurrentLineId);Call("TickEffects");Assert.IsTrue(chapter.RadioProp.activeSelf);
                Call("EndScene");Assert.IsTrue(chapter.State.radioTransferred);
            }
            finally{chapter.Cancel();chapter.enabled=true;}
        }

        [UnityTest,Timeout(240000)]public IEnumerator DayThreeCrateAndNightThreeOpeningsAreDressed()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return DressingInPlayMode();
            yield return new ExitPlayMode();
        }
        static Map02ObjectiveObject Mark(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
        static float Flat(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
        static IEnumerator DressingInPlayMode()
        {
            yield return Open();
            try
            {
                // D3_02 (after D3_01, Day 3 goes in order): the crate is fetched from the closed store and carried to the bank.
                Load("D3_01");Assert.AreEqual(4,chapter.State.phase);Assert.IsFalse(chapter.State.Available(Map02ChapterDefinition.Load().Find("D3_03")),"Day 3 goes one task at a time.");var crate=Mark("D3_02");
                Assert.Less(Flat(crate.transform.position,chapter.Sites["store"].position),1);Place(crate.transform.position);Assert.IsTrue(mission.Interact(crate));
                Assert.AreEqual("D3_02",chapter.State.carry);Assert.Less(Flat(crate.transform.position,chapter.Sites["ditch"].position),1,"The pin moves to the bank while carrying.");
                Place(crate.transform.position);Assert.IsTrue(mission.Interact(crate));Assert.IsTrue(string.IsNullOrEmpty(chapter.State.carry));Assert.AreEqual("D3_02",chapter.CurrentScene?.id);
                Call("TickDressing",0f);Assert.IsTrue(chapter.CrateProp.activeSelf&&chapter.BundleProp.activeSelf,"Crate and bundle sit in the bank hole.");
                Call("EndScene");Assert.IsTrue(chapter.Busy,"Stowing follows the scene.");Call("EndWork",true);Assert.IsTrue(chapter.State.Done("D3_02"));
                // The warmest afternoon of the chapter.
                Call("TickLight",10f);var sun=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
                if(sun!=null)Assert.Greater(sun.color.r-sun.color.b,.3f,"Golden Day 3 light.");
                // Nhịp 1: black, then the flare; Ông Sáu's roof is burning.
                Load("D3_06");var attack=Mark("N3_00");Place(attack.transform.position);Assert.IsTrue(mission.Interact(attack));Assert.AreEqual("N3_00",chapter.CurrentScene?.id);
                Call("TickEffects");Assert.AreEqual(0,chapter.EffectBlack,"The shift change is seen, not cut.");Assert.IsTrue(chapter.WaitingForLine);
                Assert.Greater(chapter.HandoverWatchers.Count,0,"The men at the rest spot.");
                Until("N3_00",4f);var locked=(Vector3)typeof(Map02ChapterDirector).GetField("lockedPosition",Private).GetValue(chapter);
                Assert.Less(Vector3.ProjectOnPlane(locked-chapter.HandoverSpot,Vector3.up).magnitude,.1f,"Nam has walked up to anh Lực.");
                foreach(var w in chapter.HandoverWatchers)Assert.Less(Vector3.Angle(Vector3.ProjectOnPlane(w.transform.forward,Vector3.up),Vector3.ProjectOnPlane(locked-w.transform.position,Vector3.up)),30,w.role+" turns to Nam.");
                Assert.IsTrue(chapter.WaitingForLine,"Nobody shouts during the handover.");
                Until("N3_00",5f);Call("TickEffects");Assert.AreEqual(1,chapter.EffectBlack,"Then dark: Nam lies down.");
                Until("N3_00",6f);Call("TickDressing",.1f);Assert.IsFalse(chapter.WaitingForLine);Assert.Greater(chapter.FlareLight.intensity,0);Assert.IsTrue(chapter.HouseFire.enabled);
                Call("EndScene");
                // Nhịp 4: Tâm fires from the bamboo while Nam clears the tree.
                Load("N3_CRAWL");Call("TickDressing",1.7f);Assert.NotNull(chapter.TamFlash);
                // The farewell: Hùng takes the bundle; the crate stays.
                Load("N3_06");Place(chapter.Sites["ditch"].position+Vector3.forward*2);Call("Update");Assert.AreEqual("N3_07",chapter.CurrentScene?.id);
                for(int i=0;i<4000&&chapter.CurrentLineId!="m2v3_farewell_06"&&chapter.CurrentScene!=null;i++)Frame();
                Call("TickDressing",0f);Assert.IsFalse(chapter.BundleProp.activeSelf);Assert.IsTrue(chapter.CrateProp.activeSelf);
            }
            finally{chapter.Cancel();chapter.enabled=true;}
        }

        [UnityTest,Timeout(240000)]public IEnumerator TheTankRollsItsTracksAndWheelsWhenItDrives()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return RollInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator RollInPlayMode()
        {
            yield return Open();
            try
            {
                // After the crawl the tank turns away into the village: it drives, it is not placed.
                Load("N3_WOUND");Call("Finish",Map02ChapterDefinition.Load().Find("N3_CRAWL"));
                if(chapter.TrackLink("L",0)==null){Assert.Inconclusive("Tank art without track links");yield break;}
                var link=chapter.TrackLink("L",0);var start=link.localPosition;var wheel=chapter.Tank.GetComponentsInChildren<Transform>().First(t=>t.name=="Wheel_L1");var turned=wheel.rotation;
                for(int i=0;i<12;i++)Call("TickTank",.1f,false);
                Assert.AreEqual("leaving",chapter.TankStage);Assert.Greater(chapter.TrackTravel,1f,"The tank has driven");
                Assert.Greater(Vector3.Distance(start,link.localPosition),.001f,"Links move round the loop");
                Assert.Greater(Quaternion.Angle(turned,wheel.rotation),10,"Road wheels turn");
                var neighbour=chapter.TrackLink("L",1);Assert.Less(Vector3.Distance(link.localPosition,neighbour.localPosition),.25f,"Links stay on the run, one pitch apart");
            }
            finally{chapter.Cancel();chapter.enabled=true;}
        }
        [UnityTest,Timeout(240000)]public IEnumerator TelemetryStagesKeepTheirOrderThroughTheChapter()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return StagesInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator StagesInPlayMode()
        {
            yield return Open();
            try
            {
                // Telemetry stage ids (Map02Stage) per checkpoint, as before the Day 3 / Night 3 rework.
                var seen=new System.Collections.Generic.List<Map02Stage>();
                foreach(var t in Map02ChapterDefinition.Load().tasks){Load(t.id);var stage=mission.World.story.stage;if(seen.Count==0||seen[seen.Count-1]!=stage)seen.Add(stage);}
                CollectionAssert.AreEqual(new[]{Map02Stage.VillageHelp,Map02Stage.QuietPreparation,Map02Stage.MissingScout,Map02Stage.QuietPreparation,Map02Stage.HoldLanes,Map02Stage.Breakout,Map02Stage.Complete},seen,string.Join(", ",seen));
            }
            finally{chapter.Cancel();chapter.enabled=true;}
        }

        [UnityTest,Timeout(600000),Explicit("Manual performance probe; writes Logs/GD8/perf.txt, asserts nothing.")]
        public IEnumerator NightThreePerformanceProbe()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();
            yield return PerfInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator PerfInPlayMode()
        {
            yield return Open();chapter.enabled=true;var report=new System.Text.StringBuilder();
            IEnumerator Measure(string label)
            {
                for(int i=0;i<30;i++)yield return null;float total=0;int frames=0;float worst=0;
                for(int i=0;i<120;i++){yield return null;total+=Time.unscaledDeltaTime;worst=Mathf.Max(worst,Time.unscaledDeltaTime);frames++;}
                report.AppendLine($"{label}: avg {total/frames*1000:0.0} ms ({frames/total:0} fps), worst {worst*1000:0.0} ms, lights {Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l=>l.isActiveAndEnabled)}, particles {Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Count(p=>p.isPlaying)}");
            }
            try
            {
                Load("N2_02");Place(chapter.Sites["bridge"].position);yield return Measure("Night 2 bridge (reference)");
                Load("N3_05B");Place(chapter.Sites["lane3"].position);yield return Measure("Night 3 third lane, fire and soldiers");
                Load("N3_08");float until=Time.time+11;while(Time.time<until)yield return null;yield return Measure("Night 3 bank after the blast");
                until=Time.time+22;while(Time.time<until&&chapter.CurrentScene?.id=="N3_09")yield return null;yield return Measure("Nhịp 6 guns and militia");
                System.IO.Directory.CreateDirectory("Logs/GD8");System.IO.File.WriteAllText("Logs/GD8/perf.txt",report.ToString());
            }
            finally{chapter.Cancel();}
        }

        [UnityTest,Timeout(300000),Explicit("Manual art review; writes Logs/Map02Models/*.png, asserts only that the cast has models.")]
        public IEnumerator CastAndPropsCapture()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();
            yield return CaptureInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator CaptureInPlayMode()
        {
            yield return Open();chapter.enabled=true;System.IO.Directory.CreateDirectory("Logs/Map02Models");
            var rig=Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None).First(c=>c.GetComponent<Camera>()?.enabled==true);
            foreach(var role in new[]{"Anh Thành","Anh Lực","Tâm","Chị Lành","Ông Sáu","Chú Bảy","Bé Tí"})Assert.NotNull(chapter.Actor(role).visual,role+" has a model");
            IEnumerator Shot(string file,Vector3 eye,Vector3 look,float fov=45)
            {
                for(int i=0;i<20;i++){rig.SetCinematicView(eye,Quaternion.LookRotation(look-eye),fov,1);yield return null;}
                ScreenCapture.CaptureScreenshot("Logs/Map02Models/"+file);yield return null;yield return null;
            }
            try
            {
                // Day 3 supper yard: Hùng, Chị Lành, Ông Sáu at home, Anh Thành in the yard (lit as day).
                Load("D3_05");var yard=chapter.Sites["family"].position;Call("TickLight",10f);
                chapter.Actor("Anh Thành").StopAt(yard+Vector3.left*2);chapter.Actor("Bé Tí").StopAt(yard+Vector3.back*1.5f);chapter.Actor("Chú Bảy").StopAt(yard+Vector3.forward*1.8f);
                chapter.Actor("Anh Lực").StopAt(yard+Vector3.right*3+Vector3.back);chapter.Actor("Tâm").StopAt(yard+Vector3.left*3.5f+Vector3.back*1.5f);
                Place(yard+Vector3.back*6);yield return Shot("cast-day3-yard.png",yard+new Vector3(1.5f,1.6f,-6.5f),yard+Vector3.up*.9f,50);
                // The last boat mid-river.
                Load("N3_06");yield return null;var boat=chapter.LastBoat.position;yield return Shot("cast-last-boat.png",boat+new Vector3(-6,2.4f,4),boat+Vector3.up*.6f,45);
                // In daylight (Day 3, after stowing): the crate and bundle in the bank hole, seen from above the rice.
                Load("D3_02");Call("TickLight",10f);Call("TickDressing",0f);var bundle=chapter.BundleProp.transform.position;
                yield return Shot("prop-charge-bank.png",bundle+new Vector3(.9f,.75f,-.7f),bundle+Vector3.up*.12f,35);
                // A rank-and-file soldier (the infantry model), stood in the yard in daylight.
                var post=chapter.Sites["gate"].position;var soldier=Object.Instantiate(chapter.Layout.enemyArt,post,Quaternion.Euler(0,225,0));
                yield return Shot("cast-soldier.png",post+new Vector3(-2.4f,1.5f,-2.6f),post+Vector3.up*1,40);Object.Destroy(soldier);
                // The modelled tank in daylight, front three-quarter and from behind above.
                var spot=post+Vector3.forward*7;var shown=Object.Instantiate(chapter.Layout.tankArt,spot,Quaternion.Euler(0,150,0));
                yield return Shot("tank-day-3q.png",spot+new Vector3(5.5f,3f,-6f),spot+Vector3.up*1.3f,45);
                yield return Shot("tank-day-rear.png",spot+new Vector3(-4.5f,5f,6.5f),spot+Vector3.up*1.1f,45);Object.Destroy(shown);
                // Burnt out at the head of the dock road at dawn: gun down, commander's hatch thrown open.
                Load("DAWN");Call("TickLight",10f);var road=chapter.Sites["tankDock"].position;
                Assert.AreEqual("destroyed",chapter.TankStage);Assert.AreEqual(75,chapter.CommanderHatchOpen,1,"Burnt out: the commander's hatch is thrown open.");
                yield return Shot("tank-burnt-dawn.png",road+new Vector3(6.5f,3.2f,-5f),road+Vector3.up*1.2f,45);
            }
            finally{rig.ClearCinematicView();chapter.Cancel();chapter.enabled=true;}
        }
    }
}
