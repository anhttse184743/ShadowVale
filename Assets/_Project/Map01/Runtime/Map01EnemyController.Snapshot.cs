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
    /// The guard's save state: what a checkpoint keeps of him and how a load puts it back.
    /// </summary>
    public sealed partial class Map01EnemyController
    {
        [System.Serializable] public sealed class Snapshot
        {
            public string id;
            public string rifleItemId;
            public Vector3 position, destination, investigate, returnPoint;
            public Quaternion rotation, returnRotation;
            public float hp, shotRemaining, alertRemaining, searchRemaining;
            public int patrolIndex;
            public bool stopped, returning, silent, pursuer;
            public int silentKind;
            public float suspicion, engagedRemaining, pauseRemaining;
            public float footstepReactionRemaining;
            public Vector3 pendingFootstep;
            public bool stoneLure;
            public Quaternion stoneFacing;
            /// <summary>1 unseen, 2 found by another guard; 0 in saves from before bodies could be found.</summary>
            public int bodyState;
            public bool wary;
            public bool hasKnownPlayer;
            public Vector3 knownPlayer, heardShotPosition;
            public float heardShotRemaining;
            public int guardBurstShots, guardMagazineSpent;
            public float guardBurstLeaseRemaining;
        }
        public Snapshot Capture()
        {
            var m = Memory; float now = Time.time;
            return new Snapshot {
                id = SaveId, rifleItemId = RifleItemId, position = transform.position, rotation = transform.rotation, hp = _health.Current,
                silent = TakenDownSilently, silentKind = silentKind, pursuer = _pursuer, suspicion = m.Suspicion,
                engagedRemaining = Mathf.Max(0, m.EngagedUntil - now), pauseRemaining = Mathf.Max(0, m.PatrolPauseUntil - now),
                footstepReactionRemaining = _hearReactionAt > 0 ? Mathf.Max(0, _hearReactionAt - now) : 0,
                pendingFootstep = _pendingFootstep, stoneLure = _stoneLure, stoneFacing = _stoneFacing,
                patrolIndex = m.PatrolIndex, shotRemaining = Mathf.Max(0, _nextShot - now),
                alertRemaining = Mathf.Max(0, m.AlertUntil - now), investigate = m.InvestigatePoint,
                searchRemaining = m.SearchUntil > 0 ? Mathf.Max(.01f, m.SearchUntil - now) : 0,
                returning = m.Returning, returnPoint = m.ReturnPoint, returnRotation = m.ReturnRotation,
                destination = _agent.isOnNavMesh && _agent.hasPath ? _agent.destination : transform.position,
                stopped = _agent.isOnNavMesh && _agent.isStopped,
                bodyState = _bodyState, wary = Wary,
                hasKnownPlayer = Engaged, knownPlayer = m.LastKnownPlayerPosition,
                heardShotPosition = _heardShotPosition, heardShotRemaining = Mathf.Max(0, _heardShotUntil - now),
                guardBurstShots = _guardFireCycle.BurstShots, guardMagazineSpent = _guardFireCycle.MagazineSpent,
                guardBurstLeaseRemaining = Mathf.Max(0, _guardBurstLeaseUntil - now)
            };
        }
        /// <summary>This guard as he would be fresh at his post: alive, calm, starting his patrol.</summary>
        public Snapshot CaptureAtPost() => new Snapshot {
            id = SaveId, rifleItemId = RifleItemId, position = _postPosition, rotation = _postRotation, hp = _health.Max,
            destination = _postPosition, patrolIndex = 0, bodyState = BodyUnseen
        };
        public void RestoreSnapshot(Snapshot saved)
        {
            RefreshRifleLoadout(saved.rifleItemId);
            var m = Memory; float now = Time.time;
            _body.Warp(saved.position);
            transform.rotation = saved.rotation;
            bushCorpse?.Dispose(); bushCorpse = null; silentKind = saved.silentKind;
            TakenDownSilently = saved.silent; SetSilent(saved.silent); ShowRifle(!saved.silent);
            _health.RestoreHealth(saved.hp);
            _lastHealth = _health.Current;
            // A body in a save from before bodies could be found counts as found: loading it must not set anyone off.
            ClearDamageEvidence();
            _heardShotPosition = saved.heardShotPosition; _heardShotUntil = now + saved.heardShotRemaining;
            m.LastKnownPlayerPosition = saved.hasKnownPlayer ? saved.knownPlayer : saved.investigate;
            m.LastSeenTime = float.NegativeInfinity;
            _bodyState = saved.bodyState != 0 ? saved.bodyState : Alive ? BodyUnseen : BodySeen;
            Wary = saved.wary;
            _sense.Reset();
            m.Suspicion = saved.suspicion; m.EngagedUntil = now + saved.engagedRemaining;
            m.PatrolPauseUntil = now + saved.pauseRemaining; _pursuer = saved.pursuer;
            _hearReactionAt = saved.footstepReactionRemaining > 0 ? now + saved.footstepReactionRemaining : 0;
            _pendingFootstep = saved.pendingFootstep;
            _stoneLure = saved.stoneLure;
            var look = Vector3.ProjectOnPlane(saved.investigate - _postPosition, Vector3.up);
            _stoneFacing = Quaternion.Dot(saved.stoneFacing, saved.stoneFacing) > .1f ? saved.stoneFacing
                : Quaternion.LookRotation(look.sqrMagnitude > .01f ? look : transform.forward);
            ApplyLureTuning();
            m.PatrolIndex = Mathf.Clamp(saved.patrolIndex, 0, Mathf.Max(0, patrolPoints.Length - 1));
            _nextShot = now + saved.shotRemaining;
            _guardFireCycle.Restore(saved.guardBurstShots, saved.guardMagazineSpent, GuardFire);
            _guardBurstLeaseUntil = now + Mathf.Clamp(saved.guardBurstLeaseRemaining, 0, 1f);
            m.AlertUntil = now + saved.alertRemaining; m.InvestigatePoint = saved.investigate;
            m.SearchUntil = saved.searchRemaining > 0 ? now + saved.searchRemaining : 0;
            m.Returning = saved.returning; m.ReturnPoint = saved.returnPoint; m.ReturnRotation = saved.returnRotation;
            _body.MoveTo(saved.destination);
            if (_agent.isOnNavMesh) _agent.isStopped = saved.stopped || !Alive;
            if (!Alive) { CreateLoot(); FillLoot(); if (saved.silent && saved.silentKind == 1) HoldBushCorpse(); }
            RebaseHold();
        }
    }
}
