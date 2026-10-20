using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>The approved-law bedside knife interaction. Gameplay actors keep their original rigs and loadout.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class Map01C3BedTakedown : MonoBehaviour
    {
        public const float HearingMeters=0f, Reach=1.6f;
        public bool Active { get; private set; }
        public bool Fired { get; private set; }
        public float SourceFrame => 1+clock*(data!=null?data.fps:60);
        public Map01C3Sleeper Victim { get; private set; }
        public GameObject Pair { get; private set; }
        public event Action Shot;
        Map01Mission mission;Map01C3Mission c3;Map01BedAnimationData data;Map01CinematicPose pose;
        Map01NamActions actions;ThirdPersonCamera follow;
        readonly Dictionary<Renderer,bool> hidden=new();
        bool playerWas,combatWas,bodyWas,actionsWas,cameraWas,immuneWas;
        float clock,fov;Vector3 start,camStart;Quaternion rotation,camRotation;
        Vector3 contactCamera,aimCamera,shotCamera,contactFocus,aimFocus;
        void Awake(){mission=GetComponent<Map01Mission>();c3=GetComponent<Map01C3Mission>();data=Map01BedAnimationData.Load();}
        public Map01C3Sleeper Nearest()
        {
            if(data==null||mission==null||mission.player==null||mission.Stopped||mission.InventoryOpen||mission.MapOpen)return null;
            return c3.Sleepers.Select(g=>g.GetComponent<Map01C3Sleeper>()).Where(s=>s!=null&&s.Sleeping&&s.Guard.Alive)
                .Where(s=>Vector3.Distance(mission.player.position,s.Approach)<=Reach&&ClearApproach(s))
                .OrderBy(s=>(mission.player.position-s.Approach).sqrMagnitude).FirstOrDefault();
        }
        bool ClearApproach(Map01C3Sleeper sleeper)
        {
            var a=mission.player.position+Vector3.up*.8f;var b=sleeper.Approach+Vector3.up*.8f;
            return !Physics.Linecast(a,b,out var hit,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                ||hit.transform.IsChildOf(mission.player)||hit.transform.IsChildOf(sleeper.Guard.transform);
        }
        public bool Ready(Map01C3Sleeper target)
        {
            var bag=GetComponent<Map01Inventory>();var combat=mission.ModernCombat;
            return target!=null&&target.Sleeping&&target.Guard.Alive&&data!=null&&!Active&&!mission.Stopped
                &&!Map01NamActions.For(mission).Busy&&combat!=null&&!combat.IsReloading
                &&data.executionIsKnife&&bag.Count("knife")>0&&Vector3.Distance(mission.player.position,target.Approach)<=Reach&&ClearApproach(target);
        }
        public string Hint()
        {
            var target=Nearest();if(target==null)return null;
            return Ready(target)?"E — Bịt miệng, hạ bằng dao":"Lính đang ngủ — cần mang dao";
        }
        public bool TryBeginNearest()=>TryBegin(Nearest());
        public bool TryBegin(Map01C3Sleeper target)
        {
            if(!Ready(target))return false;
            Victim=target;Fired=false;clock=0;Active=true;
            actions=Map01NamActions.For(mission);actions.CancelAction();actionsWas=actions.enabled;actions.enabled=false;
            mission.GetComponent<Map01StoneThrow>()?.Cancel();mission.GetComponent<Map01Scouting>()?.HoldBinoculars(false);mission.CloseGameplayPanel();
            start=mission.player.position;rotation=mission.player.rotation;
            playerWas=mission.ModernPlayer.enabled;combatWas=mission.ModernCombat.enabled;bodyWas=mission.player.GetComponent<CharacterController>().enabled;
            immuneWas=mission.ModernHealth.CinematicInvulnerable;mission.ModernHealth.CinematicInvulnerable=true;
            mission.ModernPlayer.enabled=false;mission.ModernCombat.CancelReload();mission.ModernCombat.enabled=false;mission.player.GetComponent<CharacterController>().enabled=false;
            foreach(var r in mission.player.GetComponentsInChildren<Renderer>(true)){hidden[r]=r.forceRenderingOff;r.forceRenderingOff=true;}
            mission.player.SetPositionAndRotation(target.Approach,target.BedFrame.rotation*data.approachRotation);
            target.BeginExecution();Pair=Instantiate(data.pairPrefab,target.BedFrame.position,target.BedFrame.rotation);
            pose=new Map01CinematicPose(Pair.GetComponent<Animator>(),skinGrounding:false);pose.Sample(data.execution,0);
            var cam=mission.gameCamera;follow=cam.GetComponent<ThirdPersonCamera>();cameraWas=follow!=null&&follow.enabled;if(follow!=null)follow.enabled=false;
            camStart=cam.transform.position;camRotation=cam.transform.rotation;fov=cam.fieldOfView;
            var focus=target.BedFrame.TransformPoint(new Vector3(-.35f,.95f,-.2f));
            var desired=target.BedFrame.TransformPoint(new Vector3(-1.9f,1.45f,1.4f));
            float best=-1;
            foreach(var local in new[]{new Vector3(-1.9f,1.45f,1.4f),new Vector3(-2.1f,1.6f,-1.6f),new Vector3(-1.5f,1.3f,.6f),new Vector3(-2.1f,1.8f,0)})
            {
                var point=target.BedFrame.TransformPoint(local);var direction=point-focus;float length=direction.magnitude;
                var hits=Physics.RaycastAll(focus,direction.normalized,length,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                    .Where(h=>!h.transform.IsChildOf(mission.player)&&!h.transform.IsChildOf(target.Guard.transform)).ToArray();
                float clear=hits.Length==0?length:hits.Min(h=>h.distance)-.12f;
                if(clear>=length-.15f){desired=point;break;}
                if(clear>best){best=clear;desired=focus+direction.normalized*Mathf.Max(.8f,clear);}
            }
            cam.transform.SetPositionAndRotation(desired,Quaternion.LookRotation(focus-desired));cam.fieldOfView=53;
            contactCamera=desired;contactFocus=focus;
            aimFocus=target.BedFrame.TransformPoint(new Vector3(-.3f,.95f,-.65f));
            shotCamera=target.BedFrame.TransformPoint(new Vector3(-.65f,1.35f,.35f));
            aimCamera=target.BedFrame.TransformPoint(new Vector3(-1.3f,1.45f,-1.6f));
            foreach(var local in new[]{new Vector3(-1.3f,1.45f,-1.6f),new Vector3(-1.45f,1.4f,-1.45f),new Vector3(-1.2f,1.35f,-1.35f)})
            {
                var point=target.BedFrame.TransformPoint(local);var delta=point-aimFocus;
                bool blocked=Physics.RaycastAll(aimFocus,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                    .Any(h=>!h.transform.IsChildOf(mission.player)&&!h.transform.IsChildOf(target.Guard.transform));
                if(!blocked){aimCamera=point;break;}
            }
            SetPropVisibility();mission.Cinematic=true;return true;
        }
        void Update()
        {
            if(!Active||mission.Paused)return;
            clock=Mathf.Min(data.Duration,clock+Time.deltaTime);
            if(!Fired&&clock>=data.ShotSeconds)Fire();
            if(clock>=data.Duration)Finish();
        }
        void Fire()
        {
            if(GetComponent<Map01Inventory>().Count("knife")<=0){Cancel();return;}
            Fired=true;
            Victim.Guard.CommitBedKill("knife");c3.RecordSleeperDeath(Victim.Index,true);
            // No noise event, gunshot audio, magazine change, wear or wake call.
            // The corpse remains visible and can be discovered by the existing system.
            Shot?.Invoke();
        }
        void LateUpdate()
        {
            if(!Active)return;
            pose.Sample(data.execution,clock);
            SetPropVisibility();
            float u=Mathf.SmoothStep(0,1,Mathf.InverseLerp(169f/60f,224f/60f,clock));
            var camera=mission.gameCamera;var point=Vector3.Lerp(contactCamera,aimCamera,u);var focus=Vector3.Lerp(contactFocus,aimFocus,u);
            camera.fieldOfView=Mathf.Lerp(53,50,u);
            // The contact-side close-up clears the bunk ladder during the knife impact.
            if(clock>=data.ShotSeconds-.15f)
            {
                float outBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(data.ShotSeconds+.75f,data.ShotSeconds+1.6f,clock));
                point=Vector3.Lerp(shotCamera,contactCamera,outBlend);focus=Vector3.Lerp(aimFocus,contactFocus,outBlend);camera.fieldOfView=53;
            }
            camera.transform.SetPositionAndRotation(point,Quaternion.LookRotation(focus-point));
        }
        void SetPropVisibility()
        {
            var knife=Pair.transform.Find(data.executionPropPath);
            if(knife!=null)knife.gameObject.SetActive(SourceFrame>=data.executionPropVisibleFrame);
        }
        public void Cancel(){if(Active)Release(false);}
        void Finish(){if(Active)Release(true);}
        void Release(bool completed)
        {
            var target=Victim;Active=false;pose?.Dispose();pose=null;
            if(Pair!=null)Destroy(Pair);Pair=null;
            if(target!=null){if(Fired)target.HoldCorpse(true);else target.CancelExecution();}
            mission.player.SetPositionAndRotation(completed&&target!=null?target.BedFrame.TransformPoint(data.exit):start,completed&&target!=null?target.BedFrame.rotation*data.exitRotation:rotation);
            foreach(var p in hidden)if(p.Key!=null)p.Key.forceRenderingOff=p.Value;hidden.Clear();
            mission.player.GetComponent<CharacterController>().enabled=bodyWas;mission.ModernPlayer.enabled=playerWas;mission.ModernCombat.enabled=combatWas;
            mission.ModernHealth.CinematicInvulnerable=immuneWas;actions.enabled=actionsWas;
            mission.Cinematic=false;mission.ModernCombat.RestoreAfterCinematic();
            if(mission.gameCamera!=null){mission.gameCamera.transform.SetPositionAndRotation(camStart,camRotation);mission.gameCamera.fieldOfView=fov;}
            if(follow!=null)follow.enabled=cameraWas;
            Victim=null;
        }
        void OnDisable(){if(Active)Release(false);}
    }
}
