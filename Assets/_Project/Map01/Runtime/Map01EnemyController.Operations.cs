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
    /// Map 1's operations (Map01Story, Map01C3Mission): combat on the shared FSM's memory, new posts and
    /// patrol beats.
    /// </summary>
    public sealed partial class Map01EnemyController
    {
        // ---- Map 1's operations (Map01Story, Map01C3Mission), on the shared FSM's memory ---------

        /// <summary>Into the fight at once, towards <paramref name="lastSeen"/>, holding fire for <paramref name="readySeconds"/>.</summary>
        public void BeginOperationCombat(Vector3 lastSeen, float readySeconds = 1.5f)
        {
            if (!Alive) return;
            var m = Memory; float now = Time.time;
            _hearReactionAt = 0; _stoneLure = false; ApplyLureTuning();
            m.Suspicion = 1f; m.EngagedUntil = now + 15f; m.LastKnownPlayerPosition = lastSeen;
            Brain.BeginInvestigation(lastSeen, now);
            _nextShot = Mathf.Max(_nextShot, now + readySeconds);
        }
        /// <summary>Starts the fight at once from wherever he is (the camp's alarm, a commander's call).</summary>
        public void Alert(Vector3 lastSeen) => BeginOperationCombat(lastSeen, .8f);
        /// <summary>Gives up the fight and walks back to his post (Nam is too far: the camp does not chase deep into the forest).</summary>
        public void StandDown()
        {
            if (!Alive) return;
            var m = Memory;
            m.EngagedUntil = 0; m.AlertUntil = 0; m.SearchUntil = 0; m.SuppressUntil = 0; _hearReactionAt = 0; _stoneLure = false;
            m.Suspicion = Mathf.Min(m.Suspicion, .5f); m.ClearAssignment();
            m.Returning = true; m.ReturnPoint = _postPosition; m.ReturnRotation = _postRotation;
            _body.SetPace(1f);
        }

        // Per point of the beat (Map01C3Mission): how long he stays there and which way he faces meanwhile (NaN: as he came).
        private float[] _patrolStops, _patrolYaws;
        private float _stopYaw = float.NaN;
        private int _lastPatrolIndex = -1;
        /// <summary>
        /// A new post and beat for this guard, set by a mission's layout (Map 1's C3 command post). A stationary guard
        /// (a sentry on the watchtower's platform, off the NavMesh) has his agent switched off and only turns and shoots.
        /// <paramref name="stops"/> and <paramref name="yaws"/> follow <paramref name="points"/>: seconds he waits at each,
        /// and the yaw he holds there.
        /// </summary>
        /// <param name="walk">Walk there instead of being set down on the spot (a change of post in front of Nam).</param>
        public void Repost(Vector3 position, float yaw, Vector3[] points = null, float[] stops = null, float[] yaws = null,
            float vision = 0f, bool stationary = false, bool walk = false)
        {
            var rotation = Quaternion.Euler(0, yaw, 0);
            _postPosition = position; _postRotation = rotation;
            var m = Memory; m.ReturnPoint = position; m.ReturnRotation = rotation; m.PatrolIndex = 0; m.PatrolPauseUntil = 0; _lastPatrolIndex = 0;
            Configure(points); _patrolStops = stops; _patrolYaws = yaws; _stopYaw = float.NaN;
            if (vision > 0) { visionRange = vision; SyncTuning(); }
            if (stationary && _agent != null) _agent.enabled = false;
            if (!walk || stationary)
            {
                _body.Warp(position);
                transform.rotation = rotation;
            }
            if (!stationary && patrolPoints.Length > 0) _body.MoveTo(patrolPoints[0]);
            else if (walk && !stationary && !Alerted) _body.MoveTo(position);
        }

        /// <summary>The per-point stops of a Repost beat: the shared patrol only knows one pause for every point.</summary>
        private void ApplyPatrolStops()
        {
            var m = Memory;
            if (_patrolStops == null || patrolPoints.Length == 0) return;
            if (m.PatrolIndex != _lastPatrolIndex)
            {
                int reached = (m.PatrolIndex - 1 + patrolPoints.Length) % patrolPoints.Length;
                _lastPatrolIndex = m.PatrolIndex;
                if (reached < _patrolStops.Length && _patrolStops[reached] > 0)
                {
                    m.PatrolPauseUntil = Time.time + _patrolStops[reached];
                    _stopYaw = _patrolYaws != null && reached < _patrolYaws.Length ? _patrolYaws[reached] : float.NaN;
                }
                else _stopYaw = float.NaN;
            }
            if (Time.time < m.PatrolPauseUntil && !Engaged && !Alerted && !float.IsNaN(_stopYaw))
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, _stopYaw, 0), 150 * Time.deltaTime);
        }

        public void Configure(Vector3[] points)
        {
            patrolPoints = points ?? System.Array.Empty<Vector3>();
            if (_brain != null) _brain.Memory.PatrolPoints = patrolPoints;
        }

        /// <summary>
        /// A beat round his area (<see cref="Map01PatrolBeats"/>): the loop starting at <paramref name="start"/>, the
        /// seconds he stands and the yaw he faces at each point, the walking pace. Unlike <see cref="Repost"/> he is
        /// not moved: he walks to his first point from where he stands. His post becomes that point, so a reset
        /// after a failed scouting run puts him back on his beat. A checkpoint restored afterwards keeps its own
        /// position and point index.
        /// </summary>
        public void AssignBeat(Vector3[] points, float[] stops, float[] yaws, int start, float pace)
        {
            if (points == null || points.Length == 0) return;
            EnsureBrain();
            int n = points.Length;
            start = ((start % n) + n) % n;
            T[] Turn<T>(T[] a) => a == null ? null : Enumerable.Range(0, n).Select(i => a[(start + i) % n]).ToArray();
            var loop = Turn(points);
            Configure(loop); _patrolStops = Turn(stops); _patrolYaws = Turn(yaws); _stopYaw = float.NaN;
            _postPosition = loop[0];
            if (_patrolYaws != null && !float.IsNaN(_patrolYaws[0])) _postRotation = Quaternion.Euler(0, _patrolYaws[0], 0);
            _tuning.PatrolPace = pace;
            var m = Memory; m.PatrolIndex = 0; _lastPatrolIndex = 0; m.PatrolPauseUntil = 0;
            if (Alive && !Engaged && !Alerted && _agent != null && _agent.isOnNavMesh) _body.MoveTo(loop[0]);
        }

        /// <summary>The walking pace of his beat (1 = full speed; 0.55 for a calm beat).</summary>
        public float PatrolPace => _tuning.PatrolPace;
    }
}
