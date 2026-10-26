using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>Stable actor socket for the art team. The root owns navigation; Art owns rendering.</summary>
    public sealed class Map02ChapterActor : MonoBehaviour
    {
        public string role;
        public Transform visual;
        public float pace=1.35f;
        /// <summary>Set by the director while this person walks into a scene; scenes otherwise freeze everyone.</summary>
        public bool SceneDriven {get;set;}
        Map02Mission mission;Transform follow;NavMeshPath path;int corner;float repath;
        Animator animator;AnimationClip walk,idle;Map01CinematicPose pose;
        float visualY;readonly RaycastHit[] floorHits=new RaycastHit[24];
        float soleWindow=float.PositiveInfinity,soleOffset;Animator skeleton;
        public Animator Animator=>animator;
        public bool ConversationHeld {get;set;}
        public void SetInjured(bool injured){var resting=GetComponent<Map02ScoutInjuredVisual>();if(resting==null&&injured)resting=gameObject.AddComponent<Map02ScoutInjuredVisual>();if(resting!=null)resting.SetInjured(injured,animator);}
        public void SetVisible(bool visible){if(visual!=null)visual.gameObject.SetActive(visible);}
        public void SetSitting(bool sitting){if(animator!=null&&animator.runtimeAnimatorController!=null&&animator.parameters.Any(p=>p.name=="Sitting"))animator.SetBool("Sitting",sitting);}
        public bool Supporting {get;private set;}
        public bool PairedEscort {get;set;}
        bool assisted,seekingSupport;Vector3 goal;bool travelling;AnimationClip dayIdle,dayWalk;float dayPoseClock;
        AnimationClip seatedRest;
        public void SetSeatedRest(AnimationClip clip){seatedRest=clip;if(clip==null)ReleasePerformance();}
        public void SetLocomotion(AnimationClip rest,AnimationClip walking){dayIdle=rest;dayWalk=walking;if(rest==null){pose?.Dispose();pose=null;}}
        public void SamplePerformance(AnimationClip clip,float time,bool loop=true){if(animator==null||clip==null)return;pose??=new Map01CinematicPose(animator,null,true);pose.Sample(clip,time,loop);}
        public void BlendPerformance(AnimationClip first,AnimationClip second,float clock,float weight){if(animator==null)return;pose??=new Map01CinematicPose(animator,null,true);pose.SampleBlend(first,clock,second,clock,weight);}
        public void GroundPerformance(float floor){pose?.Ground(floor);}
        public void ReleasePerformance(){pose?.Dispose();pose=null;}
        void DayLocomotion(bool moving){var rest=seatedRest!=null?seatedRest:dayIdle;if(rest==null)return;dayPoseClock+=Time.deltaTime*(moving&&role=="Hùng"?pace/Map01HungVisual.LimpSpeed:1);SamplePerformance(moving&&dayWalk!=null?dayWalk:rest,dayPoseClock,true);pose?.Ground(transform.position.y);}
        public void PrimeLocomotion()=>DayLocomotion(false);
        public void Bind(Transform existing)
        {
            visual=existing;transform.SetPositionAndRotation(existing.position,existing.rotation);existing.SetParent(transform,true);
            // Character FBXs may contain Blender preview cameras/listeners. They must
            // never take over the mission view when the actor is instantiated.
            foreach(var camera in existing.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            foreach(var listener in existing.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            foreach(var light in existing.GetComponentsInChildren<Light>(true))
                if(light.name=="Light"&&light.intensity>=100)light.enabled=false; // FBX authoring preview, not a village lamp.
            visualY=existing.localPosition.y;soleWindow=float.PositiveInfinity;soleOffset=0;skeleton=null;
            animator=existing.GetComponentInChildren<Animator>();
            if(animator!=null)animator.applyRootMotion=false;
            if(role=="Anh Thành"&&animator!=null)Map01Rifle.Attach(animator);
            if(animator!=null&&animator.runtimeAnimatorController!=null)
            {
                var clips=animator.runtimeAnimatorController.animationClips;
                walk=clips.FirstOrDefault(c=>c.name.Contains("Walk")&&!c.name.Contains("Crouch"));idle=clips.FirstOrDefault(c=>c.name.Contains("Idle"));
            }
        }
        void LateUpdate()
        {
            if(PairedEscort)return;
            if(dayIdle!=null||visual==null||!visual.gameObject.activeInHierarchy)return;
            // NavMesh sockets can sit a little above the rendered village floor.
            // Correct only the visual, keeping navigation and saved actor coordinates unchanged.
            int count=Physics.RaycastNonAlloc(transform.position+Vector3.up*.6f,Vector3.down,floorHits,1.3f,~0,QueryTriggerInteraction.Ignore);
            float floor=float.NegativeInfinity;
            for(int i=0;i<count;i++)
            {
                var hit=floorHits[i];
                if(hit.normal.y<.55f||hit.transform.IsChildOf(transform)||hit.collider is CharacterController||hit.collider.GetComponentInParent<ShadowVale.Gameplay.Combat.Health>()!=null||hit.collider.GetComponentInParent<Map02ObjectiveObject>()!=null)continue;
                floor=Mathf.Max(floor,hit.point.y);
            }
            float sole=SettleSoles();
            var local=visual.localPosition;   // the sole correction holds even where no floor is found under the socket
            local.y=visualY+(float.IsNegativeInfinity(floor)?0:Mathf.Clamp(floor-transform.position.y,-.6f,.6f))-sole;visual.localPosition=local;
        }
        // Some rigs and clips (Hùng's carried-over model, his limp) keep both feet a hand's width above the visual's base.
        // The lowest toe over the last moments is what stands on the ground: lower the visual to it at once when the
        // feet come lower, and only slowly let it back, so a walk cycle does not bob. It only ever lowers.
        float SettleSoles()
        {
            if(skeleton==null)skeleton=visual.GetComponentsInChildren<Animator>().FirstOrDefault(a=>a.isHuman);   // not always the first animator
            if(skeleton==null)return 0;
            Transform Low(HumanBodyBones toes,HumanBodyBones foot)=>skeleton.GetBoneTransform(toes)??skeleton.GetBoneTransform(foot);
            var l=Low(HumanBodyBones.LeftToes,HumanBodyBones.LeftFoot);var r=Low(HumanBodyBones.RightToes,HumanBodyBones.RightFoot);if(l==null||r==null)return 0;
            float lift=Mathf.Min(l.position.y,r.position.y)-visual.position.y;   // toes above the visual's own base: the pose alone
            soleWindow=Mathf.Min(lift,soleWindow+.06f*Time.deltaTime);
            soleOffset=Mathf.MoveTowards(soleOffset,Mathf.Clamp(soleWindow-.015f,0,.25f),.5f*Time.deltaTime);
            return soleOffset;
        }
        public void SetQuietRifle(bool slung)
        {
            if(role!="Anh Thành"||animator==null)return;var rifle=animator.GetComponent<Map01Rifle>();if(rifle?.weapon==null)return;
            var desired=animator.GetBoneTransform(slung?HumanBodyBones.Chest:HumanBodyBones.RightHand);if(rifle.weapon.transform.parent==desired)return;
            rifle.weapon.transform.SetParent(desired,false);var assets=Map01ActorAssets.Load();rifle.weapon.transform.localPosition=slung?new Vector3(.25f,0,-.2f):assets.riflePosition;
            rifle.weapon.transform.localRotation=Quaternion.Euler(slung?new Vector3(90,0,15):assets.rifleEuler);
        }
        public void Follow(Transform player,float speed,bool side=false){SetInjured(false);travelling=false;follow=player;pace=speed;repath=0;assisted=side;}
        public void SceneMove(Vector3 position,Vector3 lookAt,float speed){transform.position=position;var look=Vector3.ProjectOnPlane(lookAt-position,Vector3.up);if(look.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(look),220*Time.deltaTime);if(animator!=null&&animator.runtimeAnimatorController!=null&&animator.parameters.Any(p=>p.name=="Speed"))animator.SetFloat("Speed",speed);}
        public void GoTo(Vector3 destination,float speed){SetInjured(false);travelling=true;follow=null;goal=destination;pace=speed;repath=0;}
        public void StopAt(Vector3 position)
        {
            travelling=false;follow=null;Supporting=assisted=false;path=null;transform.position=position;goal=position;SceneDriven=false;
            if(animator!=null&&animator.runtimeAnimatorController!=null&&animator.parameters.Any(p=>p.name=="Speed"))animator.SetFloat("Speed",0);
        }
        public void SetDead(bool dead){if(dead){travelling=false;follow=null;}if(visual!=null)visual.gameObject.SetActive(!dead);gameObject.name="Actor slot — "+role+(dead?" (đã hy sinh)":"");}
        void Update()
        {
            if(PairedEscort)return;
            mission??=FindFirstObjectByType<Map02Mission>();var m=mission;if(ConversationHeld||m==null||m.Paused||ForestMenu.Visible||(m.InputBlocked&&!SceneDriven))return;
            if(follow==null&&!travelling){DayLocomotion(false);return;}
            if(!travelling&&m.Chapter!=null&&m.Chapter.State.phase<=1&&Vector3.Distance(transform.position,follow.position)>7){DayLocomotion(false);return;} // Day 1 / Night 1: never pull an escort through walls or teleport to Nam. Later escorts keep following.
            repath-=Time.deltaTime;if(repath<=0){repath=.5f;if(!travelling){var side=follow.position+follow.right*.65f-follow.forward*.15f;seekingSupport=assisted&&!NavMesh.Raycast(follow.position,side,out _,NavMesh.AllAreas);Supporting=seekingSupport&&Vector3.Distance(transform.position,side)<.85f;goal=seekingSupport?side:follow.position-follow.forward*1.6f;}var p=new NavMeshPath();if(NavMesh.SamplePosition(goal,out var found,2,NavMesh.AllAreas))goal=found.position;if(NavMesh.CalculatePath(transform.position,goal,NavMesh.AllAreas,p)&&p.status==NavMeshPathStatus.PathComplete){path=p;corner=1;}else path=null;}
            if(travelling&&Vector3.Distance(transform.position,goal)<.3f){StopAt(goal);return;}
            var before=transform.position;
            if(path!=null&&corner<path.corners.Length&&(travelling||(seekingSupport?Vector3.Distance(transform.position,goal)>.18f:Vector3.Distance(transform.position,follow.position)>1.4f)))
            {
                var dest=path.corners[corner];var delta=Vector3.ProjectOnPlane(dest-transform.position,Vector3.up);
                if(delta.magnitude<.12f)corner++;
                else{transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta),180*Time.deltaTime);transform.position=Vector3.MoveTowards(transform.position,dest,pace*Time.deltaTime);}
            }
            DayLocomotion(Vector3.Distance(before,transform.position)>.0001f);
            if(dayIdle==null&&animator!=null&&animator.runtimeAnimatorController!=null&&animator.parameters.Any(p=>p.name=="Speed"))animator.SetFloat("Speed",Vector3.Distance(before,transform.position)/Mathf.Max(.001f,Time.deltaTime));
        }
        void OnDestroy(){pose?.Dispose();}
    }
}
