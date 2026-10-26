using ShadowVale.AI.Agents;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Fsm;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Night 3's riflemen fight with Map 1's enemy brain (the shared six-state FSM: patrol, investigate, spot, take cover,
    /// engage, retreat) and, like Map 1's squads, under the squad coordinator: each wave is a squad (its group id), and the
    /// coordinator plans it on Map 2's baked tactical graph with the session's solver (QUBO; greedy as fallback), giving each
    /// man a node and a role. Until a plan lands, or without one, cover is found on the spot from the village's own walls and
    /// trees (<see cref="Map02CoverTactics"/>). The tank's escort keeps its hold, and Day 2 / Night 2's patrols keep their
    /// stealth rules; neither is registered with the coordinator.
    /// </summary>
    public sealed partial class Map02ChapterEnemy : IEnemyWeapon, IEnemyAgent
    {
        EnemyBrain brain;NavMeshEnemyBody brainBody;float sharedAt=-10,lastFired=-10;
        /// <summary>At his cover and not shooting: down behind it, up for a moment as he fires.</summary>
        public bool InCoverDucked {get;private set;}
        /// <summary>The open battle of Night 3 (not the tank's escort).</summary>
        bool Fighting=>director!=null&&director.State.phase==5&&!HoldPosition;
        public EnemyStateId? BrainState=>brain?.State;
        Vector3 returnFireAt;float returnFireUntil;
        /// <summary>Shot at by one of Nam's comrades: for a few seconds, unless he has Nam in sight, he answers towards the shooter.</summary>
        public void UnderFireFrom(Vector3 shooter){if(brain==null||Dead)return;returnFireAt=shooter+Vector3.up*1.2f;returnFireUntil=Time.time+3;}

        static EnemyTuning NightThreeTuning()=>new EnemyTuning
        {
            VisionRange=34,DetectionSeconds=.5f,EngagedMemorySeconds=8,AttackRange=24,
            TakeCoverTimeoutSeconds=5,CoverArriveDistance=1,InvestigatePace=1.4f,HuntWhenLost=true,EngageInRangeFirst=false,
            RetreatHealthFraction=.25f,RetreatDistance=10,RetreatHoldSeconds=5,
        };
        void TickBrain(Transform target,float distance,bool sees)
        {
            if(brain==null)
            {
                brainBody=new NavMeshEnemyBody(transform,agent,null,HitPoints);   // LateUpdate animates him
                brain=new EnemyBrain(id,brainBody,this,NightThreeTuning(),Map02CoverTactics.Instance);
                AgentRegistry.Register(this);   // the coordinator plans the wave from here on
                if(alerted)brain.ShareContact(target.position,Time.time,Time.time);   // a wave comes in knowing where the lane is held
            }
            agent.stoppingDistance=.5f;
            SeesPlayer=sees;brain.Tick(new PerceptionSample(sees,distance,target.position),Time.time,Time.deltaTime);
            if(!sees&&Time.time<returnFireUntil){brainBody.Face(returnFireAt);FireSuppressive(returnFireAt);}
            alerted=alerted||brain.Memory.Engaged(Time.time);
            bool atCover=brain.Memory.HasAssignment&&brain.Memory.ReachedAssignment&&agent.velocity.sqrMagnitude<.04f&&brain.State!=EnemyStateId.Retreat;
            InCoverDucked=atCover&&Time.time<shot-.4f&&Time.time>lastFired+.35f;   // rises .4 s before a shot is due and stays up just after it
            Duck(InCoverDucked);
            // The group hears what one of them sees, at most once a second.
            if(sees&&Time.time>sharedAt+1)
            {
                sharedAt=Time.time;
                foreach(var other in director.Enemies)if(other!=null&&other!=this&&other.group==group&&!other.Dead)other.brain?.ShareContact(target.position,Time.time,Time.time);
            }
        }
        // IEnemyAgent: what the squad coordinator, telemetry and the F8 console read.
        string IEnemyAgent.Id=>id;
        public string SquadId=>brain!=null?group:"";
        public EnemyBrain Brain=>brain;
        public Vector3 Position=>transform.position;
        public Vector3 Forward=>transform.forward;
        public bool Alive=>brain!=null&&!Dead;
        public float HealthFraction=>HitPoints!=null&&HitPoints.Max>0?HitPoints.Current/HitPoints.Max:0;
        public bool SeesPlayer {get;private set;}
        float IEnemyAgent.VisionRange=>brain!=null?brain.Tuning.VisionRange:34;
        float IEnemyAgent.VisionConeAngle=>130;
        public void DebugEngage(float seconds){if(brain!=null&&director?.Mission!=null)brain.ShareContact(director.Mission.Player.position,Time.time,Time.time);}
        public void DebugDamage(float fractionOfMax){if(HitPoints!=null)HitPoints.TakeDamage(HitPoints.Max*fractionOfMax,transform.position+Vector3.up,null);}
        void OnEnable(){if(brain!=null)AgentRegistry.Register(this);}
        void OnDisable()=>AgentRegistry.Unregister(this);

        // IEnemyWeapon: the brain says when; the rifleman's own sight, aim and rate say what happens.
        public void FireAtPlayer()
        {
            if(Time.time<shot||director?.Mission==null)return;
            var target=director.Mission.Player;var from=transform.position+Vector3.up*1.45f;var to=ShadowVale.Gameplay.Player.PlayerStealthAdapter.For(target.gameObject).SightPoint;float distance=Vector3.Distance(from,to);
            shot=Time.time+1.1f;if(actor!=null&&actor.runtimeAnimatorController!=null)actor.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            Report(target,from);lastFired=Time.time;Shoot(target,from,to,distance);
        }
        /// <summary>Fire at where Nam was: noise to keep his head down, never a hit.</summary>
        public void FireSuppressive(Vector3 point)
        {
            if(Time.time<shot||director?.Mission==null)return;
            shot=Time.time+1.6f;if(actor!=null&&actor.runtimeAnimatorController!=null)actor.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            var from=transform.position+Vector3.up*1.45f;Report(director.Mission.Player,from);lastFired=Time.time;
            // The round goes somewhere near the spot, and shows where it lands.
            var aim=point+new Vector3(Random.Range(-1f,1f),Random.Range(-.4f,.8f),Random.Range(-1f,1f));
            if(Physics.Raycast(from,(aim-from).normalized,out var struck,Vector3.Distance(from,aim)+4,~0,QueryTriggerInteraction.Ignore)&&struck.transform.GetComponentInParent<Health>()==null&&!(struck.collider is CharacterController))
                CoverImpact.Play(struck.point,struck.normal);
        }
        static void Report(Transform target,Vector3 from)
        {
            var sound=target.GetComponent<CombatAudio>();
            if(sound!=null&&sound.Gunshot.IsValid){var a=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(a.clip,from,.65f*a.EffectiveGain,1,0,.85f,a.startTime,a.duration);}
        }
    }

    /// <summary>
    /// Cover found on the spot (Map 2 has no baked tactical graph): a reachable point a few metres off, from which a low line
    /// to Nam is blocked by a wall, a tree or a bank, at a range he can still fight from. The nearest such point wins.
    /// </summary>
    public sealed class Map02CoverTactics:DefaultEnemyTactics
    {
        public static readonly new Map02CoverTactics Instance=new Map02CoverTactics();
        public override bool TryFindCover(EnemyContext ctx,Vector3 threat,out Vector3 cover)
        {
            cover=default;var me=ctx.Body.Position;float best=float.PositiveInfinity;
            var toThreat=Vector3.ProjectOnPlane(threat-me,Vector3.up);var facing=toThreat.sqrMagnitude>.01f?toThreat.normalized:ctx.Body.Forward;
            for(int ring=0;ring<2;ring++)for(int i=0;i<12;i++)
            {
                var desired=me+Quaternion.Euler(0,i*30,0)*facing*(4+ring*4);
                if(!NavMesh.SamplePosition(desired,out var hit,1.5f,NavMesh.AllAreas))continue;
                var p=hit.position;float range=Vector3.Distance(p,threat);
                if(range<8||range>ctx.Tuning.AttackRange-2)continue;
                // Hidden crouching (a line at hip height is blocked by scenery), but close enough to rise and fire.
                if(!Physics.Linecast(threat+Vector3.up*1.1f,p+Vector3.up*.9f,out var block,~0,QueryTriggerInteraction.Ignore)||block.collider is CharacterController||block.collider.GetComponentInParent<Health>()!=null)continue;
                float score=Vector3.Distance(me,p);if(score<best){best=score;cover=p;}
            }
            return !float.IsPositiveInfinity(best);
        }
    }
}
