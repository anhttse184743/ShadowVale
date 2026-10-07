using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public sealed class Map01EnemyController : MonoBehaviour
    {
        [SerializeField] private Vector3[] patrolPoints = System.Array.Empty<Vector3>();
        [SerializeField] private float visionRange = 24f;
        [Tooltip("Field of view while unaware; once engaged a guard keeps track of Nam all around.")]
        [SerializeField] private float visionAngle = 110f;
        [SerializeField] private float attackRange = 18f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float fireInterval = 0.65f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        private NavMeshAgent _agent;
        private Animator _animator;
        private Health _health;
        private Transform _player;
        private Health _playerHealth;
        private int _patrolIndex;
        private float _nextShot;
        private Map01Mission _mission;
        private float _alertUntil;
        private Vector3 _investigate;
        private bool _lootCreated;
        private float _suspicion, _engagedUntil, _calmUntil;
        private float _detectionSeconds = 1.5f, _hiddenScale = .32f, _crouchScale = .6f;
        private Map01Rescue _rescue;
        private int _shots;
        private Vector3 _postPosition;
        private Quaternion _postRotation;
        // Hearing something, or losing sight of Nam, sends a guard to look: there, a look around,
        // then back to where he was — his post, or the spot on his patrol.
        private const float InvestigateTimeout = 45f; // A bound only; the look-around ends it sooner.
        private float _searchUntil, _searchSeconds = 8f, _baseSpeed, _lastHealth;
        private bool _returning;
        private bool _rescuePost, _overseer, _pursuer, _holdingAI;
        private Map01RescueLayout _rescueTuning;
        private float _hearReactionAt;
        private Vector3 _pendingFootstep;
        private float _patrolPause, _pauseUntil, _pausedAt, _animatorSpeedBeforeHold;
        private bool _agentUpdatesPosition;
        private bool _agentUpdatesRotation;
        private Quaternion _heldRotation;
        private Vector3 _heldPosition;
        public event System.Action<Map01Detection> Detected;
        public bool IsPursuer => _pursuer;
        public void PauseForCinematic(bool animate=false)
        {
            if(!Alive)return;
            if(!_holdingAI){_holdingAI=true;_pausedAt=Time.time;_animatorSpeedBeforeHold=_animator!=null?_animator.speed:1;
                _heldPosition=transform.position;_heldRotation=transform.rotation;_agentUpdatesPosition=_agent!=null&&_agent.updatePosition;_agentUpdatesRotation=_agent!=null&&_agent.updateRotation;}
            if(_agent!=null&&_agent.isOnNavMesh)_agent.isStopped=true;
            if(_agent!=null)_agent.updatePosition=false;
            if(_agent!=null)_agent.updateRotation=false;
            if(_animator!=null)_animator.speed=animate?1:0;
        }
        private readonly System.Collections.Generic.HashSet<string> _animatorParams = new System.Collections.Generic.HashSet<string>();
        private bool _startled;
        private Vector3 _returnPoint;
        private Quaternion _returnRotation;
        public bool Alive => !(_health != null ? _health : GetComponent<Health>()).IsDead; // Asked every frame, all over.
        /// <summary>Checking out a noise or the last place he saw Nam — there, or looking around.</summary>
        public bool Alerted => Time.time < _alertUntil;
        /// <summary>At the spot, looking around.</summary>
        public bool Searching => Alerted && _searchUntil > 0;
        /// <summary>Done looking and walking back to where he was.</summary>
        public bool Returning => _returning;
        public Vector3 ReturnPoint => _returnPoint;
        /// <summary>Goes up every time he is hurt (lethally or not) — for whoever needs to notice attacks.</summary>
        public int HurtCount { get; private set; }
        /// <summary>The last hurt was a silent knife takedown from behind.</summary>
        public bool TakenDownSilently { get; private set; }
        /// <summary>0..1 — how sure this guard is that he has seen Nam; 1 means spotted.</summary>
        public float Suspicion => _suspicion;
        /// <summary>Spotted Nam and fighting him — refreshed while he stays in sight.</summary>
        public bool Engaged => Time.time < _engagedUntil;
        public string LegacyId => name.StartsWith("Outpost guard ", System.StringComparison.Ordinal)
            ? "expansion_guard_" + name.Substring("Outpost guard ".Length) : name;
        public string SaveId {
            get {
                string path = name;
                for (var parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
                return path;
            }
        }
        public void BindMission(Map01Mission mission)
        {
            _mission = mission;
            Map01Rifle.Attach(GetComponentInChildren<Animator>());
            GetComponent<Health>().KeepCheckpointCorpse();
            // Map 1's guard tuning lives in Map01Balance.json, not in each guard's serialized fields.
            damage = mission.Weapon.damage * mission.Settings.guardDamageScale;
            fireInterval = mission.Settings.guardShotInterval;
            _detectionSeconds = Mathf.Max(.1f, mission.Settings.detectionSeconds);
            _hiddenScale = mission.Settings.hiddenVisionScale;
            _crouchScale = mission.Settings.crouchVisionScale;
            _searchSeconds = mission.Settings.searchSeconds > 0 ? mission.Settings.searchSeconds : 8f;
            _rescue = mission.GetComponent<Map01Rescue>(); // Added by Map01Quest.Awake, before this runs.
        }

        /// <summary>
        /// Back to the post at full health and calm — a failed scouting run means the camp was
        /// reinforced, so even a guard Nam took down is replaced, loot and all. Calm for a few
        /// seconds so Nam gets to slip away instead of being re-spotted on the spot.
        /// </summary>
        public void ReturnToPost()
        {
            TakenDownSilently=false;SetSilent(false); ShowRifle(true); _startled = false;
            if (_health.IsDead) _health.Revive(); else _health.RestoreHealth(_health.Max);
            if (_lootCreated)
            {
                _lootCreated = false;
                var loot = GetComponent<ForestPoint>();
                if (loot != null) { _mission.UnregisterPoint(loot); Destroy(loot); } // _lootCreated implies a bound mission.
            }
            _lastHealth = _health.Current;
            if (_agent.isOnNavMesh) _agent.Warp(_postPosition); else transform.position = _postPosition;
            transform.rotation = _postRotation;
            _suspicion = 0; _alertUntil = 0; _engagedUntil = 0; _hearReactionAt=0;
            _searchUntil = 0; _returning = false; SetPace(1f);
            _calmUntil = Time.time + 4f;
            _patrolIndex = 0;
            if (patrolPoints.Length > 0) Go(patrolPoints[0]);
        }
        /// <summary>How far he sees Nam standing in the open.</summary>
        public float VisionRange => visionRange;
        /// <summary>His patrol loop; empty for a guard who holds his post.</summary>
        public System.Collections.Generic.IReadOnlyList<Vector3> PatrolPoints => patrolPoints;
        public float DamagePerShot => damage;
        public float FireInterval => fireInterval;

        /// <summary>A noise at <paramref name="position"/> that carries <paramref name="radius"/>
        /// metres: within it, the guard goes to see what it was. True if he heard it.</summary>
        public bool Hear(Vector3 position, float radius, Map01NoiseKind kind = Map01NoiseKind.Other)
        {
            bool rescueStealth=_rescuePost&&_rescue!=null&&_rescue.CurrentPhase==Map01Rescue.Phase.Captive;
            if(rescueStealth&&kind==Map01NoiseKind.Footstep&&_mission.Crouched)
                radius*=(_rescueTuning!=null?_rescueTuning.crouchedFootstepScale:.55f);
            if (!Alive || Vector3.Distance(position, transform.position) > radius) return false;
            if (_mission != null && _mission.Cinematic) return false;
            if (kind == Map01NoiseKind.Gunshot) ReportDetection(Map01DetectionCause.Gunshot, position);
            if(rescueStealth&&kind==Map01NoiseKind.Footstep&&!Engaged){
                // The first audible step starts the reaction clock. Further steps update its
                // source without extending it indefinitely; rocks and gunshots stay immediate.
                _pendingFootstep=position;
                if(_hearReactionAt<=0)_hearReactionAt=Time.time+(_rescueTuning!=null?_rescueTuning.footstepReactionSeconds:1f);
                return true;
            }
            _hearReactionAt=0;
            if (_mission == null || !_mission.Cinematic) BeginInvestigation(position);
            return true;
        }

        private void BeginInvestigation(Vector3 position)
        {
            // Where to come back to is kept from the first thing he heard, not from wherever the
            // next noise catches him on the way.
            if (!Alerted && !_returning) { _returnPoint = transform.position; _returnRotation = transform.rotation; }
            _returning = false;
            if (_overseer && _rescue != null && _rescue.CurrentPhase == Map01Rescue.Phase.Captive)
            {
                var delta = Vector3.ProjectOnPlane(position - _postPosition, Vector3.up);
                position = _postPosition + Vector3.ClampMagnitude(delta, 1.25f);
            }
            _investigate = position; _searchUntil = 0;
            _alertUntil = Time.time + InvestigateTimeout;
        }
        [System.Serializable] public sealed class Snapshot
        {
            public string id;
            public Vector3 position, destination, investigate, returnPoint;
            public Quaternion rotation, returnRotation;
            public float hp, shotRemaining, alertRemaining, searchRemaining;
            public int patrolIndex;
            public bool stopped, returning, silent, pursuer;
            public float suspicion, engagedRemaining, pauseRemaining;
            public float footstepReactionRemaining;
            public Vector3 pendingFootstep;
        }
        public Snapshot Capture() => new Snapshot {
            id = SaveId, position = transform.position, rotation = transform.rotation, hp = _health.Current,
            silent = TakenDownSilently, pursuer = _pursuer, suspicion = _suspicion,
            engagedRemaining = Mathf.Max(0, _engagedUntil-Time.time), pauseRemaining = Mathf.Max(0,_pauseUntil-Time.time),
            footstepReactionRemaining=_hearReactionAt>0?Mathf.Max(0,_hearReactionAt-Time.time):0,pendingFootstep=_pendingFootstep,
            patrolIndex = _patrolIndex, shotRemaining = Mathf.Max(0, _nextShot - Time.time),
            alertRemaining = Mathf.Max(0, _alertUntil - Time.time), investigate = _investigate,
            searchRemaining = _searchUntil > 0 ? Mathf.Max(.01f, _searchUntil - Time.time) : 0,
            returning = _returning, returnPoint = _returnPoint, returnRotation = _returnRotation,
            destination = _agent.isOnNavMesh && _agent.hasPath ? _agent.destination : transform.position,
            stopped = _agent.isOnNavMesh && _agent.isStopped
        };
        /// <summary>This guard as he would be fresh at his post: alive, calm, starting his patrol.</summary>
        public Snapshot CaptureAtPost() => new Snapshot {
            id = SaveId, position = _postPosition, rotation = _postRotation, hp = _health.Max,
            destination = _postPosition, patrolIndex = 0
        };
        public void RestoreSnapshot(Snapshot saved)
        {
            if (_agent.isOnNavMesh) _agent.Warp(saved.position); else transform.position = saved.position;
            transform.rotation = saved.rotation;
            TakenDownSilently = saved.silent; SetSilent(saved.silent); ShowRifle(!saved.silent);
            _health.RestoreHealth(saved.hp);
            _suspicion = saved.suspicion; _engagedUntil = Time.time + saved.engagedRemaining;
            _pauseUntil = Time.time + saved.pauseRemaining; _pursuer = saved.pursuer;
            _hearReactionAt=saved.footstepReactionRemaining>0?Time.time+saved.footstepReactionRemaining:0;_pendingFootstep=saved.pendingFootstep;
            _lastHealth = _health.Current;
            _patrolIndex = Mathf.Clamp(saved.patrolIndex, 0, Mathf.Max(0, patrolPoints.Length - 1));
            _nextShot = Time.time + saved.shotRemaining;
            _alertUntil = Time.time + saved.alertRemaining; _investigate = saved.investigate;
            _searchUntil = saved.searchRemaining > 0 ? Time.time + saved.searchRemaining : 0;
            _returning = saved.returning; _returnPoint = saved.returnPoint; _returnRotation = saved.returnRotation;
            Go(saved.destination);
            if (_agent.isOnNavMesh) _agent.isStopped = saved.stopped || !Alive;
            if (!Alive) CreateLoot();
        }
        public void ReceiveExtractionHit(float amount, Vector3 point)
        {
            // Film combat changes real health/death state, but grants no extra loot or takedown credit.
            if(!Alive)return;
            if(amount>=_health.Current)_lootCreated=true;
            _health.TakeDamage(amount,point,null);_lastHealth=_health.Current;
            if(_health.IsDead && _agent.isOnNavMesh) {_agent.ResetPath();_agent.isStopped=true;}
        }
        private void CreateLoot()
        {
            if (_lootCreated || _mission == null) return;
            _lootCreated = true;
            var loot = gameObject.AddComponent<ForestPoint>();
            loot.id = "loot_" + LegacyId; loot.label = "Túi lính tuần tra"; loot.kind = ForestPointKind.Loot;
            loot.items = new[] {
                new ForestIngredient { item_id = "ammo_rifle", count = _mission.Settings.guardDropAmmo },
                new ForestIngredient { item_id = "medkit_small", count = _mission.Settings.guardDropMedkits }
            };
            _mission.RegisterPoint(loot);
        }

        public void Configure(Vector3[] points) => patrolPoints = points ?? System.Array.Empty<Vector3>();

        public void ConfigureRescuePost(Transform post, Vector3[] points, bool overseer, Map01RescueLayout layout)
        {
            _rescuePost=true;_overseer=overseer;_pursuer=false;
            _rescueTuning=layout;
            if(overseer){var grip=_animator.GetComponent<Map01OverseerGrip>();if(grip==null)grip=_animator.gameObject.AddComponent<Map01OverseerGrip>();grip.Bind(this);}
            _postPosition=post.position;_postRotation=post.rotation;
            _baseSpeed=_agent.speed=layout.patrolSpeed;_patrolPause=layout.patrolPause;
            visionRange=layout.patrolVision;visionAngle=layout.patrolAngle;
            Configure(points);if(_agent.isOnNavMesh)_agent.Warp(_postPosition);else transform.position=_postPosition;
            transform.rotation=_postRotation;if(points.Length>0)Go(points[0]);else if(_agent.isOnNavMesh)_agent.ResetPath();
        }
        public void InitializePursuer(Map01Mission owner, Vector3 post, bool pursue)
        {
            enabled=true;
            _rescuePost=_overseer=false;_pursuer=true;_lootCreated=false;
            _holdingAI=false;_agent.updatePosition=true;_agent.updateRotation=true;
            _postPosition=post;_postRotation=Quaternion.identity;Configure(System.Array.Empty<Vector3>());BindMission(owner);
            _health.CinematicInvulnerable=false;_health.Revive();_lastHealth=_health.Current;
            TakenDownSilently=false;SetSilent(false);ShowRifle(true);
            _suspicion=0;_alertUntil=0;_searchUntil=0;_engagedUntil=0;
            if(_animator!=null)_animator.speed=1;
            if(_agent.isOnNavMesh){_agent.Warp(post);_agent.isStopped=false;}
            if(pursue)BeginInvestigation(owner.hung.position);
        }
        public void CeasePursuit()
        {
            _suspicion=_engagedUntil=_alertUntil=_searchUntil=0;
            if(_agent.isOnNavMesh){_agent.ResetPath();_agent.isStopped=true;}SetSpeed(0);
        }
        private void ReportDetection(Map01DetectionCause cause,Vector3 position)
        {
            var signal=new Map01Detection(this,cause,position);Detected?.Invoke(signal);_rescue?.ReportDetection(signal);
        }
        public bool CanSilentTakedown()
        {
            if(!Alive||Engaged||IsBoss||_mission==null||_mission.Stopped||_mission.ModernCombat==null
                ||_mission.ModernCombat.EquippedKind!=WeaponKind.Knife)return false;
            var delta=Vector3.ProjectOnPlane(_mission.player.position-transform.position,Vector3.up);
            var layout=_rescue!=null?_rescue.Layout:null;
            if(Vector3.Distance(_mission.player.position,transform.position)>(layout!=null?layout.backstabRange:1.6f))return false;
            if(delta.magnitude>(layout!=null?layout.backstabRange:1.6f)||Vector3.Angle(transform.forward,delta)<(layout!=null?layout.backstabAngle:130))return false;
            var origin=_mission.player.position+Vector3.up;
            return !Physics.Linecast(origin,transform.position+Vector3.up,out var hit,_mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                ||hit.transform==transform||hit.transform.IsChildOf(transform);
        }
        public bool TrySilentTakedown()
        {
            if(!CanSilentTakedown())return false;
            if(!Map01NamActions.For(_mission).PlayTakedown(transform))return false;
            TakenDownSilently=true;SetSilent(true);ShowRifle(false);
            if(_agent.isOnNavMesh){_agent.ResetPath();_agent.isStopped=true;}
            _health.TakeDamage(_health.Current,transform.position+Vector3.up,_mission.player.gameObject);
            var cinematic=_mission.GetComponent<Map01RescueCinematic>();
            if(cinematic==null)cinematic=_mission.gameObject.AddComponent<Map01RescueCinematic>();
            cinematic.ShowTakedown(_rescue,this);return true;
        }
        public bool SafeShot(Vector3 target)
        {
            var rifle=GetComponentInChildren<Map01Rifle>();
            Vector3 origin=rifle!=null&&rifle.weapon!=null&&rifle.weapon.Muzzle!=null?rifle.weapon.Muzzle.position:transform.position+Vector3.up*1.3f;
            var end=target+Vector3.up*1.1f;
            if(Physics.Linecast(origin,end,out var hit,~0,QueryTriggerInteraction.Ignore))
                return hit.transform==_mission.hung||hit.transform.IsChildOf(_mission.hung);
            return true;
        }
        public void FireAtHostage(Vector3 target,float amount)
        {
            FaceImmediate(target);_animator?.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            var rifle=GetComponentInChildren<Map01Rifle>();
            if(rifle!=null&&rifle.weapon!=null&&rifle.weapon.Muzzle!=null)
                _mission.Trace(rifle.weapon.Muzzle.position,target+Vector3.up*.9f,new Color(1,.85f,.45f));
            _rescue.HitHung(amount);
        }
        public void FaceImmediate(Vector3 point)
        {
            var flat=Vector3.ProjectOnPlane(point-transform.position,Vector3.up);
            if(flat.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(flat);
        }

        public bool IsBoss { get; private set; }
        /// <summary>
        /// Turns a regular outpost guard clone into the map's commander: no different model yet
        /// (Map 1's briefing doesn't have one), just a tougher, longer-sighted stand its ground.
        /// </summary>
        public void ConfigureAsBoss(float healthMultiplier, float damageMultiplier)
        {
            IsBoss = true;
            damage *= damageMultiplier;
            visionRange *= 1.2f; attackRange *= 1.2f;
            var health = GetComponent<Health>();
            if (health != null) health.SetMaxHealth(health.Max * healthMultiplier);
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _health = GetComponent<Health>();
            _postPosition = transform.position; _postRotation = transform.rotation;
            _baseSpeed = _agent.speed; _lastHealth = _health.Current;
            var controller = FindFirstObjectByType<ShadowVale.Gameplay.Player.PlayerController>();
            if (controller != null)
            {
                _player = controller.transform;
                _playerHealth = controller.GetComponent<Health>();
            }
            if (_animator != null)
            {
                int upperBody = _animator.GetLayerIndex(PlayerCombat.UpperBodyLayer);
                if (upperBody > 0) _animator.SetLayerWeight(upperBody, 1f);
                foreach (AnimatorControllerParameter p in _animator.parameters) _animatorParams.Add(p.name);
                // One of the guards' five deaths, fixed per guard (by name) so a reload replays the same fall.
                if (_animatorParams.Contains("DeathIndex"))
                    _animator.SetInteger("DeathIndex", (int)((uint)Fnv(name) % 5u));
            }
            if (patrolPoints.Length > 0) Go(patrolPoints[0]);
        }

        private void Update()
        {
            bool holding = Alive && _mission != null && (_mission.Stopped || (_rescue != null && _rescue.HoldEnemy(this)));
            if (holding) {
                if (!_holdingAI) PauseForCinematic();
                transform.position=_heldPosition;
                transform.rotation=_heldRotation;
                if (_agent.isOnNavMesh) _agent.isStopped=true;
                if (_animator!=null) {
                    var cinematic=_mission.GetComponent<Map01RescueCinematic>();
                    bool owned=cinematic!=null&&cinematic.ControlsEnemy(this);
                    _animator.speed=!_mission.Stopped||owned?1:0;
                    if(!_mission.Stopped)SetSpeed(0);
                }
                return;
            }
            if (_holdingAI) {
                float delay=Time.time-_pausedAt;_alertUntil+=delay;_engagedUntil+=delay;_nextShot+=delay;_pauseUntil+=delay;
                if(_hearReactionAt>0)_hearReactionAt+=delay;
                if(_searchUntil>0)_searchUntil+=delay;
                if(_animator!=null)_animator.speed=_animatorSpeedBeforeHold;
                _agent.updatePosition=_agentUpdatesPosition;
                _agent.updateRotation=_agentUpdatesRotation;
                if(_agent.isOnNavMesh)_agent.nextPosition=transform.position;
                if(_agent.isOnNavMesh)_agent.isStopped=!Alive;
                _holdingAI=false;
            }
            // Before the dead check: a hit that killed him outright is still an attack to account for.
            if (_health.Current < _lastHealth) OnHurt();
            _lastHealth = _health.Current;
            if (_health.IsDead)
            {
                CreateLoot();
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                SetSpeed(0f);
                return;
            }

            bool seesPlayer = CanSeePlayer(out float distance);
            if(_hearReactionAt>0&&Time.time>=_hearReactionAt){
                _hearReactionAt=0;BeginInvestigation(_pendingFootstep);
            }
            if (seesPlayer && !Engaged)
            {
                // Not an instant spot: suspicion builds while Nam stays in view, faster up close.
                float closeness = 1f - Mathf.Clamp01(distance / visionRange);
                bool rescueStealth=_rescuePost&&_rescue!=null&&_rescue.CurrentPhase==Map01Rescue.Phase.Captive;
                float detectTime=rescueStealth?Mathf.Max(_detectionSeconds,_rescueTuning!=null?_rescueTuning.closeDetectionSeconds:1.6f):_detectionSeconds;
                float gain=Mathf.Lerp(.6f,rescueStealth?1.4f:3f,closeness);
                _suspicion = Mathf.Min(1f, _suspicion + Time.deltaTime / detectTime * gain);
                // The first moment he catches something: a start, then the stare.
                if (!_startled && _suspicion > .2f) { _startled = true; Trigger("Startled"); }
                if (_suspicion < 1f)
                {
                    // Something's there — stop and stare at it rather than walk on.
                    if (_agent.isOnNavMesh) _agent.isStopped = true;
                    SetSpeed(0f);
                    Face(_player.position);
                    return;
                }
            }
            if (seesPlayer)
            {
                ReportDetection(Map01DetectionCause.Sight, _player.position);
                if (_mission != null && _mission.Cinematic) return;
                // Once Nam is out of sight again: to where he was last seen, look around, go back.
                _engagedUntil = Time.time + 6; BeginInvestigation(_player.position);
                if (_mission != null) _mission.Alarmed = true;
                Face(_player.position);
                if (distance <= attackRange)
                {
                    if (_agent.isOnNavMesh) _agent.isStopped = true;
                    SetSpeed(0f);
                    Shoot();
                }
                else
                {
                    Go(_player.position);
                    SetSpeed(_agent.velocity.magnitude);
                }
                return;
            }
            if (Engaged && TryShootHostage()) return;
            if (_pursuer && _rescue != null && _rescue.Exposed) {
                GoTo(_mission.hung.position); SetSpeed(_agent.velocity.magnitude); return;
            }

            if (!Engaged) _suspicion = Mathf.Max(0f, _suspicion - Time.deltaTime * .35f);
            if (_suspicion <= 0f && !Engaged && !Alerted) _startled = false;
            if (Alerted) Investigate();
            else if (_returning) ReturnHome();
            else { if (_agent.isOnNavMesh) _agent.isStopped = false; Patrol(); }
            SetSpeed(_agent.velocity.magnitude);
        }

        /// <summary>Hurry to where the noise was, then stand and look around for a while.</summary>
        private void Investigate()
        {
            if (_searchUntil <= 0)
            {
                SetPace(1.5f);
                GoTo(_investigate);
                if (_agent.isOnNavMesh && !_agent.pathPending && _agent.remainingDistance <= 1.5f)
                    _searchUntil = Time.time + _searchSeconds;
                return;
            }
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            transform.Rotate(0f, Mathf.Sin(Time.time * 1.2f) * 70f * Time.deltaTime, 0f);
            if (Time.time < _searchUntil) return;
            // Nothing there: back to where he came from.
            _alertUntil = 0; _searchUntil = 0; _returning = true;
            SetPace(1f);
        }

        private void ReturnHome()
        {
            GoTo(_returnPoint);
            if (!_agent.isOnNavMesh || _agent.pathPending || _agent.remainingDistance > .6f) return;
            _returning = false;
            transform.rotation = _returnRotation;
            if (patrolPoints.Length > 0) Go(patrolPoints[_patrolIndex]);
        }

        /// <summary>
        /// Hurt. A knife from behind before he has spotted Nam is a silent takedown — even while
        /// he is off checking a noise, which is what a thrown stone is for. Anything else, and he
        /// knows where it came from.
        /// </summary>
        private void OnHurt()
        {
            HurtCount++;
            if (TakenDownSilently) return;
            if (_player == null) return;
            ReportDetection(Map01DetectionCause.Sight, _player.position);
            if (_health.IsDead) return; // Killed outright — by a gun, say: loud, and nothing left to react.
            Trigger("Hit");
            _suspicion = 1f; _engagedUntil = Time.time + 6f;
            BeginInvestigation(_player.position);
            Face(_player.position);
        }

        /// <summary>Go, unless already on the way there.</summary>
        private void GoTo(Vector3 destination)
        {
            if (!_agent.isOnNavMesh) return;
            if (_agent.hasPath && !_agent.isStopped && (_agent.destination - destination).sqrMagnitude < 1f) return;
            Go(destination);
        }

        private void SetPace(float scale)
        {
            if (_baseSpeed > 0f) _agent.speed = _baseSpeed * scale;
        }

        private bool CanSeePlayer(out float distance)
        {
            distance = float.PositiveInfinity;
            if (_player == null || _playerHealth == null || _playerHealth.IsDead || Time.time < _calmUntil) return false;
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            // Crouched, Nam's head and shoulders are lower: low cover hides him then.
            Vector3 target = _player.position + Vector3.up * (_mission != null && _mission.Crouched ? .75f : 1.1f);
            Vector3 delta = target - origin;
            distance = delta.magnitude;
            if (Engaged)
            {
                if (distance > visionRange) return false;
            }
            else
            {
                // Unaware: a forward cone, shorter while Nam crouches and far shorter while he
                // hides in cover; only brushing right past a guard is noticed from any side — and
                // creeping up crouched gets closer than that, into knife reach behind him.
                bool hidden = _mission != null && _mission.Hidden, crouched = _mission != null && _mission.Crouched;
                if (distance > visionRange * (hidden ? _hiddenScale : crouched ? _crouchScale : 1f)) return false;
                float touch = crouched ? (_rescuePost ? .65f : 1.2f) : (_rescuePost ? .9f : 2.5f);
                if (distance > touch && Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(delta, Vector3.up)) > visionAngle * .5f) return false;
            }
            // Bushes and tree crowns hide Nam — even from a guard already hunting him, who then
            // goes to where he last saw him.
            if (_mission != null && _mission.SightCover != null && _mission.SightCover.Blocks(origin, target)) return false;
            if (Physics.Raycast(origin, delta.normalized, out var hit, distance, obstructionMask,
                    QueryTriggerInteraction.Ignore))
                return hit.transform == _player || hit.transform.IsChildOf(_player);
            return true;
        }

        private void Shoot()
        {
            if (Time.time < _nextShot) return;
            _nextShot = Time.time + fireInterval;
            _animator?.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            // Hùng is in the line of fire while he is being held or walked home: every third
            // round goes his way when he is in reach.
            if (++_shots % 3 == 0 && HostageInReach(out var hostage) && SafeShot(hostage)) { FireAtHostage(hostage,damage); return; }
            _playerHealth.TakeDamage(damage, _player.position + Vector3.up, gameObject);
        }

        /// <summary>
        /// Engaged but Nam out of sight: with Hùng in reach, the guard turns on him instead, at
        /// half the rate — Nam hiding does not keep Hùng safe, it hands the squad an easier target.
        /// </summary>
        private bool TryShootHostage()
        {
            if (!HostageInReach(out var hostage) || !SafeShot(hostage)) return false;
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            SetSpeed(0f);
            Face(hostage);
            if (Time.time < _nextShot) return true;
            _nextShot = Time.time + fireInterval * 2f;
            FireAtHostage(hostage, damage);
            return true;
        }

        private bool HostageInReach(out Vector3 hostage)
        {
            hostage = default;
            if (_rescue == null || !_rescue.Exposed || _mission.hung == null) return false;
            hostage = _mission.hung.position;
            Vector3 origin = transform.position + Vector3.up * 1.3f, target = hostage + Vector3.up * 1.1f;
            if (Vector3.Distance(origin, target) > attackRange) return false;
            return !Physics.Linecast(origin, target, out var hit, _mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                || hit.transform == _mission.hung || hit.transform.IsChildOf(_mission.hung);
        }

        private void Patrol()
        {
            if (Time.time < _pauseUntil) { if(_agent.isOnNavMesh)_agent.isStopped=true;return; }
            if (patrolPoints.Length == 0 || !_agent.isOnNavMesh) return;
            if (!_agent.pathPending && _agent.remainingDistance <= 0.7f)
            {
                _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
                Go(patrolPoints[_patrolIndex]);
                if (_rescuePost && _patrolPause > 0) { _pauseUntil=Time.time+_patrolPause;_agent.isStopped=true; }
            }
        }

        private void Go(Vector3 destination)
        {
            if (!_agent.isOnNavMesh) return;
            _agent.isStopped = false;
            if (NavMesh.SamplePosition(destination, out var hit, 2f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        private void Face(Vector3 position)
        {
            Vector3 direction = position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), 360f * Time.deltaTime);
        }

        private void SetSpeed(float speed)
        {
            if (_animator == null) return;
            _animator.SetFloat(PlayerCombat.AnimatorParams.Speed, speed);
            // The guards' own clips (StoryAnimationSetup): rifle up while fighting, a look round at the spot.
            if (_animatorParams.Contains("Engaged")) _animator.SetBool("Engaged", Engaged);
            if (_animatorParams.Contains("Searching")) _animator.SetBool("Searching", Searching);
        }

        private void Trigger(string parameter)
        {
            if (_animator != null && _animatorParams.Contains(parameter)) _animator.SetTrigger(parameter);
        }

        private void SetSilent(bool value)
        {
            if (_animator != null && _animatorParams.Contains("Silent")) _animator.SetBool("Silent", value);
        }

        private void ShowRifle(bool on)
        {
            var rifle = GetComponentInChildren<Map01Rifle>(true);
            if (rifle != null && rifle.weapon != null) rifle.weapon.gameObject.SetActive(on);
        }

        private static uint Fnv(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text) { hash ^= c; hash *= 16777619; }
            return hash;
        }
    }
}
