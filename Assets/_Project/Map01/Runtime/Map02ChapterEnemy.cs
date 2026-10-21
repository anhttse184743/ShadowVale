using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Audio;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public sealed class Map02ChapterEnemy : MonoBehaviour
    {
        public string id,group;
        public Health HitPoints {get;private set;}
        public bool Dead=>HitPoints==null||HitPoints.IsDead;
        Vector3 patrolHome;float patrolClock;
        Map02ChapterDirector director;NavMeshAgent agent;bool alerted;float shot,repath;Animator actor;
        public void Initialize(Map02ChapterDirector owner,string key,string roster,float hp,bool hostile)
        {
            patrolHome=transform.position;director=owner;id=key;group=roster;alerted=hostile;
            var visual=owner.Layout!=null&&owner.Layout.enemyArt!=null?owner.Layout.enemyArt:Resources.Load<GameObject>("Characters/EnemyCommanderVisual");
            if(visual!=null){var model=Instantiate(visual,transform);model.name="Art — replaceable enemy";model.transform.localPosition=Vector3.zero;actor=model.GetComponentInChildren<Animator>();}
            var body=gameObject.AddComponent<CapsuleCollider>();body.height=1.75f;body.radius=.3f;body.center=Vector3.up*.875f;
            HitPoints=gameObject.AddComponent<Health>();HitPoints.KeepCheckpointCorpse();HitPoints.BindAnimator(actor);HitPoints.RestoreHealth(hp);
            agent=gameObject.AddComponent<NavMeshAgent>();agent.height=1.75f;agent.radius=.3f;agent.speed=2.2f;agent.stoppingDistance=9;
            if(hp<=0){body.enabled=false;agent.enabled=false;}
        }
        void Update()
        {
            if(Dead){if(agent!=null&&agent.enabled)agent.enabled=false;return;}
            var mission=director.Mission;
            if(mission.InputBlocked||mission.Paused||ForestMenu.Visible){if(agent.isOnNavMesh)agent.isStopped=true;return;}
            if(!agent.isOnNavMesh)return;
            var target=mission.Player;var from=transform.position+Vector3.up*1.45f;var to=target.position+Vector3.up;
            float distance=Vector3.Distance(from,to);bool sees=false;
            if(distance<34&&Physics.Raycast(from,(to-from).normalized,out var hit,distance+.1f,~0,QueryTriggerInteraction.Ignore))sees=hit.transform==target||hit.transform.IsChildOf(target);
            if(sees&&distance<24&&Vector3.Angle(transform.forward,to-from)<65)alerted=true;if(!alerted){agent.stoppingDistance=.3f;patrolClock+=Time.deltaTime;agent.isStopped=false;if(!agent.pathPending&&agent.remainingDistance<.6f){var point=patrolHome+Quaternion.Euler(0,(Mathf.FloorToInt(patrolClock/4)%3)*120,0)*Vector3.forward*5;if(NavMesh.SamplePosition(point,out var patrol,3,NavMesh.AllAreas))agent.SetDestination(patrol.position);}return;}
            agent.stoppingDistance=9;agent.isStopped=false;repath-=Time.deltaTime;if(repath<=0){repath=.5f;agent.SetDestination(target.position);}
            if(actor!=null&&actor.runtimeAnimatorController!=null)actor.SetFloat("Speed",agent.velocity.magnitude);
            if(!sees||Time.time<shot)return;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(Vector3.ProjectOnPlane(to-from,Vector3.up)),220*Time.deltaTime);
            shot=Time.time+1.1f;var sound=target.GetComponent<CombatAudio>();if(sound!=null&&sound.Gunshot.IsValid){var a=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(a.clip,from,.65f*a.EffectiveGain,1,0,.85f,a.startTime,a.duration);}
            // Every damage event requires an actual unobstructed hit, never a periodic HP tax.
            if(Physics.Raycast(from,(to-from).normalized,out var actual,distance+.2f,~0,QueryTriggerInteraction.Ignore)&&actual.transform.GetComponentInParent<Health>()==target.GetComponent<Health>())
                target.GetComponent<Health>().TakeDamage(6,actual.point,gameObject);
        }
    }
}
