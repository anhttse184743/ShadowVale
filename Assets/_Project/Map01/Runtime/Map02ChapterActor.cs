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
        Map02Mission mission;Transform follow;NavMeshPath path;int corner;float repath;
        Animator animator;AnimationClip walk,idle;Map01CinematicPose pose;
        float visualY;readonly RaycastHit[] floorHits=new RaycastHit[24];
        Vector3 goal;bool travelling;
        public Animator Animator=>animator;
        public bool ConversationHeld {get;set;}
        public void SetInjured(bool injured){var resting=GetComponent<Map02ScoutInjuredVisual>();if(resting==null&&injured)resting=gameObject.AddComponent<Map02ScoutInjuredVisual>();if(resting!=null)resting.SetInjured(injured,animator);}
        public void SetVisible(bool visible){if(visual!=null)visual.gameObject.SetActive(visible);}
        public void SetSitting(bool sitting){if(animator!=null&&animator.runtimeAnimatorController!=null&&animator.parameters.Any(p=>p.name=="Sitting"))animator.SetBool("Sitting",sitting);}
        public void Bind(Transform existing)
        {
            visual=existing;transform.position=existing.position;existing.SetParent(transform,true);
            visualY=existing.localPosition.y;
            animator=existing.GetComponentInChildren<Animator>();
            if(animator!=null&&animator.runtimeAnimatorController!=null)
            {
                var clips=animator.runtimeAnimatorController.animationClips;
                walk=clips.FirstOrDefault(c=>c.name.Contains("Walk")&&!c.name.Contains("Crouch"));idle=clips.FirstOrDefault(c=>c.name.Contains("Idle"));
            }
        }
        void LateUpdate()
        {
            if(visual==null||!visual.gameObject.activeInHierarchy)return;
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
            if(!float.IsNegativeInfinity(floor)){var local=visual.localPosition;local.y=visualY+Mathf.Clamp(floor-transform.position.y,-.6f,.6f);visual.localPosition=local;}
        }
        public void Follow(Transform player,float speed){SetInjured(false);SetSitting(false);travelling=false;follow=player;pace=speed;repath=0;}
        public void SceneMove(Vector3 position,Vector3 lookAt,float speed){transform.position=position;var look=Vector3.ProjectOnPlane(lookAt-position,Vector3.up);if(look.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(look),220*Time.deltaTime);if(animator!=null&&animator.runtimeAnimatorController!=null&&animator.parameters.Any(p=>p.name=="Speed"))animator.SetFloat("Speed",speed);}
        public void GoTo(Vector3 destination,float speed){SetInjured(false);travelling=true;follow=null;goal=destination;pace=speed;repath=0;}
        public void StopAt(Vector3 position)
        {
            travelling=false;follow=null;path=null;transform.position=position;goal=position;
            if(animator!=null&&animator.runtimeAnimatorController!=null&&animator.parameters.Any(p=>p.name=="Speed"))animator.SetFloat("Speed",0);
        }
        public void SetDead(bool dead){if(dead){travelling=false;follow=null;}if(visual!=null)visual.gameObject.SetActive(!dead);gameObject.name="Actor slot — "+role+(dead?" (đã hy sinh)":"");}
        void Update()
        {
            mission??=FindFirstObjectByType<Map02Mission>();var m=mission;if(ConversationHeld||m==null||m.Paused||m.InputBlocked||(follow==null&&!travelling))return;
            // Keep following even when Nam runs ahead. Only complete navigable routes are used.
            repath-=Time.deltaTime;if(repath<=0){repath=.5f;if(!travelling)goal=follow.position-follow.forward*1.6f;var p=new NavMeshPath();if(NavMesh.SamplePosition(goal,out var found,2,NavMesh.AllAreas))goal=found.position;if(NavMesh.CalculatePath(transform.position,goal,NavMesh.AllAreas,p)&&p.status==NavMeshPathStatus.PathComplete){path=p;corner=1;}else path=null;}
            if(travelling&&Vector3.Distance(transform.position,goal)<.3f){StopAt(goal);return;}
            var before=transform.position;
            if(path!=null&&corner<path.corners.Length&&(travelling||Vector3.Distance(transform.position,follow.position)>1.4f))
            {
                var dest=path.corners[corner];var delta=Vector3.ProjectOnPlane(dest-transform.position,Vector3.up);
                if(delta.magnitude<.12f)corner++;
                else{transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta),180*Time.deltaTime);transform.position=Vector3.MoveTowards(transform.position,dest,pace*Time.deltaTime);}
            }
            if(animator!=null&&animator.runtimeAnimatorController!=null&&animator.parameters.Any(p=>p.name=="Speed"))animator.SetFloat("Speed",Vector3.Distance(before,transform.position)/Mathf.Max(.001f,Time.deltaTime));
        }
        void OnDestroy(){pose?.Dispose();}
    }
}
