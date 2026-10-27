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
    public sealed class Map02NightTwoRouteTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(240000)]public IEnumerator PatrolCarriesApprovedTorchAndFiresOnlyThroughClearSight()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.enabled=false;
            try
            {
                Load(m,"D2_07");Place(m,c.Sites["command"].position);yield return null;
                Assert.Less(Vector3.ProjectOnPlane(c.NightPatrolPosition-c.Layout.nightTwoPatrolCenter,Vector3.up).magnitude,.5f,"Patrol belongs in the yellow area of the latest marked map.");
                Assert.AreEqual(new Vector3(29,.77f,-16.66f).x,c.Sites["scout"].position.x,.1f,"The Day 2 scout site stays separate from the night hatch.");
                AssertBankApproach(c);
                foreach(var e in c.Enemies)e.enabled=false;
                yield return new WaitForEndOfFrame();
                Assert.AreEqual(3,c.Enemies.Count(e=>e.group==Map02ChapterDirector.NightPatrolGroup));
                for(int i=0;i<3;i++)
                {
                    var e=Patrol(c,i);var prop=e.GetComponent<Map02PatrolFlashlight>();Assert.NotNull(prop);Assert.NotNull(prop.Emitter);
                    Assert.AreEqual(1,e.GetComponentsInChildren<Map02PatrolFlashlight>().Length);
                    Assert.AreEqual(0,prop.Emitter.parent.GetComponentsInChildren<Collider>().Length,"The held torch must not block Nam or its own light ray.");
                    Assert.Less(Vector3.Distance(prop.Emitter.position,e.transform.position+Vector3.up*1.7f),.8f);
                }
                c.SyncNightPatrolPresentation();
                CapturePatrol(Patrol(c,0));
                Assert.Less(Vector3.Distance(c.NightPatrolLight.transform.position,Patrol(c,0).GetComponent<Map02PatrolFlashlight>().Emitter.position),.05f,"The light must begin at the lens.");
                Assert.Less(Vector3.Angle(c.NightPatrolLight.transform.forward,Patrol(c,0).GetComponent<Map02PatrolFlashlight>().Emitter.forward),.1f);
                var guard=Patrol(c,0);var agent=guard.GetComponent<NavMeshAgent>();agent.isStopped=true;
                guard.transform.rotation=Quaternion.identity;Place(m,guard.transform.position+Vector3.forward*2);
                var health=m.Player.GetComponent<ShadowVale.Gameplay.Combat.Health>();float hp=health.Current;
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=guard.transform.position+Vector3.forward+Vector3.up;wall.transform.localScale=new Vector3(4,4,.2f);Physics.SyncTransforms();
                CallEnemy(guard,"Update");Assert.IsFalse(guard.Alerted,"A wall must block front detection.");Assert.AreEqual(hp,health.Current);
                Object.DestroyImmediate(wall);guard.transform.rotation=Quaternion.identity;Physics.SyncTransforms();CallEnemy(guard,"Update");
                Assert.IsTrue(guard.Alerted,"A guard must attack Nam standing openly in front, even before N2_02.");Assert.Less(health.Current,hp);
                yield return new WaitForEndOfFrame();Assert.IsTrue(guard.GetComponent<Map02PatrolFlashlight>().Emitter.gameObject.activeInHierarchy);
                hp=health.Current;wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=guard.transform.position+Vector3.forward+Vector3.up;wall.transform.localScale=new Vector3(4,4,.2f);Physics.SyncTransforms();
                typeof(Map02ChapterEnemy).GetField("shot",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(guard,0f);CallEnemy(guard,"Update");Assert.AreEqual(hp,health.Current,"An alerted guard must never shoot through cover.");Object.DestroyImmediate(wall);
                Place(m,c.Sites["command"].position);m.Restore(Map02Mission.Parse(JsonUtility.ToJson(m.Capture())));yield return null;
                Assert.AreEqual(3,Object.FindObjectsByType<Map02PatrolFlashlight>(FindObjectsSortMode.None).Length,"Loading must replace old torch instances.");
            }
            finally{c.Cancel();c.enabled=true;}
            yield return new ExitPlayMode();
        }
        static Map02ChapterEnemy Patrol(Map02ChapterDirector c,int i)=>c.Enemies.Single(e=>e.id==Map02ChapterDirector.NightPatrolGroup+"_"+i);
        static void CallEnemy(Map02ChapterEnemy enemy,string method)=>typeof(Map02ChapterEnemy).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(enemy,null);
        static void AssertBankApproach(Map02ChapterDirector c)
        {
            var path=new NavMeshPath();Assert.IsTrue(NavMesh.CalculatePath(c.Sites["command"].position,c.NightObservationSpawn,NavMesh.AllAreas,path));Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);
            Assert.IsTrue(c.NightBankPositionAllowed(c.NightDoorStand),"Dry ground at the hatch stays walkable; the mission gate controls entry.");
            foreach(var p in c.NightBankRoute)Assert.IsTrue(c.Enemies.Any(e=>e.group==Map02ChapterDirector.NightPatrolGroup&&Vector3.Distance(e.transform.position,p)<40),"Bank route must be under patrol pressure.");
            var guards=c.Enemies.Where(e=>e.group==Map02ChapterDirector.NightPatrolGroup).OrderBy(e=>e.id).ToArray();
            Assert.AreEqual(10,Vector3.ProjectOnPlane(guards[1].transform.position-guards[0].transform.position,Vector3.up).magnitude,.1f);
            Assert.AreEqual(10,Vector3.ProjectOnPlane(guards[2].transform.position-guards[1].transform.position,Vector3.up).magnitude,.1f);
            Assert.Less(Mathf.Abs(Vector3.Cross(guards[1].transform.position-guards[0].transform.position,guards[2].transform.position-guards[0].transform.position).y), .1f);
        }
        static void CapturePatrol(Map02ChapterEnemy unit)
        {
            var go=new GameObject("Patrol flashlight QA camera");var camera=go.AddComponent<Camera>();camera.transform.position=unit.transform.position+unit.transform.forward*3-unit.transform.right*1.8f+Vector3.up*1.65f;camera.transform.LookAt(unit.transform.position+Vector3.up*1.3f);camera.fieldOfView=38;
            var rt=new RenderTexture(1000,900,24);var image=new Texture2D(1000,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1000,900),0,0);image.Apply();Directory.CreateDirectory("Logs/Map02Chapter");File.WriteAllBytes("Logs/Map02Chapter/night2-held-flashlight-runtime.png",image.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
        [Test]public void VersionThreeDefaultsKeepCompletedTasksAndResetTheWholeNightRoute()
        {
            var state=Through("N2_03");state.night2GuidanceStage=0;state.night2RouteVisited=null;state.Validate();Assert.IsTrue(state.Done("N2_03"));Assert.AreEqual(1,state.night2GuidanceStage);Assert.NotNull(state.night2RouteVisited);
            state.night2ChainCuePlayed=true;state.night2GuidanceStage=7;state.night2RouteVisited.Add("ditchApproach");state.patrolWarnings=1;state.patrolFocusSeconds=2;state.patrolFocusDirection=Vector3.right;state.ResetPatrolRoute();
            Assert.IsFalse(state.Done("N2_03"));Assert.AreEqual(0,state.night2RouteVisited.Count);Assert.IsFalse(state.night2ChainCuePlayed);Assert.AreEqual(0,state.patrolFocusSeconds);Assert.AreEqual(1,state.night2GuidanceStage);state.Validate();
            state.night2GuidanceStage=8;Assert.Throws<InvalidDataException>(state.Validate);
        }
        [UnityTest,Timeout(240000)]public IEnumerator NightCanBeCompletedWithoutCombatUsingRealDoorAndOrderedRoute()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.enabled=false;
            try
            {
                Load(m,"N2_01");Assert.AreEqual(3,c.NightPatrolLights.Count(l=>l!=null&&l.enabled));
                var hung=c.Actor("Hùng").transform.position;Place(m,Marker("N2_02").transform.position);Assert.IsTrue(m.Interact(Marker("N2_02")));Assert.IsTrue(c.State.Done("N2_02"));Assert.IsFalse(c.Busy||m.InputBlocked);Assert.IsTrue(m.CanSave());
                Call(c,"TickNightRadio",20f,70f,false,12f);Assert.AreEqual(1,c.State.night2GuidanceStage,"A line saying the light approaches must wait for the real turn.");
                Call(c,"TickNightRadio",0f,20f,true,12f);Assert.AreEqual(2,c.State.night2GuidanceStage);
                Call(c,"TickNightRadio",20f,5f,false,12f);Assert.AreEqual(2,c.State.night2GuidanceStage,"Do not announce 'qua rồi' while the light still faces Nam.");
                Call(c,"TickNightRadio",0f,60f,false,12f);Assert.AreEqual(3,c.State.night2GuidanceStage);
                Call(c,"TickNightRadio",20f,60f,false,12f);Call(c,"TickNightRadio",20f,60f,false,12f);Call(c,"TickNightRadio",20f,60f,false,12f);
                Assert.IsTrue(c.State.night2ChainCuePlayed);Assert.AreEqual(6,c.State.night2GuidanceStage);
                var sound=(AudioSource)Field(c,"chainSound");Assert.NotNull(sound.clip);Assert.Greater(sound.clip.length,3);Assert.IsTrue(sound.isPlaying);
                m.SetPaused(true);Call(c,"Update");Assert.IsFalse(sound.isPlaying,"The chain cue must pause with the game.");m.SetPaused(false);Call(c,"Update");Assert.IsTrue(sound.isPlaying);
                var save=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(save);yield return null;Assert.IsFalse(sound.isPlaying,"Loading after the cue must not replay it.");
                Call(c,"TickNightRadio",0f,60f,false,70f);Assert.AreEqual(7,c.State.night2GuidanceStage);Assert.IsFalse(c.Busy);
                Assert.Less(Vector3.Distance(hung,c.Actor("Hùng").transform.position),.01f,"Hùng remains at the radio station.");

                Place(m,c.NightDoorStand);Call(c,"Update");Assert.IsFalse(c.State.Done("N2_03"),"Arriving at the store alone cannot inspect the door.");
                var camera=Object.FindObjectsByType<ShadowVale.Gameplay.Player.ThirdPersonCamera>(FindObjectsSortMode.None).First(r=>r.GetComponent<Camera>().enabled);camera.enabled=false;
                Assert.IsTrue(c.NightBankMovementRestricted);Assert.IsTrue(c.NightBankPositionAllowed(c.Sites["command"].position),"Only the river is blocked, not village roads.");
                Assert.IsFalse(m.Interact(Marker("N2_03")),"Surface arrival cannot inspect underground supplies.");
                var store=c.UndergroundStore;Assert.IsTrue(store.TryDoorInteraction());Assert.IsFalse(c.State.Done("N2_03"));yield return WaitGameSeconds(1.3f);
                Place(m,store.TopStand);m.Player.GetComponent<CharacterController>().Move(Vector3.down*.12f);Assert.IsTrue(store.TryEnterLadder());
                for(int i=0;i<90&&store.IsClimbing;i++)store.StepTraversal(.1f,true,false);
                Assert.IsTrue(store.Inside);Place(m,store.InspectionPoint);c.UpdateMarkers();Assert.IsTrue(m.Interact(Marker("N2_03")));Assert.IsTrue(c.State.Done("N2_03"));Assert.IsTrue(c.State.night2ChainCuePlayed);Assert.IsFalse(c.NightBankMovementRestricted);
                CaptureDoor(c);
                Place(m,store.BottomStand);m.Player.GetComponent<CharacterController>().Move(Vector3.down*.12f);Assert.IsTrue(store.TryEnterLadder());for(int i=0;i<90&&store.IsClimbing;i++)store.StepTraversal(.1f,false,true);Assert.IsFalse(store.Inside);
                Assert.AreEqual(4,c.NightTwoRoute.Length);Survey(c);
                Place(m,c.Sites["command"].position);WalkEvacuationRoute(m);
                Place(m,c.Sites["dock"].position);Call(c,"TickNightTwoRoute",0f);Assert.IsFalse(c.State.Done("N2_04"));Assert.AreEqual(0,c.NightTwoRouteProgress);
                Place(m,c.NightTwoRoute[1]);Call(c,"TickNightTwoRoute",0f);Assert.AreEqual(0,c.NightTwoRouteProgress,"Visiting out of order must not advance.");
                for(int i=0;i<3;i++)
                {
                    Place(m,c.NightTwoRoute[i]);Call(c,"TickNightTwoRoute",0f);Assert.AreEqual(i+1,c.NightTwoRouteProgress);
                    Assert.Less(Vector3.Distance(Marker("N2_04").transform.position,c.NightTwoRoute[i+1]),.05f);
                    m.Restore(Map02Mission.Parse(JsonUtility.ToJson(m.Capture())));yield return null;Assert.AreEqual(i+1,c.NightTwoRouteProgress);
                }
                Place(m,c.NightTwoRoute[3]);Call(c,"TickNightTwoRoute",0f);Assert.IsFalse(c.State.Done("N2_04"));Assert.IsTrue(m.Interact(Marker("N2_04")));Assert.IsTrue(c.State.Done("N2_04"));Assert.AreEqual(4,c.NightTwoRouteProgress);
                // No warning or kill is required for any of these inspections.
                Assert.AreEqual(0,c.State.patrolWarnings);Assert.AreEqual(3,c.Enemies.Count(e=>e.group==Map02ChapterDirector.NightPatrolGroup&&!e.Dead));
                Place(m,Marker("N2_05").transform.position);Call(c,"TickNightRadio",20f,90f,false,90f);Assert.IsTrue(m.Interact(Marker("N2_05")));Assert.AreEqual("N2_05",c.CurrentScene?.id);Call(c,"EndScene");Assert.AreEqual(4,c.State.phase);
            }
            finally{m.SetPaused(false);c.Cancel();c.enabled=true;}
            yield return new ExitPlayMode();
        }
        static void Survey(Map02ChapterDirector c){Directory.CreateDirectory("Logs/Map02Chapter");File.WriteAllText("Logs/Map02Chapter/night2-route-survey.txt",string.Join("\n",c.NightTwoRoute.Select(p=>p.ToString("F2"))));}
        static void CaptureDoor(Map02ChapterDirector c)
        {
            var go=new GameObject("Night door QA camera");var camera=go.AddComponent<Camera>();camera.transform.position=c.NightDoorPosition+new Vector3(2,2.5f,-3);camera.transform.LookAt(c.NightDoorPosition);camera.fieldOfView=45;var rt=new RenderTexture(800,600,24);var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(800,600,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,800,600),0,0);texture.Apply();Directory.CreateDirectory("Logs/Map02Chapter");File.WriteAllBytes("Logs/Map02Chapter/night2-door.png",texture.EncodeToPNG());Object.DestroyImmediate(texture);}
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
        static void WalkEvacuationRoute(Map02Mission m)
        {
            var c=m.Chapter;var previous=c.NightDoorStand;
            foreach(var point in c.NightTwoRoute){var path=new NavMeshPath();Assert.IsTrue(NavMesh.CalculatePath(previous,point,NavMesh.AllAreas,path));Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);Map02DayTwoRouteTests.WalkRoute(m,path.corners,"night2 evacuation");previous=point;}
        }
        static object Field(Map02ChapterDirector c,string field)=>typeof(Map02ChapterDirector).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);
        static object Call(Map02ChapterDirector c,string method,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
        static Map02ObjectiveObject Marker(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
        static Map02ChapterState Through(string id){var state=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){Assert.IsTrue(state.Complete(t.id),t.id);if(t.id==id)break;}return state;}
        static void Load(Map02Mission m,string through){var save=m.Capture();save.chapter=Through(through);save.patrolCheckpoint=null;m.Restore(save);}
        static void Place(Map02Mission m,Vector3 p){var body=m.Player.GetComponent<CharacterController>();body.enabled=false;m.Player.position=p;body.enabled=true;m.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>().RestoreMotion(false);Physics.SyncTransforms();}
    }
}
