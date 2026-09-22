using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public enum ForestGuardState { Patrol, Investigate, SpotPlayer, TakeCover, Engage, Down }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ForestGuard : MonoBehaviour
    {
        public string id;
        public Vector3[] patrol;
        public ForestGuardState state;
        public float hp;
        public float suspicion;
        private ForestMission mission;
        private NavMeshAgent agent;
        private bool initialized;
        private int waypoint;
        private float timer, lastSeen, shotAt, patrolPause;
        private Vector3 lastKnown;
        public bool Alive => state != ForestGuardState.Down;

        [System.Serializable] public sealed class Snapshot
        {
            public string id;
            public Vector3 position, lastKnown, destination;
            public Quaternion rotation;
            public ForestGuardState state;
            public float hp, suspicion, timer, lastSeenAgo, shotRemaining, patrolPause;
            public int waypoint;
            public bool stopped;
        }
        public Snapshot Capture() => new Snapshot {
            id = id, position = transform.position, rotation = transform.rotation, state = state,
            hp = hp, suspicion = suspicion, timer = timer, lastKnown = lastKnown,
            lastSeenAgo = Time.time - lastSeen, shotRemaining = Mathf.Max(0, shotAt - Time.time),
            waypoint = waypoint, patrolPause = patrolPause,
            destination = agent != null && agent.isOnNavMesh && agent.hasPath ? agent.destination : transform.position,
            stopped = agent != null && agent.isOnNavMesh && agent.isStopped
        };
        public void RestoreSnapshot(Snapshot data)
        {
            if (agent.isOnNavMesh) agent.Warp(data.position); else transform.position = data.position;
            transform.rotation = data.rotation;
            if (data.state == ForestGuardState.Down) { Hit(mission.GuardData.max_hp + 1); return; }
            hp = data.hp; state = data.state; suspicion = data.suspicion;
            timer = data.timer; lastKnown = data.lastKnown; lastSeen = Time.time - data.lastSeenAgo;
            shotAt = Time.time + data.shotRemaining; patrolPause = data.patrolPause;
            waypoint = patrol.Length == 0 ? 0 : Mathf.Clamp(data.waypoint, 0, patrol.Length - 1);
            Go(data.destination);
            if (agent.isOnNavMesh) agent.isStopped = data.stopped;
        }

        private void Start()
        {
            mission = FindFirstObjectByType<ForestMission>();
            agent = GetComponent<NavMeshAgent>();
            if (mission == null || !mission.IsInitialized)
            {
                // The mission reports its configuration error once; don't cascade
                // into one NullReferenceException per guard.
                if (mission == null) Debug.LogError("ForestGuard requires a ForestMission in the scene.", this);
                enabled = false;
                return;
            }
            hp = mission.GuardData.max_hp;
            agent.speed = mission.GuardData.move_speed;
            initialized = true;
            if (patrol.Length > 0) Go(patrol[0]);
        }

        private void Go(Vector3 target)
        {
            if (agent.isOnNavMesh && NavMesh.SamplePosition(target, out var hit, 4, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        public void Hear(Vector3 location, float radius)
        {
            if (!initialized || !Alive || state >= ForestGuardState.SpotPlayer) return;
            if (Vector3.Distance(location, transform.position) > Mathf.Min(radius, mission.GuardData.hearing_range)) return;
            lastKnown = location;
            state = ForestGuardState.Investigate;
            timer = mission.GuardData.alert_decay_seconds;
            Go(location);
        }

        public void Hit(float damage)
        {
            if (!initialized || !Alive) return;
            hp -= damage;
            if (hp <= 0)
            {
                state = ForestGuardState.Down;
                if (agent.isOnNavMesh) agent.isStopped = true;
                foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
                var visual = transform.Find("VisualRoot");
                if (visual != null) { visual.localRotation = Quaternion.Euler(0, 0, 90); visual.localPosition = Vector3.up * .3f; }
                var loot = gameObject.AddComponent<ForestPoint>();
                loot.id = "loot_" + id; loot.label = "Túi lính tuần tra"; loot.kind = ForestPointKind.Loot;
                loot.items = new[] { new ForestIngredient { item_id = "ammo_rifle", count = 12 } };
                mission.RegisterPoint(loot);
                return;
            }
            Spot();
        }

        private bool SeesPlayer()
        {
            var delta = mission.player.position - transform.position;
            var range = mission.GuardData.vision_range * (mission.Hidden ? mission.Settings.hiddenVisionScale : 1);
            if (delta.magnitude > range) return false;
            if (state < ForestGuardState.SpotPlayer && Vector3.Angle(transform.forward, delta) > mission.GuardData.vision_angle * .5f) return false;
            return !Physics.Linecast(transform.position + Vector3.up * 1.25f,
                mission.player.position + Vector3.up * 1.1f, mission.ObstructionMask, QueryTriggerInteraction.Ignore);
        }

        private void Spot()
        {
            lastKnown = mission.player.position;
            lastSeen = Time.time;
            if (state >= ForestGuardState.SpotPlayer) return;
            state = ForestGuardState.SpotPlayer;
            timer = .8f;
            if (agent.isOnNavMesh) agent.isStopped = true;
            mission.Alarmed = true;
            mission.EmitNoise(transform.position, 12);
        }

        private void Update()
        {
            if (!initialized || mission == null || !mission.IsInitialized || !Alive || mission.Stopped) return;
            bool visible = SeesPlayer();
            if (visible)
            {
                suspicion += Time.deltaTime / mission.Settings.detectionSeconds;
                if (suspicion >= 1 || state >= ForestGuardState.SpotPlayer) Spot();
            }
            else suspicion = Mathf.Max(0, suspicion - Time.deltaTime * .4f);

            timer -= Time.deltaTime;
            switch (state)
            {
                case ForestGuardState.Patrol:
                    if (patrol.Length > 0 && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < .7f)
                    {
                        patrolPause += Time.deltaTime;
                        if (patrolPause > 1.8f) { patrolPause = 0; waypoint = (waypoint + 1) % patrol.Length; Go(patrol[waypoint]); }
                    }
                    break;
                case ForestGuardState.Investigate:
                    if (timer <= 0) { state = ForestGuardState.Patrol; suspicion = 0; if (patrol.Length > 0) Go(patrol[waypoint]); }
                    break;
                case ForestGuardState.SpotPlayer:
                    transform.rotation = Quaternion.LookRotation((lastKnown - transform.position).normalized);
                    if (timer <= 0)
                    {
                        state = ForestGuardState.TakeCover; timer = 3;
                        if (agent.isOnNavMesh) agent.isStopped = false;
                        Go(mission.GuardCover(transform.position, lastKnown));
                    }
                    break;
                case ForestGuardState.TakeCover:
                    if (timer <= 0 || (agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < .8f))
                    { state = ForestGuardState.Engage; }
                    break;
                case ForestGuardState.Engage:
                    if (visible)
                    {
                        if (agent.isOnNavMesh) agent.isStopped = true;
                        var direction = lastKnown - transform.position; direction.y = 0;
                        if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(direction);
                        if (Time.time >= shotAt)
                        {
                            shotAt = Time.time + mission.Settings.guardShotInterval;
                            mission.Trace(transform.position + Vector3.up * 1.2f, mission.player.position + Vector3.up, new Color(1, .38f, .15f));
                            mission.Damage(mission.Weapon.damage * mission.Settings.guardDamageScale);
                        }
                    }
                    else
                    {
                        if (agent.isOnNavMesh) agent.isStopped = false;
                        Go(lastKnown);
                        if (Time.time - lastSeen > mission.GuardData.alert_decay_seconds)
                        { state = ForestGuardState.Investigate; timer = mission.GuardData.alert_decay_seconds; }
                    }
                    break;
            }
        }
    }
}
