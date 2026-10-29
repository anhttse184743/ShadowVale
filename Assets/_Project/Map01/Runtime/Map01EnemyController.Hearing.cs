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
    /// What puts a guard on edge without seeing Nam: a comrade's body, noises and distant shots, and the
    /// investigation they start (with the overseer's stone lure).
    /// </summary>
    public sealed partial class Map01EnemyController
    {
        // ---- A comrade's body (Map01BodyDiscovery) -----------------------------------------------
        private const int BodyUnseen = 1, BodySeen = 2;
        /// <summary>Unseen (1) or seen by another guard (2); a save older than the rule brings 0 (see RestoreSnapshot).</summary>
        private int _bodyState = BodyUnseen;
        /// <summary>Dead, and another guard has found him — a body is found only once.</summary>
        public bool BodyFound => !Alive && _bodyState == BodySeen;
        /// <summary>Pulled into a bush (Map01BushTakedown): hidden for good, nobody finds him.</summary>
        public bool BodyHidden => silentKind == 1;
        public void MarkBodyFound() => _bodyState = BodySeen;
        /// <summary>He has seen a comrade's body: from then on he is quicker to be sure of what he sees, and stays so.</summary>
        public bool Wary { get; private set; }
        /// <summary>A wary guard needs this share of the usual time to be sure he has seen Nam.</summary>
        public const float WaryDetectionScale = .75f;
        public void MakeWary() { if (Alive) Wary = true; }
        /// <summary>Walks on the NavMesh (not the watchtower's sentry, whose agent is off).</summary>
        public bool CanMove => _agent != null && _agent.enabled && _agent.isOnNavMesh;

        /// <summary>A comrade's body at <paramref name="at"/>: a start, then over to look at it and around it. The overseer
        /// only looks from beside Hùng (<see cref="BeginInvestigation"/>).</summary>
        public void InspectBody(Vector3 at)
        {
            if (!Alive || Engaged) return;
            if (!_startled) { _startled = true; Trigger("Startled"); }
            _hearReactionAt = 0;
            BeginInvestigation(at);
            Wary = true;
        }

        /// <summary>Off to <paramref name="to"/> at the investigating hurry, without a start (the C3 kẻng runner).</summary>
        public void Dispatch(Vector3 to)
        {
            if (!Alive || Engaged) return;
            _hearReactionAt = 0; _stoneLure = false; ApplyLureTuning();
            Brain.BeginInvestigation(to, Time.time);
            Wary = true;
        }

        /// <summary>A noise at <paramref name="position"/> that carries <paramref name="radius"/>
        /// metres: within it, the guard goes to see what it was. True if he heard it.</summary>
        public bool Hear(Vector3 position, float radius, NoiseKind kind = NoiseKind.Other)
        {
            if (BedPoseOwned) return false;
            bool stealth = RescueStealth;
            if (stealth && kind == NoiseKind.Footstep && _mission.Crouched)
                radius *= _rescueTuning != null ? _rescueTuning.crouchedFootstepScale : .55f;
            if (!Alive || !HearingSensor.Hears(transform.position, position, radius)) return false;
            if (kind == NoiseKind.Stone) LastStoneHeardAt = Time.time;
            if (_mission != null && _mission.Cinematic) return false;
            if (kind == NoiseKind.Gunshot)
            {
                _heardShotPosition = position; _heardShotUntil = Time.time + _tuning.InvestigateTimeoutSeconds;
                ReportDetection(Map01DetectionCause.Gunshot, position);
            }
            // Finish checking a thrown stone instead of cancelling the lure for a quiet step behind
            // him. Sight and gunshots still apply.
            if (stealth && kind == NoiseKind.Footstep && _mission.Crouched && _stoneLure && !Engaged) return true;
            if (stealth && kind == NoiseKind.Footstep && !Engaged)
            {
                // The first audible step starts the reaction clock; later steps move its source
                // without extending it. Stones and gunshots stay immediate.
                _pendingFootstep = position;
                if (_hearReactionAt <= 0) _hearReactionAt = Time.time + (_rescueTuning != null ? _rescueTuning.footstepReactionSeconds : 1f);
                return true;
            }
            _hearReactionAt = 0;
            BeginInvestigation(position, kind == NoiseKind.Stone);
            return true;
        }

        bool INoiseListener.OnNoise(Vector3 position, float radius, NoiseKind kind) => Hear(position, radius, kind);

        /// <summary>
        /// A Mosin shot from beyond <see cref="Map01Mission.ShotPinpointMeters"/> but within <paramref name="radius"/>
        /// (2026-10-22): loud enough to put him on edge, too far off to tell where it came from. He goes to search round
        /// a spot roughly that way, vaguer the further it was, and nothing more: no detection, no known threat for
        /// reinforcements. Only seeing Nam gives him away (stealth sniping as in Sniper Elite or MGS V: shoot, then move).
        /// True if he heard it.
        /// </summary>
        public bool HearDistantShot(Vector3 shooter, float radius)
        {
            if (BedPoseOwned || !Alive || (_mission != null && _mission.Cinematic)) return false;
            if (!HearingSensor.Hears(transform.position, shooter, radius)) return false;
            if (Engaged) return true;
            var off = Vector3.ProjectOnPlane(shooter - transform.position, Vector3.up);
            float d = off.magnitude;
            if (d < .01f) return true;
            var along = off / d; var side = Vector3.Cross(Vector3.up, along);
            var guess = transform.position + along * (d * Random.Range(.6f, 1.15f)) + side * (d * Random.Range(-.35f, .35f));
            if (_mission != null && Physics.Raycast(guess + Vector3.up * 40f, Vector3.down, out var ground, 80f, _mission.ObstructionMask, QueryTriggerInteraction.Ignore))
                guess.y = ground.point.y;   // C3 stands on terraces: the spot on the ground there, not at his own height
            _hearReactionAt = 0;
            BeginInvestigation(guess);
            return true;
        }

        /// <summary>
        /// Off to check <paramref name="position"/> (the FSM's Investigate). The overseer does not
        /// leave Hùng for a sound: he looks from beside him, and for a stone he goes a few steps
        /// towards it and keeps his eyes on that spot (the stone lure) — never into the river.
        /// </summary>
        private void BeginInvestigation(Vector3 position, bool stone = false)
        {
            if (_overseer && RescueStealth && !Engaged)
            {
                var delta = Vector3.ProjectOnPlane(position - _postPosition, Vector3.up);
                position = _postPosition + Vector3.ClampMagnitude(delta,
                    stone ? (_rescueTuning != null ? _rescueTuning.overseerStoneRadius : 12f) : 1.25f);
                if (stone)
                {
                    _stoneFacing = delta.sqrMagnitude > .01f ? Quaternion.LookRotation(delta) : transform.rotation;
                    var path = new NavMeshPath();
                    if (NavMesh.SamplePosition(position, out var hit, 1.5f, NavMesh.AllAreas)
                        && _agent.isOnNavMesh && _agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                        position = hit.position;
                    else position = _postPosition; // A stone in the river must not draw him into water.
                }
            }
            else if (HoldsPost && !stone && !Engaged)
            {
                // A sentry turns to look at a sound but stays put; only a stone draws him off.
                var delta = Vector3.ProjectOnPlane(position - _postPosition, Vector3.up);
                position = _postPosition + Vector3.ClampMagnitude(delta, .3f);
            }
            _stoneLure = stone && _overseer;
            ApplyLureTuning();
            if (!_overseer && !IsBoss && CanMove) position = ReachableInvestigation(position);
            Brain.BeginInvestigation(position, Time.time);
        }

        // A sound on a roof or prop is not itself a walking destination. Prefer the original
        // point; otherwise search the reachable floor around it, without inventing a player position.
        private Vector3 ReachableInvestigation(Vector3 source)
        {
            var path = new NavMeshPath();
            bool Reach(Vector3 at, out Vector3 result)
            {
                result = default;
                if (!NavMesh.SamplePosition(at, out var hit, 2f, NavMesh.AllAreas)
                    || !_agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
                result = hit.position; return true;
            }
            if (Reach(source, out var direct)) return direct;
            var floor = new Vector3(source.x, transform.position.y, source.z);
            Vector3 best = transform.position;
            float closest = float.PositiveInfinity;
            for (float radius = 0; radius <= 8; radius += 2)
                for (int angle = 0; angle < 360; angle += 45)
                {
                    if (!Reach(floor + Quaternion.Euler(0, angle, 0) * Vector3.forward * radius, out var candidate)) continue;
                    float distance = Vector3.ProjectOnPlane(candidate - source, Vector3.up).sqrMagnitude;
                    if (distance < closest) { closest = distance; best = candidate; }
                }
            return best;
        }

        /// <summary>On a stone lure the overseer walks right up to the spot and stares at it longer.</summary>
        private void ApplyLureTuning()
        {
            _tuning.InvestigateArriveDistance = _stoneLure ? .55f : _baseInvestigateArrive;
            _tuning.SearchSeconds = _stoneLure ? (_rescueTuning != null ? _rescueTuning.overseerStoneFocusSeconds : 11f) : _baseSearchSeconds;
        }
    }
}
