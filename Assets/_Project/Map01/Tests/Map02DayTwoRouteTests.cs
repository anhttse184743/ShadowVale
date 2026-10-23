using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map02DayTwoRouteTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(240000)] public IEnumerator DayTwoRoutesRescueWireAndAntennaArePlayable()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.enabled=false;
            try
            {
                Load(m,"D2_01");
                Survey(c);
                Assert.Greater(c.DayTwoOpenPath.Length,2,"Open field approach must be complete.");Assert.Greater(c.DayTwoCoveredPath.Length,2,"Bamboo approach must be complete.");
                AssertDistinctRoutes(c);
                AssertRoute(c.DayTwoOpenPath);AssertRoute(c.DayTwoCoveredPath);
                WalkRoute(m,c.DayTwoOpenPath,"open");WalkRoute(m,c.DayTwoCoveredPath,"covered");
                WalkRoute(m,c.DayTwoOpenPath.Reverse().ToArray(),"open return");WalkRoute(m,c.DayTwoCoveredPath.Reverse().ToArray(),"covered return");
                Place(m,c.Sites["command"].position);
                Call(c,"EnsureGroup","scout_patrol",3,false);
                Assert.AreEqual(3,c.Enemies.Count(e=>e.group=="scout_patrol"));
                Call(c,"EnsureGroup","scout_patrol",3,false);Assert.AreEqual(3,c.Enemies.Count(e=>e.group=="scout_patrol"));
                // Both a living patrol and its saved corpses permit finding Tâm; no respawn.
                var patrolSave=JsonUtility.ToJson(m.Capture());CheckRescueWithPatrol(m,patrolSave,true);CheckRescueWithPatrol(m,patrolSave,false);
                Assert.IsTrue(c.State.Done("D2_03"));Assert.IsFalse(c.State.combatGroups.Contains("D2_03"));
                Place(m,Marker("D2_04").transform.position);Assert.IsTrue(m.Interact(Marker("D2_04")));Assert.NotNull(c.Companion);
                Assert.Less(Vector3.Distance(Marker("D2_04").transform.position,c.Sites["command"].position),.1f);
                var saved=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(saved);Assert.NotNull(c.Companion,"Save/load must resume following.");
                Assert.IsFalse(m.CharacterGearLocked);Assert.IsTrue(m.CharacterCanSprint);
                // This test drives the actual destination check, without waiting for a 100m escort.
                Place(m,c.Sites["command"].position);c.Actor("Tâm").StopAt(c.Sites["command"].position+Vector3.right);
                Call(c,"Update");Assert.AreEqual("D2_04",c.CurrentScene?.id);Call(c,"EndScene");Assert.IsTrue(c.State.Done("D2_04"));
                Place(m,Marker("D2_05").transform.position);Assert.IsTrue(m.Interact(Marker("D2_05")));Assert.IsFalse(c.State.Done("D2_05"));
                saved=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(saved);Assert.AreEqual(1,c.State.wireStage);
                Assert.AreEqual("D2_05",c.State.carry);Place(m,Marker("D2_05").transform.position);Assert.IsTrue(m.Interact(Marker("D2_05")));Assert.IsTrue(c.State.Done("D2_05"));
                StartAntenna(m);Assert.IsFalse(c.MaySave);var foot=m.Player.position;
                Step(c,1,true);Assert.Greater(m.Player.position.y,foot.y+.9f);var stopped=m.Player.position;Step(c,.5f);Assert.Less(Vector3.Distance(stopped,m.Player.position),.001f,"Releasing W must stop climbing.");
                Step(c,.1f,cancel:true);for(int i=0;i<20&&c.Busy;i++)Step(c,.1f);Assert.IsFalse(c.Busy);Assert.IsFalse(c.State.Done("D2_06"));Assert.Less(Vector3.Distance(m.Player.position,foot),.01f);
                StartAntenna(m);for(int i=0;i<50;i++)Step(c,.1f,true);
                Assert.AreEqual(c.Layout.antennaHeight,c.AntennaClimbHeight,.01f);
                CaptureAntenna(m);
                for(int i=0;i<50;i++)Step(c,.1f,tie:true);Assert.IsFalse(c.AntennaTied,"Wrong direction must not accept tying.");
                for(int i=0;i<26;i++)Step(c,.1f,right:true);
                for(int i=0;i<21;i++)Step(c,.1f);
                Assert.IsFalse(c.State.Done("D2_06"),"Stable signal alone must not finish.");
                for(int i=0;i<16;i++)Step(c,.1f,tie:true);Assert.IsTrue(c.AntennaTied);Assert.IsFalse(c.State.Done("D2_06"));
                for(int i=0;i<40&&c.CurrentScene==null;i++)Step(c,.1f,down:true);
                Assert.Less(Vector3.Distance(m.Player.position,foot),.01f);Assert.AreEqual("D2_06",c.CurrentScene?.id,"Radio reply starts only once Nam is on the ground.");Call(c,"EndScene");
                Assert.IsTrue(c.State.Done("D2_06"));Assert.IsTrue(c.State.Available(Map02ChapterDefinition.Load().Find("D2_07")));
                saved=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));Assert.AreEqual(3,saved.chapter.version);
            }
            finally{c.Cancel();c.enabled=true;}
            yield return new ExitPlayMode();
        }
        static void Survey(Map02ChapterDirector c)
        {
            Directory.CreateDirectory("Logs/Map02Chapter");File.WriteAllText("Logs/Map02Chapter/day2-route-survey.txt","Open\n"+string.Join("\n",c.DayTwoOpenPath.Select(p=>p.ToString("F2")))+"\nCovered\n"+string.Join("\n",c.DayTwoCoveredPath.Select(p=>p.ToString("F2")))+"\nBridge "+c.Sites["bridge"].position+"\nScout "+c.Sites["scout"].position+"\nAntenna "+c.AntennaBase.position);
        }
        static void AssertRoute(Vector3[] points){for(int i=1;i<points.Length;i++){var path=new NavMeshPath();Assert.IsTrue(NavMesh.CalculatePath(points[i-1],points[i],NavMesh.AllAreas,path));Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);}}
        static void AssertDistinctRoutes(Map02ChapterDirector c)=>Assert.IsTrue(c.DayTwoCoveredPath.Any(p=>c.DayTwoOpenPath.All(q=>Vector3.Distance(p,q)>10)),"Routes must really diverge.");
        static void CheckRescueWithPatrol(Map02Mission m,string json,bool defeated)
        {
            var save=Map02Mission.Parse(json);if(defeated)foreach(var enemy in save.chapter.enemies)enemy.health=0;
            Assert.IsTrue(save.chapter.Complete("D2_02"));m.Restore(save);var c=m.Chapter;
            Call(c,"EnsureGroup","scout_patrol",3,false);Assert.AreEqual(3,c.Enemies.Count);Assert.AreEqual(defeated?0:3,c.Enemies.Count(e=>!e.Dead));
            Place(m,Marker("D2_03").transform.position);Assert.IsTrue(m.Interact(Marker("D2_03")));Call(c,"EndScene");Assert.IsTrue(c.State.Done("D2_03"));
        }
        internal static void WalkRoute(Map02Mission m,Vector3[] points,string name)
        {
            var probe=new GameObject("Day 2 route collision probe");probe.layer=m.Player.gameObject.layer;var original=m.Player.GetComponent<CharacterController>();var body=probe.AddComponent<CharacterController>();body.height=original.height;body.radius=original.radius;body.center=original.center;body.stepOffset=original.stepOffset;
            probe.transform.position=points[0]+Vector3.up*.1f;Physics.SyncTransforms();
            try
            {
                for(int i=1;i<points.Length;i++)
                {
                    int budget=Mathf.CeilToInt(Vector3.Distance(probe.transform.position,points[i])/.12f)+100;
                    while(budget-->0)
                    {
                        var delta=Vector3.ProjectOnPlane(points[i]-probe.transform.position,Vector3.up);if(delta.magnitude<.3f)break;
                        body.Move(Vector3.ClampMagnitude(delta,.12f)+Vector3.down*.04f);
                    }
                    Assert.Less(Vector3.ProjectOnPlane(points[i]-probe.transform.position,Vector3.up).magnitude,.6f,name+" blocked before corner "+i+" at "+probe.transform.position);
                    Assert.Less(Mathf.Abs(points[i].y-probe.transform.position.y),.85f,name+" must remain above the walkable ground.");
                }
            }
            finally{Object.DestroyImmediate(probe);}
        }
        static void CaptureAntenna(Map02Mission m)
        {
            Call(m.Chapter,"SampleAntennaTraversal");
            var go=new GameObject("Antenna QA camera");var camera=go.AddComponent<Camera>();camera.transform.position=m.Player.position+new Vector3(5,2,-7);camera.transform.LookAt(m.Player.position+Vector3.up);camera.fieldOfView=55;camera.farClipPlane=180;
            var rt=new RenderTexture(1000,800,24);var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes("Logs/Map02Chapter/day2-antenna.png",image.EncodeToPNG());Object.DestroyImmediate(image);}
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
        static void StartAntenna(Map02Mission m){var c=m.Chapter;Place(m,Marker("D2_06").transform.position);Assert.IsTrue(m.Interact(Marker("D2_06")));if(c.ConversationActive)Call(c,"EndScene");Assert.IsTrue(c.Busy);}
        static Map02ObjectiveObject Marker(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
        static object Call(Map02ChapterDirector c,string method,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
        static void Step(Map02ChapterDirector c,float dt,bool up=false,bool down=false,bool left=false,bool right=false,bool tie=false,bool cancel=false)=>Call(c,"StepAntennaTraversal",dt,up,down,left,right,tie,cancel);
        static void Load(Map02Mission m,string through){var save=m.Capture();save.chapter=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){Assert.IsTrue(save.chapter.Complete(t.id),t.id);if(t.id==through)break;}save.patrolCheckpoint=null;m.Restore(save);}
        static void Place(Map02Mission m,Vector3 p){var body=m.Player.GetComponent<CharacterController>();body.enabled=false;m.Player.position=p;body.enabled=true;Physics.SyncTransforms();}
    }
}
