using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>Day 3 / Night 3 staging: people in a talk hold their marks and face the speaker, each line is shot over the
    /// listener's shoulder; N3_07 opens on the tank's gun swinging onto the last boat before anyone speaks.</summary>
    public sealed class Map02TalkStagingTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(300000)]public IEnumerator TalkersHoldTheirMarksFaceTheSpeakerAndEachLineHasItsShot()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return TalkInPlayMode();
            yield return new ExitPlayMode();
        }
        [UnityTest,Timeout(300000)]public IEnumerator TheFarewellOpensOnTheTankGunSwingingOntoTheBoat()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return BoatInPlayMode();
            yield return new ExitPlayMode();
        }
        [UnityTest,Timeout(300000)]public IEnumerator AfterTheHitNamKeepsLowAndMakesHisOwnWayToAnhLucsCover()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return CrouchInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator CrouchInPlayMode()
        {
            Map02Mission m=null;yield return Ready(x=>m=x);var c=m.Chapter;
            yield return Before(m,"N3_CRAWL");
            Assert.IsFalse(c.MovementLocked,"He walks there himself, no scripted slide.");
            var walker=m.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>();
            float wait=Time.time+.5f;while(Time.time<wait)yield return null;
            Assert.IsTrue(walker.IsSneaking,"Wounded, he keeps low: the crouch.");
            var mark=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(o=>o.chapterId=="N3_CRAWL").transform.position;
            var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=mark+Vector3.back*.8f;cc.enabled=true;Physics.SyncTransforms();
            wait=Time.time+3;while(Time.time<wait&&c.CurrentScene?.id!="N3_CRAWL")yield return null;
            Assert.AreEqual("N3_CRAWL",c.CurrentScene?.id,"Reaching anh Lực's cover plays \"...Còn sống.\"");
        }
        [UnityTest,Timeout(300000)]public IEnumerator WoundedNamRunsALittleSlowerAndBleedsUntilBandaged()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return BleedInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator BleedInPlayMode()
        {
            Map02Mission m=null;yield return Ready(x=>m=x);var c=m.Chapter;
            yield return Before(m,"N3_06");var hp=m.Player.GetComponent<Health>();hp.CinematicInvulnerable=false;hp.RestoreHealth(90);
            Assert.IsTrue(c.Bleeding,"The shoulder bleeds after the hit.");Assert.IsTrue(m.CharacterCanSprint,"Wounded, he can still run.");
            float before=hp.Current,from=Time.time;while(Time.time<from+2)yield return null;
            Assert.AreEqual(before-(Time.time-from)*Map02ChapterDirector.BleedPerSecond,hp.Current,1.2f,"2 HP a second.");
            m.Bag.Add("medkit_small",2);yield return null;Assert.IsTrue(m.Bag.UseItem("medkit_small"),"A medkit dresses it.");yield return null;
            Assert.IsFalse(c.Bleeding);before=hp.Current;from=Time.time;while(Time.time<from+1.5f)yield return null;
            Assert.AreEqual(before,hp.Current,.01f,"No more bleeding once bandaged.");
        }
        [UnityTest,Timeout(300000)]public IEnumerator TheTankShellsTheVillageBeforeTheRadioCall()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return WreckInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator WreckInPlayMode()
        {
            Map02Mission m=null;yield return Ready(x=>m=x);var c=m.Chapter;
            yield return Before(m,"N3_06");Begin(c,"N3_06");Assert.AreEqual("N3_06",c.CurrentScene?.id);
            float start=Time.time;while(Time.time<start+1.6f)yield return null;
            Assert.IsTrue(c.Tank.activeSelf,"The tank is in the lanes, shelling.");Assert.IsTrue(c.WaitingForLine,"The radio call waits for the shelling.");
            Assert.GreaterOrEqual(c.WreckFiresBurning,1,"The first house hit burns.");
            while(Time.time<start+7&&c.WaitingForLine)yield return null;
            Assert.AreEqual(3,c.WreckFiresBurning,"Three houses burning.");Assert.AreEqual("m2v3_coordinates_01",c.CurrentLineId);
        }
        const System.Reflection.BindingFlags Flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        static IEnumerator Ready(System.Action<Map02Mission> found)
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);m.Player.GetComponent<Health>().CinematicInvulnerable=true;found(m);
        }
        static IEnumerator Before(Map02Mission m,string task)
        {
            var snap=m.Capture();var s=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id==task)break;s.Complete(t.id);}
            snap.chapter=s;m.Restore(snap);float until=Time.time+1.5f;while(Time.time<until)yield return null;
        }
        static void Begin(Map02ChapterDirector c,string task)=>typeof(Map02ChapterDirector).GetMethod("BeginScene",Flags).Invoke(c,new object[]{Map02ChapterDefinition.Load().Find(task),0,0});
        static string SpeakerOf(string line)=>Map02ChapterDefinition.Load().entries.First(e=>e.id==line).speaker;
        static float Flat(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));

        static IEnumerator TalkInPlayMode()
        {
            Map02Mission m=null;yield return Ready(x=>m=x);var c=m.Chapter;
            yield return Before(m,"D3_04");Begin(c,"D3_04");
            Assert.AreEqual("D3_04",c.CurrentScene?.id);Assert.Greater(c.Talkers.Count,0,"Hùng and ông Sáu are in the talk.");
            var cam=m.GameCamera;var checkedLines=new System.Collections.Generic.HashSet<string>();float until=Time.time+40;
            var marks=new System.Collections.Generic.Dictionary<Map02ChapterActor,Vector3>();
            while(Time.time<until&&c.CurrentScene!=null&&checkedLines.Count<2)
            {
                yield return null;
                if(c.WaitingForLine)continue;
                foreach(var t in c.Talkers)if(!t.Travelling&&!marks.ContainsKey(t))marks[t]=t.transform.position;   // once he stands
                var line=c.CurrentLineId;if(checkedLines.Contains(line))continue;
                float spoken=Time.time;while(Time.time<spoken+.8f&&c.CurrentLineId==line)yield return null;   // let them turn
                if(c.CurrentLineId!=line)continue;
                var speaker=SpeakerOf(line);var who=speaker=="Nam"?m.Player:c.Actor(speaker).transform;
                foreach(var t in c.Talkers.Where(t=>t.transform!=who&&!t.Travelling))   // someone still walking in looks where he goes
                    Assert.Less(Vector3.Angle(Vector3.ProjectOnPlane(t.transform.forward,Vector3.up),Vector3.ProjectOnPlane(who.position-t.transform.position,Vector3.up)),25,t.role+" faces "+speaker+" on "+line);
                if(who!=m.Player)Assert.Less(Vector3.Angle(Vector3.ProjectOnPlane(m.Player.forward,Vector3.up),Vector3.ProjectOnPlane(who.position-m.Player.position,Vector3.up)),25,"Nam faces "+speaker);
                var head=who.position+Vector3.up*1.4f;
                Assert.Less(Vector3.Angle(cam.transform.forward,head-cam.transform.position),15,"The shot of "+line+" is on "+speaker);
                Assert.Greater(Vector3.Distance(cam.transform.position,head),1.2f,"Over the listener's shoulder, not in the speaker's face.");
                foreach(var p in c.Talkers)Assert.Greater(Flat(p.transform.position,m.Player.position),1.1f,p.role+" does not stand on Nam.");
                checkedLines.Add(line);
            }
            Assert.GreaterOrEqual(checkedLines.Count,2,"Two lines were checked.");
            Assert.Greater(marks.Count,0);
            foreach(var pair in marks)Assert.Less(Flat(pair.Key.transform.position,pair.Value),.2f,pair.Key.role+" keeps his mark.");
        }
        static IEnumerator BoatInPlayMode()
        {
            Map02Mission m=null;yield return Ready(x=>m=x);var c=m.Chapter;
            yield return Before(m,"N3_07");Begin(c,"N3_07");
            Assert.AreEqual("N3_07",c.CurrentScene?.id);float start=Time.time;
            while(Time.time<start+3)yield return null;
            Assert.IsTrue(c.WaitingForLine,"Nobody speaks while the tank rolls out.");
            var cam=m.GameCamera;
            while(Time.time<start+20&&c.WaitingForLine)yield return null;
            Assert.IsFalse(c.WaitingForLine);Assert.AreEqual("m2v3_farewell_01",c.CurrentLineId,"Chú Bảy shouts once the boat is in shot.");
            float wait=Time.time+.3f;while(Time.time<wait)yield return null;
            var boat=c.LastBoat.position;var tank=c.Tank.transform;
            var gun=Vector3.ProjectOnPlane(tank.rotation*Quaternion.AngleAxis(c.TurretYaw,Vector3.up)*Vector3.forward,Vector3.up);
            Assert.Less(Vector3.Angle(gun,Vector3.ProjectOnPlane(boat-tank.position,Vector3.up)),15,"The gun is on the boat.");
            Assert.Less(Vector3.Angle(cam.transform.forward,boat-cam.transform.position),15,"The camera has panned to the boat.");
            Assert.Less(Vector3.Distance(cam.transform.position,boat),7.5f,"Close on the boat.");
            Assert.Less(cam.GetComponent<Camera>().fieldOfView,40);
        }
    }
}
