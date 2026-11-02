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
    /// What a checkpoint keeps of the chapter: capturing and restoring its state, cancelling, and re-applying it to the world.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        public void Restore(Map02ChapterState state)
        {
            Cancel();State=state??new Map02ChapterState();State.Validate();EnsureDayState();workTask=Map02ChapterDefinition.Load().Find(State.activeTask);
            Mission.World.story.stage=State.finished?Map02Stage.Complete:Map02Stage.Contact;
            foreach(var e in Enemies)if(e!=null)Destroy(e.gameObject);Enemies.Clear();
            foreach(var saved in State.enemies)
            {
                float hp=saved.group==NightPatrolGroup?NightPatrolSavedHealth(saved.health,State.nightPatrolHealthVersion):saved.health;
                SpawnEnemy(saved.id,saved.group,saved.group==NightPatrolGroup&&hp>0?MigrateNightPatrolPosition(saved.id,saved.position):saved.position,hp,saved.group!="scout_patrol"&&saved.group!=NightPatrolGroup&&saved.group!=EvacuationPatrolGroup,saved.looted);
            }
            State.nightPatrolHealthVersion=1;
            ammoHinted=false;
            // Saves from before evacuation-driven lanes stored seconds held.
            var held=Map02ChapterDefinition.Load().Find(State.activeTask);if(held!=null&&held.kind=="defend"&&State.taskProgress>1)State.taskProgress=Mathf.Clamp01(State.taskProgress/Mathf.Max(1,held.seconds));
            if(State.completed.Count==0)foreach(var pair in initialRolePositions)roles[pair.Key].StopAt(pair.Value);
            placedActors=false;phaseApplied=-1;ApplyState();
            foreach(var saved in State.actors)if(roles.TryGetValue(saved.role,out var actor))actor.StopAt(saved.position);
            PlaceChildWithMother();
            if(State.phase==2||State.phase==3){roles["Anh Thành"].StopAt(sites["command"].position);if(State.Done("D2_04"))PlaceRecoveredScout();}
            if(State.phase==3)roles["Hùng"].StopAt(sites["radio"].position+Vector3.right*1.4f);
            if(State.phase==0&&State.Done("D1_01"))roles["Anh Thành"].GoTo(sites["command"].position,1.6f);
            if(State.phase==4&&State.Done("D3_05"))roles["Anh Thành"].StopAt(SupperYard);
            if(State.phase>=5&&State.Done("N3_02"))foreach(var name in new[]{"Ông Sáu","Chị Lành"})if(Vector3.Distance(roles[name].transform.position,sites["southStore"].position)>8)roles[name].StopAt(sites["southStore"].position+(name=="Ông Sáu"?Vector3.right:Vector3.left)*1.4f);
            if(State.phase>=5&&State.Done("N3_03")&&Vector3.Distance(roles["Bé Tí"].transform.position,sites["southStore"].position)>8)roles["Bé Tí"].StopAt(SafeSpot("Bé Tí"));
            hungCanal=null;
            if(State.activeTask=="N3_08"&&State.taskProgress>1){State.coverClock=State.taskProgress;State.taskProgress=Mathf.Clamp01(State.coverClock/CoverSlowest);} // Saves that counted seconds.
            if(State.phase==5&&State.Done("N3_00")&&!State.hungDead)
            {
                if(State.activeTask=="N3_08"){TickTank(0,true);roles["Hùng"].StopAt(Along(HungCanal,State.taskProgress*Length(HungCanal)));pansDone=Mathf.FloorToInt(State.taskProgress*3);}
                else if(State.Done("N3_08"))roles["Hùng"].StopAt(ChargePoint);else roles["Hùng"].GoTo(sites["ditch"].position,NightRun);
            }
            if(State.Available(workTask))
            {
                if(workTask.kind=="escortScout"||workTask.kind=="escortHung"||workTask.kind=="patrolHung")
                {if(workTask.id=="D2_04")StartScoutEscort(true);else{escort=roles[workTask.kind=="escortScout"?"Tâm":"Hùng"];escort.Follow(Mission.Player,1.35f);}}
                else if(workTask.kind=="rescueFamily"||workTask.kind=="rescueChild")
                {if(State.introduced.Contains(workTask.id))StartRescueEscort(workTask);}
            }
            handsBusy=!string.IsNullOrEmpty(State.carry);combat.SetHandsOccupied(handsBusy);
            player.SurfaceSpeedMultiplier=State.wounded?WoundedPace:1;
            player.SprintAllowed=()=>Mission.CharacterCanSprint;
            detectionClock=0;scannedAt=-99;patrolLoadGrace=2;
            if(State.phase==1&&State.Done("N1_03")&&!State.Done("N1_04"))roles["Hùng"].GoTo(sites["nightOneRest"].position,1.25f);
            ArrangeDayFamily();UpdateMarkers();SyncDayProps();TickLight(10f);
            TickTank(0,true);TickBoats(0,true);
            var def=Map02ChapterDefinition.Load();
            if(State.phase==4&&State.Available(def.Find("D3_05")))roles["Hùng"].StopAt(HungAtSupper);
            if(State.Done("N3_09")&&!State.finished)BeginScene(def.Find("DAWN"));
            else if(State.Done("N3_05C")&&!State.Done("N3_WOUND"))BeginScene(def.Find("N3_WOUND"));
            else if(State.Done("N3_08")&&!State.Done("N3_09"))BeginScene(def.Find("N3_09"));
        }
        public Map02ChapterState Capture()
        {
            State.actors=roles.Select(pair=>new Map02ChapterState.ActorSnapshot{role=pair.Key,position=pair.Value.transform.position}).ToList();
            State.enemies=Enemies.Where(e=>e!=null).Select(e=>new Map02ChapterState.UnitSnapshot{id=e.id,group=e.group,position=e.transform.position,health=e.HitPoints.Current,looted=e.Looted}).ToList();
            return JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(State));
        }
        public void Cancel()
        {
            if(doingWork&&workTask?.kind=="antenna"){doingWork=false;ReleaseAntennaTraversal();}
            antennaTied=false;
            CancelEvacuationCapture();
            CancelEvacuationChoices();
            CancelNightPatrolFailure();
            UndergroundStore?.CancelTraversal();
            CancelNightRadio();
            EndScoutEscort();
            ReleaseWorkWeapon();ReleaseDayScene();ReleaseDayFreePose();CloseDay(false);EndDuck();dayReturnInput=false;dayArrivalStarting=dayDockEnding=false;
            if(health!=null&&sceneTask!=null)health.CinematicInvulnerable=protectedBefore;
            ClearEntrances();DropBarks();ReleaseConversation();ReleaseSceneAction();phaseFade=0;binocularHeld=false;binocular=false;observed=0;detectionClock=0;if(rig!=null)rig.SetBinoculars(false,1);if(combat!=null)combat.SetHandsOccupied(false);handsBusy=false;if(namPose!=null){namPose.Dispose();namPose=null;combat?.RestoreAfterCinematic();}foreach(var w in hiddenWeapons)if(w!=null)w.SetActive(true);hiddenWeapons.Clear();if(signalAudio!=null){signalAudio.Stop();Destroy(signalAudio);signalAudio=null;}voice?.Stop();if(Patching)EndPatch();EndStepsRehearsal();sceneTask=null;doingWork=false;lines=null;escort?.StopAt(escort.transform.position);escort=null;secondaryEscort?.StopAt(secondaryEscort.transform.position);secondaryEscort=null;rig?.ClearCinematicView();
        }
        void ApplyState()
        {
            Mission.World.story.stage=State.finished?Map02Stage.Complete:State.phase==0?(State.Done("D1_01")?Map02Stage.VillageHelp:Map02Stage.Contact):State.phase==2?Map02Stage.MissingScout:State.phase<5?Map02Stage.QuietPreparation:State.Done("N3_07")?Map02Stage.Breakout:Map02Stage.HoldLanes;
            bool phaseChanged=placedActors&&placedPhase!=State.phase;bool fresh=State.completed.Count==0;
            if(!placedActors||placedPhase!=State.phase)
            {
                if(!fresh)roles["Anh Thành"].StopAt(State.phase==6?sites["ditch"].position+Vector3.right*2.5f:sites["command"].position);
                roles["Anh Lực"].StopAt(State.phase==6?sites["ditch"].position+Vector3.left*2.5f:(State.phase==4?sites["ditch"].position:State.phase==5?(State.Done("N3_05C")?LucCover():LucNightThree()):sites["gate"].position));
                roles["Chị Lành"].StopAt(State.Done("N3_02")?SafeSpot("Chị Lành"):State.phase<=1?DayFamily+Vector3.left*.8f:sites["family"].position);
                roles["Ông Sáu"].StopAt(State.Done("N3_02")?SafeSpot("Ông Sáu"):State.phase<=1?DayFamily+Vector3.forward*3.5f+Vector3.left*1.4f:SauYard);   // the open yard in front: behind is the house wall   // Day 1 / Night 1: the family's seat; later days: the yard
                roles["Bé Tí"].StopAt(State.Done("N3_03")?SafeSpot("Bé Tí"):State.phase<=1?DayFamily+Vector3.left*1.3f+Vector3.forward*3.1f:sites[State.phase==5?"child":"family"].position+Vector3.back*.8f);   // Night 3: lost at the buffalo pen
                roles["Chú Bảy"].StopAt(sites["dock"].position);
                roles["Tâm"].StopAt(State.phase==5&&State.Done("N3_CRAWL")?HiddenFromTank(sites["tamCover"].position):sites[State.Done("D2_04")?"bamboo":"scout"].position);
                if(!fresh&&!State.hungDead&&escort!=roles["Hùng"])roles["Hùng"].StopAt(sites[State.phase==5?(State.Done("N3_00")?"ditch":"rest"):State.phase==4?"family":State.phase>=2?"radio":State.phase==1?(State.Done("N1_03")?"gate":State.Done("N1_02")?"bamboo":"family"):State.Done("D1_02")?"family":"arrival"].position+Vector3.right*1.4f);
                placedActors=true;placedPhase=State.phase;
            }
            roles["Hùng"].SetDead(State.hungDead);
            roles["Tâm"].SetVisible(State.phase>=2);
            roles["Tâm"].SetInjured(State.phase==2&&!State.Done("D2_04")&&State.activeTask!="D2_04");
            PlaceChildWithMother();
            if(phaseChanged){phaseFade=1.2f;player.RestoreMotion(false);TickLight(10f);}
            health.CinematicInvulnerable=State.Done("N3_WOUND")&&!State.Done("N3_CRAWL")||State.activeTask=="N3_08";
            combat.ReloadDurationMultiplier=State.wounded?1.35f:1;
            if(State.finished)health.CinematicInvulnerable=true;
            if(State.phase==5&&State.activeTask=="N3_08")health.CinematicInvulnerable=true;
            foreach(var unit in Enemies)if(unit!=null){if(State.Done("N3_09"))unit.gameObject.SetActive(false);else if(unit.group=="scout_patrol")unit.gameObject.SetActive(State.phase==2);}
            HungLocomotion();
            roles["Hùng"].PrimeLocomotion();roles["Anh Thành"].SetQuietRifle(State.phase<=1);roles["Anh Thành"].SetLocomotion(State.phase<=1?dayNamAnimator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="Idle_Tuned"):null,State.phase<=1?ordinaryWalk:null);roles["Anh Thành"].PrimeLocomotion();ArrangeDayFamily();
            SyncDayProps();RefreshMemorial();RefreshDayTwoProps();UpdateMarkers();RefreshNightPatrol();SyncNightBankState();SyncEvacuationPatrol();
        }
    }
}
