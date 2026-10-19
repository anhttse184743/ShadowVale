using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map01OperationsTests:ForestSceneTestBase
    {
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static readonly FieldInfo Storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
        [TestCase(112,121,1,2)][TestCase(111,122,1,0)][TestCase(241,214,1,2)][TestCase(555,555,3,0)][TestCase(123,450,0,0)]
        public void LockScoresRepeatedDigitsWithoutDoubleCounting(int secret,int guess,int exact,int misplaced)
        {var r=Map01SupplyLock.Score(secret,guess);Assert.AreEqual(exact,r.exact);Assert.AreEqual(misplaced,r.misplaced);}
        [Test]public void FailedLockCanRetryWithoutLosingCodeOrNotebook()
        {
            var p=new Map01SupplyLock(241);for(int i=0;i<6;i++)Assert.IsFalse(p.Submit(0));Assert.AreEqual(0,p.Remaining);
            p.Retry();Assert.AreEqual(6,p.Remaining);Assert.AreEqual(6,p.History.Count);Assert.IsTrue(p.Submit(241));Assert.IsTrue(p.Solved);
            var restored=new Map01SupplyLock(p.Secret,p.History,p.AttemptStart);Assert.IsTrue(restored.Solved);Assert.AreEqual(7,restored.History.Count);
        }
        [Test]public void CircuitRoutesToDummyLoadAndRejectsBrokenSource()
        {
            var p=new Map01RadioCircuit(1,new int[Map01RadioCircuit.Count]);Assert.IsTrue(p.Solved());Assert.AreEqual(20,p.ConnectedCount);
            for(int i=0;i<Map01RadioCircuit.Count;i++){p.Rotate(i);Assert.IsFalse(p.Solved(),"Every repair must matter: "+i);p.Rotate(i,-1);Assert.IsTrue(p.Solved());}
            p.Tick(91);Assert.AreEqual(0,p.Remaining);
        }
        [Test]public void CircuitMigratesOldBoardWithoutAcceptingItAsSolved()
        {
            var p=new Map01RadioCircuit(241,new int[16]);Assert.AreEqual(20,p.Rotations.Length);Assert.IsFalse(p.Solved());
            var restored=new Map01RadioCircuit(241,(int[])p.Rotations.Clone());CollectionAssert.AreEqual(p.Rotations,restored.Rotations);
        }
        [Test]public void SupplyCodesVaryButRemainStableForSavedSeed()
        {
            var codes=new System.Collections.Generic.HashSet<int>();
            for(int seed=1;seed<=300;seed++){
                int code=Map01SupplyLock.GenerateCode(seed);codes.Add(code);
                Assert.AreEqual(code,Map01SupplyLock.GenerateCode(seed));
                Assert.IsTrue(Map01SupplyLock.Digits(code).All(d=>d>=0&&d<=5));
            }
            Assert.Greater(codes.Count,100);
        }
        [Test]public void RandomRadioRoundsAreSolvableVariedAndRestoreExactly()
        {
            var paths=new System.Collections.Generic.HashSet<string>();var counts=new System.Collections.Generic.HashSet<int>();
            for(int seed=1;seed<=300;seed++){
                var p=new Map01RadioCircuit(seed);counts.Add(p.TotalRounds);
                do{
                    Assert.AreEqual(10,p.Remaining);Assert.IsFalse(p.Solved());Assert.AreEqual(4+p.Round,p.ScrambledCount);
                    paths.Add(string.Join(",",p.SolvedMasks));
                    int edit=Array.FindIndex(p.InitiallyScrambled,e=>e);p.Rotate(edit);
                    var restored=new Map01RadioCircuit(seed,(int[])p.Rotations.Clone(),p.Remaining,p.Round);
                    CollectionAssert.AreEqual(p.SolvedMasks,restored.SolvedMasks);CollectionAssert.AreEqual(p.Rotations,restored.Rotations);
                    CollectionAssert.AreEqual(p.InitiallyScrambled,restored.InitiallyScrambled);Assert.AreEqual(p.Round,restored.Round);
                    for(int i=0;i<Map01RadioCircuit.Count;i++)while(p.Rotations[i]!=0)p.Rotate(i);
                    Assert.IsTrue(p.Solved(),"Unsolvable seed "+seed+" round "+p.Round);
                }while(p.AdvanceRound());
                Assert.AreEqual(p.TotalRounds-1,p.Round);
            }
            Assert.Greater(paths.Count,5);CollectionAssert.AreEquivalent(new[]{2,3},counts);
        }
        [Test]public void EveryRadioCellCanRotateAndAllChangesSurviveRestore()
        {
            var p=new Map01RadioCircuit(171);
            for(int i=0;i<Map01RadioCircuit.Count;i++){
                int before=p.Rotations[i];p.Rotate(i);Assert.AreEqual((before+1)%4,p.Rotations[i]);
            }
            var restored=new Map01RadioCircuit(171,(int[])p.Rotations.Clone(),p.Remaining,p.Round);
            CollectionAssert.AreEqual(p.Rotations,restored.Rotations);
        }
        [Test]public void PowerSabotageNeedsThreeAccurateNewPullsAndCanTimeout(){
            for(int seed=1;seed<=200;seed++){
                var p=new Map01PowerSabotage(seed);
                for(int i=0;i<3;i++){
                    for(int tick=0;Mathf.Abs(p.Needle-p.Center)>.08f&&tick<1500;tick++)p.Tick(.005f);
                    Assert.IsTrue(p.Pull());Assert.AreEqual(i+1,p.Step);
                }Assert.IsTrue(p.Won);Assert.IsFalse(p.Pull());
            }
            var wrong=new Map01PowerSabotage(1);Assert.IsFalse(wrong.Pull());Assert.IsTrue(wrong.Complete&&!wrong.Won);
            var timeout=new Map01PowerSabotage(1);timeout.Tick(10);Assert.IsTrue(timeout.Complete&&!timeout.Won);
        }
        [Test]public void RadioClockAndRoundGateCannotBeSkipped()
        {
            var p=new Map01RadioCircuit(171);Assert.IsFalse(p.AdvanceRound());
            p.Tick(-2);Assert.AreEqual(10,p.Remaining);p.Tick(9.99f);Assert.Greater(p.Remaining,0);p.Tick(.02f);Assert.AreEqual(0,p.Remaining);
        }
        [TestCase(241,214,"1 số đúng vị trí.","2 số đúng nhưng sai vị trí.")]
        [TestCase(241,355,"Không có số nào đúng.","Cả 3 số đều không thuộc mã.")]
        [TestCase(241,241,"Đúng cả 3 số", "Khóa đã mở.")]
        public void LockExplainsFeedbackInPlainLanguage(int secret,int code,string a,string b)
        {var text=Map01SupplyLock.Feedback(Map01SupplyLock.Score(secret,code));StringAssert.Contains(a,text);StringAssert.Contains(b,text);}
        [Test]public void AllOperationDialogueHasExactCaptionAndCorrectAudioChannel()
        {
            var lines=Map01OperationLines.SupplyOrder.Concat(Map01OperationLines.SupplyReport).Concat(Map01OperationLines.Handoff)
                .Concat(Map01OperationLines.RadioReport).Concat(Map01OperationLines.CommanderReport).Concat(Map01OperationLines.EarlyCommanderReport).Concat(Map01OperationLines.RetreatReport);
            foreach(var line in lines){
                var recording=DialogueVoice.Find(line.voice);Assert.NotNull(recording,line.voice);Assert.NotNull(recording.clip,line.voice);
                Assert.AreEqual(line.speaker+(line.radio?" (bộ đàm): ":": ")+line.text,recording.caption,line.voice);
                Assert.AreEqual(line.radio,recording.radio);StringAssert.EndsWith(line.radio?"_radio":"_clean",recording.resource);
                Assert.Greater(recording.clip.length,.3f);Assert.AreEqual(1,recording.clip.channels);
            }
            foreach(var cue in new[]{"m1_ops_radio_alarm","m1_ops_enemy_spotted","m1_ops_enemy_order"})Assert.NotNull(DialogueVoice.Find(cue)?.clip,cue);
        }
        [UnityTest]public IEnumerator OnlyOriginalSupplyKeeperDropsKeyEvenWhenClonedForExtraction()
        {
            Open();yield return new EnterPlayMode();yield return Ready("key-ownership");
            var s=M.GetComponent<Map01Story>();var keeper=s.Layout.keyKeeper;
            Assert.AreEqual(1,M.Enemies.Count(e=>e.IsSupplyKeyKeeper));
            var clone=Object.Instantiate(keeper.gameObject,keeper.transform.position+Vector3.right*3,Quaternion.identity);
            clone.name="Key inheritance regression";var copied=clone.GetComponent<Map01EnemyController>();copied.BindMission(M);M.AddEnemy(copied);
            Assert.IsTrue(copied.carriesSupplyKey,"Regression must simulate a copied serialized flag.");Assert.IsFalse(copied.IsSupplyKeyKeeper);
            yield return Clear(new[]{copied,keeper});
            Assert.IsFalse(copied.GetComponent<ForestPoint>().items.Any(i=>i.item_id==Map01Story.KeyItem&&i.count>0));
            Assert.AreEqual(Map01Logistics.For(M).PackName(copied),copied.GetComponent<ForestPoint>().label,"Not the keeper: his kind's pack, no key.");
            Assert.AreEqual(1,keeper.GetComponent<ForestPoint>().items.Single(i=>i.item_id==Map01Story.KeyItem).count);
            M.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ExtractionStage);
            var extraction=M.GetComponentInChildren<Map01Extraction>();Assert.IsTrue(extraction.Prepared);extraction.autoContinueToMap2=false;
            var blockers=M.Enemies.Where(e=>e.name.StartsWith("Extraction guard ")).ToArray();Assert.AreEqual(4,blockers.Length);
            Assert.IsTrue(blockers.All(e=>!e.carriesSupplyKey&&!e.IsSupplyKeyKeeper));yield return Clear(blockers);
            Assert.IsTrue(blockers.All(e=>!e.GetComponent<ForestPoint>().items.Any(i=>i.item_id==Map01Story.KeyItem&&i.count>0)));
            var saved=blockers[0].Capture();blockers[0].RestoreSnapshot(saved);
            Assert.IsFalse(blockers[0].GetComponent<ForestPoint>().items.Any(i=>i.item_id==Map01Story.KeyItem&&i.count>0));
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator SupplyRequiresKeyPuzzleReportAndPhysicalHandoff()
        {Open();yield return new EnterPlayMode();yield return Ready("point-1");yield return SupplyFlow();StopRecording();yield return new ExitPlayMode();}
        [UnityTest]public IEnumerator RadioFailureCallsOnlyOneSavedSquadAndSuccessDoesNotExtract()
        {Open();yield return new EnterPlayMode();yield return Ready("point-2");yield return RadioFlow();StopRecording();yield return new ExitPlayMode();}
        [UnityTest]public IEnumerator OfficerNeedsDistantBinocularsAndRestoresCombatBeforeDiscovery()
        {Open();yield return new EnterPlayMode();yield return Ready("point-3");yield return OfficerFlow();StopRecording();yield return new ExitPlayMode();}
        static void StopRecording(){foreach(var movie in Object.FindObjectsByType<Map01OperationMovie>(FindObjectsSortMode.None))movie.End();}
        static void Open(){Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");}
        static bool Recording=>SessionState.GetBool(Map01OperationsRecording.Key,false);
        static IEnumerator Ready(string take)
        {
            float end=Time.realtimeSinceStartup+30;while((M==null||!M.IsInitialized||M.ModernCombat==null||M.GetComponent<Map01Story>().Commander==null)&&Time.realtimeSinceStartup<end)yield return null;
            if(Recording){SetPreviewResolution(1920,1080);EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();Application.runInBackground=true;Time.captureFramerate=15;M.gameCamera.gameObject.AddComponent<Map01OperationMovie>().Begin(take);}
            Assert.NotNull(M);Assert.IsTrue(M.IsInitialized);Storage.SetValue(null,Path.GetFullPath("Logs/Map01Operations/saves/"+Guid.NewGuid().ToString("N")));
            foreach(var e in M.Enemies)e.enabled=false;M.ModernHealth.CinematicInvulnerable=true;
            Assert.AreEqual(4,M.GetComponent<Map01Story>().Guards(1).Length);Assert.AreEqual(5,M.GetComponent<Map01Story>().Guards(2).Length);Assert.AreEqual(5,M.GetComponent<Map01Story>().Guards(3).Length);
        }
        static void At(Vector3 at){var cc=M.player.GetComponent<CharacterController>();bool enabled=cc.enabled;cc.enabled=false;M.player.position=at;cc.enabled=enabled;M.ModernPlayer.RestoreMotion(false);Physics.SyncTransforms();}
        static IEnumerator Clear(Map01EnemyController[] guards)
        {
            foreach(var e in guards){e.GetComponent<Health>().TakeDamage(999999,e.transform.position,null);e.enabled=true;}
            yield return null;foreach(var e in guards)e.enabled=false;
        }
        static IEnumerator Confirm()
        {
            var d=M.GetComponent<Map01StoryDialogue>();
            if(Recording){while(d.IsPlaying){yield return WaitGameSeconds(Mathf.Clamp(d.Current.text.Length/20f,2,5));d.Advance();}}
            else{yield return WaitGameSeconds(1.1f);d.Close(true);}
            yield return null;
        }
        static IEnumerator IdleInteraction()
        {
            float end=Time.time+25;while((Map01NamActions.For(M).Busy||M.GetComponent<Map01Story>().Operating)&&Time.time<end)yield return null;
            Assert.IsFalse(M.GetComponent<Map01Story>().Operating);if(!M.GetComponent<Map01StoryDialogue>().IsPlaying)Assert.IsTrue(M.ModernCombat.enabled&&M.ModernPlayer.enabled);yield return null;
        }
        static IEnumerator SupplyFlow()
        {
            var s=M.GetComponent<Map01Story>();var q=M.GetComponent<Map01Quest>();var bag=M.GetComponent<Map01Inventory>();q.RestoreStage(2);
            At(s.Commander.position+s.Commander.forward*1.8f);yield return IdleInteraction();
            Assert.IsTrue(s.TryInteract());yield return null;
            Assert.AreEqual("m1_supply_debrief_nam_report",M.GetComponent<Map01StoryDialogue>().GetComponent<DialogueVoice>().CurrentId);
            Assert.IsTrue(M.GetComponent<DialogueVoice>().HasSpeech,"Commander briefing must actually start the recording, not only show text.");
            yield return Confirm();Assert.AreEqual(3,q.Stage);
            At(s.Layout.doorApproach.position);Assert.IsTrue(s.TryInteract());Assert.IsFalse(s.Progress.doorUnlocked);
            yield return Clear(s.Guards(1));var point=s.Layout.keyKeeper.GetComponent<ForestPoint>();Assert.NotNull(point);Assert.IsTrue(point.items.Any(i=>i.item_id==Map01Story.KeyItem&&i.count==1));
            At(point.transform.position);M.GetComponent<Map01PlayerInteraction>().Interact(point);yield return IdleInteraction();Assert.IsTrue(s.Progress.keyCollected);
            At(s.Layout.doorApproach.position);Assert.IsTrue(s.TryInteract());Assert.IsTrue(s.Progress.doorUnlocked);
            yield return WaitGameSeconds(.6f);var route=new UnityEngine.AI.NavMeshPath();
            Assert.IsTrue(UnityEngine.AI.NavMesh.CalculatePath(M.player.position,s.Layout.chestApproach.position,UnityEngine.AI.NavMesh.AllAreas,route));Assert.AreEqual(UnityEngine.AI.NavMeshPathStatus.PathComplete,route.status);
            Assert.Less(Vector3.Distance(route.corners.Last(),s.Layout.chestApproach.position),.15f,"The open warehouse must have a real walkable landing at the chest.");
            var aimRouting=RouteInputToGame();using var heldAim=new HeldAimMouse();var aimMouse=heldAim.Device;InputSystem.QueueStateEvent(aimMouse,new MouseState().WithButton(MouseButton.Right));InputSystem.Update();yield return null;
            Assert.IsNotNull(Keyboard.current,"The gameplay interaction loop needs a keyboard to release the UI input gate.");
            Assert.IsTrue(aimMouse.rightButton.isPressed,$"Held mouse fixture: value={aimMouse.rightButton.ReadValue()}, enabled={aimMouse.enabled}, current={Mouse.current?.name}");Assert.AreEqual(aimMouse,Mouse.current);
            int magazine=M.ModernCombat.RoundsInMagazine,reserve=bag.Count("ammo_rifle");var equipped=M.ModernCombat.EquippedKind;
            Assert.IsTrue(s.TryInteract());var ui=M.GetComponent<Map01OperationPuzzleUI>();Assert.IsTrue(ui.Active);if(Recording)yield return WaitGameSeconds(3);ui.StartPuzzle();if(Recording)yield return WaitGameSeconds(3);
            var guess=Map01SupplyLock.Digits(ui.Lock.Secret);
            int wrongFirst=Map01SupplyLock.Code(guess[0],guess[2],guess[1]);
            if(wrongFirst==ui.Lock.Secret)wrongFirst=Map01SupplyLock.Code((guess[0]+1)%6,guess[1],guess[2]);
            ui.SubmitLock(wrongFirst);
            if(Recording){yield return WaitGameSeconds(.3f);Capture("lock-feedback",true);yield return WaitGameSeconds(2);}
            int wrong=(ui.Lock.Secret+1)%6;while(ui.Lock.Remaining>0)ui.SubmitLock(wrong);Assert.AreEqual(0,ui.Lock.Remaining);Assert.IsFalse(s.Progress.supplies);
            Capture("lock-ui",true);if(Recording)yield return WaitGameSeconds(2);ui.Lock.Retry();ui.SubmitLock(ui.Lock.Secret);
            yield return WaitGameSeconds(2.2f);Capture("chest-opening");yield return WaitGameSeconds(2);Capture("cargo-in-hand");yield return IdleInteraction();
            yield return null;Assert.IsTrue(M.ModernCombat.IsAiming,"Holding aim through the puzzle and pickup must restore aim without releasing it.");Assert.AreEqual(magazine,M.ModernCombat.RoundsInMagazine);Assert.AreEqual(reserve,bag.Count("ammo_rifle"));Assert.AreEqual(equipped,M.ModernCombat.EquippedKind);
            InputSystem.QueueStateEvent(aimMouse,new MouseState());InputSystem.Update();heldAim.Dispose();RestoreInputRouting(aimRouting);
            Assert.IsTrue(s.Progress.supplies&&s.Progress.chestUnlocked);Assert.AreEqual(1,bag.Count(Map01Story.CargoItem));Assert.AreEqual(0,bag.Count("supplies"),"Reward is deferred until handoff.");Assert.AreEqual(4,q.Stage);
            Assert.IsTrue(s.TryInteract());yield return Confirm();Assert.IsTrue(s.Progress.supplyReported);Assert.AreEqual(4,q.Stage,"Radio report must send Nam home, not to point 2.");
            At(s.Commander.position+s.Commander.forward*1.8f);Assert.IsTrue(s.TryInteract());yield return null;Assert.AreEqual("m1_ops_handoff_01",M.GetComponent<DialogueVoice>().CurrentId,"Handoff must speak immediately, before the bag animation.");yield return Confirm();yield return IdleInteraction();yield return Confirm();
            Assert.AreEqual(5,q.Stage);Assert.IsTrue(s.Progress.suppliesDelivered&&s.Progress.supplyRewardGranted);Assert.AreEqual(0,bag.Count(Map01Story.CargoItem));Assert.AreEqual(1,bag.Count("supplies"));
            var snapshot=s.Capture();s.Restore(snapshot,q.Stage);Assert.AreEqual(1,bag.Count("supplies"));Assert.IsTrue(s.Progress.suppliesDelivered);
        }
        static IEnumerator RadioFlow()
        {
            var s=M.GetComponent<Map01Story>();var q=M.GetComponent<Map01Quest>();q.RestoreStoryStage(5,new Map01Story.Snapshot{rescueAccepted=true,supplies=true,supplyReported=true,suppliesDelivered=true,supplyRewardGranted=true});
            yield return Clear(s.Guards(2));At(s.Layout.radioApproach.position);Assert.IsTrue(s.TryInteract());var ui=M.GetComponent<Map01OperationPuzzleUI>();yield return ReadyPuzzle(ui);yield return WaitGameSeconds(Recording?3:1);
            var nodes=M.player.GetComponentsInChildren<Transform>();var index=nodes.First(t=>t.name=="RightHandIndex3");var middle=nodes.First(t=>t.name=="LeftHandMiddle1");var hand=M.player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftHand);
            float rightError=0,leftError=0;bool captured=false;Exception captureError=null;var pump=M.gameCamera.gameObject.AddComponent<ExtractionCapturePump>();
            pump.draw=()=>{try{Capture("radio-panel");rightError=Vector3.Distance(index.position,s.Layout.radioRightContact.position);leftError=Vector3.Distance(Vector3.Lerp(hand.position,middle.position,.6f),s.Layout.radioLeftContact.position);
                File.WriteAllText("Logs/Map01Operations/radio-contact.txt","Right fingertip "+rightError+"m; left palm "+leftError+"m; player "+M.player.position+"; right finger "+index.position+" target "+s.Layout.radioRightContact.position);
            }catch(Exception error){captureError=error;}captured=true;};
            float captureDeadline=Time.realtimeSinceStartup+5;while(!captured&&Time.realtimeSinceStartup<captureDeadline)yield return null;Object.Destroy(pump);Assert.IsTrue(captured);if(captureError!=null)throw captureError;
            Assert.Less(rightError,.07f,"Right fingers must touch the actual radio dial.");Assert.Less(leftError,.07f,"Left hand must brace the cabinet.");ui.StartPuzzle();
            float remaining=ui.Circuit.Remaining;ui.TogglePause();float resumeAt=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<resumeAt)yield return null;
            Assert.IsTrue(M.Paused);Assert.AreEqual(remaining,ui.Circuit.Remaining,"Pause must freeze the circuit clock.");ui.TogglePause();Assert.IsFalse(M.Paused);
            if(Recording)yield return WaitGameSeconds(3);Capture("circuit-ui",true);
            Assert.IsFalse(ui.Circuit.Solved());ui.SubmitCircuit();Assert.IsTrue(ui.Active,"Wrong confirmation must show a reason before closing.");Assert.IsFalse(s.Progress.radioResponseSpawned);
            ui.TogglePause();float pausedFailure=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<pausedFailure)yield return null;
            Assert.IsTrue(ui.Active);ui.TogglePause();if(Recording){yield return WaitGameSeconds(.3f);Capture("circuit-error",true);}
            yield return WaitGameSeconds(2.7f);Assert.IsFalse(ui.Active);
            float end=Time.time+8;while(!s.Progress.radioResponseSpawned&&Time.time<end)yield return null;
            Assert.IsTrue(s.Progress.radioResponseSpawned);Assert.AreEqual(3,s.ResponseRemaining);Assert.IsTrue(M.ModernCombat.enabled&&M.ModernPlayer.enabled);
            var response=M.Enemies.Where(e=>e.name.StartsWith("operation_radio_response_")).ToArray();foreach(var e in response)e.enabled=false;
            Assert.IsFalse(s.Progress.radioDisabled);if(Recording)yield return WaitGameSeconds(3);yield return Clear(response);Assert.AreEqual(0,s.ResponseRemaining);Assert.IsTrue(response.All(e=>e.GetComponent<ForestPoint>()==null));
            Assert.IsTrue(s.TryInteract());yield return ReadyPuzzle(ui);ui.StartPuzzle();ui.Finish(Map01OperationResult.Failure);yield return null;Assert.AreEqual(3,M.Enemies.Count(e=>e.name.StartsWith("operation_radio_response_")));
            Assert.IsTrue(s.TryInteract());yield return ReadyPuzzle(ui);ui.StartPuzzle();
            int rounds=ui.Circuit.TotalRounds;
            for(int round=0;round<rounds;round++){
                Assert.IsTrue(ui.Active);for(int i=0;i<Map01RadioCircuit.Count;i++)while(ui.Circuit.Rotations[i]!=0)ui.Circuit.Rotate(i);
                Assert.IsTrue(ui.Circuit.Solved());ui.SubmitCircuit();
                if(round+1<rounds){Assert.IsTrue(ui.AwaitingNextRound);Assert.IsFalse(s.Progress.radioDisabled);ui.StartPuzzle();}
            }
            yield return null;
            Assert.AreEqual(5,q.Stage,"Radio damage alone must not skip the power supply.");Assert.IsTrue(s.Progress.radioDisabled);Assert.IsFalse(s.Progress.powerDisabled);
            Assert.NotNull(s.Layout.sabotage);Assert.IsTrue(s.Layout.sabotage.damagedRadio.activeSelf);CaptureDevice("radio-damaged",false);
            s.Target(out _,out var label);StringAssert.Contains("NGUỒN",label);
            At(s.Layout.sabotage.powerApproach.position);Assert.IsTrue(s.TryInteract());yield return ReadyPuzzle(ui);yield return WaitGameSeconds(1);
            Assert.Less(Vector3.Distance(index.position,s.Layout.sabotage.powerRightContact.position),.11f,"Power control must be within the actual hand reach.");ui.StartPuzzle();
            Assert.IsTrue(ui.IsPower);ui.SubmitPower();yield return WaitGameSeconds(2.7f);Assert.IsFalse(ui.Active);Assert.IsFalse(s.Progress.powerDisabled);
            Assert.AreEqual(3,M.Enemies.Count(e=>e.name.StartsWith("operation_radio_response_")),"A power retry cannot duplicate the response squad.");
            Assert.IsTrue(s.TryInteract());yield return ReadyPuzzle(ui);ui.StartPuzzle();Capture("power-ui",true);yield return WaitGameSeconds(.3f);
            for(int fuse=0;fuse<3;fuse++){
                float deadline=Time.time+9;
                while(Mathf.Abs(ui.Power.Needle-ui.Power.Center)>Map01PowerSabotage.Window*.5f-.015f&&Time.time<deadline){
                    Assert.IsTrue(ui.Active,"Power interaction closed while awaiting fuse "+fuse+"; stage="+q.Stage+" hp="+M.PlayerHealth);
                    yield return null;
                }
                Assert.Less(Mathf.Abs(ui.Power.Needle-ui.Power.Center),Map01PowerSabotage.Window*.5f,
                    $"Fuse {fuse}: remaining={ui.Power.Remaining} needle={ui.Power.Needle} center={ui.Power.Center} preparing={ui.Preparing} ready={ui.AwaitingNextRound} paused={M.Paused} menu={ForestMenu.Visible}");
                ui.SubmitPower();if(fuse<2){Assert.IsTrue(ui.AwaitingNextRound);Assert.AreEqual(5,q.Stage);ui.StartPuzzle();}
            }
            yield return WaitGameSeconds(3);Assert.IsTrue(s.Layout.sabotage.Smoking);Assert.Greater(s.Layout.sabotage.GetComponentInChildren<ParticleSystem>().particleCount,5,"Damaged power must emit real smoke particles.");CaptureDevice("power-smoke",true);
            Assert.AreEqual(6,q.Stage);Assert.IsTrue(s.Progress.powerDisabled);Assert.IsNull(M.GetComponentInChildren<Map01Extraction>());
            var save=M.GetComponent<Map01SaveSystem>();Assert.IsTrue(save.SaveSlot(0,out var error,true),error);Map01SaveSystem.BeginGame(0);yield return null;yield return WaitGameSeconds(1);
            s=M.GetComponent<Map01Story>();q=M.GetComponent<Map01Quest>();foreach(var e in M.Enemies)e.enabled=false;
            Assert.AreEqual(3,M.Enemies.Count(e=>e.name.StartsWith("operation_radio_response_")));Assert.AreEqual(0,s.ResponseRemaining);Assert.IsTrue(s.Progress.radioResponseSpawned&&s.Progress.radioDisabled&&s.Progress.powerDisabled);Assert.IsTrue(s.Layout.sabotage.Smoking);
            At(s.Commander.position+s.Commander.forward*2);yield return WaitGameSeconds(7);yield return IdleInteraction();Assert.IsTrue(s.TryInteract());Assert.IsTrue(M.GetComponent<Map01StoryDialogue>().IsPlaying,"Radio report did not begin: safe="+s.SafeToReport+" stopped="+M.Stopped+" busy="+Map01NamActions.For(M).Busy);yield return Confirm();Assert.AreEqual(7,q.Stage);Assert.IsNull(M.GetComponentInChildren<Map01Extraction>());
        }
        static IEnumerator ReadyPuzzle(Map01OperationPuzzleUI ui){float deadline=Time.time+8;while(ui.Preparing&&Time.time<deadline)yield return null;Assert.IsFalse(ui.Preparing,"Nam could not reach the radio panel.");}
        static IEnumerator OfficerFlow()
        {
            var s=M.GetComponent<Map01Story>();var q=M.GetComponent<Map01Quest>();q.RestoreStoryStage(7,new Map01Story.Snapshot{supplies=true,suppliesDelivered=true,supplyReported=true,radioDisabled=true});
            q.CommanderEnemy.enabled=false;
            At(s.Layout.commanderPost.position+Vector3.back*6);yield return WaitGameSeconds(2.3f);Assert.IsFalse(s.Progress.commanderConfirmed,"Nearby alone cannot confirm an officer.");
            At(s.Layout.observationSites[0].position);M.Crouched=false;var scouting=M.GetComponent<Map01Scouting>();var target=q.CommanderEnemy.transform.position+Vector3.up*1.3f;
            yield return WaitGameSeconds(.3f);foreach(var enemy in M.Enemies)if(enemy.Alive)enemy.RestoreSnapshot(enemy.CaptureAtPost());
            var routing=RouteInputToGame();var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));InputSystem.Update();
            float until=Time.time+7;
            while(!M.GetComponent<Map01StoryDialogue>().IsPlaying&&Time.time<until){
                scouting.HoldBinoculars(true);LookAtOfficer(M,target);yield return null;
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();RestoreInputRouting(routing);
            Assert.IsTrue(M.GetComponent<Map01StoryDialogue>().IsPlaying,"Distant binocular report missing: binocular="+scouting.Binoculars+" moving="+M.ModernPlayer.IsMoving+" safe="+s.SafeToReport+" at="+M.player.position+" target="+target);
            yield return Confirm();Assert.IsTrue(s.Progress.commanderReported&&s.Progress.commanderDiscovery);Assert.IsTrue(M.ModernCombat.enabled&&M.ModernPlayer.enabled);Assert.IsFalse(M.Cinematic);
            Assert.IsNull(M.GetComponentInChildren<Map01Extraction>());if(Recording)yield return WaitGameSeconds(4);
        }
        sealed class HeldAimMouse:IDisposable
        {
            readonly Mouse[] physical;bool disposed;
            readonly Keyboard ownedKeyboard;
            public readonly Mouse Device;
            public HeldAimMouse(){if(Keyboard.current==null)ownedKeyboard=InputSystem.AddDevice<Keyboard>("HeldAimTestKeyboard");physical=InputSystem.devices.OfType<Mouse>().Where(m=>m.enabled).ToArray();foreach(var m in physical)InputSystem.DisableDevice(m);Device=InputSystem.AddDevice<Mouse>("HeldAimTest");InputSystem.onAfterUpdate+=HoldAim;}
            // The batch editor can soft-reset devices when a cinematic changes cursor lock/focus.
            // Model a physically held button at every input update, rather than one initial event.
            void HoldAim(){if(!disposed&&Device.added){Device.MakeCurrent();var state=new MouseState().WithButton(MouseButton.Right);InputState.Change(Device,state,InputUpdateType.Dynamic);InputState.Change(Device,state,InputUpdateType.Editor);}}
            public void Dispose(){if(disposed)return;disposed=true;InputSystem.onAfterUpdate-=HoldAim;if(Device.added)InputSystem.RemoveDevice(Device);if(ownedKeyboard!=null&&ownedKeyboard.added)InputSystem.RemoveDevice(ownedKeyboard);foreach(var m in physical)if(m.added)InputSystem.EnableDevice(m);}
        }
        internal static void LookAtOfficer(Map01Mission mission,Vector3 target)
        {
            var rig=mission.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>();
            var look=Quaternion.LookRotation(target-mission.player.position-Vector3.up*1.6f).eulerAngles;
            // Binoculars use the player's real look angles, before cinematic camera overrides.
            typeof(ShadowVale.Gameplay.Player.ThirdPersonCamera).GetField("_yaw",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(rig,look.y);
            typeof(ShadowVale.Gameplay.Player.ThirdPersonCamera).GetField("_pitch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(rig,look.x>180?look.x-360:look.x);
        }
        static void CaptureDevice(string name,bool power){
            var l=M.GetComponent<Map01Story>().Layout;var c=M.gameCamera;var pos=c.transform.position;var rot=c.transform.rotation;float fov=c.fieldOfView;
            var target=power?l.sabotage.smokeOrigin.position-Vector3.up*.05f:(l.radioRightContact.position+l.radioLeftContact.position)*.5f;
            var hidden=M.player.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();foreach(var renderer in hidden)renderer.enabled=false;
            var front=power?-l.sabotage.powerApproach.forward:Vector3.ProjectOnPlane(l.radioApproach.position-target,Vector3.up).normalized;
            try{c.transform.SetPositionAndRotation(target+front*(power?2.7f:1.5f)+Vector3.up*(power?1.2f:.5f)+Vector3.Cross(Vector3.up,front)*.45f,Quaternion.identity);c.transform.LookAt(target);c.fieldOfView=48;Capture(name);}
            finally{foreach(var renderer in hidden)if(renderer!=null)renderer.enabled=true;c.transform.SetPositionAndRotation(pos,rot);c.fieldOfView=fov;}
        }
        static void Capture(string name,bool ui=false)
        {
            Directory.CreateDirectory("Logs/Map01Operations");
            if(ui){ScreenCapture.CaptureScreenshot("Logs/Map01Operations/"+name+".png");return;}
            var c=M.gameCamera;var rt=RenderTexture.GetTemporary(1280,720,24);var old=c.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/Map01Operations/"+name+".png",image.EncodeToPNG());}
            finally{c.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
        }
        [TearDown]public void RestoreStorage()=>Storage.SetValue(null,null);
    }
}
