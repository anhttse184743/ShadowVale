using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map02ReportAndNightPatrolTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(240000)]public IEnumerator ScoutReportsWithoutMovingCommanderAndNightPatrolIsVisible()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.enabled=false;
            try
            {
                Load(m,"D2_03");var commander=c.Actor("Anh Thành");var anchor=commander.transform.position;
                Place(m,Marker("D2_04").transform.position);Assert.IsTrue(m.Interact(Marker("D2_04")));yield return WaitGameSeconds(.8f);
                Assert.Less(Vector3.Distance(anchor,commander.transform.position),.01f,"Thành must stay at the report position while Nam escorts Tâm.");
                Assert.Less(Vector3.Distance(Marker("D2_04").transform.position,anchor),.1f);
                Place(m,anchor);c.Actor("Tâm").StopAt(anchor+Vector3.right*1.4f);Call(c,"Update");Assert.AreEqual("D2_04",c.CurrentScene?.id);Call(c,"LateUpdate");Call(c,"EndScene");
                Assert.IsTrue(c.State.Done("D2_04"));yield return WaitGameSeconds(.4f);
                AssertScoutAndCommander(c,anchor);
                for(int i=0;i<3;i++){m.Restore(Map02Mission.Parse(JsonUtility.ToJson(m.Capture())));yield return null;AssertScoutAndCommander(c,anchor);}
                Capture("day2-report",c.Sites["command"].position+new Vector3(7,3,-8),c.Sites["command"].position+Vector3.up);

                Load(m,"D2_07");yield return WaitGameSeconds(.3f);
                Assert.AreEqual(3,c.State.phase);Assert.AreEqual(3,c.Enemies.Count(e=>e.group==Map02ChapterDirector.NightPatrolGroup));
                AssertNightRoster(c);Assert.IsTrue(c.NightPatrolLight.enabled);Assert.IsTrue(c.Instruction.Contains("NAM"));
                c.State.patrolScanSeconds=c.Layout.patrolSweepSeconds*.125f;Call(c,"TickPatrol",0f);var left=c.NightPatrolLight.transform.forward;
                c.State.patrolScanSeconds=c.Layout.patrolSweepSeconds*.625f;Call(c,"TickPatrol",0f);var right=c.NightPatrolLight.transform.forward;
                Assert.Greater(Vector3.Angle(left,right),60,"The rendered flashlight must visibly sweep both sides.");
                Assert.IsTrue(c.NightPatrolLight.GetComponentsInChildren<MeshRenderer>().Length==0,"Beam is owned separately for phase visibility.");
                var beam=GameObject.Find("Luồng đèn nhìn thấy được");Assert.NotNull(beam);Assert.Greater(beam.GetComponent<MeshFilter>().sharedMesh.vertexCount,10);
                var walker=NightWalker(c);var before=walker.transform.position;
                for(int i=0;i<50;i++){Call(c,"TickPatrol",.1f);yield return null;}
                Assert.Greater(Vector3.Distance(before,walker.transform.position),.2f,"The night patrol must actually move.");
                var viewRig=Object.FindObjectsByType<ShadowVale.Gameplay.Player.ThirdPersonCamera>(FindObjectsSortMode.None).First(r=>r.GetComponent<Camera>().enabled);viewRig.enabled=false;var camera=viewRig.GetComponent<Camera>();
                Place(m,c.Sites["bridge"].position);c.HoldBinoculars(true);camera.transform.position=m.Player.position+new Vector3(0,2,3);camera.transform.LookAt(c.NightPatrolPosition+Vector3.up);
                Assert.Less(Vector3.Angle(camera.transform.forward,c.NightPatrolPosition+Vector3.up-camera.transform.position),1);
                var target=c.NightPatrolPosition+Vector3.up;var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=Vector3.Lerp(camera.transform.position,target,.5f);wall.transform.rotation=Quaternion.LookRotation(target-camera.transform.position);wall.transform.localScale=new Vector3(20,15,.5f);Physics.SyncTransforms();
                for(int i=0;i<30;i++)Call(c,"Update");Assert.AreEqual(0,(float)typeof(Map02ChapterDirector).GetField("observed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c),.001f,"A wall must reset observation progress.");Assert.IsFalse(c.State.Done("N2_01"),"A wall must prevent observing the patrol.");Object.DestroyImmediate(wall);Physics.SyncTransforms();
                // Real observation is advanced by actual frames with the camera facing the visible guard.
                c.enabled=true;float end=Time.time+5;
                while(Time.time<end&&!c.State.Done("N2_01")){camera.transform.LookAt(c.NightPatrolPosition+Vector3.up);yield return null;}
                c.enabled=false;Assert.IsTrue(c.State.Done("N2_01"),"Looking at the visible patrol with held binoculars must complete observation.");c.HoldBinoculars(false);
                var snapshot=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(snapshot);yield return null;AssertNightRoster(c);
                Capture("night2-patrol",c.Sites["bridge"].position+new Vector3(0,2,3),c.NightPatrolPosition+Vector3.up);
                Load(m,"N2_05");Assert.IsFalse(c.NightPatrolLight.enabled);Assert.IsFalse(GameObject.Find("Luồng đèn nhìn thấy được")!=null);
            }
            finally{c.Cancel();c.enabled=true;}
            yield return new ExitPlayMode();
        }
        static void AssertScoutAndCommander(Map02ChapterDirector c,Vector3 anchor)
        {
            Assert.AreEqual(1,Object.FindObjectsByType<Map02ChapterActor>(FindObjectsSortMode.None).Count(a=>a.role=="Tâm"));
            Assert.AreEqual(1,c.Actor("Tâm").GetComponentsInChildren<Renderer>().Count(r=>r.enabled&&r.gameObject.activeInHierarchy),"Only the standing/crouched Tâm mesh may render after escort.");
            Assert.IsFalse(c.Actor("Tâm").GetComponent<Map02ScoutInjuredVisual>()?.Injured??false);
            Assert.Greater(Vector3.Distance(c.Actor("Tâm").transform.position,c.Actor("Anh Lực").transform.position),8);
            Assert.Less(Vector3.Distance(anchor,c.Actor("Anh Thành").transform.position),.01f);
        }
        static void AssertNightRoster(Map02ChapterDirector c){Assert.AreEqual(3,c.Enemies.Count(e=>e.group==Map02ChapterDirector.NightPatrolGroup&&e.gameObject.activeInHierarchy));foreach(var e in c.Enemies.Where(e=>e.group==Map02ChapterDirector.NightPatrolGroup))Assert.NotNull(e.GetComponentInChildren<Animator>().runtimeAnimatorController);}
        static Map02ChapterEnemy NightWalker(Map02ChapterDirector c)=>c.Enemies.Single(e=>e.id==Map02ChapterDirector.NightPatrolGroup+"_1");
        static Map02ObjectiveObject Marker(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
        static object Call(Map02ChapterDirector c,string method,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
        static void Load(Map02Mission m,string through){var save=m.Capture();save.chapter=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){Assert.IsTrue(save.chapter.Complete(t.id),t.id);if(t.id==through)break;}save.patrolCheckpoint=null;m.Restore(save);}
        static void Place(Map02Mission m,Vector3 p){var body=m.Player.GetComponent<CharacterController>();body.enabled=false;m.Player.position=p;body.enabled=true;m.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>().RestoreMotion(false);Physics.SyncTransforms();}
        static void Capture(string name,Vector3 eye,Vector3 target)
        {
            var go=new GameObject("Report and patrol QA");var camera=go.AddComponent<Camera>();camera.transform.position=eye;camera.transform.LookAt(target);camera.fieldOfView=55;var rt=new RenderTexture(1000,800,24);var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1000,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1000,800),0,0);texture.Apply();Directory.CreateDirectory("Logs/Map02Chapter");File.WriteAllBytes("Logs/Map02Chapter/"+name+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);}
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
    }
}
