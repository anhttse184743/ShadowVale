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
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
public sealed class RescueBushTests:ForestSceneTestBase {
    static Map01Mission sceneMission;
    static Map01Mission M{get{if(sceneMission==null)sceneMission=Object.FindFirstObjectByType<Map01Mission>();return sceneMission;}}
    static Map01Rescue R=>M.GetComponent<Map01Rescue>();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Setup(){
        var mission=M;Assert.NotNull(mission);
        SetPreviewResolution(960,540);
        typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Path.GetFullPath("Logs/RescueStory/saves/"+Guid.NewGuid().ToString("N")));
        foreach(var g in mission.Enemies)if(g!=null)g.enabled=false;
        ShaderUtil.allowAsyncCompilation=false;
        var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline??UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        var mode=pipeline?.GetType().GetProperty("gpuResidentDrawerMode");mode?.SetValue(pipeline,Enum.ToObject(mode.PropertyType,0));
    }
    static IEnumerator Warmup(){float deadline=Time.realtimeSinceStartup+30;var warm=Map01InteractionWarmup.For(M);while(!warm.BushReady&&Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(warm.BushReady);}
    [Test]public void TimingNeedsThreeWinsAllowsOneMissAndFailsOnSecond(){
        var t=new Map01BushTiming();t.Tick(.1f,true);Assert.AreEqual(1,t.Misses);
        for(int i=0;i<3;i++){t.Tick(.35f,false);t.Tick(t.Center*2,true);}
        Assert.IsTrue(t.Complete&&t.Won);Assert.AreEqual(3,t.Successes);
        var loss=new Map01BushTiming();loss.Tick(2,false);loss.Tick(.35f,false);loss.Tick(.1f,true);Assert.IsTrue(loss.Complete&&!loss.Won);
        var spam=new Map01BushTiming();spam.Tick(.1f,true);spam.Tick(.1f,true);Assert.AreEqual(1,spam.Misses);spam.Tick(.3f,true);spam.Tick(.1f,true);Assert.IsTrue(spam.Complete&&!spam.Won);
    }
    [Test]public void AllNewAssetsUseHumanoidMotionAndVietnameseDirectVoices(){
        Assert.IsTrue(Map01CinematicPose.Clip("Nam_Bush_Approach").humanMotion);
        foreach(var who in new[]{"Nam","Enemy"})foreach(var clip in new[]{"GrabPull","StruggleLoop","Finish","Escape"})Assert.IsTrue(Map01CinematicPose.Clip(who+"_Bush_"+clip).humanMotion);
        foreach(var id in new[]{"m1_rescue_alarm","m1_rescue_execute_order","m1_rescue_interrogate","m1_rescue_shelter"}){var line=DialogueVoice.Find(id);Assert.NotNull(line.clip);Assert.IsFalse(line.radio);Assert.AreEqual("vi",line.language);}
        foreach(var id in new[]{"m1_v2_rescue_approach_01","m1_v2_rescue_approach_02","m1_v2_rescue_approach_03"}){var line=DialogueVoice.Find(id);Assert.NotNull(line.clip);Assert.AreEqual(id.EndsWith("_02"),line.radio);Assert.AreEqual("vi",line.language);}
        StringAssert.Contains("Địch đông quá, tôi không thể tấn công trực diện.",DialogueVoice.Caption("m1_v2_rescue_approach_01"));
    }
    [UnityTest,Timeout(600000)]public IEnumerator IntroAndExecutionStayBoundAndPresentDeathBeforeFailure(){
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
        yield return StoryReview();yield return new ExitPlayMode();
    }
    static IEnumerator StoryReview(){
        Setup();yield return Warmup();yield return TakeRescueOrder(M);var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.player.position=R.RetryPoint;cc.enabled=true;
        var d=M.GetComponent<Map01RescueCinematic>()??M.gameObject.AddComponent<Map01RescueCinematic>();
        Assert.IsTrue(d.ShowIntro(R,R.Squad[0]));Assert.IsTrue(R.IntroSeen);
        var h=M.hung.GetComponentInChildren<Map01HungVisual>();
        var gameplayView=M.gameCamera.transform.position;var gameplayRotation=M.gameCamera.transform.rotation;
        yield return Record("Intro",()=>d.IsPlaying,()=>{
            Assert.IsTrue(h.RopeVisible);Assert.IsFalse(M.GetComponent<Map01SaveSystem>().SaveSlot(1,out _,true));
            if(d.Elapsed>6.55f){Assert.Less(Vector3.Distance(gameplayView,M.gameCamera.transform.position),.03f,"The radio conversation belongs to the restored player view.");Assert.Less(Quaternion.Angle(gameplayRotation,M.gameCamera.transform.rotation),.2f);}
        });
        Assert.GreaterOrEqual(d.Elapsed,8);Assert.IsTrue(M.ModernCombat.enabled&&M.ModernPlayer.enabled);
        yield return ExecutionReview();
    }
    [UnityTest,Timeout(600000)]public IEnumerator ExecutionShowsBoundDeathThenReturnsToNam(){
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
        Setup();yield return Warmup();yield return TakeRescueOrder(M);R.MarkIntroSeen();
        var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.player.position=R.RetryPoint;cc.enabled=true;
        yield return WaitGameSeconds(.8f);yield return ExecutionReview();yield return new ExitPlayMode();
    }
    static IEnumerator ExecutionReview(){
        var h=M.hung.GetComponentInChildren<Map01HungVisual>();
        var d=M.GetComponent<Map01RescueCinematic>()??M.gameObject.AddComponent<Map01RescueCinematic>();
        R.ReportDetection(new Map01Detection(R.Squad[1],Map01DetectionCause.Sight,M.player.position));
        Assert.AreEqual(Map01Rescue.Phase.Alarm,R.CurrentPhase);
        bool checkedAim=false,sawCaptive=false,sawReaction=false;float maxHead=0;var alarmCamera=M.gameCamera.transform.position;var alarmPlayer=M.player.position;float maxRetreat=0;
        yield return Record("Execution",()=>d.IsPlaying,()=>{
            Assert.IsTrue(h.RopeVisible);maxHead=Mathf.Max(maxHead,h.actor.GetBoneTransform(HumanBodyBones.Head).position.y-M.hung.position.y);
            maxRetreat=Mathf.Max(maxRetreat,Vector3.Distance(alarmPlayer,M.player.position));
            if(d.ExecutionShowsCaptive){
                sawCaptive=true;var p=M.gameCamera.WorldToViewportPoint(h.actor.GetBoneTransform(HumanBodyBones.Head).position);
                Assert.Greater(p.z,0);Assert.That(p.x,Is.InRange(.1f,.9f));Assert.That(p.y,Is.InRange(.1f,.9f),"Bound collapse must remain on screen.");
            }else{
                Assert.Less(Vector3.Distance(M.gameCamera.transform.position,M.player.position),Vector3.Distance(alarmCamera,alarmPlayer)+.3f,"Establishing and reaction shots stay near Nam.");
                if(d.ExecutionShowsReaction){sawReaction=true;var p=M.gameCamera.WorldToViewportPoint(M.player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head).position);Assert.Greater(p.z,0);Assert.That(p.x,Is.InRange(.1f,.9f));Assert.That(p.y,Is.InRange(.1f,.9f));}
            }
            int beat=(int)typeof(Map01RescueCinematic).GetField("storyBeat",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(d);
            float began=(float)typeof(Map01RescueCinematic).GetField("beatAt",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(d);
            var pose=typeof(Map01RescueCinematic).GetField("captivePose",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(d);
            var selected=(AnimationClip)typeof(Map01CinematicPose).GetField("selected",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pose);
            Assert.AreEqual("Hung_Bound_Execution",selected.name,"The shot must not swap Hung's wrist bindings or captive pose.");
            if(beat==2&&d.Elapsed-began>.25f){
                var left=h.actor.GetBoneTransform(HumanBodyBones.LeftHand).position;var right=h.actor.GetBoneTransform(HumanBodyBones.RightHand).position;
                Assert.That(Vector3.Distance(left,right),Is.InRange(.025f,.4f),"Both tied hands must remain separate before the shot.");
                var muzzle=R.Squad[0].GetComponentInChildren<Map01Rifle>().weapon.Muzzle;
                Assert.Less(Vector3.Angle(muzzle.forward,h.actor.GetBoneTransform(HumanBodyBones.Chest).position-muzzle.position),5,"Execution muzzle must aim at Hung before firing.");checkedAim=true;
            }
        });
        Assert.IsTrue(checkedAim);Assert.IsTrue(sawCaptive&&sawReaction,"Show Hung's death and return to Nam before failure.");Assert.Greater(maxRetreat,.2f,"Nam must physically react rather than remain frozen.");
        Assert.IsTrue(R.Failed);Assert.IsTrue(h.RopeVisible);Assert.Less(maxHead,1.5f,"Captive never stands during the execution.");
        Assert.Less(h.actor.GetBoneTransform(HumanBodyBones.Head).position.y-M.hung.position.y,.8f,"Bound captive must finish collapsed.");
        Assert.Greater(d.Elapsed,6);
    }
    [UnityTest,Timeout(600000)]public IEnumerator BushSuccessAndEscapeKeepEnemyAliveUntilFinisherAndRestoreControls(){
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
        yield return BushReview();yield return new ExitPlayMode();
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static bool PlaceBush(Map01EnemyController target,bool flank=false){
        var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.Crouched=true;M.ModernPlayer.RestoreMotion(true);M.ModernCombat.Equip(WeaponKind.Knife);
        var b=M.GetComponent<Map01BushTakedown>();
        foreach(var bush in M.Points.Where(p=>p.kind==ForestPointKind.Hide).OrderBy(p=>Vector3.Distance(p.transform.position,R.CaptivePost))){
            if(bush.radius<.72f||!NavMesh.SamplePosition(bush.transform.position,out var center,.5f,NavMesh.AllAreas))continue;
            foreach(var facing in new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left}){
                if(!NavMesh.SamplePosition(center.position+facing*.6f,out var guard,.35f,NavMesh.AllAreas))continue;
                target.GetComponent<NavMeshAgent>().Warp(guard.position);target.transform.rotation=Quaternion.LookRotation(facing);
                var playerPosition=center.position-facing*.15f;
                if(flank){
                    if(!NavMesh.SamplePosition(guard.position+Vector3.Cross(Vector3.up,facing)*.85f-facing*.3f,out var side,.2f,NavMesh.AllAreas))continue;
                    playerPosition=side.position;
                }
                M.player.SetPositionAndRotation(playerPosition,Quaternion.LookRotation(facing));Physics.SyncTransforms();
                if(b.Qualifies(target,out _)){cc.enabled=true;return true;}
            }
        }
        cc.enabled=true;return false;
    }
    [UnityTest]public IEnumerator FlankCatchUsesContactBeforePullAndEnemyCanEscape(){
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
        yield return WaitGameSeconds(.5f);
        yield return FlankReview();
        yield return new ExitPlayMode();
    }
    static IEnumerator FlankReview(){
        var mission=M;Assert.NotNull(mission);var rescue=mission.GetComponent<Map01Rescue>();Assert.NotNull(rescue);
        Assert.NotNull(mission.GetComponent<Map01BushTakedown>(),"Wait for the rescue startup before placing the test pair.");
        Setup();yield return Warmup();
        rescue.MarkIntroSeen();
        var victim=mission.Enemies.First(g=>g!=null&&g.name=="patrol_1");Assert.IsTrue(victim!=null);
        Assert.IsTrue(PlaceBush(victim,true));
        Assert.Less(Vector3.Angle(victim.transform.forward,M.player.position-victim.transform.position),130);
        var bush=M.GetComponent<Map01BushTakedown>();var position=victim.transform.position;Assert.IsTrue(bush.TryBegin(victim));
        yield return Record("BushFlank",()=>bush.Active,()=>{
            if(bush.CurrentPhase==Map01BushTakedown.Phase.Pull&&bush.PhaseTime<Map01BushTakedown.GripAt)Assert.Less(Vector3.Distance(position,victim.transform.position),.001f);
        });
        Assert.IsTrue(victim.Alive);
        // A valid escape immediately starts the established hostage alarm scene.
        // Finish that scene before checking its gameplay handoff.
        M.GetComponent<Map01RescueCinematic>()?.Skip();
        Assert.IsTrue(M.ModernPlayer.enabled&&M.ModernCombat.enabled);
    }
    static IEnumerator BushReview(){
        Setup();yield return Warmup();R.MarkIntroSeen();var bush=M.GetComponent<Map01BushTakedown>();var input=RouteInputToGame();var mouse=InputSystem.AddDevice<Mouse>();
        try{
            var victim=R.Squad[1];Assert.IsTrue(PlaceBush(victim),"A real authored bush must accommodate a safe paired action.");
            var origin=M.player.position;var rotation=M.player.rotation;
            M.player.position=victim.transform.position+victim.transform.forward*.5f;Assert.IsFalse(bush.Qualifies(victim,out _),"A frontal approach cannot pull a guard.");M.player.SetPositionAndRotation(origin,rotation);
            var tiny=new GameObject("Test cramped hide").AddComponent<ForestPoint>();tiny.kind=ForestPointKind.Hide;tiny.radius=.45f;tiny.transform.position=origin;M.RegisterPoint(tiny);
            Assert.IsFalse(bush.Qualifies(victim,out _),"A cramped bush cannot fit the pair.");M.UnregisterPoint(tiny);Object.Destroy(tiny.gameObject);
            var gameplayAvatar=M.player.GetComponentInChildren<Animator>().avatar;
            var originalHands=M.player.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name=="Player_LeftHand"||r.name=="Player_RightHand").ToDictionary(r=>r,r=>r.sharedMesh);
            Assert.AreEqual(2,originalHands.Count);
            int ammo=M.ModernCombat.RoundsInMagazine;float hp=M.ModernHealth.Current;int stones=M.GetComponent<Map01Inventory>().Count("stone");
            victim.enabled=true;
            Assert.IsTrue(bush.TryBegin(victim));foreach(var h in originalHands){Assert.IsFalse(h.Key.enabled);var variant=M.player.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name==h.Key.name.Replace("Player_","Hug_"));Assert.Greater(variant.sharedMesh.vertexCount,h.Value.vertexCount,"Close-up hands must use the refined topology.");}Assert.IsTrue(victim.Alive);Assert.IsFalse(M.GetComponent<Map01SaveSystem>().SaveSlot(1,out _,true));
            var unmovedGuard=victim.transform.position;bool checkedGrip=false;
            bool miss=false;yield return Record("BushSuccess",()=>bush.Active,()=>{
                if(bush.CurrentPhase==Map01BushTakedown.Phase.Pull&&bush.PhaseTime<Map01BushTakedown.GripAt)Assert.Less(Vector3.Distance(unmovedGuard,victim.transform.position),.001f,"The guard must not move before Nam catches him.");
                if(!checkedGrip&&bush.CurrentPhase==Map01BushTakedown.Phase.Pull&&bush.SampledPhaseTime>=1.2f){
                    var n=M.player.GetComponentInChildren<Animator>();var e=victim.GetComponentInChildren<Animator>();
                    Assert.Less(Vector3.Distance(Map01BushTakedown.PalmPoint(n),Map01BushTakedown.EmbracePoint(e,victim.transform,true)),.05f,"Nam must establish contact before dragging.");checkedGrip=true;
                }
                if(bush.CurrentPhase==Map01BushTakedown.Phase.Pull||bush.CurrentPhase==Map01BushTakedown.Phase.Struggle)Assert.IsTrue(victim.Alive);
                if(bush.Active)Assert.IsFalse(M.ModernCombat.GetComponentsInChildren<Weapon>(true).First(w=>w.Kind==WeaponKind.Knife).gameObject.activeInHierarchy,"Knife must remain stowed throughout the hug/pull branch.");
                bool click=false;
                if(bush.CurrentPhase==Map01BushTakedown.Phase.Struggle&&bush.Timing.Gap==0){
                    if(!miss&&bush.Timing.Cursor>.05f){click=true;miss=true;}
                    else if(miss&&bush.Timing.InWindow)click=true;
                }
                InputSystem.QueueStateEvent(mouse,click?new MouseState().WithButton(MouseButton.Left):new MouseState());
            });
            Assert.IsTrue(checkedGrip,"Review the catch before the first pull, not only the final struggle.");
            Assert.IsFalse(victim.Alive);Assert.AreEqual(1,victim.Capture().silentKind);Assert.AreEqual(1,bush.Timing.Misses);Assert.AreEqual(3,bush.Timing.Successes);
            Assert.AreEqual(ammo,M.ModernCombat.RoundsInMagazine);Assert.AreEqual(hp,M.ModernHealth.Current);Assert.AreEqual(stones,M.GetComponent<Map01Inventory>().Count("stone"));
            Assert.IsTrue(M.ModernCombat.enabled&&M.ModernPlayer.enabled&&M.player.GetComponent<CharacterController>().enabled);
            Assert.IsTrue(M.ModernCombat.GetComponentsInChildren<Weapon>(true).First(w=>w.Kind==WeaponKind.Knife).gameObject.activeInHierarchy);
            foreach(var h in originalHands){Assert.AreSame(h.Value,h.Key.sharedMesh,"Gameplay mesh buffers must remain unchanged.");Assert.IsTrue(h.Key.enabled,"Restore gameplay hand visibility after the bush branch.");}
            Assert.AreSame(gameplayAvatar,M.player.GetComponentInChildren<Animator>().avatar,"Restore the gameplay Avatar after the bush branch.");
            var saved=victim.Capture();victim.RestoreSnapshot(saved);Assert.AreEqual(1,victim.Capture().silentKind);
            var escaping=R.Squad[0];Assert.IsTrue(PlaceBush(escaping));escaping.enabled=true;Assert.IsTrue(bush.TryBegin(escaping));
            InputSystem.QueueStateEvent(mouse,new MouseState());
            var namReference=RecoveryReference(M.player.GetComponentInChildren<Animator>(),out var namReferenceObject,bush.GameplayAvatar);
            var enemyReference=RecoveryReference(escaping.GetComponentInChildren<Animator>(),out var enemyReferenceObject);
            bool checkedRecovery=false;
            try{
                yield return Record("BushEscape",()=>bush.Active,()=>{
                    Assert.IsTrue(escaping.Alive);
                    if(bush.CurrentPhase!=Map01BushTakedown.Phase.Escape||bush.PhaseTime<1.45f||bush.SampledPhaseTime<1.4f)return;
                    namReference.Sample(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Animations/KnifeCarry/Nam_Knife_Sneak_Idle.anim"),bush.SampledPhaseTime-.75f,true);
                    enemyReference.Sample(Map01CinematicPose.Clip("Enemy_Bush_Recovery"),bush.SampledPhaseTime-.75f,true);
                    if(!checkedRecovery)File.WriteAllText("Logs/recovery-joints.txt","sample="+bush.SampledPhaseTime+" clock="+bush.PhaseTime+"\n"+string.Join("\n",new[]{(M.player.GetComponentInChildren<Animator>(),namReferenceObject.GetComponentInChildren<Animator>()),(escaping.GetComponentInChildren<Animator>(),enemyReferenceObject.GetComponentInChildren<Animator>())}.SelectMany(p=>new[]{HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightHand}.Select(j=>p.Item1.name+" "+j+" actual="+p.Item1.GetBoneTransform(j).localRotation+" expected="+p.Item2.GetBoneTransform(j).localRotation))));
                    foreach(var pair in new[]{(M.player.GetComponentInChildren<Animator>(),namReferenceObject.GetComponent<Animator>()),(escaping.GetComponentInChildren<Animator>(),enemyReferenceObject.GetComponent<Animator>())})
                        foreach(var joint in new[]{HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightHand})
                            if(pair.Item1.GetComponent<Map01OverseerGrip>()==null||joint==HumanBodyBones.RightLowerArm||joint==HumanBodyBones.RightHand)
                                Assert.Less(Quaternion.Angle(pair.Item1.GetBoneTransform(joint).localRotation,pair.Item2.GetBoneTransform(joint).localRotation),6,pair.Item1.name+" "+joint+": escape arms must finish in the real gameplay recovery pose.");
                    var foregrip=escaping.GetComponentInChildren<Map01OverseerGrip>();
                    if(foregrip!=null){
                        Assert.Less(foregrip.PalmError,.05f,"Recovered support hand must reach the rifle foregrip.");
                        Assert.Less(Quaternion.Angle(escaping.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftHand).rotation,enemyReferenceObject.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftHand).rotation),35.1f,"The support wrist must not twist to mount the rifle.");
                    }
                    Assert.Greater(Vector3.Distance(M.player.position,escaping.transform.position),.65f,"Both actors must separate after the hold is broken.");checkedRecovery=true;
                });
                Assert.IsTrue(checkedRecovery,"Observe the recovered arm pose before returning control.");
            }finally{namReference.Dispose();enemyReference.Dispose();Object.Destroy(namReferenceObject);Object.Destroy(enemyReferenceObject);}
            Assert.AreSame(gameplayAvatar,M.player.GetComponentInChildren<Animator>().avatar,"Restore gameplay Avatar after escape.");
            Assert.IsTrue(escaping.Alive);Assert.AreEqual(2,bush.Timing.Misses);Assert.IsTrue(R.CombatAlarm,"Overseer away from the hostage rallies combat, never executes Hung.");Assert.IsFalse(R.Failed);Assert.IsFalse(M.Cinematic);
        }finally{InputSystem.RemoveDevice(mouse);RestoreInputRouting(input);}
    }
    static Map01CinematicPose RecoveryReference(Animator original,out GameObject root,Avatar gameplayAvatar=null){
        // Bind a neutral model, never a copy of the already deformed grapple
        // skeleton: Humanoid binding uses the hierarchy's initial bone axes.
        string file=gameplayAvatar!=null?"Player/Player.fbx":"Enemies/lính rig.fbx";
        root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/"+file));
        foreach(var renderer in root.GetComponentsInChildren<Renderer>())renderer.enabled=false;
        root.transform.position=Vector3.down*1000;
        var actor=root.GetComponentInChildren<Animator>();actor.avatar=gameplayAvatar!=null?gameplayAvatar:original.avatar;actor.Rebind();return new Map01CinematicPose(actor);
    }
    [UnityTest]public IEnumerator EitherArrivalStartsWalkingFromActualPositions(){
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
        yield return ShelterReview();yield return new ExitPlayMode();
    }
    static IEnumerator ShelterReview(){
        Setup();yield return Warmup();R.MarkIntroSeen();M.GetComponent<Map01Quest>().RestoreStage(Map01Quest.EscortStage);
        var layout=R.Layout;var forward=Vector3.ProjectOnPlane(layout.shelterRun[1].position-layout.shelterDoor.position,Vector3.up).normalized;
        M.hung.GetComponent<NavMeshAgent>().Warp(layout.shelterDoor.position);
        var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;
        var route=Map01Rescue.Path(R.CaptivePost,layout.shelterDoor.position);Assert.Greater(route.Length,1);
        float remaining=9;var start=route[0];
        for(int i=route.Length-1;i>0;i--){float length=Vector3.Distance(route[i],route[i-1]);if(length>=remaining){start=Vector3.Lerp(route[i],route[i-1],remaining/length);break;}remaining-=length;}
        Assert.IsTrue(NavMesh.SamplePosition(start,out var hit,3,NavMesh.AllAreas));M.player.position=hit.position;cc.enabled=true;
        Physics.SyncTransforms();yield return WaitGameSeconds(.55f);
        var d=M.GetComponent<Map01RescueCinematic>();Assert.AreEqual(Map01RescueCinematic.Mode.Shelter,d.CurrentMode);Assert.Less(Vector3.Distance(M.player.position,hit.position),1);
        Assert.IsTrue((bool)typeof(Map01RescueCinematic).GetField("hungFirst",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(d));
        float minimum=float.PositiveInfinity;yield return Record("HungFirstShelter",()=>d.IsPlaying,()=>minimum=Mathf.Min(minimum,Vector3.Distance(M.player.position,M.hung.position)));
        Assert.IsTrue(R.RewardDelivered);Assert.GreaterOrEqual(d.Elapsed,8);Assert.GreaterOrEqual(minimum,1.35f);Assert.IsFalse(R.CompleteDelivery());Assert.Less(Vector3.Distance(M.hung.position,layout.reportPoint.position),.15f);
    }
    [UnityTest]public IEnumerator IntroSkipAndRetryKeepSeenFlagAndBushEscOnlyPauses(){
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
        yield return LifecycleReview();yield return new ExitPlayMode();
    }
    static IEnumerator LifecycleReview(){
        Setup();yield return Warmup();yield return TakeRescueOrder(M);var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.player.position=R.RetryPoint;cc.enabled=true;
        var director=M.GetComponent<Map01RescueCinematic>()??M.gameObject.AddComponent<Map01RescueCinematic>();
        Assert.IsTrue(director.ShowIntro(R,R.Squad[0]));director.Skip();Assert.IsTrue(R.IntroSeen);Assert.IsFalse(M.Cinematic);
        Assert.IsTrue(M.GetComponent<Map01SaveSystem>().RestartRescue(false));yield return WaitGameSeconds(.9f);Assert.IsTrue(R.IntroSeen);
        foreach(var g in M.Enemies)g.enabled=false;
        var bush=M.GetComponent<Map01BushTakedown>();Assert.IsTrue(PlaceBush(R.Squad[1]));Assert.IsTrue(bush.TryBegin(R.Squad[1]));
        var routing=RouteInputToGame();var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
        try{
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return WaitGameSeconds(2.1f);Assert.AreEqual(0,bush.Timing.Successes);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;yield return null;yield return null;
            Assert.IsTrue(bush.Paused&&M.Paused);float frozen=bush.Timing.Elapsed;
            for(int i=0;i<8;i++)yield return null;Assert.AreEqual(frozen,bush.Timing.Elapsed);Assert.IsTrue(bush.Victim.Alive);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;yield return null;yield return null;
            Assert.IsFalse(bush.Paused);Assert.IsTrue(bush.Active);Assert.AreEqual(0,bush.Timing.Successes);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());
            yield return WaitGameSeconds(7);Assert.IsFalse(bush.Active);Assert.IsTrue(bush.Victim.Alive);
            Assert.IsTrue(R.CurrentPhase==Map01Rescue.Phase.Alarm||R.Failed,"A patrol escape with overseer still at the hostage must start execution.");
            M.GetComponent<Map01RescueCinematic>().Skip();Assert.IsTrue(R.Failed);
        }finally{InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);RestoreInputRouting(routing);M.SetPaused(false);}
    }
    static IEnumerator Record(string name,Func<bool> playing,Action step){
        var folder="Logs/RescueStory/"+name;Directory.CreateDirectory(folder);Directory.CreateDirectory(folder+"/GameView");var pump=M.gameObject.AddComponent<ExtractionCapturePump>();
        foreach(var path in Directory.GetFiles(folder,"*.png").Concat(Directory.GetFiles(folder+"/GameView","*.png")))File.Delete(path);
        var rows=new System.Collections.Generic.List<string>{"frame,time,voice"};float began=Time.time,next=Time.time;
        if(name.StartsWith("Bush")||name=="Execution"){Directory.CreateDirectory(folder+"/Contacts");foreach(var path in Directory.GetFiles(folder+"/Contacts"))File.Delete(path);}
        float deadline=Time.realtimeSinceStartup+240;int frame=0;
        // Fixed simulation steps belong to this recording fixture only, so
        // diagnostics cannot introduce uneven playback into the review video.
        try{Time.captureDeltaTime=name.StartsWith("Bush")?1f/60:0;while(playing()&&Time.realtimeSinceStartup<deadline){
            if(Time.time<next){yield return null;continue;}next=Time.time+(name=="Intro"||name=="Execution"?.5f:1f/30);
            bool done=false;Exception error=null;pump.draw=()=>{try{
                step();
                Capture(M.gameCamera,folder+"/"+frame.ToString("D4")+".png");
                ScreenCapture.CaptureScreenshot(folder+"/GameView/"+frame.ToString("D4")+".png");
                if(name.StartsWith("Bush")&&frame%6==0)CaptureContacts(folder,frame);
                if(name=="Execution"&&frame%6==0)CaptureBound(folder,frame);
                string voice=M.GetComponentsInChildren<DialogueVoice>().FirstOrDefault(v=>!string.IsNullOrEmpty(v.CurrentId))?.CurrentId??"";
                rows.Add(frame+","+(Time.time-began).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+voice);frame++;
            }catch(Exception e){error=e;}finally{done=true;}};
            while(!done&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(done,"LateUpdate must execute while recording the sequence.");if(error!=null)throw error;
        }Assert.IsFalse(playing(),"Cinematic must finish within its real route/voice timing.");}
        finally{File.WriteAllText(folder+"/frames.txt",frame.ToString());File.WriteAllLines(folder+"/frames.csv",rows);Time.captureDeltaTime=0;Object.Destroy(pump);}
    }
    static void Capture(Camera camera,string path){
        var rt=RenderTexture.GetTemporary(960,540,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(960,540,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
    }
    static void CaptureContacts(string folder,int frame){
        var bush=M.GetComponent<Map01BushTakedown>();if(bush.Victim==null||!bush.Active)return;
        var camera=M.gameCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        var enemy=bush.Victim.GetComponentInChildren<Animator>();var nam=M.player.GetComponentInChildren<Animator>();
        var focus=Vector3.Lerp((enemy.GetBoneTransform(HumanBodyBones.Chest).position+nam.GetBoneTransform(HumanBodyBones.Chest).position)*.5f,
            (enemy.GetBoneTransform(HumanBodyBones.Head).position+nam.GetBoneTransform(HumanBodyBones.Head).position)*.5f,.4f);
        var heading=bush.Victim.transform.rotation;
        var leaves=Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None)
            .Where(l=>Vector3.Distance(l.transform.position,focus)<8&&(l.name.ToLowerInvariant().Contains("shrub")||l.name.ToLowerInvariant().Contains("grass")||l.name.ToLowerInvariant().Contains("bush")||l.name.ToLowerInvariant().Contains("fern")))
            .SelectMany(l=>l.GetLODs().SelectMany(x=>x.renderers)).Where(r=>r!=null&&r.enabled).Distinct().ToArray();
        try{
            // Diagnostic close views expose joints; gameplay captures above
            // retain the real light foliage reveal throughout the interaction.
            foreach(var r in leaves)r.enabled=false;
            camera.fieldOfView=44;
            int i=0;foreach(var angle in new[]{new Vector3(-1.4f,.35f,.5f),new Vector3(1.4f,.35f,.5f),new Vector3(0,.35f,1.7f),new Vector3(2.6f,.55f,1.3f),new Vector3(-2.6f,.55f,-1.3f)}){
                camera.transform.position=focus+heading*angle;camera.transform.rotation=Quaternion.LookRotation(focus-camera.transform.position);
                Capture(camera,folder+"/Contacts/"+frame.ToString("D4")+"-"+i+++".png");
            }
            File.WriteAllText(folder+"/Contacts/"+frame.ToString("D4")+".txt",bush.CurrentPhase+"\nNam actor "+nam.transform.localEulerAngles+"\nEnemy actor "+enemy.transform.localEulerAngles+
                "\nLeft torso contact "+Vector3.Distance(Map01BushTakedown.PalmPoint(nam),Map01BushTakedown.EmbracePoint(enemy,bush.Victim.transform,true))+
                "\nHead penetration "+bush.HeadPenetration+"\nRight contact "+Vector3.Distance(enemy.GetBoneTransform(HumanBodyBones.RightHand).position,Vector3.Lerp(nam.GetBoneTransform(HumanBodyBones.LeftLowerArm).position,nam.GetBoneTransform(HumanBodyBones.LeftHand).position,.62f))+
                "\nEmbracing contact "+Vector3.Distance(Map01BushTakedown.RightPalmPoint(nam),Map01BushTakedown.EmbracePoint(enemy,bush.Victim.transform))+
                "\nDefender embracing-arm contact "+Vector3.Distance(enemy.GetBoneTransform(HumanBodyBones.LeftHand).position,Vector3.Lerp(nam.GetBoneTransform(HumanBodyBones.RightLowerArm).position,nam.GetBoneTransform(HumanBodyBones.RightHand).position,.78f)-bush.Victim.transform.right*.025f+Vector3.up*.015f));
            if(bush.CurrentPhase==Map01BushTakedown.Phase.Struggle){
                File.AppendAllText(folder+"/Contacts/"+frame.ToString("D4")+".txt","\nNam root "+M.player.position+"\nEnemy root "+bush.Victim.transform.position+"\nNam shoulder "+nam.GetBoneTransform(HumanBodyBones.LeftUpperArm).position+"\nEnemy head "+enemy.GetBoneTransform(HumanBodyBones.Head).position+
                    "\nArm span "+(Vector3.Distance(nam.GetBoneTransform(HumanBodyBones.LeftUpperArm).position,nam.GetBoneTransform(HumanBodyBones.LeftLowerArm).position)+Vector3.Distance(nam.GetBoneTransform(HumanBodyBones.LeftLowerArm).position,nam.GetBoneTransform(HumanBodyBones.LeftHand).position)));
                Assert.Less(bush.HeadPenetration,.005f,"No arm may pass through the defender head or helmet.");
                Assert.Less(Vector3.Distance(Map01BushTakedown.PalmPoint(nam),Map01BushTakedown.EmbracePoint(enemy,bush.Victim.transform,true)),.04f,"Closed left hand must contact the torso, not the face.");
                Assert.Less(Vector3.Distance(Map01BushTakedown.RightPalmPoint(nam),Map01BushTakedown.EmbracePoint(enemy,bush.Victim.transform)),.04f,"Embracing palm must contact the torso, not float behind it.");
                var neutral=nam.avatar.humanDescription.skeleton.ToDictionary(b=>b.name,b=>b.rotation);
                foreach(var side in new[]{"Left","Right"}){
                    foreach(var f in new[]{"Thumb","Index","Middle","Ring","Pinky"})foreach(int joint in new[]{1,2,3}){var bone=nam.GetComponentsInChildren<Transform>().First(t=>t.name==side+"Hand"+f+joint);Assert.Greater(Quaternion.Angle(bone.localRotation,neutral[bone.name]),30,"Every joint on both Nam hands must stay closed in the hug branch.");}
                    var index=nam.GetComponentsInChildren<Transform>().First(t=>t.name==side+"HandIndex2");
                    Assert.Greater(Quaternion.Angle(index.localRotation,neutral[index.name]),40,"Authored clenched fists must survive Humanoid playback.");
                }
            }
        }finally{foreach(var r in leaves)r.enabled=true;camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
    }
    static void CaptureBound(string folder,int frame){
        var camera=M.gameCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        var actor=M.hung.GetComponentInChildren<Map01HungVisual>().actor;
        var focus=M.hung.position+Vector3.up*.65f;
        try{
            camera.fieldOfView=44;int i=0;
            foreach(var angle in new[]{new Vector3(1.9f,.55f,.1f),new Vector3(.1f,.7f,-2.1f)}){
                camera.transform.position=focus+M.hung.rotation*angle;camera.transform.rotation=Quaternion.LookRotation(focus-camera.transform.position);
                Capture(camera,folder+"/Contacts/"+frame.ToString("D4")+"-"+i+++".png");
            }
            File.WriteAllText(folder+"/Contacts/"+frame.ToString("D4")+".txt",string.Join("\n",new[]{HumanBodyBones.Hips,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}.Select(j=>j+" "+M.hung.InverseTransformPoint(actor.GetBoneTransform(j).position))));
        }finally{camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
    }
    [TearDown]public void Cleanup(){Time.captureDeltaTime=0;typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);}
}}
