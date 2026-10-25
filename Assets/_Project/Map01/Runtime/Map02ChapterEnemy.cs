using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterEnemy : MonoBehaviour
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
        public bool Investigating=>investigate.HasValue;
        public bool CanSilentTakedown(Map02Mission mission)
        {
            if (Dead || alerted || !gameObject.activeInHierarchy || mission == null || mission.InputBlocked || mission.Skills == null) return false;
            float range = mission.Skills.TakedownRange(1.6f);
            var delta = mission.Player.position - transform.position;
            if (delta.magnitude > range || Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(delta, Vector3.up)) < 130) return false;
            return !Physics.Linecast(mission.Player.position + Vector3.up, transform.position + Vector3.up, out var hit, ~0, QueryTriggerInteraction.Ignore)
                || hit.transform == transform || hit.transform.IsChildOf(transform);
        }
        Vector3? investigate;
        public void Hear(Vector3 point,bool hostile)
        {
            if(brain!=null){if(hostile){alerted=true;brain.ShareContact(point,Time.time,Time.time);}else brain.BeginInvestigation(point,Time.time);return;}
            if(hostile){alerted=true;if(Stealth)lastSeen=point;}else investigate=point;
        }
        // Day 2 / Night 2's stealth sight: a prone or crouched Nam, rice that hides him, and a chase only to where he was last
        // seen. Night 3's fights keep the open-battle sight they were tuned with.
        bool Stealth=>director!=null&&(director.State.phase==2||director.State.phase==3);
        Vector3 patrolHome;float patrolClock;
        Vector3? lastSeen;
        float sightSince=-10;bool sawLast;
        /// <summary>
        /// A rifleman's chance to hit Nam with a shot he can see: likely up close, unlikely far off, less again when Nam runs,
        /// crouches or lies flat. The shot that comes as he first sights Nam is always a warning (25/10/2026; before, every
        /// shot with a clear line hit).
        /// </summary>
        bool Hits(Transform target,float distance)
        {
            if(Time.time-sightSince<.6f)return false;
            float p=distance<8?.75f:distance<20?Mathf.Lerp(.75f,.35f,(distance-8)/12):Mathf.Lerp(.35f,.15f,Mathf.Clamp01((distance-20)/14));
            var mover=target.GetComponent<PlayerController>();
            if(mover!=null){if(mover.IsProne)p*=.5f;else if(mover.IsSneaking)p*=.7f;p*=mover.PlanarSpeed>4?.55f:mover.PlanarSpeed>.5f?.85f:1;}
            return Random.value<p;
        }
        Map02ChapterDirector director;NavMeshAgent agent;bool alerted;float shot,repath;Animator actor;
        public void Initialize(Map02ChapterDirector owner,string key,string roster,float hp,bool hostile)
        {
            patrolHome=transform.position;director=owner;id=key;group=roster;alerted=hostile;
            var visual=owner.Layout!=null&&owner.Layout.enemyArt!=null?owner.Layout.enemyArt:Resources.Load<GameObject>("Characters/EnemyCommanderVisual");
            if(visual!=null){var model=Instantiate(visual,transform);model.name="Art — replaceable enemy";model.transform.localPosition=Vector3.zero;actor=model.GetComponentInChildren<Animator>();this.model=model.transform;}
            if(actor!=null)
            {
                actor.runtimeAnimatorController=Map01ActorAssets.Load().enemyController;
                actor.enabled=true;actor.applyRootMotion=false;actor.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                actor.Rebind();actor.Update(0);
                Map01EnemyGrip.Bind(this);
            }
            var body=gameObject.AddComponent<CapsuleCollider>();body.height=1.75f;body.radius=.3f;body.center=Vector3.up*.875f;
            HitPoints=gameObject.AddComponent<Health>();HitPoints.KeepCheckpointCorpse();HitPoints.BindAnimator(actor);HitPoints.RestoreHealth(hp);
            agent=gameObject.AddComponent<NavMeshAgent>();agent.height=1.75f;agent.radius=.3f;agent.speed=2.2f;agent.stoppingDistance=9;
            HitPoints.Damaged+=OnDamaged;
            if(hp<=0){body.enabled=false;agent.enabled=false;}
            HitPoints.Damaged+=_=>Suppress();
        }
        void Hold(Transform target,Vector3 from,Vector3 to,float distance,bool sees)
        {
            // Escort of the tank: no chase. Ducks while suppressed, otherwise fires at Nam (who cannot fall here).
            if(agent.isOnNavMesh)agent.isStopped=true;
            Duck(Suppressed);
            if(sees||!Stealth)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(Vector3.ProjectOnPlane(to-from,Vector3.up)),220*Time.deltaTime);
            if(Suppressed||!sees||Time.time<shot)return;
            shot=Time.time+1.1f;var sound=target.GetComponent<CombatAudio>();if(sound!=null&&sound.Gunshot.IsValid){var a=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(a.clip,from,.65f*a.EffectiveGain,1,0,.85f,a.startTime,a.duration);}
            Shoot(target,from,to,distance);
        }
        /// <summary>
        /// One round at Nam: aimed at him when <see cref="Hits"/> says it hits, a little wide when it misses. Whatever it meets
        /// first takes it: Nam, or the wall, tree or bank in the way (dust, sparks and the smack of it), so cover visibly works.
        /// </summary>
        void Shoot(Transform target,Vector3 from,Vector3 to,float distance)
        {
            bool hit=Hits(target,distance);var aim=to;
            if(!hit){var side=Vector3.Cross(Vector3.up,(to-from).normalized);aim+=side*(Random.value<.5f?-1:1)*Random.Range(.35f,1f)*(.6f+distance*.03f)+Vector3.up*Random.Range(-.6f,.5f);}
            if(!Physics.Raycast(from,(aim-from).normalized,out var struck,distance+8,~0,QueryTriggerInteraction.Ignore))return;
            var health=struck.transform.GetComponentInParent<Health>();
            if(health!=null){if(hit&&health==target.GetComponent<Health>())health.TakeDamage(6,struck.point,gameObject);return;}
            if(!(struck.collider is CharacterController))CoverImpact.Play(struck.point,struck.normal);
        }
        /// <summary>Down behind cover (the enemy rig has no crouch clip: the body drops behind it) and his collider with him.</summary>
        void Duck(bool down)
        {
            if(model!=null)model.localPosition=Vector3.Lerp(model.localPosition,down?Vector3.down*.55f:Vector3.zero,Time.deltaTime*10);
            if(capsule==null)capsule=GetComponent<CapsuleCollider>();
            if(capsule!=null){float h=down?1.2f:1.75f;if(Mathf.Abs(capsule.height-h)>.01f){capsule.height=h;capsule.center=Vector3.up*h/2;}}
        }
        CapsuleCollider capsule;
        void OnDamaged(DamageNotice notice)
        {
            if(Dead)return;
            alerted=true;investigate=null;brain?.OnHurtByPlayer(notice.Source!=null?notice.SourcePosition:director.Mission.Player.position,Time.time);   // he turns to whoever shot him
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
        void Update()
        {
            if(Dead){if(agent!=null&&agent.enabled)agent.enabled=false;return;}
            var mission=director.Mission;
            if(mission.CharacterWorldBlocked||ForestMenu.Visible){if(agent.isOnNavMesh)agent.isStopped=true;return;}
            if(!agent.isOnNavMesh)return;
            if(investigate.HasValue&&!alerted&&!HoldPosition&&!Fighting){agent.isStopped=false;agent.stoppingDistance=.5f;agent.SetDestination(investigate.Value);if(Vector3.Distance(transform.position,investigate.Value)<1)investigate=null;return;}
            var target=mission.Player;var from=transform.position+Vector3.up*1.45f;var to=PlayerStealthAdapter.For(target.gameObject).SightPoint;   // standing, crouched (behind low cover) or prone
            float distance=Vector3.Distance(from,to);bool sees=false;
            if(distance<34&&Physics.Raycast(from,(to-from).normalized,out var hit,distance+.1f,~0,QueryTriggerInteraction.Ignore))sees=hit.transform==target||hit.transform.IsChildOf(target);
            if(Map01Smoke.Blocks(from,to)||Stealth&&VillageRiceInstances.Conceals(target.GetComponent<PlayerController>()))sees=false;
            if(sees&&Stealth)lastSeen=target.position;
            if(sees&&!sawLast)sightSince=Time.time;sawLast=sees;
            if(HoldPosition){Hold(target,from,to,distance,sees);return;}
            if(Fighting){TickBrain(target,distance,sees);return;}
            if(sees&&distance<24&&Vector3.Angle(transform.forward,to-from)<65)alerted=true;if(!alerted){agent.stoppingDistance=.3f;patrolClock+=Time.deltaTime;agent.isStopped=false;if(!agent.pathPending&&agent.remainingDistance<.6f){var point=patrolHome+Quaternion.Euler(0,(Mathf.FloorToInt(patrolClock/4)%3)*120,0)*Vector3.forward*5;if(NavMesh.SamplePosition(point,out var patrol,3,NavMesh.AllAreas))agent.SetDestination(patrol.position);}return;}
            if(Stealth){agent.stoppingDistance=sees?9:.5f;agent.isStopped=!sees&&!lastSeen.HasValue;repath-=Time.deltaTime;if(repath<=0&&lastSeen.HasValue){repath=.5f;agent.SetDestination(lastSeen.Value);}}
            else{agent.stoppingDistance=9;agent.isStopped=false;repath-=Time.deltaTime;if(repath<=0){repath=.5f;agent.SetDestination(target.position);}}
            if(!sees||Time.time<shot)return;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(Vector3.ProjectOnPlane(to-from,Vector3.up)),220*Time.deltaTime);
            shot=Time.time+1.1f;if(actor!=null&&actor.runtimeAnimatorController!=null)actor.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            var sound=target.GetComponent<CombatAudio>();if(sound!=null&&sound.Gunshot.IsValid){var a=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(a.clip,from,.65f*a.EffectiveGain,1,0,.85f,a.startTime,a.duration);}
            // Every damage event requires an actual unobstructed hit, never a periodic HP tax.
            Shoot(target,from,to,distance);
        }
    }
}
