using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The chapter's talk scenes: starting one, its lines in turn, its end, and committing the task it finishes.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        void BeginScene(Map02ChapterTask task,int from=0,int take=0)
        {
            if(!State.Available(task))return;
            lines=Map02ChapterDefinition.Load().Lines(task.scene??"").Skip(from).Take(take>0?take:int.MaxValue).ToArray();scenePurpose=0;lines=DayLines(task,lines);
            if(lines.Length==0){Finish(task);return;}
            DropBarks();if(task.id=="N3_07")SpawnEscort(); // They come up behind the tank.
            ReleaseDayFreePose();sceneTask=task;sceneCoughed=false;pendingVoice=null;PrepareSceneMotion(task);PrepareDayScene(task);lineIndex=0;lineClock=sceneClock=skipHeld=0;lockedPosition=Mission.Player.position;lockedRotation=Mission.Player.rotation;if(State.phase>=2)StageConversation(task);
            player.RestoreMotion(false);DialogueVoice.StopAll();
            var actor=Mission.Player.GetComponentInChildren<Animator>();namIdle=actor.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="Idle_Tuned");namPose=new Map01CinematicPose(actor,null,task.id=="D1_01");
            if(task.id=="D1_01"){var arrival=FindFirstObjectByType<Map02Arrival>();arrivalBriefingFloor=arrival?.SpawnPoints?.Length>0?arrival.SpawnPoints[0].y:lockedPosition.y;}
            foreach(var weapon in Mission.Player.GetComponentsInChildren<Weapon>(true))if(weapon.gameObject.activeSelf){hiddenWeapons.Add(weapon.gameObject);weapon.gameObject.SetActive(false);}
            var cam=rig.GetComponent<Camera>();cameraBefore=cam.transform.position;cameraRotation=cam.transform.rotation;cameraFov=cam.fieldOfView;
            protectedBefore=health.CinematicInvulnerable;health.CinematicInvulnerable=true;
            BeginPerformance(task);PlanEntrances(task);StageTalk(task);StartLine();
        }
        void Speak(){voice.Stop();pendingVoice=null;lineClock=0;OnPerformanceLine();if(HoldMealVoice)return;if(!Map02DialogueCatalog.HasApprovedRecording(lines[lineIndex]))return;if(CoughFirst(lines[lineIndex])){lineClock=-CoughLead;pendingVoice=lines[lineIndex].RecordingId;return;}voice.Play(lines[lineIndex].RecordingId);}
        void TickScene(float dt)
        {
            sceneClock+=dt;lineClock+=dt;TickPendingVoice();TickMealVoice();TickMealCrickets();TickSupperSound();TickChildPeek();
            if(sceneTask.id=="N3_09"&&tank!=null)SampleSacrifice();
            if(sceneTask.id=="N3_00")TickHandover(dt);
            if(sceneTask.id=="N3_WOUND")TickRetreats();
            SceneSampled?.Invoke(sceneTask.id,sceneClock);
            if(sceneTask.id=="N3_WOUND"&&sceneClock>2)health.CinematicInvulnerable=true;var kb=Keyboard.current;
            skipHeld=kb?.escapeKey.isPressed==true?skipHeld+Time.unscaledDeltaTime:0;
            if(sceneTask.id=="N1_01"&&lineIndex==lines.Length-1&&kb?.eKey.wasPressedThisFrame==true&&lineClock>.6f){EndScene();return;}
            if(skipHeld>=1){EndScene();return;}
            if(!spoken){TickGate();if(!spoken)return;}
            float duration=DayLineDuration(LineSeconds(lines[lineIndex]))+PauseAfter(lineIndex);
            bool next=(lineClock>=duration&&!voice.HasSpeech)||(kb?.eKey.wasPressedThisFrame==true&&lineClock>.6f);
            if(HoldMealVoice)next=false;
            if(next)
            {
                if(lineIndex+1<lines.Length){lineIndex++;StartLine();}
                else if(PerformanceCanEnd()&&!MedicineHolding&&((sceneTask.kind!="cinematic"&&sceneTask.id!="N1_04"&&sceneTask.id!="D1_02")||sceneTask.id=="N3_07"||sceneClock>=SceneDuration))EndScene();   // the packet is in her hands before the scene lets go; the farewell's 120 s is a cap, not a hold
            }
        }
        void LateUpdate()
        {
            if(EvacuationCaptureActive){PresentEvacuationCapture();return;}
            if(NightPatrolFailureActive){PresentNightPatrolFailure();return;}
            DayLateUpdate();UpdateWorkWeapon();if(!ready)return;
            if(doingWork&&workTask?.kind=="antenna"){SampleAntennaTraversal();SampleAntennaSupport();}
            PanToHung();HoldCargo();HungChargeLate();HoldStepsPair();if(sceneTask==null){HoldCoverPosition();UpdateHungRadio();UpdateBandageSleeve();return;}
            HoldConversation();FaceTalk();MoveDaySceneRoots();Mission.Player.SetPositionAndRotation(lockedPosition,lockedRotation);var overrideClip=NamPoseClip();namPose?.Sample(overrideClip!=null?overrideClip:namIdle,sceneClock,overrideClip==null||overrideClip==ordinaryWalk);
            if(sceneTask.id=="D1_01")namPose?.Ground(arrivalBriefingFloor);   // the briefing takes over the retargeted idle from arrival; keep its soles on the same bank
            SampleDayScene();SamplePerformance();UpdateHungRadio();UpdateBandageSleeve();
            string speaker=lines[lineIndex].speaker;Vector3 subject=Mission.Player.position;
            if(!lines[lineIndex].radio&&speaker!="Nam"&&roles.TryGetValue(speaker,out var actor))subject=actor.transform.position;
            if(sceneTask.id=="D1_03"&&speaker=="Ông Sáu")subject=porchDoor-porchOut*.5f;   // his voice from the inner room: look to the doorway
            var focus=Vector3.Lerp(subject,Mission.Player.position,.3f)+Vector3.up*1.25f;
            if(Vector3.Distance(subject,Mission.Player.position)>10)focus=Mission.Player.position+Vector3.up*1.25f;
            var eye=focus-Mission.Player.forward*3.4f+Mission.Player.right*2.5f+Vector3.up*1.4f;
            if(FinaleCamera(out var staged,out var stagedFocus)){eye=staged;focus=stagedFocus;}else if(TalkCamera(out var talkEye,out var talkFocus)){eye=talkEye;focus=talkFocus;}
            if(sceneTask.id=="DAWN"){focus=Vector3.Lerp(Mission.Player.position,memorialArt!=null?memorialArt.transform.position:sites["ditch"].position,.5f)+Vector3.up*.8f;eye=focus-Mission.Player.forward*4+Mission.Player.right*3+Vector3.up*1.5f;}
            // Medium/wide framing; obstruction tested below.
            var ray=eye-focus;foreach(var hit in Physics.SphereCastAll(focus,.12f,ray.normalized,ray.magnitude,~0,QueryTriggerInteraction.Ignore))if(hit.collider.GetComponentInParent<Health>()==null&&hit.collider.GetComponentInParent<Map02ChapterActor>()==null&&(tank==null||!hit.transform.IsChildOf(tank.transform))&&hit.distance>.65f)eye=focus+ray.normalized*Mathf.Min(ray.magnitude,Mathf.Max(.7f,hit.distance-.12f));
            if(sceneTask.id=="D1_02"&&BandageMocap){var right=bandageFacing*Vector3.right;focus=bandageAt+right*.3f+Vector3.up*.75f;eye=focus+bandageFacing*Vector3.forward*2.4f+right*.05f+Vector3.up*.55f;}   // Both hands and the upper-arm dressing stay in view.
            else if(sceneTask.id=="D1_02"){focus=roles["Hùng"].transform.position+Vector3.up*.85f;eye=focus+new Vector3(-3f,.35f,2f);}
            float mealFov=45;if(sceneTask.id=="N1_01")MealShot(out eye,out focus,out mealFov);
            bool shift=sceneTask.id=="N1_04"&&ShiftMocap;if(shift){ShiftShot(out eye,out focus);mealFov=40;}
            if(sceneTask.id=="D1_03"&&HandOverMocap&&medicineStart>=0)HandOverShot(out eye,out focus);
            float blend=sceneTask.id=="N1_01"||shift||sceneTask.id=="D1_02"&&BandageMocap||sceneTask.id=="D1_03"&&HandOverMocap?1:Mathf.SmoothStep(0,1,sceneClock/.8f);   // a cut (into the house; onto chị Lành already at his side), not a glide
            rig.SetCinematicView(Vector3.Lerp(cameraBefore,eye,blend),Quaternion.Slerp(cameraRotation,Quaternion.LookRotation(focus-eye),blend),Mathf.Lerp(cameraFov,sceneTask.id=="N1_01"||shift?mealFov:sceneTask.id=="D1_02"?45:stagedFov??58,blend),1);
            SamplePerformanceCamera();
        }
        void EndScene()
        {
            if(sceneTask==null)return; // Repeated finish/skip calls are harmless.
            foreach(var e in entrances)if(Vector3.Distance(e.actor.transform.position,e.to)>=1.5f)e.actor.StopAt(e.to); // Skipped walks end where the scene meant them to.
            ClearEntrances();
            ReleaseConversation();ReleaseDayScene();ReleaseSceneAction();var task=sceneTask;if(task.id=="N3_09")roles["Hùng"].StopAt(RetreatPoint);int purpose=scenePurpose;scenePurpose=0;namPose?.Dispose();namPose=null;foreach(var w in hiddenWeapons)if(w!=null)w.SetActive(true);hiddenWeapons.Clear();voice.Stop();sceneTask=null;lines=null;rig.ClearCinematicView();health.CinematicInvulnerable=protectedBefore;
            ForestMenu.SuppressKeysAfterCutscene();Mission.RequireInteractionRelease();combat.RestoreAfterCinematic();
            dayDockEnding=false;
            if(purpose==5){Day.arrivalSeen=true;dayArrivalStarting=false;Mission.StoreCheckpoint();return;}
            if(purpose!=0){if(!State.introduced.Contains(task.id))State.introduced.Add(task.id);if(purpose==4)OpenDay(DayActivity.Dock,task);else if(purpose==1)StartWork(task);else if(purpose==2)CompleteFamily(task);else StartRescueEscort(task);return;}
            Finish(task);
        }
        void Finish(Map02ChapterTask task)
        {
            if(task.id=="D1_03")Day.GiveMedicine(0);if(task.id=="N1_03")Day.GiveMedicine(1);
            if(!State.Complete(task.id))return;
            CommitFinishedTask(task);
        }
        void CommitFinishedTask(Map02ChapterTask task)
        {
            TaskCompleted?.Invoke(task.id);
            if(task.id=="D2_03")QueueScoutWarning();
            if(task.id=="N1_03")roles["Hùng"].GoTo(sites["nightOneRest"].position,1.25f);
            if(task.id=="D1_01")roles["Anh Thành"].GoTo(sites["command"].position,1.6f);
            if(task.id=="D2_04")PlaceRecoveredScout();
            if(task.id=="N3_00")roles["Hùng"].GoTo(sites["ditch"].position,NightRun);
            StageNightThree(task);
            ApplyState();Mission.RefreshChapter();
            if(task.id=="N2_02")Mission.EnsurePatrolCheckpoint();
            Mission.StoreCheckpoint();
            var def=Map02ChapterDefinition.Load();
            if(task.id=="N3_09")BeginScene(def.Find("DAWN"));
            if(task.id=="N3_05C")BeginScene(def.Find("N3_WOUND")); // These happen to Nam; he never walks to a pin to trigger them.
            if(task.id=="N3_08")BeginScene(def.Find("N3_09"));
            if(task.id=="N3_04"){Bark("m2v3_wound_01");Bark("m2v3_wound_02");}
            if(task.id=="N3_WOUND")crawlCall=4; // The scene just ended on his first call.
            if(task.id=="D3_04")BeginStepsRehearsal();   // Hùng walks Ông Sáu down to the dock steps, then home for supper
            if(State.finished){Mission.World.story.stage=Map02Stage.Complete;Mission.StoreCheckpoint();}
            if(State.finished)Mission.Say("MAP 2 HOÀN THÀNH · Ta giữ được làng. Hùng đã hy sinh cứu chuyến thuyền cuối.");
        }
    }
}
