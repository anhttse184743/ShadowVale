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
        static void Frame(float dt=.1f){Call("TickScene",dt);Call("TickTank",dt,false);Call("TickEffects");}
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
                for(int i=0;i<40&&chapter.EffectRed==0;i++)Frame();
                Assert.Greater(chapter.EffectRed,0,"Red, blurred view after the hit.");Assert.Less(chapter.ListenerCutoff,2000,"Sound sinks.");Assert.Greater(rig.ShakeAmplitude,0);
                Call("EndScene");Call("TickEffects");Assert.Greater(chapter.EffectRed,0,"Still hurt while crawling to cover.");
                Load("N3_CRAWL");Call("TickEffects");Assert.AreEqual(0,chapter.EffectRed);Assert.AreEqual(22000,chapter.ListenerCutoff);
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
                // D3_02: the crate is fetched from the closed store and carried to the bank.
                Load("N2_05");Assert.AreEqual(4,chapter.State.phase);var crate=Mark("D3_02");
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
                Call("TickEffects");Assert.AreEqual(1,chapter.EffectBlack);Assert.IsTrue(chapter.WaitingForLine);
                Until("N3_00",1.5f);Call("TickDressing",.1f);Assert.IsFalse(chapter.WaitingForLine);Assert.Greater(chapter.FlareLight.intensity,0);Assert.IsTrue(chapter.HouseFire.enabled);
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
    }
}
