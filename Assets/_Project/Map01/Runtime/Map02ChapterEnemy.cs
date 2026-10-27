using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public sealed class Map02ChapterEnemy : MonoBehaviour
    {
        public string id,group;
        public Health HitPoints {get;private set;}
        public bool Dead=>HitPoints==null||HitPoints.IsDead;
        /// <summary>Night 3: his rounds have been picked up.</summary>
        public bool Looted {get;set;}
        /// <summary>The tank's escort: holds its spot and fires; ducks when shot at.</summary>
        public bool HoldPosition {get;set;}
        public bool Suppressed=>Time.time<suppressedUntil;
        float suppressedUntil;Transform model;
        public void Suppress(float seconds=2.5f){if(!Dead)suppressedUntil=Mathf.Max(suppressedUntil,Time.time+seconds);}
        public bool Alerted=>alerted;
        public bool TrackingVisibleTarget {get;private set;}
        public bool Investigating=>investigate.HasValue;
        public bool CanSilentTakedown(Map02Mission mission)
        {
            if(group==Map02ChapterDirector.EvacuationPatrolGroup)return false;
            if(group==Map02ChapterDirector.NightPatrolGroup&&director.NightObservationRulesActive)return false;
            if (Dead || alerted || !gameObject.activeInHierarchy || mission == null || mission.InputBlocked || mission.Skills == null) return false;
            float range = mission.Skills.TakedownRange(1.6f);
            var delta = mission.Player.position - transform.position;
            if (delta.magnitude > range || Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(delta, Vector3.up)) < 130) return false;
            return !Physics.Linecast(mission.Player.position + Vector3.up, transform.position + Vector3.up, out var hit, ~0, QueryTriggerInteraction.Ignore)
                || hit.transform == transform || hit.transform.IsChildOf(transform);
        }
        Vector3? investigate;
        public void Hear(Vector3 point,bool hostile){if(hostile){alerted=true;lastSeen=point;}else investigate=point;}
        Vector3 patrolHome;float patrolClock;
        Vector3? lastSeen;
        Map02ChapterDirector director;NavMeshAgent agent;bool alerted;float shot,repath;Animator actor;
        public void Initialize(Map02ChapterDirector owner,string key,string roster,float hp,bool hostile)
        {
            patrolHome=transform.position;director=owner;id=key;group=roster;alerted=hostile;
            var visual=roster==Map02ChapterDirector.NightPatrolGroup?Resources.Load<GameObject>("Characters/EnemyCommanderVisual"):owner.Layout!=null&&owner.Layout.enemyArt!=null?owner.Layout.enemyArt:Resources.Load<GameObject>("Characters/EnemyCommanderVisual");
            if(visual!=null){var model=Instantiate(visual,transform);model.name="Art — replaceable enemy";model.transform.localPosition=Vector3.zero;actor=model.GetComponentInChildren<Animator>();this.model=model.transform;}
            if(actor!=null)
            {
                actor.runtimeAnimatorController=Map01ActorAssets.Load().enemyController;
                actor.enabled=true;actor.applyRootMotion=false;actor.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                actor.Rebind();actor.Update(0);
                Map01EnemyGrip.Bind(this);
            }
            var body=gameObject.AddComponent<CapsuleCollider>();body.height=1.75f;body.radius=.3f;body.center=Vector3.up*.875f;
            HitPoints=gameObject.AddComponent<Health>();HitPoints.KeepCheckpointCorpse();HitPoints.BindAnimator(actor);
            if(roster==Map02ChapterDirector.NightPatrolGroup){HitPoints.SetMaxHealth(Map02ChapterDirector.NightPatrolHealth);HitPoints.AllowsLethalBulletOverride=false;HitPoints.BulletDamageModifier=(amount,point)=>amount>0?Map02ChapterDirector.NightPatrolBulletDamage:0;}
            HitPoints.RestoreHealth(hp);
            agent=gameObject.AddComponent<NavMeshAgent>();agent.height=1.75f;agent.radius=.3f;agent.speed=2.2f;agent.stoppingDistance=9;
            HitPoints.Damaged+=OnDamaged;
            if(hp<=0){body.enabled=false;agent.enabled=false;}
            HitPoints.Damaged+=_=>Suppress();
        }
        void Hold(Transform target,Vector3 from,Vector3 to,float distance,bool sees)
        {
            // Escort of the tank: no chase. Ducks while suppressed, otherwise fires at Nam (who cannot fall here).
            if(agent.isOnNavMesh)agent.isStopped=true;
            if(model!=null)model.localPosition=Vector3.Lerp(model.localPosition,Suppressed?Vector3.down*.55f:Vector3.zero,Time.deltaTime*10);
            if(sees)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(Vector3.ProjectOnPlane(to-from,Vector3.up)),220*Time.deltaTime);
            if(Suppressed||!sees||Time.time<shot)return;
            shot=Time.time+1.1f;var sound=target.GetComponent<CombatAudio>();if(sound!=null&&sound.Gunshot.IsValid){var a=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(a.clip,from,.65f*a.EffectiveGain,1,0,.85f,a.startTime,a.duration);}
            if(Physics.Raycast(from,(to-from).normalized,out var actual,distance+.2f,~0,QueryTriggerInteraction.Ignore)&&actual.transform.GetComponentInParent<Health>()==target.GetComponent<Health>())
                target.GetComponent<Health>().TakeDamage(6,actual.point,gameObject);
        }
        void OnDamaged(DamageNotice notice)
        {
            if(Dead)return;
            alerted=true;investigate=null;
            if(actor!=null&&actor.runtimeAnimatorController!=null)actor.SetTrigger("Hit");
        }
        void LateUpdate()
        {
            if(actor==null||actor.runtimeAnimatorController==null||Dead)return;
            bool moving=agent!=null&&agent.enabled&&agent.isOnNavMesh&&!agent.isStopped;
            actor.SetFloat("Speed",moving?agent.velocity.magnitude:0);
            actor.SetBool("Engaged",alerted);
            actor.SetBool("Searching",!alerted&&investigate.HasValue&&(!moving||agent.velocity.sqrMagnitude<.04f));
        }
        void OnDestroy(){if(HitPoints!=null)HitPoints.Damaged-=OnDamaged;}
        public static bool NightObservationShotHits(float roll)=>roll<.9f;
        EnemyFeedback nightCinematicFeedback;
        public void FireNightPatrolCinematic(Vector3 target)
        {
            var facing=Vector3.ProjectOnPlane(target-transform.position,Vector3.up);
            if(facing.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(facing);
            alerted=true;
            if(actor!=null&&actor.runtimeAnimatorController!=null){actor.SetBool("Engaged",true);actor.SetTrigger(PlayerCombat.AnimatorParams.Attack);}
            if(nightCinematicFeedback==null)nightCinematicFeedback=EnemyFeedback.Attach(gameObject,director.Mission.Player.GetComponent<PlayerController>());
            // Shared gunfire feedback emits the tracer from the rifle muzzle and clips it at cover.
            // This is presentation only; the cutscene still owns Nam's death and the checkpoint retry.
            nightCinematicFeedback.Fire(target,director.Mission.Player,~0,out _);
        }
        public void StopAtStoreArrival()
        {
            alerted=false;TrackingVisibleTarget=false;lastSeen=null;investigate=null;
            if(agent!=null&&agent.enabled&&agent.isOnNavMesh){agent.ResetPath();agent.isStopped=true;}
        }
        void Update()
        {
            TrackingVisibleTarget=false;
            if(Dead){if(agent!=null&&agent.enabled)agent.enabled=false;return;}
            var mission=director.Mission;
            if(group==Map02ChapterDirector.EvacuationPatrolGroup&&director.EvacuationCaptureActive)
            {director.ControlEvacuationCaptureUnit(this,agent);return;}
            if(mission.CharacterWorldBlocked||ForestMenu.Visible){if(agent.isOnNavMesh)agent.isStopped=true;return;}
            if(!agent.isOnNavMesh)return;
            if(director.ControlsEvacuationUnit(this,agent))return;
            bool night=group==Map02ChapterDirector.NightPatrolGroup;
            if(night&&director.NightPatrolStopped){StopAtStoreArrival();return;}
            // After the radio report the mission owns detection, warning and the lethal volley.
            if(night&&director.NightPatrolScriptedApproach)
            {
                StopAtStoreArrival();director.TickNightPatrolUnit(this,agent);return;
            }
            bool observationRules=night&&director.NightObservationRulesActive;
            if(observationRules&&mission.Player.GetComponent<PlayerController>()?.IsProne==true)
            {
                alerted=false;lastSeen=null;investigate=null;agent.ResetPath();
                director.TickNightPatrolUnit(this,agent);return;
            }
            if(investigate.HasValue&&!alerted&&!HoldPosition){agent.isStopped=false;agent.stoppingDistance=.5f;agent.SetDestination(investigate.Value);if(Vector3.Distance(transform.position,investigate.Value)<1)investigate=null;return;}
            var target=mission.Player;var from=transform.position+Vector3.up*1.45f;var to=PlayerStealthAdapter.For(target.gameObject).SightPoint;
            float distance=Vector3.Distance(from,to);bool sees=false;
            if(distance<(observationRules?director.NightPatrolDetectionRange:34)&&Physics.Raycast(from,(to-from).normalized,out var hit,distance+.1f,~0,QueryTriggerInteraction.Ignore))sees=hit.transform==target||hit.transform.IsChildOf(target);
            if(Map01Smoke.Blocks(from,to)||VillageRiceInstances.Conceals(target.GetComponent<PlayerController>()))sees=false;
            if(sees)lastSeen=target.position;
            if(HoldPosition){Hold(target,from,to,distance,sees);return;}
<<<<<<< HEAD
            bool night=group==Map02ChapterDirector.NightPatrolGroup;
            if(sees&&distance<(night?12:24)&&Vector3.Angle(transform.forward,to-from)<65||night&&director.NightGuardIlluminates(this))alerted=true;
            if(night&&!alerted){director.TickNightPatrolUnit(this,agent);return;}if(!alerted){agent.stoppingDistance=.3f;patrolClock+=Time.deltaTime;agent.isStopped=false;if(!agent.pathPending&&agent.remainingDistance<.6f){var point=patrolHome+Quaternion.Euler(0,(Mathf.FloorToInt(patrolClock/4)%3)*120,0)*Vector3.forward*5;if(NavMesh.SamplePosition(point,out var patrol,3,NavMesh.AllAreas))agent.SetDestination(patrol.position);}return;}
            agent.speed=2.2f;agent.stoppingDistance=sees?9:.5f;agent.isStopped=!sees&&!lastSeen.HasValue;repath-=Time.deltaTime;if(repath<=0&&lastSeen.HasValue){repath=.5f;agent.SetDestination(lastSeen.Value);}
=======
            if(observationRules)
            {
                if(director.NightGuardIlluminates(this))alerted=true;
            }
            else if(sees&&distance<(night?12:24)&&Vector3.Angle(transform.forward,to-from)<65||night&&director.NightGuardIlluminates(this))alerted=true;
            TrackingVisibleTarget=sees&&alerted;
            if(night&&!alerted){director.TickNightPatrolUnit(this,agent);return;}
            if(!alerted){agent.stoppingDistance=.3f;patrolClock+=Time.deltaTime;agent.isStopped=false;if(!agent.pathPending&&agent.remainingDistance<.6f){var point=patrolHome+Quaternion.Euler(0,(Mathf.FloorToInt(patrolClock/4)%3)*120,0)*Vector3.forward*5;if(NavMesh.SamplePosition(point,out var patrol,3,NavMesh.AllAreas))agent.SetDestination(patrol.position);}return;}
            if(observationRules){agent.ResetPath();agent.isStopped=true;}
            else
            {
                agent.stoppingDistance=sees?9:.5f;agent.isStopped=!sees&&!lastSeen.HasValue;repath-=Time.deltaTime;if(repath<=0&&lastSeen.HasValue){repath=.5f;agent.SetDestination(lastSeen.Value);}
            }
>>>>>>> Tai
            if(!sees||Time.time<shot)return;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(Vector3.ProjectOnPlane(to-from,Vector3.up)),220*Time.deltaTime);
            shot=Time.time+1.1f;if(actor!=null&&actor.runtimeAnimatorController!=null)actor.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            var sound=target.GetComponent<CombatAudio>();if(sound!=null&&sound.Gunshot.IsValid){var a=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(a.clip,from,.65f*a.EffectiveGain,1,0,.85f,a.startTime,a.duration);}
            // Every damage event requires an actual unobstructed hit, never a periodic HP tax.
            if(Physics.Raycast(from,(to-from).normalized,out var actual,distance+.2f,~0,QueryTriggerInteraction.Ignore)&&actual.transform.GetComponentInParent<Health>()==target.GetComponent<Health>()&&(!observationRules||NightObservationShotHits(Random.value)))
                target.GetComponent<Health>().TakeDamage(observationRules?target.GetComponent<Health>().Max*.5f:6,actual.point,gameObject);
        }
    }
}
