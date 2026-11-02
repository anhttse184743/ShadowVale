using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The phases from the radio call to the boat pulling away: boarding, the departure and its dialogue,
    /// skipping, and the camera views along the river.
    /// </summary>
    public sealed partial class Map01Extraction
    {
        private void EndRadio()
        {
            speech?.Stop();
            namPose?.Dispose(); namPose=null;
            foreach(var prop in radioHiddenWeapons)if(prop!=null)prop.SetActive(true);radioHiddenWeapons.Clear();
            if(radioActions!=null)radioActions.enabled=radioActionsEnabled;
            SetPlayerDrivers(true);
            cameraRig.ClearCinematicView(); mission.Cinematic=false;
            mission.ModernCombat?.RestoreAfterCinematic();
            CurrentPhase=Phase.Extraction; subtitle=null;
            ForestMenu.SuppressKeysAfterCutscene();
            Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false;
            mission.Say("Tới bến phía bắc, lên thuyền cùng Hùng và chỉ huy.",8);
        }
        private void BeginBoarding()
        {
            mission.CloseGameplayPanel(); mission.Cinematic=true; quest.BeginBoarding();
            if(mission.ModernHealth!=null)mission.ModernHealth.CinematicInvulnerable=true;
            skipHeld=0;skipReleased=false;
            SetPlayerDrivers(false);
            var controller=mission.player.GetComponent<CharacterController>();
            if(controller!=null) controller.enabled=false;
            initialPlayerPosition=mission.player.position; initialPlayerRotation=mission.player.rotation;
            urgentRun=gameplayRifleRun??namActor.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="Nam_Rifle_Run");
            if(urgentRun==null)Debug.LogError("Assign the gameplay Nam_Rifle_Run clip to extraction; a generic run is not suitable.");
            urgentJump=Resources.Load<AnimationClip>("Cutscenes/Nam_Urgent_Board_Jump");
            EnsureRifle(namActor);namBoatCarry=Map01BoatCarry.For(namActor);namBoatCarry.Draw();
            var walk=urgentRun??board;
            namPose=new PosePlayer(namActor);
            namPose.Play(walk);
            hungPose.Play(travel); commanderPose.Play(travel);
            speech=DialogueVoice.For(gameObject);
            speech.Play("m1_board_invite_current");
            subtitle=DialogueVoice.Caption("m1_board_invite_current");
            PrepareShoreActors();
            ChangePhase(Phase.Approach);
        }
        private void ChangePhase(Phase phase) { CurrentPhase=phase; clock=0; }
        private void SetPlayerDrivers(bool enabled)
        {
            if(mission.ModernPlayer!=null) mission.ModernPlayer.enabled=enabled;
            if(mission.ModernCombat!=null) mission.ModernCombat.enabled=enabled;
        }
        private void StartDeparture()
        {
            Seat(mission.player,0);
            commander.localRotation=Quaternion.identity;
            foreach(var weapon in namActor.GetComponentsInChildren<Weapon>(true)) weapon.gameObject.SetActive(weapon.IsGun);
            if(!seatedRifles.Any(p=>p.actor==namActor)) EnsureRifle(namActor);
            namPose.Play(travel);namBoatCarry??=Map01BoatCarry.For(namActor);namBoatCarry.Draw(); hungPose.Play(travel); commanderPose.Play(rowLoop);
            HidePassengerGuns();
            oarHeld=true;SpawnPursuitIfNeeded();
            speech=DialogueVoice.For(gameObject);speech.Play("m1_depart_order");
            subtitle=DialogueVoice.Caption("m1_depart_order");
            departureVoiceIndex=0;nextDepartureVoiceAt=6;
            ChangePhase(Phase.Departing); wake.Play();
        }
        private bool UpdateDepartureDialogue()
        {
            if(departureVoiceIndex<DepartureVoiceIds.Length && clock>=nextDepartureVoiceAt && !speech.HasSpeech) {
                string id=DepartureVoiceIds[departureVoiceIndex++];
                speech.Play(id);subtitle=DialogueVoice.Caption(id);
                nextDepartureVoiceAt=clock+DialogueVoice.Length(id)+.3f;
            }
            return departureVoiceIndex==DepartureVoiceIds.Length && !speech.HasSpeech && clock>=nextDepartureVoiceAt;
        }
        private void ApplyDeparture(float progress)
        {
            float rhythm=(progress+.12f*(1-Mathf.Cos(progress*Mathf.PI*15))/(Mathf.PI*15))/(1+.24f/(Mathf.PI*15));
            float eased=rhythm*rhythm*(3-2*rhythm);
            var position=RiverPoint(departurePath,eased);
            var direction=progress<.998f ? RiverPoint(departurePath,Mathf.Min(1,eased+.002f))-position
                : position-RiverPoint(departurePath,Mathf.Max(0,eased-.002f));
            boat.position=position+Vector3.up*(Mathf.Sin(progress*38)*.025f);
            if(direction.sqrMagnitude>.0001f) boat.rotation=Quaternion.LookRotation(direction.normalized)*Quaternion.Euler(0,0,Mathf.Sin(progress*29)*1.1f);
            var focus=boat.position+Vector3.up*.8f;
            View(Vector3.Lerp(boatOrigin+new Vector3(5.4f,3.1f,-3.5f),boatOrigin+new Vector3(8,5,-7),Mathf.SmoothStep(0,1,progress)),focus,Mathf.Lerp(48,58,progress));
        }
        private void FinishDeparture()
        {
            speech?.Stop();
            ApplyDeparture(1); wake.Stop(); motor.Stop();
            namPose.Play(travel);hungPose.Play(travel);commanderPose.Play(rowStop);
            namPose.Settle();hungPose.Settle();commanderPose.Settle(true);
            clock=18;LateUpdate(); // Freeze the same final grip for playback and skip.
            foreach(var enemy in pursuit)if(enemy!=null)Destroy(enemy);pursuit.Clear();
            foreach(var enemy in shoreEnemies)if(enemy!=null)enemy.enabled=true;shoreEnemies.Clear();
            if(mission.ModernHealth!=null)mission.ModernHealth.CinematicInvulnerable=false;
            CurrentPhase=Phase.Complete; subtitle=null; mission.Cinematic=false;
            quest.CompleteExtraction();
            var save=mission.GetComponent<Map01SaveSystem>();
            if(!save.AutoSaveOnExit(out var error)) { Debug.LogError(error); mission.Say(error,12); }
            ForestMenu.SuppressKeysAfterCutscene(); Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        }
        public void Skip()
        {
            if(CurrentPhase==Phase.Radio) { EndRadio(); return; }
            if(CurrentPhase<Phase.Approach || CurrentPhase>Phase.Departing) return;
            // Same final transforms, quest commit and save as natural playback. No scene load.
            if(CurrentPhase!=Phase.Departing) StartDeparture();
            FinishDeparture();
        }
        private void BoardingView() {
            var wide=new Vector3(5.4f,3.1f,83.5f);var close=new Vector3(3.5f,2.1f,84.2f);
            float detail=CurrentPhase==Phase.Boarding?Mathf.SmoothStep(0,1,clock/2):CurrentPhase==Phase.Seating?1-Mathf.SmoothStep(0,1,clock/SeatingSeconds):0;
            View(Vector3.Lerp(wide,close,detail),Vector3.Lerp(new Vector3(.7f,.7f,87),new Vector3(.25f,.85f,87),detail),Mathf.Lerp(48,44,detail));
        }
        private void View(Vector3 position,Vector3 focus,float fov) => cameraRig.SetCinematicView(position,Quaternion.LookRotation(focus-position),fov,1);
        public static Vector3 Along(Vector3[] anchors,float progress)
        {
            float p=Mathf.Clamp01(progress)*(anchors.Length-1);
            int i=Mathf.Min((int)p,anchors.Length-2);
            return Vector3.Lerp(anchors[i],anchors[i+1],p-i);
        }
        public static Vector3 RiverPoint(Vector3[] points,float progress)
        {
            float p=Mathf.Clamp01(progress)*(points.Length-1);int i=Mathf.Min((int)p,points.Length-2);float t=p-i;
            var a=points[Mathf.Max(0,i-1)];var b=points[i];var c=points[i+1];var d=points[Mathf.Min(points.Length-1,i+2)];
            return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
        }
    }
}
