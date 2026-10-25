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
            Assert.Less(Vector3.Angle(cam.transform.forward,boat-cam.transform.position),15,"The camera has whipped to the boat.");
            Assert.Less(cam.GetComponent<Camera>().fieldOfView,35,"On a long lens.");
            Assert.Less(Vector3.Distance(cam.transform.position,tank.position),8,"From behind the gun.");
        }
    }
}
