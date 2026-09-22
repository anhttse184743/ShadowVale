using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public enum ForestGuardState { Patrol, Investigate, SpotPlayer, TakeCover, Engage, Down }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ForestGuard : MonoBehaviour
    {
        public string id, squadId;
        public bool isCommander;
        public Vector3[] patrol;
        public ForestGuardState state;
        public float hp, suspicion;
        public int ShotsFired { get; private set; }
        public Vector3 LastKnownPosition => lastKnown;
        public bool HasOrder => Time.time < orderUntil;
        ForestMission mission; ForestSquadCoordinator coordinator;NavMeshAgent agent;
        bool initialized,visible,stoppedByMission;
        int waypoint,burstLeft,rounds;
        float timer,lastSeen=-999,shotAt,patrolPause,nextSense,nextPath,orderUntil;
        Vector3 lastKnown,orderPosition;
        System.Random random;
        public bool Alive => state != ForestGuardState.Down;
        ForestResearchConfig config;

        void Start()
        {
            mission=FindFirstObjectByType<ForestMission>();agent=GetComponent<NavMeshAgent>();
            if(mission==null||!mission.IsInitialized){if(mission==null)Debug.LogError("ForestGuard requires a ForestMission in the scene.",this);enabled=false;return;}
            coordinator=FindFirstObjectByType<ForestSquadCoordinator>();config=coordinator!=null?coordinator.Config:new ForestResearchConfig();
            hp=mission.GuardData.max_hp;agent.speed=mission.GuardData.move_speed;agent.stoppingDistance=.55f;
            int seed=config.seed;foreach(char ch in id??name)seed=unchecked(seed*31+ch);random=new System.Random(seed);
            rounds=config.magazineRounds;initialized=true;nextSense=Time.time+(float)random.NextDouble()*.2f;
            if(patrol!=null&&patrol.Length>0)Go(patrol[0]);
        }
        void Go(Vector3 target)
        {
            if(!agent.isOnNavMesh||Time.time<nextPath)return;
            nextPath=Time.time+.6f;
            if(NavMesh.SamplePosition(target,out var nav,2,NavMesh.AllAreas)){agent.isStopped=false;agent.SetDestination(nav.position);}
        }
        public void Hear(Vector3 location,float radius)
        {
            if(!initialized||!Alive||state>=ForestGuardState.SpotPlayer)return;
            if(Vector3.Distance(location,transform.position)>Mathf.Min(radius,mission.GuardData.hearing_range))return;
            lastKnown=location;state=ForestGuardState.Investigate;timer=mission.GuardData.alert_decay_seconds;Go(location);
        }
        public void ReceiveOrder(Vector3 destination,Vector3 reportedPosition,float observedAt,float memory)
        {
            if(!initialized||!Alive)return;
            orderPosition=destination;orderUntil=observedAt+memory;
            // Do not overwrite a more recent direct observation with a delayed radio message.
            if(observedAt>=lastSeen){lastKnown=reportedPosition;lastSeen=observedAt;}
            state=ForestGuardState.TakeCover;timer=3;nextPath=0;Go(orderPosition);mission.Alarmed=true;
        }
        public void Hit(float damage)
        {
            if(!initialized||!Alive)return;
            hp-=damage;
            if(hp<=0)
            {
                state=ForestGuardState.Down;if(agent.isOnNavMesh)agent.isStopped=true;
                foreach(var c in GetComponentsInChildren<Collider>())c.enabled=false;
                var visual=transform.Find("VisualRoot");if(visual!=null){visual.localRotation=Quaternion.Euler(0,0,90);visual.localPosition=Vector3.up*.3f;}
                var loot=gameObject.AddComponent<ForestPoint>();loot.id="loot_"+id;loot.label=isCommander?"Túi chỉ huy":"Túi lính tuần tra";loot.kind=ForestPointKind.Loot;
                loot.items=new[]{new ForestIngredient{item_id="ammo_rifle",count=12}};mission.RegisterPoint(loot);
                coordinator?.Event(isCommander?"commander_down":"guard_down",squadId,transform.position);return;
            }
            // A hit alerts the guard, but only sight/noise supplies the attacker's location.
            if(SeesPlayer())Spot();else{state=ForestGuardState.Investigate;timer=mission.GuardData.alert_decay_seconds;}
        }
        bool SeesPlayer()
        {
            var delta=mission.player.position-transform.position;
            float range=mission.GuardData.vision_range*(mission.Hidden?mission.Settings.hiddenVisionScale:1);
            if(delta.magnitude>range)return false;
            if(state<ForestGuardState.SpotPlayer&&Vector3.Angle(transform.forward,delta)>mission.GuardData.vision_angle*.5f)return false;
            return !Physics.Linecast(transform.position+Vector3.up*1.3f,mission.player.position+Vector3.up*1.1f,mission.ObstructionMask,QueryTriggerInteraction.Ignore);
        }
        void Spot()
        {
            lastKnown=mission.player.position;lastSeen=Time.time;coordinator?.Report(this,lastKnown);
            if(state>=ForestGuardState.SpotPlayer)return;
            state=ForestGuardState.SpotPlayer;timer=.45f;shotAt=Time.time+.45f+(float)random.NextDouble()*.25f;
            if(agent.isOnNavMesh)agent.isStopped=true;
            mission.Alarmed=true;mission.EmitNoise(transform.position,12);
        }
        void Face(Vector3 p){var d=p-transform.position;d.y=0;if(d.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(d),360*Time.deltaTime);}
        void FireBurst()
        {
            if(Time.time<shotAt)return;
            if(rounds<=0){rounds=config.magazineRounds;burstLeft=0;shotAt=Time.time+config.reloadSeconds;coordinator?.Event("reload",squadId,transform.position);return;}
            if(burstLeft<=0)burstLeft=config.burstRounds;
            Vector3 origin=transform.position+Vector3.up*1.25f;
            if(ForestBallistics.MuzzleBlocked(origin,transform)){shotAt=Time.time+.25f;return;}
            var direction=(lastKnown+Vector3.up*1.0f-origin).normalized;
            float spread=config.aimSpreadDegrees*(agent.velocity.sqrMagnitude>1?1.5f:1);
            direction=Quaternion.Euler(((float)random.NextDouble()-.5f)*2*spread,((float)random.NextDouble()-.5f)*2*spread,0)*direction;
            Vector3 end=origin+direction*mission.Weapon.range;bool damaged=false;
            if(ForestBallistics.Cast(origin,direction,mission.Weapon.range,transform,out var hit))
            {
                end=hit.point;
                if(hit.collider.transform==mission.player||hit.collider.transform.IsChildOf(mission.player)){mission.Damage(mission.Weapon.damage*config.guardDamageScale);damaged=true;}
            }
            mission.Trace(origin,end,new Color(1,.38f,.15f));ShotsFired++;rounds--;burstLeft--;
            shotAt=Time.time+(burstLeft>0?config.shotSpacing:config.burstPause+((float)random.NextDouble()*.25f));
            coordinator?.Event(damaged?"enemy_shot_hit":"enemy_shot",squadId,end);
        }
        void Update()
        {
            if(!initialized||mission==null||!mission.IsInitialized||!Alive)return;
            if(mission.Stopped){if(agent.isOnNavMesh)agent.isStopped=true;stoppedByMission=true;return;}
            if(stoppedByMission){if(agent.isOnNavMesh)agent.isStopped=false;stoppedByMission=false;}
            if(Time.time>=nextSense)
            {
                float elapsed=Mathf.Min(.3f,Time.time-nextSense+.18f);nextSense=Time.time+.18f;visible=SeesPlayer();
                suspicion=visible?Mathf.Min(1,suspicion+elapsed/Mathf.Max(.15f,mission.Settings.detectionSeconds)):Mathf.Max(0,suspicion-elapsed*.4f);
                if(visible&&(suspicion>=1||state>=ForestGuardState.SpotPlayer))Spot();
            }
            timer-=Time.deltaTime;
            if(state>=ForestGuardState.TakeCover&&Time.time-lastSeen>config.memorySeconds)
            {state=ForestGuardState.Investigate;timer=mission.GuardData.alert_decay_seconds;orderUntil=0;Go(lastKnown);}
            switch(state)
            {
                case ForestGuardState.Patrol:
                    if(patrol!=null&&patrol.Length>0&&agent.isOnNavMesh&&!agent.pathPending&&agent.remainingDistance<.7f)
                    {patrolPause+=Time.deltaTime;if(patrolPause>1.8f){patrolPause=0;waypoint=(waypoint+1)%patrol.Length;Go(patrol[waypoint]);}}
                    break;
                case ForestGuardState.Investigate:
                    Go(lastKnown);
                    if(timer<=0){state=ForestGuardState.Patrol;suspicion=0;if(patrol!=null&&patrol.Length>0)Go(patrol[waypoint]);}
                    break;
                case ForestGuardState.SpotPlayer:
                    Face(lastKnown);
                    if(timer<=0){state=ForestGuardState.TakeCover;timer=2;Go(HasOrder?orderPosition:mission.GuardCover(transform.position,lastKnown));}
                    break;
                case ForestGuardState.TakeCover:
                    if(visible){Face(lastKnown);FireBurst();}
                    if(timer<=0||(agent.isOnNavMesh&&!agent.pathPending&&agent.remainingDistance<.8f))state=ForestGuardState.Engage;
                    break;
                case ForestGuardState.Engage:
                    if(HasOrder&&Vector3.Distance(transform.position,orderPosition)>.9f)Go(orderPosition);
                    else if(visible){if(agent.isOnNavMesh)agent.isStopped=true;}
                    else Go(lastKnown);
                    if(visible){Face(lastKnown);FireBurst();}
                    break;
            }
        }
    }
}
