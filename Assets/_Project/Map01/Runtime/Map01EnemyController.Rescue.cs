using System.Linq;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The rescue set-piece (Map01Rescue): the jetty posts, pursuers, the shot at Hùng and cinematic holds.
    /// </summary>
    public sealed partial class Map01EnemyController
    {
        // ---- The rescue set-piece ----------------------------------------------------------------

        /// <summary>One of the four jetty guards (Map01Rescue): his post, his loop, the layout's tuning.</summary>
        public void ConfigureRescuePost(Transform post, Vector3[] points, bool overseer, Map01RescueLayout layout)
        {
            EnsureBrain();
            _rescuePost = true; _overseer = overseer; _pursuer = false;
            _rescueTuning = layout;
            if (overseer)
            {
                var grip = _animator.GetComponent<Map01OverseerGrip>();
                if (grip == null) grip = _animator.gameObject.AddComponent<Map01OverseerGrip>();
                grip.Bind(this);
            }
            _postPosition = post.position; _postRotation = post.rotation;
            _agent.speed = _body.BaseSpeed = layout.patrolSpeed;
            _tuning.PatrolPauseSeconds = layout.patrolPause;
            visionRange = layout.patrolVision; visionAngle = layout.patrolAngle;
            SyncTuning();
            Configure(points);
            _body.Warp(_postPosition);
            transform.rotation = _postRotation;
            if (points.Length > 0) _body.MoveTo(points[0]); else if (_agent.isOnNavMesh) _agent.ResetPath();
        }

        /// <summary>A pursuer spawned after Hùng is freed: alive, armed, heading for Hùng if <paramref name="pursue"/>.</summary>
        public void InitializePursuer(Map01Mission owner, Vector3 post, bool pursue)
        {
            enabled = true;
            EnsureBrain();
            _rescuePost = _overseer = false; _pursuer = true; _lootCreated = false;
            carriesSupplyKey = false; operationIndex = 0;
            _holdingAI = false; _agent.updatePosition = true; _agent.updateRotation = true;
            _postPosition = post; _postRotation = Quaternion.identity; Configure(System.Array.Empty<Vector3>()); BindMission(owner);
            _health.CinematicInvulnerable = false; _health.Revive(); _lastHealth = _health.Current;
            TakenDownSilently = false; SetSilent(false); ShowRifle(true); _bodyState = BodyUnseen; Wary = false;
            _hearReactionAt = 0; _stoneLure = false; ApplyLureTuning();
            Brain.ResetToPost(0f);
            if (_animator != null) _animator.speed = 1;
            if (_agent.isOnNavMesh) { _agent.Warp(post); _agent.isStopped = false; }
            if (pursue && owner.hung != null) BeginInvestigation(owner.hung.position);
        }

        /// <summary>Hùng is home: a pursuer stops where he is.</summary>
        public void CeasePursuit()
        {
            var m = Memory;
            m.Suspicion = 0; m.EngagedUntil = 0; m.AlertUntil = 0; m.SearchUntil = 0;
            if (_agent.isOnNavMesh) { _agent.ResetPath(); _agent.isStopped = true; }
            _body.Animate(false);
        }

        /// <summary>The rescue's alarm: a jetty guard who has not seen Nam joins the fight at <paramref name="lastSeen"/>.</summary>
        public void JoinRescueCombat(Vector3 lastSeen)
        {
            if (!Alive || !_rescuePost) return;
            var m = Memory; float now = Time.time;
            _hearReactionAt = 0; _stoneLure = false; ApplyLureTuning();
            m.Suspicion = 1f; m.EngagedUntil = now + 10f; m.LastKnownPlayerPosition = lastSeen;
            Brain.BeginInvestigation(lastSeen, now);
        }

        private void ReportDetection(Map01DetectionCause cause, Vector3 position)
        {
            var signal = new Map01Detection(this, cause, position);
            Detected?.Invoke(signal);
            _rescue?.ReportDetection(signal);
        }

        // A guard with the hostage in reach checks his line of fire every frame: the rifle is looked up once.
        private Map01Rifle _rifle;
        private Map01Rifle Rifle => _rifle != null && _rifle.gameObject.activeInHierarchy ? _rifle : _rifle = GetComponentInChildren<Map01Rifle>();
        /// <summary>Whether a round at <paramref name="target"/> would reach Hùng and nobody else.</summary>
        public bool SafeShot(Vector3 target)
        {
            var rifle = Rifle;
            Vector3 origin = rifle != null && rifle.weapon != null && rifle.weapon.Muzzle != null ? rifle.weapon.Muzzle.position : transform.position + Vector3.up * 1.3f;
            var end = target + Vector3.up * 1.1f;
            if (Physics.Linecast(origin, end, out var hit, ~0, QueryTriggerInteraction.Ignore))
                return hit.transform == _mission.hung || hit.transform.IsChildOf(_mission.hung);
            return true;
        }

        /// <param name="bodyPoint">Aim at <paramref name="target"/> itself (a bone, in the execution film) rather than his chest above it.</param>
        public void FireAtHostage(Vector3 target, float amount, bool bodyPoint = false, bool cinematicListener = false)
        {
            FaceImmediate(target);
            if (_animator != null && _animator.runtimeAnimatorController != null) _animator.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            if (_rescue == null || _mission == null || _mission.hung == null) return;
            AudibleShots++;
            if (!FireRound(target + (bodyPoint ? Vector3.zero : Vector3.up * .9f), _mission.hung, out var impact, cinematicListener)) return;
            float before = _rescue.HungHealth;
            _rescue.HitHung(amount);
            if (_rescue.HungHealth < before) BloodImpact.Play(impact, impact - transform.position);
        }

        public void FaceImmediate(Vector3 point)
        {
            var flat = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (flat.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(flat);
        }

        /// <summary>Freeze for a cinematic (or a stopped mission); <paramref name="animate"/> keeps his animator running.</summary>
        public void PauseForCinematic(bool animate = false)
        {
            if (!Alive) return;
            if (!_holdingAI)
            {
                _holdingAI = true; _pausedAt = Time.time; _animatorSpeedBeforeHold = _animator != null ? _animator.speed : 1;
                _heldPosition = transform.position; _heldRotation = transform.rotation;
                _agentUpdatesPosition = _agent != null && _agent.updatePosition; _agentUpdatesRotation = _agent != null && _agent.updateRotation;
            }
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            if (_agent != null) { _agent.updatePosition = false; _agent.updateRotation = false; }
            if (_animator != null) _animator.speed = animate ? 1 : 0;
        }
    }
}
