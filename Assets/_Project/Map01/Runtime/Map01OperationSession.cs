using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Owns a temporary hands-free interaction and restores the exact gameplay owner.</summary>
    public sealed class Map01OperationSession : IDisposable
    {
        readonly Map01Mission mission;
        readonly Map01NamActions actions;
        readonly bool playerEnabled,combatEnabled,actionsEnabled,cinematic,invulnerable;
        readonly ThirdPersonCamera rig;
        readonly Vector3 position,cameraPosition;
        readonly Quaternion rotation,cameraRotation;
        readonly float fov;
        readonly List<GameObject> weapons=new();
        readonly Animator actor;
        Map01CinematicPose pose;bool authoredSkeleton;
        readonly AnimationClip idle;
        bool disposed;
        public Animator Actor=>actor;
        public static Vector3 GroundPoint(Vector3 point,Transform player)
        {
            float floor=float.NegativeInfinity;
            foreach(var hit in Physics.RaycastAll(point+Vector3.up*.6f,Vector3.down,2,~0,QueryTriggerInteraction.Ignore))
                if(hit.normal.y>.65f&&hit.point.y<=point.y+.25f&&(player==null||!hit.transform.IsChildOf(player))&&hit.transform.GetComponentInParent<Health>()==null)floor=Mathf.Max(floor,hit.point.y);
            if(!float.IsNegativeInfinity(floor))point.y=floor;return point;
        }
        public Map01OperationSession(Map01Mission owner)
        {
            mission=owner; position=owner.player.position;rotation=owner.player.rotation;
            playerEnabled=owner.ModernPlayer.enabled;combatEnabled=owner.ModernCombat.enabled;
            actions=Map01NamActions.For(owner);actionsEnabled=actions!=null&&actions.enabled;
            cinematic=owner.Cinematic;invulnerable=owner.ModernHealth.CinematicInvulnerable;
            cameraPosition=owner.gameCamera.transform.position;cameraRotation=owner.gameCamera.transform.rotation;fov=owner.gameCamera.fieldOfView;
            rig=owner.gameCamera.GetComponent<ThirdPersonCamera>();
            owner.CloseGameplayPanel();owner.GetComponent<Map01StoneThrow>()?.Cancel();owner.GetComponent<Map01Scouting>()?.HoldBinoculars(false);
            DialogueVoice.StopAll();owner.Say(null,0);
            owner.ModernPlayer.enabled=false;owner.ModernCombat.enabled=false;if(actions!=null)actions.enabled=false;
            owner.player.position=GroundPoint(owner.player.position,owner.player);
            owner.Cinematic=true;owner.ModernHealth.CinematicInvulnerable=true;
            actor=owner.player.GetComponentInChildren<Animator>();
            idle=actor.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name=="Nam_Shelter_Idle")??Resources.Load<AnimationClip>("Rescue/Nam_Shelter_Idle");
            foreach(var weapon in owner.player.GetComponentsInChildren<Weapon>(true))if(weapon.gameObject.activeSelf){weapons.Add(weapon.gameObject);weapon.gameObject.SetActive(false);}
            pose=new Map01CinematicPose(actor);
            foreach(var e in owner.Enemies)if(e!=null&&e.Alive)e.PauseForCinematic(true);
            Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
        }
        public void Sample(AnimationClip clip,float time,Vector3 focus,float blend=1)
        {
            if(disposed)return;
            bool authored=clip!=null&&clip.name=="Nam_SupplyCollect";
            if(authored!=authoredSkeleton){pose.Dispose();pose=new Map01CinematicPose(actor,authored?Resources.Load<Avatar>("OperationsAnimation/Nam_SupplyCollectAvatar"):null);authoredSkeleton=authored;}
            if(clip!=null&&clip.name=="Nam_RadioPanel"&&time<.7f)pose.SampleBlend(idle,time,clip,time,Mathf.SmoothStep(0,1,time/.7f));
            else pose.Sample(clip??idle,time,clip==null||clip.isLooping);
            pose.Ground(mission.player.position.y);
            var forward=mission.player.forward;var right=mission.player.right;
            focus=(focus+mission.player.position+Vector3.up*1.25f)*.5f;
            var eye=mission.player.position+Vector3.up*1.65f+forward*.25f+right*1.02f;
            var delta=eye-focus;
            foreach(var hit in Physics.SphereCastAll(focus,.12f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore))
                if(hit.transform.GetComponentInParent<Health>()==null&&hit.distance>.65f)eye=focus+delta.normalized*Mathf.Min(delta.magnitude,Mathf.Max(.7f,hit.distance-.12f));
            blend=Mathf.SmoothStep(0,1,blend);
            rig.SetCinematicView(Vector3.Lerp(cameraPosition,eye,blend),Quaternion.Slerp(cameraRotation,Quaternion.LookRotation(focus-eye),blend),Mathf.Lerp(fov,68,blend),1);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;pose.Dispose();
            if(mission==null||mission.ModernPlayer==null)return;
            foreach(var w in weapons)if(w!=null)w.SetActive(true);
            mission.ModernPlayer.enabled=playerEnabled;mission.ModernCombat.enabled=combatEnabled;if(actions!=null)actions.enabled=actionsEnabled;
            mission.ModernHealth.CinematicInvulnerable=invulnerable;mission.Cinematic=cinematic;
            mission.ModernPlayer.RestoreMotion(mission.Crouched);mission.ModernCombat.RestoreAfterCinematic();rig.ClearCinematicView();
            mission.GetComponent<Map01Inventory>().SuppressFire=true;ForestMenu.SuppressKeysAfterCutscene();Cursor.visible=false;Cursor.lockState=CursorLockMode.Locked;
        }
    }
}
