using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class C3BedAnimationTests:ForestSceneTestBase
    {
        const string Work="Docs/Reviews/C3_BedKnife_Unity_20261020";
        const string Sandbox="Assets/_Project/Map01/Tests/Generated/C3 Bed Knife Sandbox.unity";
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static Map01C3Mission C=>M.GetComponent<Map01C3Mission>();
        static Map01C3BedTakedown Action=>M.GetComponent<Map01C3BedTakedown>();
        static Map01C3Sleeper Bed(int i)=>C.Sleepers[i].GetComponent<Map01C3Sleeper>();
        static RenderTexture capture;
        static Texture2D pixels;
        static int bedKillEvents;
        static void CountBedKill(EnemyKilledEvent e){if(e.Method=="takedown"&&e.WeaponId=="knife")bedKillEvents++;}
        [Serializable] sealed class Witness {public WitnessRow[] rows;}
        [Serializable] sealed class WitnessRow {public int frame;public string actor,bone;public Vector3 point;}
        [SetUp] public void Isolate()=>SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath(Work+"/TestSaves"));
        [TearDown] public void Release()=>SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
        static void Open()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Sandbox));if(!File.Exists(Sandbox))AssetDatabase.CopyAsset("Assets/_Project/Scenes/Maps/Map 1.unity",Sandbox);
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene(Sandbox);
        }
        static void Quiet()
        {
            M.GetComponent<Map01Rescue>().MarkIntroSeen();M.ModernHealth.CinematicInvulnerable=true;
            foreach(var g in M.Enemies){g.enabled=false;g.GetComponent<NavMeshAgent>().enabled=false;}
            C.enabled=false;
        }
        static void PrepareKnife()
        {
            var bag=M.GetComponent<Map01Inventory>();
            if(bag.Count("knife")==0)bag.Add("knife",1);
            if(bag.SlotItem(Map01Inventory.Slot.Pistol)!="pistol_k54")bag.Add("pistol_k54",1);
            M.ModernCombat.RestoreMagazine(WeaponKind.Pistol,2);
            PlaceNam(M,Bed(0).Approach);Physics.SyncTransforms();
        }
        static void Picture(string path)
        {
            if(capture==null){capture=new RenderTexture(1920,1080,24);pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);}
            var camera=M.gameCamera;var previous=camera.targetTexture;var active=RenderTexture.active;camera.targetTexture=capture;camera.Render();RenderTexture.active=capture;
            pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path,path.EndsWith(".jpg")?pixels.EncodeToJPG(88):pixels.EncodeToPNG());camera.targetTexture=previous;RenderTexture.active=active;
        }
        [Test] public void ApprovedDataKeepsClipTimingAndSeparateGameplayRigs()
        {
            var d=Map01BedAnimationData.Load();Assert.NotNull(d);Assert.NotNull(d.pairPrefab);Assert.NotNull(d.sleeperPrefab);
            Assert.AreEqual("31a0087e64de55bd6e6971362d9e515e6a77831439dc6699bbe13db6ddc5c0c7",d.sourceHash);
            Assert.AreEqual(6.65f,d.execution.length,.02f);Assert.AreEqual(3.3f,d.wake.length,.02f);
            Assert.Greater(d.sleepingBounds.size.z,1f);Assert.Greater(d.sleepingBounds.size.y,.3f);
            Assert.IsNull(d.pairPrefab.GetComponent<ShadowVale.Gameplay.Player.PlayerController>(),"Authored visuals do not install another gameplay controller.");
            Assert.NotNull(d.sleeperPrefab.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="Dich_AK47"),"The rifle hidden in the first Blender pose must still be exported.");
            Assert.AreEqual(92,d.wakePickupFrame);Assert.IsTrue(d.executionIsKnife);Assert.AreEqual(175,d.executionPropVisibleFrame);
            var names=d.pairPrefab.GetComponentsInChildren<Transform>(true).Select(t=>t.name).ToArray();
            Assert.Contains("Nam_Dao_KaBar",names);Assert.IsFalse(names.Any(n=>n.Contains("K54")||n.Contains("Holster")));
            Assert.Contains("Dich_Than",names); // The approved enemy mesh includes his helmet.

        }
        [UnityTest] public IEnumerator ImportedBonesMatchTheApprovedBlenderAtTenKeyPoses()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();
            var d=Map01BedAnimationData.Load();var go=Object.Instantiate(d.pairPrefab);
            var rows=JsonUtility.FromJson<Witness>(File.ReadAllText(Work+"/source_witness.json")).rows;float worst=0;int checkedPoints=0,hiddenPropPoints=0;
            var report=new System.Text.StringBuilder();
            try
            {
                using var pose=new Map01CinematicPose(go.GetComponent<Animator>(),skinGrounding:false);
                foreach(var row in rows)
                {
                    pose.Sample(d.execution,(row.frame-1)/60f);
                    var rig=go.GetComponentsInChildren<Transform>(true).Single(t=>t.name==row.actor);
                    var bone=string.IsNullOrEmpty(row.bone)?rig:rig.GetComponentsInChildren<Transform>().Single(t=>t.name==row.bone);
                    // Blender hides the pre-draw prop at zero scale/origin. FBX preserves
                    // a default transform instead. It must stay invisible in that phase;
                    // compare its world contact only once drawn, as the runtime does.
                    if(string.IsNullOrEmpty(row.bone)&&row.frame<d.executionPropVisibleFrame)
                    {
                        Assert.IsFalse(rig.gameObject.activeInHierarchy);hiddenPropPoints++;
                        report.AppendLine("hidden prop frame="+row.frame+" visibility checked; position is not a contact");continue;
                    }
                    checkedPoints++;
                    float error=Vector3.Distance(bone.position,row.point);worst=Mathf.Max(worst,error);
                    report.Append(row.actor+"/"+row.bone+" frame="+row.frame+" error="+error+" actual="+bone.position.ToString("F6")+" expected="+row.point.ToString("F6"));
                    pose.Sample(d.execution,row.frame/60f);report.Append(" nextSampleError="+Vector3.Distance(bone.position,row.point));
                    pose.Sample(d.execution,(row.frame-2)/60f);report.AppendLine(" previousSampleError="+Vector3.Distance(bone.position,row.point));
                }
                Assert.AreEqual(86,checkedPoints);Assert.AreEqual(4,hiddenPropPoints);
                File.WriteAllText(Work+"/unity_source_witness.txt","compared_points="+checkedPoints+" hidden_prop_visibility_checks="+hiddenPropPoints+" max_world_error_m="+worst+"\n"+report);
            }
            finally{Object.Destroy(go);}
            yield return new ExitPlayMode();
            Assert.Less(worst,.003f,"Imported world-space contacts must match the source; see unity_source_witness.txt.");
        }
        [UnityTest] public IEnumerator SleepersAreVisibleOnMeasuredBedsAndOutsidePerceptionAndCoordination()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();yield return null;Quiet();
            for(int i=0;i<3;i++)
            {
                var b=Bed(i);Assert.IsTrue(b.Sleeping&&b.Visual.activeInHierarchy&&b.Guard.gameObject.activeInHierarchy);
                Assert.AreEqual(8.59f,b.BedFrame.position.y,.01f);Assert.IsFalse(b.Guard.enabled);Assert.IsFalse(b.Guard.GetComponent<NavMeshAgent>().enabled);
                Assert.IsFalse(M.Enemies.Contains(b.Guard));Assert.IsFalse(AgentRegistry.All.Contains(b.Guard));
                Assert.IsFalse(b.Guard.Hear(b.BodyPoint,20,NoiseKind.Stone));Assert.IsFalse(b.Guard.SeesPlayer);
                var head=b.Visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="Head");
                var pillow=b.BedFrame.position+new Vector3(0,.6f,.64f);
                Assert.Less(Vector3.Distance(Vector3.ProjectOnPlane(head.position,Vector3.up),Vector3.ProjectOnPlane(pillow,Vector3.up)),.45f,"His head must be at the real pillow end, not the foot of the bed.");
            }
            PlaceNam(M,Bed(0).Approach);yield return WaitGameSeconds(.4f);Assert.IsTrue(Bed(0).Sleeping);Assert.IsFalse(Bed(0).Guard.Engaged);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator FootstepsStayQuietAndExistingLoudNoiseRulePlaysWakeBeforeCombat()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();yield return null;Quiet();
            M.EmitNoise(Bed(0).BodyPoint,12,NoiseKind.Footstep);yield return null;Assert.IsTrue(Bed(0).Sleeping);
            M.EmitNoise(Bed(0).BodyPoint,30,NoiseKind.Gunshot);yield return null;
            Assert.IsTrue(Bed(0).Waking);Assert.IsFalse(Bed(0).Guard.enabled);Assert.IsFalse(M.Enemies.Contains(Bed(0).Guard));
            yield return WaitGameSeconds(2.6f);
            var nodes=Bed(0).Visual.GetComponentsInChildren<Transform>(true);
            var held=nodes.Single(t=>t.name=="Dich_AK47");var leaning=nodes.Single(t=>t.name=="Sung_DungCot");
            Assert.IsTrue(leaning.gameObject.activeInHierarchy);Assert.IsFalse(held.gameObject.activeInHierarchy);
            yield return WaitGameSeconds(.5f);
            Assert.IsTrue(held.gameObject.activeInHierarchy&&held.GetComponent<Renderer>().enabled);Assert.IsFalse(leaning.gameObject.activeInHierarchy);
            Assert.Less(Vector3.Distance(held.GetComponent<Renderer>().bounds.center,nodes.Single(t=>t.name=="RightHand").position),1f,"The visible wake rifle must follow the authored hand, not float by the bed.");
            yield return WaitGameSeconds(.5f);
            Assert.AreEqual(Map01C3Sleeper.Mode.Awake,Bed(0).CurrentMode);Assert.IsTrue(M.Enemies.Contains(Bed(0).Guard));
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator KnifeExecutionIsSilentFreeOfAmmoCostAndLeavesDiscoverableSavedCorpse()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();yield return null;Quiet();PrepareKnife();
            int rifle=M.ModernCombat.MagazineRounds(WeaponKind.Rifle);float condition=M.GetComponent<Map01Inventory>().Condition(Map01Inventory.Slot.Pistol);
            bedKillEvents=0;noiseEvents=0;EventBus.Subscribe<EnemyKilledEvent>(CountBedKill);M.NoiseEmitted+=CountNoise;
            Assert.IsTrue(Action.TryBegin(Bed(0)),"E's real interaction gate must accept the measured approach.");
            Assert.IsTrue(M.Cinematic);yield return WaitGameSeconds(3.7f);Assert.IsTrue(Bed(0).Guard.Alive);Assert.AreEqual(2,M.ModernCombat.MagazineRounds(WeaponKind.Pistol));
            yield return WaitGameSeconds(.4f);Assert.IsFalse(Bed(0).Guard.Alive);Assert.AreEqual(2,M.ModernCombat.MagazineRounds(WeaponKind.Pistol));
            Assert.AreEqual(rifle,M.ModernCombat.MagazineRounds(WeaponKind.Rifle));Assert.IsTrue(Bed(0).Guard.TakenDownSilently);Assert.IsFalse(Bed(0).Guard.BodyHidden);
            yield return WaitGameSeconds(2.8f);Assert.IsFalse(Action.Active||M.Cinematic);Assert.IsTrue(M.ModernPlayer.enabled&&M.ModernCombat.enabled);
            Assert.AreEqual(1,bedKillEvents);EventBus.Unsubscribe<EnemyKilledEvent>(CountBedKill);M.NoiseEmitted-=CountNoise;
            Assert.AreEqual(0,noiseEvents,"Knife execution emits no hearing event.");
            Assert.IsTrue(Bed(1).Sleeping&&Bed(2).Sleeping,"Adjacent sleepers must remain asleep.");
            Assert.AreEqual(condition,M.GetComponent<Map01Inventory>().Condition(Map01Inventory.Slot.Pistol),.001f);
            var state=JsonUtility.FromJson<Map01C3Mission.State>(JsonUtility.ToJson(C.Capture()));Assert.IsTrue(state.sleepersKilled[0]&&state.sleepersExecuted[0]);
            C.Restore(null);Assert.IsTrue(Bed(0).Sleeping&&Bed(0).Guard.Alive);C.Restore(state);
            Assert.IsFalse(Bed(0).Guard.Alive);Assert.AreEqual(Map01C3Sleeper.Mode.Dead,Bed(0).CurrentMode);Assert.IsTrue(M.Enemies.Contains(Bed(0).Guard));
            yield return new ExitPlayMode();
        }
        static int noiseEvents;
        static Keyboard heldKeyboard;
        static bool interactDown;
        static void CountNoise(Vector3 at,float radius,NoiseKind kind){noiseEvents++;}
        static void HoldInteract(){if(heldKeyboard!=null){heldKeyboard.MakeCurrent();InputSystem.QueueStateEvent(heldKeyboard,interactDown?new KeyboardState(Key.E):new KeyboardState());}}
        [UnityTest] public IEnumerator MissingKnifeRejectsAndEmptyPistolCanExecuteButCancellationRestoresControls()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();yield return null;Quiet();PrepareKnife();
            var bag=M.GetComponent<Map01Inventory>();var items=bag.CaptureItems();
            bag.RestoreFromSave(items.Where(i=>i.item_id!="knife").Concat(new[]{new ForestIngredient{item_id="knife",count=0}}).ToArray(),bag.Stones,null,0);
            yield return WaitGameSeconds(.4f);Assert.IsFalse(Action.TryBegin(Bed(0)));Assert.IsFalse(M.Cinematic);
            bag.Add("knife",1);M.ModernCombat.RestoreMagazine(WeaponKind.Pistol,0);
            Assert.IsTrue(Action.TryBegin(Bed(0)),"An empty pistol is irrelevant to a knife execution.");
            yield return WaitGameSeconds(.4f);Action.Cancel();yield return null;
            Assert.AreEqual(0,M.ModernCombat.MagazineRounds(WeaponKind.Pistol));Assert.IsTrue(Bed(0).Guard.Alive&&Bed(0).Sleeping);
            Assert.IsFalse(M.Cinematic);Assert.IsTrue(M.ModernPlayer.enabled&&M.ModernCombat.enabled);
            Assert.IsTrue(M.player.GetComponent<CharacterController>().enabled);Assert.IsNull(Action.Pair);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator ActualEKeyStartsKnifeAndPauseStopsTheImpactClock()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();yield return null;Quiet();PrepareKnife();
            yield return WaitGameSeconds(.5f);
            var routing=RouteInputToGame();var keyboard=InputSystem.AddDevice<Keyboard>();heldKeyboard=keyboard;interactDown=false;InputSystem.onBeforeUpdate+=HoldInteract;
            try
            {
                yield return WaitGameSeconds(.2f);interactDown=true;yield return WaitGameSeconds(.2f);interactDown=false;
                Assert.IsTrue(Action.Active,"The real E key must route to the knife action.");
                M.SetPaused(true);float frame=Action.SourceFrame;
                for(int n=0;n<12;n++)yield return null;
                Assert.AreEqual(frame,Action.SourceFrame,.001f);Assert.IsTrue(Bed(0).Guard.Alive);
                M.SetPaused(false);yield return WaitGameSeconds(4.2f);Assert.IsTrue(Action.Fired);Assert.IsFalse(Bed(0).Guard.Alive);
                Action.Cancel();yield return null;Assert.IsFalse(M.Cinematic);Assert.AreEqual(Map01C3Sleeper.Mode.Dead,Bed(0).CurrentMode);
            }
            finally{M.SetPaused(false);InputSystem.onBeforeUpdate-=HoldInteract;heldKeyboard=null;InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing);}
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator KnifeVisibilityFollowsTheApprovedDrawAndAllBedsAcceptTheirApproach()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();yield return null;Quiet();PrepareKnife();
            for(int i=0;i<3;i++)
            {
                PlaceNam(M,Bed(i).Approach);Physics.SyncTransforms();Assert.IsTrue(Action.TryBegin(Bed(i)),"Measured approach for bed "+i);
                var prop=Action.Pair.transform.Find(Map01BedAnimationData.Load().executionPropPath);Assert.NotNull(prop);Assert.IsFalse(prop.gameObject.activeInHierarchy);
                Action.Cancel();yield return null;
            }
            PlaceNam(M,Bed(0).Approach);Physics.SyncTransforms();Assert.IsTrue(Action.TryBegin(Bed(0)));
            yield return WaitGameSeconds(3.1f);Assert.IsTrue(Action.Pair.transform.Find(Map01BedAnimationData.Load().executionPropPath).gameObject.activeInHierarchy);
            Action.Cancel();yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator SaveRestoreKeepsSleepingAndAwakeReservesDistinct()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();yield return null;Quiet();
            var fresh=C.Capture();C.WakeFromBedShot(Bed(0).BodyPoint,4);yield return WaitGameSeconds(.2f);var called=C.Capture();
            C.Restore(fresh);Assert.IsTrue(C.Sleepers.All(g=>g.GetComponent<Map01C3Sleeper>().Sleeping));
            C.Restore(called);Assert.AreEqual(Map01C3Sleeper.Mode.Awake,Bed(0).CurrentMode);Assert.IsTrue(M.Enemies.Contains(Bed(0).Guard));Assert.IsTrue(Bed(2).Sleeping);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator ActualSaveFileReloadKeepsKnifeCorpseSleepingAndAwakeStates()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();yield return null;Quiet();PrepareKnife();
            Assert.IsTrue(Action.TryBegin(Bed(0)));yield return WaitGameSeconds(6.9f);
            C.WakeFromBedShot(Bed(2).BodyPoint,1);yield return WaitGameSeconds(3.6f);
            var save=M.GetComponent<Map01SaveSystem>();Assert.IsTrue(save.IsSandbox);
            Assert.IsTrue(save.SaveSlot(0,out var error,true),error);
            C.Restore(null);Assert.IsTrue(Bed(0).Sleeping&&Bed(0).Guard.Alive);
            save.Load();yield return null;yield return null;yield return WaitForMap01Ready();yield return WaitGameSeconds(.6f);Quiet();
            Assert.IsFalse(Bed(0).Guard.Alive);Assert.AreEqual(Map01C3Sleeper.Mode.Dead,Bed(0).CurrentMode);
            Assert.IsTrue(Bed(1).Sleeping);Assert.AreEqual(Map01C3Sleeper.Mode.Awake,Bed(2).CurrentMode);
            Assert.AreEqual(2,M.ModernCombat.MagazineRounds(WeaponKind.Pistol));Assert.AreEqual(1,M.GetComponent<Map01Inventory>().Count("knife"));
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator GuardKilledAfterLeavingTheBedKeepsHisNormalCombatCorpse()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();yield return null;Quiet();
            C.WakeFromBedShot(Bed(0).BodyPoint,4);yield return WaitGameSeconds(Map01BedAnimationData.Load().wake.length+.3f);
            var bed=Bed(0);bed.Guard.enabled=false;bed.Guard.GetComponent<NavMeshAgent>().enabled=false;
            var away=bed.BedFrame.position+new Vector3(3,0,-3);bed.Guard.transform.position=away;
            bed.Guard.GetComponent<Health>().RestoreHealth(0);yield return null;yield return null;
            Assert.AreEqual(Map01C3Sleeper.Mode.Awake,bed.CurrentMode);Assert.IsFalse(bed.Visual.activeSelf);
            Assert.Less(Vector3.Distance(away,bed.Guard.transform.position),.01f,"An awake combat casualty must not return to the bed proxy.");
            Assert.IsFalse(C.Capture().sleepersKilled[0],"The normal enemy snapshot owns this combat corpse.");
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator CaptureActualUnitySleepWakeAndExecutionAt1080p()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();yield return null;Quiet();PrepareKnife();
            SetPreviewResolution(1920,1080);
            var follow=M.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>();follow.enabled=false;
            PlaceNam(M,new Vector3(68,8.59f,50));
            M.gameCamera.transform.position=new Vector3(56.9f,10.1f,60.3f);M.gameCamera.transform.LookAt(new Vector3(54.82f,9.4f,62.8f));
            yield return null;yield return null;Picture(Work+"/Unity_West_TwoSleepers.png");
            M.gameCamera.transform.position=new Vector3(79f,10.1f,57.4f);M.gameCamera.transform.LookAt(new Vector3(77.02f,9.4f,59.68f));
            yield return null;yield return null;Picture(Work+"/Unity_East_Sleeper.png");
            M.gameCamera.transform.position=Bed(0).BedFrame.TransformPoint(new Vector3(-1.9f,1.45f,1.4f));
            M.gameCamera.transform.LookAt(Bed(0).BedFrame.TransformPoint(new Vector3(-.35f,.95f,-.2f)));M.gameCamera.fieldOfView=53;
            PrepareKnife();Assert.IsTrue(Action.TryBegin(Bed(0)));Time.captureFramerate=60;
            for(int f=1;f<=400;f++)
            {
                while(Action.Active&&Action.SourceFrame<f)yield return null;
                Picture(Work+"/UnityFrames/frame_"+f.ToString("D4")+".jpg");
                if(f==112||f==155||f==195||f==239||f==278)Picture(Work+"/Unity_Execution_"+f+".png");
            }
            Time.captureFramerate=0;Assert.IsFalse(Bed(0).Guard.Alive);Assert.AreEqual(2,M.ModernCombat.MagazineRounds(WeaponKind.Pistol));
            yield return WaitGameSeconds(.2f);Assert.IsFalse(Action.Active||M.Cinematic);
            C.Restore(null);PlaceNam(M,new Vector3(68,8.59f,50));
            M.gameCamera.transform.position=new Vector3(57.2f,10.05f,60.3f);M.gameCamera.transform.LookAt(new Vector3(54.82f,9.4f,62.8f));
            Time.captureFramerate=60;C.CallReinforcements();
            for(int f=1;f<=240;f++)
            {
                yield return null;Picture(Work+"/UnityWakeFrames/frame_"+f.ToString("D4")+".jpg");
                if(f==30||f==105||f==195)Picture(Work+"/Unity_Wake_"+f+".png");
            }
            Time.captureFramerate=0;Assert.AreEqual(Map01C3Sleeper.Mode.Awake,Bed(0).CurrentMode);
            Object.Destroy(capture);Object.Destroy(pixels);capture=null;pixels=null;
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator CaptureEastRoomAndKnifeExecutionFromInsideAt1080p()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();yield return null;Quiet();PrepareKnife();
            SetPreviewResolution(1920,1080);M.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>().enabled=false;
            PlaceNam(M,new Vector3(68,8.59f,50));
            M.gameCamera.transform.position=new Vector3(79.1f,10.05f,60.1f);M.gameCamera.transform.LookAt(Bed(2).BodyPoint);
            yield return null;Picture(Work+"/Unity_East_Sleeper.png");
            PlaceNam(M,Bed(2).Approach);Physics.SyncTransforms();Assert.IsTrue(Action.TryBegin(Bed(2)));
            Time.captureFramerate=60;
            while(Action.SourceFrame<155)yield return null;Picture(Work+"/Unity_East_Knife_155.png");
            while(Action.SourceFrame<239)yield return null;Picture(Work+"/Unity_East_Knife_239.png");
            while(Action.Active)yield return null;
            Time.captureFramerate=0;Assert.IsFalse(Bed(2).Guard.Alive);Assert.IsTrue(Bed(0).Sleeping&&Bed(1).Sleeping);
            Assert.AreEqual(2,M.ModernCombat.MagazineRounds(WeaponKind.Pistol));
            Object.Destroy(capture);Object.Destroy(pixels);capture=null;pixels=null;
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator CaptureBedCameraAlternativesForVisualReview()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();yield return null;Quiet();PrepareKnife();
            Time.captureFramerate=60;Assert.IsTrue(Action.TryBegin(Bed(0)));
            while(Action.SourceFrame<239)yield return null;
            var options=new[]{new Vector3(-.25f,1.35f,-1.35f),new Vector3(-.25f,1.45f,-1.6f),new Vector3(-.05f,1.35f,-1.65f),new Vector3(-.4f,1.35f,-1.45f),
                new Vector3(-.25f,1.35f,.8f),new Vector3(.1f,1.35f,.2f),new Vector3(-.65f,1.35f,.35f),new Vector3(-1.3f,1.6f,-1.6f)};
            for(int i=0;i<options.Length;i++)
            {
                M.gameCamera.transform.position=Bed(0).BedFrame.TransformPoint(options[i]);
                M.gameCamera.transform.LookAt(Bed(0).BedFrame.TransformPoint(new Vector3(-.3f,.95f,-.65f)));M.gameCamera.fieldOfView=i==0?60:53;
                Picture(Work+"/CameraOptions/option_"+(i+1)+".png");
            }
            Time.captureFramerate=0;Action.Cancel();Object.Destroy(capture);Object.Destroy(pixels);capture=null;pixels=null;
            yield return new ExitPlayMode();
        }
    }
}
