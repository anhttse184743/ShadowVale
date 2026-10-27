using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map02DayTwoStateTests:ForestSceneTestBase
    {
        [Test] public void WireRequiresPickupAndDeliveryAndCannotBeDuplicated()
        {
            var s=Through("D2_04");Assert.IsFalse(s.DeliverWire());Assert.IsTrue(s.TakeWire());
            Assert.IsFalse(s.TakeWire());Assert.IsFalse(s.Done("D2_05"));Assert.AreEqual("D2_05",s.TrackedTask.id);
            s=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(s));s.Validate();
            Assert.AreEqual(1,s.wireStage);Assert.IsTrue(s.DeliverWire());Assert.IsFalse(s.DeliverWire());Assert.IsFalse(s.TakeWire());
            Assert.AreEqual(2,s.wireStage);Assert.IsNull(s.carry);Assert.IsTrue(s.Available(Map02ChapterDefinition.Load().Find("D2_06")));
        }
        [Test] public void OldVersionThreeWireSavesKeepCompletedTasksAndPendingWork()
        {
            var oldJson=System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(Through("D2_05")),"\"wireStage\":2,","");
            var done=JsonUtility.FromJson<Map02ChapterState>(oldJson);done.night2RouteVisited=null;done.Validate();
            Assert.AreEqual(3,done.version);Assert.AreEqual(2,done.wireStage);Assert.IsTrue(done.Done("D2_05"));
            var pending=Through("D2_04");pending.Validate();Assert.IsTrue(pending.TakeWire());
        }
        [Test] public void PatrolResetRewindsOnlyNightRouteAndKeepsChapterResources()
        {
            var s=Through("N2_04");s.patrolWarnings=1;s.patrolScanSeconds=7.5f;s.storedRounds=37;
            s.night2RouteVisited.Add("dockApproach");s.night2ChainCuePlayed=true;
            var load=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(s));load.Validate();
            Assert.AreEqual(7.5f,load.patrolScanSeconds);Assert.IsTrue(load.night2ChainCuePlayed);Assert.AreEqual(s.night2RouteVisited,load.night2RouteVisited);
            load.ResetPatrolRoute();load.Validate();Assert.IsTrue(load.Done("N2_02"));Assert.IsFalse(load.Done("N2_03"));Assert.IsFalse(load.Done("N2_04"));
            Assert.AreEqual(0,load.patrolWarnings);Assert.AreEqual(0,load.patrolScanSeconds);Assert.AreEqual(37,load.storedRounds);
            Assert.IsEmpty(load.night2RouteVisited);Assert.IsFalse(load.night2ChainCuePlayed);Assert.AreEqual("N2_03",load.TrackedTask.id);
        }
        [Test] public void InvalidNewStateIsRejectedWithoutChangingVersionThree()
        {
            var s=Through("D2_04");s.wireStage=1;Assert.Throws<InvalidDataException>(()=>s.Validate());
            s=Through("D2_04");s.patrolScanSeconds=float.NaN;Assert.Throws<InvalidDataException>(()=>s.Validate());
            s=Through("N2_02");s.patrolWarnings=-1;Assert.Throws<InvalidDataException>(()=>s.Validate());
            s=Through("N2_02");s.night2RouteVisited.Add(" ");Assert.Throws<InvalidDataException>(()=>s.Validate());
        }
        [UnityTest,Timeout(240000)] public IEnumerator LiveWireEscortAndPatrolSaveLoadKeepTheirContracts()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.enabled=false;
            Map02ObjectiveObject Mark(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
            void Load(string id){var snapshot=m.Capture();snapshot.chapter=Through(id);snapshot.patrolCheckpoint=null;m.Restore(snapshot);}
            Map02Mission.Snapshot RoundTrip()=>Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));
            try
            {
                Load("D2_04");var wire=Mark("D2_05");Place(m,wire.transform.position);var store=m.Player.position;
                var ammo=m.Player.GetComponent<PlayerCombat>();ammo.RestoreMagazine(7);m.Inventory[m.RifleAmmo]=19;
                Assert.IsTrue(m.Interact(wire));Assert.IsFalse(c.State.Done("D2_05"));Assert.IsFalse(m.Interact(wire),"Pickup cannot also deliver remotely.");
                Assert.IsTrue(m.CanSave());m.Restore(RoundTrip());Assert.AreEqual(1,c.State.wireStage);
                Assert.Less(Vector3.Distance(wire.transform.position,c.Sites["radio"].position),.1f);Assert.Greater(Vector3.Distance(store,wire.transform.position),3);
                Place(m,wire.transform.position);Assert.IsTrue(m.Interact(wire));Assert.IsTrue(c.State.Done("D2_05"));Assert.IsFalse(m.Interact(wire));
                Assert.AreEqual(7,ammo.RoundsInMagazine);Assert.AreEqual(19,m.Inventory[m.RifleAmmo]);

                Load("D2_03");var scout=Mark("D2_04");Place(m,scout.transform.position);Assert.IsTrue(m.Interact(scout));
                Assert.IsTrue(m.CanSave(),m.SaveBlockReason());var snapshot=RoundTrip();m.Restore(snapshot);
                Assert.AreSame(c.Actor("Tâm"),c.Companion);Assert.IsFalse(c.State.Done("D2_04"));
                Assert.AreEqual(1,m.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>().SurfaceSpeedMultiplier);
                var start=c.Actor("Tâm").transform.position;var path=new NavMeshPath();
                Assert.IsTrue(NavMesh.CalculatePath(start,c.Sites["command"].position,NavMesh.AllAreas,path));Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);
                var next=path.corners.Skip(1).First(p=>Vector3.Distance(start,p)>.5f);Place(m,Vector3.MoveTowards(start,next,4));
                float until=Time.time+3;while(Time.time<until&&Vector3.Distance(start,c.Actor("Tâm").transform.position)<.5f)yield return null;
                Assert.Greater(Vector3.Distance(start,c.Actor("Tâm").transform.position),.4f,"Restored Tâm must really resume following.");
                c.Cancel();Assert.IsNull(c.Companion);

                Load("N2_02");m.Inventory[m.RifleAmmo]=23;ammo.RestoreMagazine(6);
                m.Player.GetComponent<Health>().RestoreHealth(61);
                // Recreate the baseline with these resources, as on a natural report completion.
                snapshot=m.Capture();snapshot.patrolCheckpoint=null;m.Restore(snapshot);
                var baseline=m.Capture().patrolCheckpoint;Assert.IsNotEmpty(baseline);
                Assert.IsTrue(c.State.Complete("N2_03"));Assert.IsTrue(c.State.Complete("N2_04"));
                Place(m,c.Sites["dock"].position);c.State.patrolWarnings=1;c.State.patrolScanSeconds=7;
                c.State.night2RouteVisited.Add("dockApproach");c.State.night2ChainCuePlayed=true;m.StoreCheckpoint();
                snapshot=RoundTrip();m.Restore(snapshot);Assert.AreEqual(1,c.State.patrolWarnings);Assert.AreEqual(7,c.State.patrolScanSeconds);
                Assert.AreEqual(baseline,m.Capture().patrolCheckpoint);Assert.IsTrue(m.RetryPatrol());
                Assert.Less(Vector3.Distance(m.Player.position,c.Sites["bridge"].position),.1f);
                Assert.IsFalse(c.State.Done("N2_03"));Assert.IsFalse(c.State.Done("N2_04"));Assert.AreEqual(0,c.State.patrolWarnings);
                Assert.AreEqual(0,c.State.patrolScanSeconds);Assert.IsFalse(c.State.night2ChainCuePlayed);Assert.IsEmpty(c.State.night2RouteVisited);
                Assert.AreEqual(23,m.Inventory[m.RifleAmmo]);Assert.AreEqual(6,ammo.RoundsInMagazine);Assert.AreEqual(61,m.Player.GetComponent<Health>().Current);
                for(int i=0;i<3;i++){m.StoreCheckpoint();m.Restore(RoundTrip());Assert.AreEqual(baseline,m.Capture().patrolCheckpoint);}
                Assert.IsTrue(c.State.Complete("N2_03"));Assert.IsTrue(c.State.Complete("N2_04"));
                snapshot=m.Capture();snapshot.patrolCheckpoint=null;Place(m,c.Sites["dock"].position);m.Restore(snapshot);
                Assert.IsTrue(m.RetryPatrol(),"Old saves without a patrol baseline must migrate.");Assert.IsFalse(c.State.Done("N2_03"));

                snapshot=m.Capture();var corrupt=Map02Mission.Parse(snapshot.patrolCheckpoint);corrupt.chapter.patrolWarnings=1;
                snapshot.patrolCheckpoint=JsonUtility.ToJson(corrupt);Assert.Throws<InvalidDataException>(()=>Map02Mission.Parse(JsonUtility.ToJson(snapshot)));

                // Use unobstructed physics in the air to isolate the actual cone/detection/retry code
                // from authored trees. Only elapsed scan time is controlled; the player collider is real.
                Load("N2_02");var scoutSite=c.Sites["nightPatrol"];var storeSite=c.Sites["store"];
                var originalScout=scoutSite.position;var originalStore=storeSite.position;
                var tick=typeof(Map02ChapterDirector).GetMethod("TickPatrol",BindingFlags.Instance|BindingFlags.NonPublic);
                void Set(string name,object value)=>typeof(Map02ChapterDirector).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,value);
                float period=Mathf.Max(6,c.Layout.patrolSweepSeconds);
                try
                {
                    var exposed=c.Sites["bridge"].position+Vector3.up*100;Place(m,exposed);
                    scoutSite.position=exposed-Vector3.forward*10;storeSite.position=exposed+Vector3.up;Physics.SyncTransforms();
                    Set("patrolLoadGrace",0f);
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=exposed-Vector3.forward*5+Vector3.up;wall.transform.localScale=new Vector3(8,6,.6f);Physics.SyncTransforms();
                    for(int i=0;i<25;i++){c.State.patrolScanSeconds=period*.5f;tick.Invoke(c,new object[]{.1f});}
                    Assert.AreEqual(0,c.State.patrolWarnings,"Opaque cover must block the actual searchlight detection ray.");Object.DestroyImmediate(wall);Physics.SyncTransforms();
                    for(int i=0;i<25&&c.State.patrolWarnings==0;i++){c.State.patrolScanSeconds=period*.5f;tick.Invoke(c,new object[]{.1f});}
                    Assert.AreEqual(1,c.State.patrolWarnings,"First real unobstructed exposure must warn, without retrying.");
                    Assert.Greater(c.State.patrolFocusSeconds,2.9f);float heldScan=c.State.patrolScanSeconds;tick.Invoke(c,new object[]{.5f});Assert.AreEqual(heldScan,c.State.patrolScanSeconds,.001f,"The first warning must pause the sweep to inspect the last sighting.");
                    Assert.Greater(Vector3.Distance(m.Player.position,c.Sites["bridge"].position),50);
                    c.State.Complete("N2_03");c.State.Complete("N2_04");m.StoreCheckpoint();m.Restore(RoundTrip());
                    Assert.Greater(c.State.patrolFocusSeconds,2,"Save/load must preserve the inspection hold.");
                    tick.Invoke(c,new object[]{.1f});Assert.AreEqual(1,c.State.patrolWarnings,"Loading one warning must not trigger an immediate second detection.");
                    // Restore deliberately recovers out-of-world saves. Re-enter this synthetic
                    // unobstructed detection fixture before testing the second exposure.
                    Place(m,exposed);Physics.SyncTransforms();
                    Set("patrolLoadGrace",0f);Set("scannedAt",Time.time-period-1);
                    for(int i=0;i<25&&c.State.Done("N2_04");i++){c.State.patrolScanSeconds=period*.5f;tick.Invoke(c,new object[]{.1f});}
                    Assert.AreEqual(0,c.State.patrolWarnings);Assert.IsFalse(c.State.Done("N2_03"));Assert.IsFalse(c.State.Done("N2_04"));
                    Assert.Less(Vector3.Distance(m.Player.position,c.Sites["bridge"].position),.1f,"Second exposure must use the bridge baseline, not the latest store/dock checkpoint.");
                }
                finally{scoutSite.position=originalScout;storeSite.position=originalStore;}
            }
            finally{c.Cancel();c.enabled=true;}
            yield return new ExitPlayMode();
        }
        [UnityTest,Timeout(240000)] public IEnumerator AntennaStaysMountedAfterClimbCompletionAndReload()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.enabled=false;
            object Call(string name,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,args);
            GameObject Prop(string name)=>(GameObject)typeof(Map02ChapterDirector).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c);
            void Step(float dt,bool up=false,bool down=false,bool right=false,bool tie=false)=>Call("StepAntennaTraversal",dt,up,down,false,right,tie,false);
            try
            {
                var save=m.Capture();save.chapter=Through("D2_04");save.patrolCheckpoint=null;m.Restore(save);
                var marker=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId=="D2_05");
                Place(m,marker.transform.position);Assert.IsTrue(m.Interact(marker));Call("HoldCargo");
                Assert.IsTrue(c.HeldCargo==null||!c.HeldCargo.activeSelf,"Wire pickup must never show a supply box.");
                Assert.IsTrue(Prop("carriedWire").activeSelf);Assert.IsNotNull(Prop("carriedWire").transform.Find("Ăng-ten mang theo"));
                Place(m,marker.transform.position);Assert.IsTrue(m.Interact(marker));Assert.IsTrue(Prop("antennaLadder").activeSelf);Assert.IsFalse(Prop("antennaMast").activeSelf);
                marker=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId=="D2_06");
                Assert.Less(Vector3.Distance(marker.transform.position,c.AntennaBase.position),.01f);
                Place(m,marker.transform.position);Assert.IsTrue(m.Interact(marker));if(c.ConversationActive)Call("EndScene");
                var foot=m.Player.position;
                Step(.9f,up:true);Call("UpdateWorkWeapon");Call("SampleAntennaTraversal");
                var animator=m.Player.GetComponentInChildren<Animator>();
                var leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Assert.Less(Mathf.Abs(Vector3.Dot(leftFoot.position-rightFoot.position,m.Player.right)),.40f,"Feet must remain between the ladder rails.");
                foreach(var weapon in m.Player.GetComponentsInChildren<Weapon>(true))
                    foreach(var renderer in weapon.GetComponentsInChildren<Renderer>(true))Assert.IsTrue(renderer.forceRenderingOff,"No weapon may render while climbing.");
                Assert.IsTrue(Prop("antennaLadder").GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh.vertexCount>100),"Use the detailed Blender ladder.");
                WriteAntennaPose(m,animator);
                CaptureAntennaReview(m,"climb",new Vector3(2.7f,1.5f,-3.4f));
                for(int i=0;i<50;i++)Step(.1f,up:true);
                Call("SampleAntennaTraversal");CaptureAntennaReview(m,"top",new Vector3(3.5f,2.2f,-4.5f));
                for(int i=0;i<26;i++)Step(.1f,right:true);for(int i=0;i<21;i++)Step(.1f);for(int i=0;i<16;i++)Step(.1f,tie:true);
                Assert.IsTrue(c.AntennaTied);var mounted=Prop("antennaMast").transform.position;
                for(int i=0;i<40&&c.CurrentScene==null;i++)Step(.1f,down:true);
                Assert.Less(Vector3.Distance(m.Player.position,foot),.01f);Assert.IsTrue(Prop("antennaMast").activeSelf);
                Assert.Less(Vector3.Distance(mounted,Prop("antennaMast").transform.position),.01f,"Antenna must stay on the tree while Nam descends.");
                Call("EndScene");Assert.IsTrue(c.State.Done("D2_06"));
                save=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(save);
                Assert.IsTrue(Prop("antennaMast").activeSelf);Assert.IsTrue(Prop("installedCable").activeSelf);Assert.IsFalse(Prop("carriedWire").activeSelf);
                Assert.Less(Vector3.Distance(mounted,Prop("antennaMast").transform.position),.01f);
            }
            finally{c.Cancel();c.enabled=true;}
            yield return new ExitPlayMode();
        }
        static void WriteAntennaPose(Map02Mission mission,Animator animator)
        {
            var lines=new System.Collections.Generic.List<string>();
            foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.Chest})
                lines.Add(bone+" "+mission.Player.InverseTransformPoint(animator.GetBoneTransform(bone).position).ToString("F3"));
            File.WriteAllLines("SourceArt/Map02Antenna/pose-check.txt",lines);
        }
        static void CaptureAntennaReview(Map02Mission mission,string name,Vector3 offset)
        {
            Directory.CreateDirectory("SourceArt/Map02Antenna");
            var go=new GameObject("Antenna review camera");var camera=go.AddComponent<Camera>();
            camera.transform.position=mission.Player.position+offset;camera.transform.LookAt(mission.Player.position+Vector3.up*1.1f);camera.fieldOfView=48;camera.farClipPlane=150;
            var target=new RenderTexture(1200,1000,24);var previous=RenderTexture.active;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;var png=new Texture2D(1200,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1200,1000),0,0);png.Apply();File.WriteAllBytes("SourceArt/Map02Antenna/unity-"+name+".png",png.EncodeToPNG());Object.DestroyImmediate(png);}
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(target);Object.DestroyImmediate(go);}
        }
        static Map02ChapterState Through(string id){var s=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){Assert.IsTrue(s.Complete(t.id),t.id);if(t.id==id)break;}return s;}
        static void Place(Map02Mission m,Vector3 p){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=p;cc.enabled=true;m.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>().RestoreMotion(false);Physics.SyncTransforms();}
    }

    // Explicit request only; lets the open Editor run targeted tests without a second project instance.
    [InitializeOnLoad] public static class Map02DayTwoTestRunner
    {
        const string Key="ShadowVale.Map02DayTwoTests";
        const string Request="Temp/Map02DayTwoTests.request",Report="Logs/Map02Chapter/day2-phase1-results.xml";
        static TestRunnerApi runner;
        static Map02DayTwoTestRunner(){EditorApplication.update+=Tick;EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))Register();};}
        static void Register(){if(runner!=null)return;runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results());}
        static void Tick()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key,false))return;
            var request=File.ReadAllText(Request).Trim();bool followup=request=="followup";
            SessionState.SetString(Key+".report",request=="performance-focus"?"Logs/Map02Chapter/phase4-focus-results.xml":request=="performance"?"Logs/Map02Chapter/phase4-results.xml":(request=="night2"||request=="night2-focus")?"Logs/Map02Chapter/night2-phase3-results.xml":(request=="report-patrol"||request=="report-only")?"Logs/Map02Chapter/report-patrol-results.xml":(request=="day2-route"||request=="day2-geometry")?"Logs/Map02Chapter/day2-phase2-results.xml":request=="bridge"?"Logs/Map02Chapter/bridge-child-results.xml":request=="movement"?"Logs/Map02Chapter/free-movement-results.xml":request=="cast"?"Logs/Map02Chapter/cast-results.xml":request=="enemy"?"Logs/Map02Chapter/enemy-animation-results.xml":request=="preview"?"Logs/Map02Chapter/route-preview-results.xml":(request=="hud"||request=="hud-focus")?"Logs/Map02Chapter/shared-hud-results.xml":Report);
            File.Delete(Request);Directory.CreateDirectory("Logs/Map02Chapter");SessionState.SetBool(Key,true);Register();
            var names=request=="antenna"?new[]{"ShadowVale.Map01.Tests.Map02DayTwoStateTests.AntennaStaysMountedAfterClimbCompletionAndReload"}:request=="performance-focus"?new[]{"ShadowVale.Map01.Tests.Map02PerformanceTests"}:request=="performance"?new[]{"ShadowVale.Map01.Tests.Map02PerformanceTests","ShadowVale.Map01.Tests.Map02DayTwoRouteTests","ShadowVale.Map01.Tests.Map02NightTwoRouteTests","ShadowVale.Map01.Tests.Map02ChapterMechanicsTests","ShadowVale.Map01.Tests.Map02ReportAndNightPatrolTests"}:request=="night2-focus"?new[]{"ShadowVale.Map01.Tests.Map02NightTwoRouteTests"}:request=="night2"?new[]{"ShadowVale.Map01.Tests.Map02NightTwoRouteTests","ShadowVale.Map01.Tests.Map02DayTwoStateTests","ShadowVale.Map01.Tests.Map02ReportAndNightPatrolTests","ShadowVale.Map01.Tests.Map02RoutePreviewTests","ShadowVale.Map01.Tests.BandageWeaponTests"}:request=="report-only"?new[]{"ShadowVale.Map01.Tests.Map02ReportAndNightPatrolTests"}:request=="report-patrol"?new[]{"ShadowVale.Map01.Tests.Map02ReportAndNightPatrolTests","ShadowVale.Map01.Tests.Map02DayTwoStateTests","ShadowVale.Map01.Tests.Map02DayTwoRouteTests","ShadowVale.Map01.Tests.Map02CastTests"}:request=="day2-geometry"?new[]{"ShadowVale.Map01.Tests.Map02DayTwoRouteTests","ShadowVale.Map01.Tests.Map02BridgeCollisionTests"}:request=="day2-route"?new[]{"ShadowVale.Map01.Tests.Map02DayTwoRouteTests","ShadowVale.Map01.Tests.Map02DayTwoStateTests","ShadowVale.Map01.Tests.Map02ChapterMechanicsTests","ShadowVale.Map01.Tests.Map02FreeMovementTests","ShadowVale.Map01.Tests.Map02BridgeCollisionTests"}:request=="bridge"?new[]{"ShadowVale.Map01.Tests.Map02BridgeCollisionTests","ShadowVale.Map01.Tests.Map02CastTests"}:request=="movement"?new[]{"ShadowVale.Map01.Tests.Map02FreeMovementTests","ShadowVale.Map01.Tests.Map02DayTwoStateTests","ShadowVale.Map01.Tests.AimToggleIntegrationTests"}:request=="cast"?new[]{"ShadowVale.Map01.Tests.Map02CastTests","ShadowVale.Map01.Tests.Map02DayTwoStateTests"}:request=="enemy"?new[]{"ShadowVale.Map01.Tests.Map02EnemyAnimationTests"}:request=="hud-focus"?new[]{"ShadowVale.Map01.Tests.Map02CharacterHudTests","ShadowVale.Map01.Tests.ForestHudTests.ItemsUseTheirOwnKeysWithNoShortcutBar","ShadowVale.Map01.Tests.MinimapTests"}:request=="hud"?new[]{"ShadowVale.Map01.Tests.Map02CharacterHudTests","ShadowVale.Map01.Tests.Map02DayTwoStateTests","ShadowVale.Map01.Tests.Map02RuntimeTests","ShadowVale.Map01.Tests.Map02RoutePreviewTests","ShadowVale.Map01.Tests.M16IntegrationTests","ShadowVale.Map01.Tests.MinimapTests","ShadowVale.Map01.Tests.ForestHudTests"}:request=="preview"?new[]{"ShadowVale.Map01.Tests.Map02RoutePreviewTests"}:followup?new[]{"ShadowVale.Map01.Tests.Map02DayTwoStateTests","ShadowVale.Map01.Tests.Map02RuntimeTests","ShadowVale.Map01.Tests.Map02ClarityTests.FocusIsStableSelectableAndCannotAbandonCarriedCargo"}:new[]{
                "ShadowVale.Map01.Tests.Map02DayTwoStateTests",
                "ShadowVale.Map01.Tests.Map02ChapterTests",
                "ShadowVale.Map01.Tests.Map02ChapterMechanicsTests"};
            runner.Execute(new ExecutionSettings(new Filter{testMode=UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,testNames=names}));
        }
        sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor tests){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result){if(!SessionState.GetBool(Key,false))return;TestRunnerApi.SaveResultToFile(result,Path.GetFullPath(SessionState.GetString(Key+".report",Report)));SessionState.SetBool(Key,false);}
        }
    }
}
